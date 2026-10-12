using System.Collections.Generic;
using Fusion;
using NewBattle.Gameplay;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Battle royale botu: loot toplar, savasir, alandan kacar, sona kalmaya calisir.
///
/// Eski surum sadece en yakin GERCEK oyuncuya yumrukla kosuyordu; 15 botlu bir
/// macta bu, oyuncuya kosan 15 zombi demekti. Simdi her bot bir oyuncu gibi
/// davranir ve herkes herkese dusmandir (takim arkadaslari haric).
///
/// Karar sirasi (her thinkInterval saniyede bir):
///   1. Alan  : guvenli alanin disindaysa (ya da daralma yaklasiyorsa bir
///              sonraki alanin disindaysa) once iceri kos. Kosarken ates edebilir.
///   2. Savas : gorunen bir dusman varsa ve silahi varsa savas; silahsizsa ve
///              rakip de silahsizsa yumruga gir.
///   3. Kacis : silahsiz ama rakip silahliysa ve yakinda silah yoksa uzaklas.
///   4. Loot  : silah, mermi, can, kalkan ihtiyacina gore en degerli yakin esya.
///   5. Dolas : alanin icinde rastgele bir noktaya yuru; son gorulen dusmani ara.
///
/// Adil olsun diye: gorus mesafesi sinirli, cimende saklanan oyuncuyu gormez,
/// agac/kaya arkasini gormez, tepki suresi ve nisan hatasi var, seri atis
/// yapip ara verir ve gercek oyuncudan biraz yavas kosar.
///
/// Yol bulma sahnedeki NavMesh ile. NavMesh yoksa ya da eskiyse bot dogrudan
/// yurur; takildigini anlarsa yana kacip yeniden yol arar.
///
/// Tum karar host'ta (state authority) verilir; istemciler sadece sonucu gorur.
/// </summary>
[RequireComponent(typeof(PlayerStateMachine), typeof(PlayerShooting))]
public class TrainingBot : NetworkBehaviour
{
    private enum Goal
    {
        Wander,
        Loot,
        Fight,
        Flee,
        Zone
    }

    [Header("Hareket")]
    [Tooltip("Botun kosu hizi carpani. 1'in altinda: gercek oyuncu botlardan biraz hizli.")]
    [SerializeField, Range(0.5f, 1f)] private float moveSpeedMultiplier = 0.88f;
    [SerializeField] private float stoppingDistance = 1.6f;
    [SerializeField] private float separationRadius = 1.1f;
    [SerializeField] private float separationWeight = 1.5f;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.6f;

    [Header("Algi")]
    [Tooltip("Kararin kac saniyede bir yenilendigi. Eski prefabda 0.95'ti; savas icin cok yavasti, " +
             "o yuzden alanin adi degisti ve eski deger okunmuyor.")]
    [SerializeField, Min(0.05f)] private float thinkInterval = 0.25f;
    [SerializeField, Min(2f)] private float sightRange = 16f;
    [Tooltip("Gorusu kesen katmanlar (agac, kaya, bina).")]
    [SerializeField] private LayerMask sightBlockers = ~0;

    [Header("Savas")]
    [SerializeField] private bool attackEnabled = true;
    [Tooltip("Yumruk icin hedefe bu kadar yaklasir.")]
    [SerializeField] private float attackDistance = 1.8f;
    [Tooltip("Silahli botun hedefle arasinda tutmaya calistigi mesafe (silah menzilinin orani).")]
    [SerializeField, Range(0.2f, 0.9f)] private float preferredRangeRatio = 0.3f;
    [Tooltip("Nisan hatasi (derece). Botlar kusursuz nisanci olmasin.")]
    [SerializeField, Range(0f, 20f)] private float aimErrorDegrees = 7f;
    [Tooltip("Yeni bir hedef gorunce ates etmeden once bekledigi sure.")]
    [SerializeField] private Vector2 reactionSeconds = new(0.35f, 0.7f);
    [SerializeField] private Vector2 burstSeconds = new(0.6f, 1.2f);
    [SerializeField] private Vector2 burstPauseSeconds = new(0.25f, 0.6f);

    [Header("Loot")]
    [SerializeField, Min(2f)] private float lootSearchRadius = 26f;
    [Tooltip("Esyanin ustunde bu kadar bekleyip alamazsa vazgecer.")]
    [SerializeField, Min(0.5f)] private float lootGiveUpSeconds = 3.5f;

    [Header("Alan")]
    [Tooltip("Daralma bu kadar saniye icinde baslayacaksa bot bir sonraki alana yonelir.")]
    [SerializeField, Min(0f)] private float zoneLookaheadSeconds = 12f;

    private PlayerStateMachine _stateMachine;
    private PlayerShooting _shooting;
    private PlayerLoadout _loadout;
    private HealthController _health;
    private PlayerCombatStats _ownStats;
    private ParachuteDescent _descent;
    private LobbyCountdownController _lobbyCountdown;
    private DropPhaseController _dropPhase;
    private SafeZoneController _safeZone;

    private TickTimer _thinkTimer;
    private TickTimer _repathTimer;

    private Goal _goal;
    private PlayerRegistry _target;
    private bool _targetVisible;
    private Vector3? _lastKnownTargetPosition;
    private TimedLootPickup _lootTarget;
    private float _lootWaitStarted = -1f;
    private readonly Dictionary<TimedLootPickup, float> _ignoredLoot = new();
    private readonly List<TimedLootPickup> _expiredIgnores = new();

    private Vector3 _destination;
    private Vector3 _wanderPoint;
    private float _wanderUntil;

    private NavMeshPath _path;
    private readonly Vector3[] _corners = new Vector3[32];
    private int _cornerCount;
    private int _cornerIndex;
    private Vector3 _pathTarget;

    private float _reactionReadyAt;
    private float _burstUntil;
    private float _pauseUntil;
    private float _aimError;
    private float _strafeSign = 1f;
    private float _strafeSwitchAt;

    private Vector3 _stuckCheckPosition;
    private float _stuckCheckAt;
    private float _unstuckUntil;
    private Vector3 _unstuckDirection;

    private float _lastHealth;
    private float _alertUntil;

    // Oyuncu seviyesine gore zorluk (bkz. BotDifficulty).
    private float _aimErrorScale = 1f;
    private float _reactionScale = 1f;

    private readonly RaycastHit[] _sightHits = new RaycastHit[16];

    private void Awake()
    {
        _stateMachine = GetComponent<PlayerStateMachine>();
        _shooting = GetComponent<PlayerShooting>();
        _loadout = GetComponent<PlayerLoadout>();
        _health = GetComponent<HealthController>();
        _ownStats = GetComponent<PlayerCombatStats>();
        _descent = GetComponent<ParachuteDescent>();
        _path = new NavMeshPath();
    }

    public override void Spawned()
    {
        _lobbyCountdown = FindFirstObjectByType<LobbyCountdownController>();
        _dropPhase = FindFirstObjectByType<DropPhaseController>();
        _safeZone = FindFirstObjectByType<SafeZoneController>();

        // InputAuthority'si olan nesne gercek oyuncudur; yapay zeka sadece botlarda calisir.
        enabled = Object.InputAuthority == PlayerRef.None;

        if (enabled)
        {
            _stateMachine.MoveSpeedMultiplier = moveSpeedMultiplier;

            NewBattle.Progression.BotDifficultyStep difficulty = BotDifficulty.Current;
            _aimErrorScale = difficulty.aimErrorMultiplier;
            _reactionScale = difficulty.reactionMultiplier;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || Object.InputAuthority != PlayerRef.None)
            return;

        if (_health != null && _health.currentHealth <= 0f)
            return;

        if (_lobbyCountdown != null && !_lobbyCountdown.MatchStarted)
            return;

        if (_dropPhase != null && !_dropPhase.GameplayStarted)
            return;

        // Inis surerken hareketi ParachuteDescent yonetir.
        if (_descent != null && _descent.IsDescending)
            return;

        float now = Runner.SimulationTime;

        if (_thinkTimer.ExpiredOrNotRunning(Runner))
        {
            Think(now);
            _thinkTimer = TickTimer.CreateFromSeconds(Runner, thinkInterval);
        }

        NetworkInputData input = default;

        Vector3 move = ComputeMovement(now);
        if (move.sqrMagnitude > 0.0001f)
        {
            move += CalculateSeparation() * separationWeight;
            move.y = 0f;

            if (move.sqrMagnitude > 0.0001f)
            {
                move.Normalize();
                input.JoystickInput = new Vector2(move.x, move.z);
            }
        }

        Vector3 aim = ComputeAim(now);
        if (aim.sqrMagnitude > 0.0001f)
            input.RightJoystickVector = new Vector2(aim.x, aim.z);

        _stateMachine.ProcessInput(input);

        if (input.RightJoystickVector.sqrMagnitude > 0.01f)
            _shooting.ProcessShooting(input.RightJoystickVector);
    }

    #region Karar

    private void Think(float now)
    {
        // Vurulan bot etrafina daha dikkatli bakar: gorus mesafesi kisa sure uzar.
        if (_health != null)
        {
            if (_health.currentHealth < _lastHealth - 0.01f)
                _alertUntil = now + 3f;

            _lastHealth = _health.currentHealth;
        }

        PlayerRegistry previousTarget = _target;
        _target = PickTarget(now, out _targetVisible);

        if (_target != null)
        {
            _lastKnownTargetPosition = _target.transform.position;

            if (_target != previousTarget)
                _reactionReadyAt = now + Random.Range(reactionSeconds.x, reactionSeconds.y) * _reactionScale;
        }

        // Nisan hatasi karar basina degisir; her tick degisseydi karakter titrerdi.
        _aimError = Random.Range(-aimErrorDegrees, aimErrorDegrees) * _aimErrorScale;

        if (now >= _strafeSwitchAt)
        {
            _strafeSign = Random.value < 0.5f ? -1f : 1f;
            _strafeSwitchAt = now + Random.Range(1.2f, 2.6f);
        }

        ForgetExpiredLoot(now);

        bool zoneKnown = GetZone(out Vector3 zoneCenter, out float zoneRadius);
        Vector3 position = transform.position;
        Vector3 fromCenter = Flat(position - zoneCenter);
        bool outsideZone = zoneKnown && fromCenter.magnitude > zoneRadius - 1.5f;

        bool armed = HasUsableGun();
        TimedLootPickup previousLoot = _lootTarget;
        _lootTarget = outsideZone ? null : ChooseLoot(now, zoneKnown, zoneCenter, zoneRadius);

        if (_lootTarget != previousLoot)
            _lootWaitStarted = -1f;

        bool targetArmed = _target != null && IsArmed(_target);

        // Silahli rakip karsisinda geri cekilmek icin iki sebep: silahsiz olmak
        // (yakinda alinacak bir silah yoksa) ya da cani bitmek uzere olmak
        // (yakinda can yoksa). Geri cekilirken nisan devam eder; bot kacarken
        // de ates eder.
        bool outgunned = !armed && !(_lootTarget is RiflePickup);
        bool lowHealth = HealthNeed() > 0.7f;
        bool healNearby = _lootTarget is RecoveryPickup;

        if (outsideZone)
        {
            _goal = Goal.Zone;
            Vector3 inward = fromCenter.sqrMagnitude > 0.01f ? fromCenter.normalized : Vector3.forward;
            _destination = zoneCenter + inward * (zoneRadius * 0.45f);
        }
        else if (targetArmed && !CanBrawl(_target) && (outgunned || (lowHealth && !healNearby)))
        {
            _goal = Goal.Flee;
            Vector3 away = Flat(position - _target.transform.position);
            away = away.sqrMagnitude > 0.01f ? away.normalized : Flat(Random.onUnitSphere).normalized;
            _destination = ClampIntoZone(position + away * 8f, zoneKnown, zoneCenter, zoneRadius);
        }
        else if (_target != null && (armed || CanBrawl(_target)) && !(lowHealth && healNearby))
        {
            _goal = Goal.Fight;
        }
        else if (_lootTarget != null)
        {
            _goal = Goal.Loot;
            _destination = _lootTarget.transform.position;
        }
        else
        {
            _goal = Goal.Wander;

            // Silahliysa son gordugu dusmanin pesine duser; degilse alanda gezinir.
            if (armed && _lastKnownTargetPosition.HasValue)
            {
                _destination = _lastKnownTargetPosition.Value;

                if (Flat(_destination - position).magnitude < stoppingDistance * 1.5f)
                    _lastKnownTargetPosition = null;
            }
            else
            {
                if (now >= _wanderUntil || Flat(_wanderPoint - position).magnitude < 2.5f ||
                    (zoneKnown && Flat(_wanderPoint - zoneCenter).magnitude > zoneRadius * 0.8f))
                {
                    _wanderPoint = RandomPointInZone(zoneKnown, zoneCenter, zoneRadius);
                    _wanderUntil = now + Random.Range(8f, 14f);
                }

                _destination = _wanderPoint;
            }
        }
    }

    private PlayerRegistry PickTarget(float now, out bool visible)
    {
        visible = false;
        PlayerRegistry best = null;
        float bestScore = float.MaxValue;
        float range = sightRange * (now < _alertUntil ? 1.5f : 1f);
        Vector3 position = transform.position;
        IReadOnlyList<PlayerRegistry> registry = PlayerRegistry.All;

        for (int i = 0; i < registry.Count; i++)
        {
            PlayerRegistry entry = registry[i];

            if (entry == null || entry.gameObject == gameObject || !entry.IsAlive || entry.Runner != Runner)
                continue;

            if (_ownStats != null && _ownStats.IsTeammate(entry.Stats))
                continue;

            float distance = Flat(entry.transform.position - position).magnitude;
            if (distance > range)
                continue;

            // Cimen kurali oyuncuyla ayni: saklanan hedefi bot da goremez.
            if (entry.Visibility != null && !entry.Visibility.IsBodyVisibleFrom(position))
                continue;

            if (!HasLineOfSight(entry))
                continue;

            // En yakin, ama elindeki hedefi kolay birakmasin ve zayif dusmani tercih etsin.
            float score = distance;
            if (entry == _target)
                score -= 4f;
            if (entry.Health != null)
                score += entry.Health.currentHealth / Mathf.Max(1f, entry.Health.maxHealth) * 2f;

            if (score < bestScore)
            {
                bestScore = score;
                best = entry;
            }
        }

        visible = best != null;
        return best;
    }

    /// <summary>Silahsizken yumruga girmeye deger mi: rakip de silahsiz ya da cok yakin.</summary>
    private bool CanBrawl(PlayerRegistry target)
    {
        float distance = Flat(target.transform.position - transform.position).magnitude;
        return distance < 3f || (!IsArmed(target) && distance < 8f);
    }

    private static bool IsArmed(PlayerRegistry target)
    {
        return target.Loadout != null && target.Loadout.Object != null &&
               target.Loadout.Object.IsValid && target.Loadout.IsRifleSelected;
    }

    private bool HasUsableGun()
    {
        if (_loadout == null || !_loadout.HasRangedWeapon)
            return false;

        WeaponDefinition weapon = _loadout.CurrentWeapon;
        return weapon != null && !weapon.isMelee && (weapon.infiniteAmmo || _loadout.Ammo > 0);
    }

    #endregion

    #region Loot

    private TimedLootPickup ChooseLoot(float now, bool zoneKnown, Vector3 zoneCenter, float zoneRadius)
    {
        TimedLootPickup best = null;
        float bestScore = 0f;
        Vector3 position = transform.position;

        foreach (TimedLootPickup pickup in TimedLootPickup.AllActive)
        {
            if (pickup == null || !pickup.IsUsefulFor(_loadout))
                continue;

            if (_ignoredLoot.TryGetValue(pickup, out float until) && until > now)
                continue;

            Vector3 lootPosition = pickup.transform.position;
            float distance = Flat(lootPosition - position).magnitude;

            if (distance > lootSearchRadius)
                continue;

            // Alanin disindaki esya icin olmeye degmez.
            if (zoneKnown && Flat(lootPosition - zoneCenter).magnitude > zoneRadius - 1f)
                continue;

            float need = LootNeed(pickup);
            if (need <= 0f)
                continue;

            float score = need / (1f + distance / 8f);
            if (score > bestScore)
            {
                bestScore = score;
                best = pickup;
            }
        }

        return best;
    }

    private float LootNeed(TimedLootPickup pickup)
    {
        switch (pickup)
        {
            case RiflePickup:
                return _loadout.HasRangedWeapon ? AmmoNeed() * 0.6f : 3f;

            case AmmoPickup:
                return AmmoNeed() * 2f;

            case RecoveryPickup recovery when recovery.Type == RecoveryType.Health:
                return HealthNeed() * 3.5f;

            case RecoveryPickup:
                return _health != null
                    ? 0.8f + (1f - _health.currentShield / Mathf.Max(1f, _health.maxShield))
                    : 0f;

            case AirdropCratePickup:
                return 4f;

            default:
                return 1f;
        }
    }

    private float AmmoNeed()
    {
        if (_loadout == null || !_loadout.HasRangedWeapon)
            return 0f;

        return 1f - Mathf.Clamp01(_loadout.Ammo / (float)Mathf.Max(1, _loadout.MaxRifleAmmo));
    }

    private float HealthNeed()
    {
        return _health != null ? 1f - Mathf.Clamp01(_health.currentHealth / Mathf.Max(1f, _health.maxHealth)) : 0f;
    }

    private void ForgetExpiredLoot(float now)
    {
        _expiredIgnores.Clear();

        foreach (KeyValuePair<TimedLootPickup, float> entry in _ignoredLoot)
        {
            if (entry.Key == null || entry.Value <= now)
                _expiredIgnores.Add(entry.Key);
        }

        foreach (TimedLootPickup pickup in _expiredIgnores)
            _ignoredLoot.Remove(pickup);
    }

    #endregion

    #region Hareket

    private Vector3 ComputeMovement(float now)
    {
        Vector3 position = transform.position;
        Vector3 direction;

        switch (_goal)
        {
            case Goal.Fight:
                direction = FightMovement(now);
                break;

            case Goal.Loot:
                direction = LootMovement(now);
                break;

            default:
                direction = Flat(_destination - position).magnitude > stoppingDistance
                    ? SteerTowards(_destination)
                    : Vector3.zero;
                break;
        }

        return ApplyUnstuck(direction, now);
    }

    private Vector3 FightMovement(float now)
    {
        if (_target == null)
            return Vector3.zero;

        Vector3 toTarget = Flat(_target.transform.position - transform.position);
        float distance = toTarget.magnitude;
        Vector3 forward = distance > 0.01f ? toTarget / distance : transform.forward;

        if (!HasUsableGun())
        {
            // Yumruk: dibine kadar gir.
            return distance > attackDistance * 0.8f ? SteerTowards(_target.transform.position) : Vector3.zero;
        }

        WeaponDefinition weapon = _loadout.CurrentWeapon;
        float preferred = Mathf.Clamp(weapon.range * preferredRangeRatio, 5f, 14f);
        Vector3 strafe = Vector3.Cross(Vector3.up, forward) * _strafeSign;

        if (!_targetVisible || distance > preferred + 2f)
            return SteerTowards(_target.transform.position);

        if (distance < preferred - 2f)
            return (-forward + strafe * 0.6f).normalized;

        // Ideal mesafede: yana kayarak ates et, sabit hedef olma.
        return strafe;
    }

    private Vector3 LootMovement(float now)
    {
        if (_lootTarget == null)
            return Vector3.zero;

        Vector3 lootPosition = _lootTarget.transform.position;
        float distance = Flat(lootPosition - transform.position).magnitude;
        float standRadius = Mathf.Max(0.35f, _lootTarget.CollectionRadius * 0.5f);

        if (distance > standRadius)
        {
            _lootWaitStarted = -1f;
            return SteerTowards(lootPosition);
        }

        // Esyanin ustunde bekle: toplama halkasi dolsun.
        if (_lootWaitStarted < 0f)
            _lootWaitStarted = now;

        if (now - _lootWaitStarted > lootGiveUpSeconds)
        {
            // Baska biri aliyor ya da ulasilamiyor; bir sure unut.
            _ignoredLoot[_lootTarget] = now + 12f;
            _lootTarget = null;
            _lootWaitStarted = -1f;
        }

        return Vector3.zero;
    }

    /// <summary>NavMesh yolunun bir sonraki kosesine dogru yon. NavMesh yoksa dogrudan hedefe.</summary>
    private Vector3 SteerTowards(Vector3 destination)
    {
        Vector3 position = transform.position;

        if (_repathTimer.ExpiredOrNotRunning(Runner) || Flat(destination - _pathTarget).sqrMagnitude > 4f)
        {
            Repath(position, destination);
            _repathTimer = TickTimer.CreateFromSeconds(Runner, repathInterval);
        }

        Vector3 waypoint = destination;

        while (_cornerIndex < _cornerCount)
        {
            if (Flat(_corners[_cornerIndex] - position).magnitude > 0.6f)
            {
                waypoint = _corners[_cornerIndex];
                break;
            }

            _cornerIndex++;
        }

        Vector3 direction = Flat(waypoint - position);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private void Repath(Vector3 from, Vector3 to)
    {
        _pathTarget = to;
        _cornerCount = 0;
        _cornerIndex = 0;

        if (!NavMesh.SamplePosition(from, out NavMeshHit start, 3f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(to, out NavMeshHit end, 4f, NavMesh.AllAreas))
            return;

        if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path) ||
            _path.status == NavMeshPathStatus.PathInvalid)
            return;

        _cornerCount = _path.GetCornersNonAlloc(_corners);

        // Ilk kose botun kendi konumu; ondan sonrakine yonel.
        _cornerIndex = _cornerCount > 1 ? 1 : 0;
    }

    /// <summary>
    /// Bir saniyede neredeyse hic ilerleyemeyen bot takilmistir (eski NavMesh,
    /// dar gecit, baska bir bot). Kisa bir sure yana kacar ve yeni yol arar.
    /// </summary>
    private Vector3 ApplyUnstuck(Vector3 direction, float now)
    {
        if (now < _unstuckUntil)
            return _unstuckDirection;

        if (direction.sqrMagnitude < 0.0001f)
        {
            _stuckCheckAt = now + 1f;
            _stuckCheckPosition = transform.position;
            return direction;
        }

        if (now >= _stuckCheckAt)
        {
            if (Flat(transform.position - _stuckCheckPosition).sqrMagnitude < 0.16f)
            {
                Vector3 side = Vector3.Cross(Vector3.up, direction) * (Random.value < 0.5f ? -1f : 1f);
                _unstuckDirection = (side - direction * 0.3f).normalized;
                _unstuckUntil = now + 0.7f;
                _repathTimer = default;
            }

            _stuckCheckPosition = transform.position;
            _stuckCheckAt = now + 1f;
        }

        return direction;
    }

    private Vector3 CalculateSeparation()
    {
        IReadOnlyList<PlayerRegistry> registry = PlayerRegistry.All;
        Vector3 separation = Vector3.zero;

        for (int i = 0; i < registry.Count; i++)
        {
            PlayerRegistry other = registry[i];

            if (other == null || other.gameObject == gameObject || other.Object == null)
                continue;

            // Sadece botlar birbirinden kacinir; oyuncudan kacmak istemiyoruz.
            if (other.IsHuman)
                continue;

            Vector3 away = Flat(transform.position - other.transform.position);
            float distance = away.magnitude;

            if (distance <= 0.001f || distance >= separationRadius)
                continue;

            separation += away.normalized * (1f - distance / separationRadius);
        }

        return Vector3.ClampMagnitude(separation, 1f);
    }

    #endregion

    #region Nisan

    private Vector3 ComputeAim(float now)
    {
        if (!attackEnabled || _target == null || !_target.IsAlive || !_targetVisible)
            return Vector3.zero;

        if (now < _reactionReadyAt)
            return Vector3.zero;

        Vector3 toTarget = Flat(_target.transform.position - transform.position);
        float distance = toTarget.magnitude;
        if (distance < 0.01f)
            return Vector3.zero;

        Vector3 direction = toTarget / distance;

        if (!HasUsableGun())
            return distance <= attackDistance ? direction : Vector3.zero;

        WeaponDefinition weapon = _loadout.CurrentWeapon;
        if (distance > weapon.range * 0.92f)
            return Vector3.zero;

        // Seri atis: bir sure ates et, kisa ara ver. Kesintisiz ates eden bot
        // hem yapay gorunur hem de oyuncuya nefes aldirmaz.
        if (now >= _burstUntil && now >= _pauseUntil)
        {
            _burstUntil = now + Random.Range(burstSeconds.x, burstSeconds.y);
            _pauseUntil = _burstUntil + Random.Range(burstPauseSeconds.x, burstPauseSeconds.y);
        }

        if (now >= _burstUntil)
            return Vector3.zero;

        // Uzaktaki hedefte hata biraz buyur.
        float error = _aimError * (0.6f + distance / Mathf.Max(1f, weapon.range));
        return Quaternion.Euler(0f, error, 0f) * direction;
    }

    private bool HasLineOfSight(PlayerRegistry target)
    {
        Vector3 from = transform.position + Vector3.up * 1.2f;
        Vector3 to = target.transform.position + Vector3.up;
        Vector3 delta = to - from;
        float length = delta.magnitude;

        if (length < 0.01f)
            return true;

        int hitCount = Physics.RaycastNonAlloc(from, delta / length, _sightHits, length, sightBlockers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _sightHits[i].collider;

            // Karakterler (kendisi, hedef, baskalari) gorusu kesmez; sadece dunya kesir.
            if (hit.transform.IsChildOf(transform) || hit.GetComponentInParent<HealthController>() != null)
                continue;

            if (_sightHits[i].distance < length - 0.3f)
                return false;
        }

        return true;
    }

    #endregion

    #region Alan

    private bool GetZone(out Vector3 center, out float radius)
    {
        center = Vector3.zero;
        radius = 0f;

        if (_safeZone == null || _safeZone.Object == null || !_safeZone.Object.IsValid ||
            _safeZone.SafeRadius <= 0f)
            return false;

        bool shrinkSoon = _safeZone.IsShrinking ||
                          (_safeZone.IsWaitingForShrink && _safeZone.RemainingPhaseSeconds < zoneLookaheadSeconds);

        if (shrinkSoon && _safeZone.HasNextZone)
        {
            center = _safeZone.NextCenter;
            radius = _safeZone.NextRadius;
        }
        else
        {
            center = _safeZone.SafeCenter;
            radius = _safeZone.SafeRadius;
        }

        return true;
    }

    private Vector3 RandomPointInZone(bool zoneKnown, Vector3 center, float radius)
    {
        Vector3 point;

        if (zoneKnown)
        {
            Vector2 offset = Random.insideUnitCircle * radius * 0.6f;
            point = center + new Vector3(offset.x, 0f, offset.y);
        }
        else
        {
            Vector2 offset = Random.insideUnitCircle * 12f;
            point = transform.position + new Vector3(offset.x, 0f, offset.y);
        }

        return NavMesh.SamplePosition(point, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : point;
    }

    private static Vector3 ClampIntoZone(Vector3 point, bool zoneKnown, Vector3 center, float radius)
    {
        if (!zoneKnown)
            return point;

        Vector3 offset = Flat(point - center);
        float limit = radius * 0.8f;

        return offset.magnitude > limit ? center + offset.normalized * limit : point;
    }

    #endregion

    private static Vector3 Flat(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }
}
