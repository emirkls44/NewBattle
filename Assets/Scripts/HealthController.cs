using System;
using Fusion;
using UnityEngine;

public class HealthController : NetworkBehaviour
{
    public enum DeathCause : byte
    {
        None = 0,
        Player = 1,
        Zone = 2
    }

    [Header("Health Settings")]
    public float maxHealth = 100f;
    [Networked] public float currentHealth { get; set; }

    [Header("Shield Settings")]
    public float maxShield = 100f;
    [Networked] public float currentShield { get; set; }

    [Tooltip("Kalkanin hasarin ne kadarini emdigi. 1 = hasar once tamamen kalkandan " +
             "gider, kalkan bitince tasan kisim cana isler.")]
    [Range(0f, 1f)] public float shieldAbsorbRatio = 1f;

    [Header("Yere Dusme (takim modu)")]
    [Tooltip("Takim modunda cani biten oyuncu, takimindan ayakta biri varsa olmez: yere duser. " +
             "Takimda ayakta kimse kalmazsa yerdekiler de olur.")]
    public bool knockdownEnabled = true;
    [Tooltip("Yere dusunce can bu olur ve zamanla azalir (bitince olur).")]
    [Min(1f)] public float downedHealth = 100f;
    [Tooltip("Kimse kaldirmazsa yerdeki oyuncu bu surede olur (saniye).")]
    [Min(3f)] public float bleedOutSeconds = 30f;
    [Tooltip("Yerdeyken alinan hasarin carpani (1.5 = %50 daha hizli azalir).")]
    [Min(1f)] public float downedDamageMultiplier = 1.5f;
    [Tooltip("Takim arkadasinin kaldirmak icin yaninda durmasi gereken sure (saniye).")]
    [Min(0.5f)] public float reviveSeconds = 5f;
    [Tooltip("Kaldirma halkasinin yaricapi (metre).")]
    [Min(0.5f)] public float reviveRadius = 1.8f;
    [Tooltip("Kaldirilinca canin ne kadari dolu olur (0.5 = %50). Kalkan ve silah olmaz.")]
    [Range(0.05f, 1f)] public float reviveHealthFraction = 0.5f;
    [Tooltip("Yerdeyken surunme hizi (normal hizin orani). 0 = hic hareket edemez.")]
    [Range(0f, 1f)] public float crawlSpeedMultiplier = 0.3f;

    /// <summary>Yerde, takim arkadasinin kaldirmasini bekliyor (canli sayilir ama hareket edemez).</summary>
    [Networked] public NetworkBool IsDowned { get; private set; }
    /// <summary>Kaldirma ilerlemesi 0-1.</summary>
    [Networked] public float ReviveProgress { get; private set; }
    [Networked] private PlayerRef KnockerPlayer { get; set; }
    [Networked] private NetworkId KnockerId { get; set; }
    [Networked] private NetworkString<_16> KnockerLabel { get; set; }

    /// <summary>Ayakta ve savasabilir (olu ya da yerde degil).</summary>
    public bool IsStanding => currentHealth > 0f && !IsDowned;

    public float ReviveRadius => reviveRadius;

    // Findik alani: rakibi yavaslatir (aga yazilir ki tahmin eden istemci de ayni hizda yurusun).
    [Networked] private TickTimer SlowTimer { get; set; }
    [Networked] private float SlowFactor { get; set; }

    /// <summary>Su an yavaslamis mi (findik alani).</summary>
    public bool IsSlowed => Runner != null && SlowTimer.IsRunning && !SlowTimer.Expired(Runner);

    /// <summary>Yurume hizi carpani: yerdeyken surunme, findik alaninda yavaslama.</summary>
    public float MoveMultiplier => (IsDowned ? crawlSpeedMultiplier : 1f) * (IsSlowed ? SlowFactor : 1f);

    /// <summary>Host: bu kadar sure hizi carpanla yavaslat (her tick yenilenir).</summary>
    public void ApplySlow(float factor, float seconds)
    {
        if (!HasStateAuthority)
            return;

        SlowFactor = Mathf.Clamp(factor, 0.02f, 1f);
        SlowTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    [Networked] public DeathCause LastDeathCause { get; private set; }
    /// <summary>Oldureni (oyuncu ya da bot) ag nesnesi.</summary>
    [Networked] public NetworkId KillerObject { get; private set; }
    [Networked] public NetworkString<_16> KillerWeapon { get; private set; }

    public event Action OnDeath;
    public event Action OnHealthChanged;

    private ChangeDetector _changeDetector;
    private NewBattle.Gameplay.PlayerPresence _presence;
    private PlayerCombatStats _ownStats;
    private CharacterAnimationDriver _animationDriver;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _animationDriver = GetComponent<CharacterAnimationDriver>();

        if (HasStateAuthority)
        {
            currentHealth = maxHealth;
            currentShield = 0f;
            IsDowned = false;
            ReviveProgress = 0f;
        }

        if (GetComponent<NewBattle.Gameplay.ReviveRingVisual>() == null)
            gameObject.AddComponent<NewBattle.Gameplay.ReviveRingVisual>();
    }

    public void TakeDamage(float baseDamage)
    {
        TakeDamage(baseDamage, PlayerRef.None);
    }

    public void TakeDamage(float baseDamage, PlayerRef attacker)
    {
        TakeDamage(baseDamage, attacker, null, null);
    }

    /// <summary>
    /// attackerObject / weaponLabel: kimin hangi silahla vurdugu. Olum
    /// mesajinda ("Dorian seni Pompali ile vurdu!") kullanilir. Ikisi de
    /// bossa hasar oyuncudan degil, guvenli alandan gelmistir.
    /// </summary>
    public void TakeDamage(float baseDamage, PlayerRef attacker, NetworkObject attackerObject, string weaponLabel)
    {
        if (!HasStateAuthority || currentHealth <= 0f || baseDamage <= 0f)
            return;

        if (IsFriendlyFire(attacker))
            return;

        // Bot zorlugu (oyuncu seviyesine gore): bkz. BotDifficulty.
        baseDamage *= NewBattle.Gameplay.BotDifficulty.DamageScale(attackerObject, Object);
        if (baseDamage <= 0f)
            return;

        if (IsDowned)
        {
            TakeDownedDamage(baseDamage, attacker, attackerObject, weaponLabel);
            return;
        }

        bool shieldWasHit = currentShield > 0f;

        // Kalkan EMER, bagisiklik vermez. Hasarin shieldAbsorbRatio kadari once
        // kalkandan dusulur; kalkan yetmezse arta kalan dogrudan cana isler.
        //
        // Onceki model doluluk oranini hasar azaltma yuzdesine ceviriyordu ve tam
        // kalkanda cana SIFIR hasar gidiyordu - yani 100 kalkanli bir oyuncu
        // kalkani bitene kadar oldurulemiyordu. Bir battle royale'de bu, ilk
        // kalkani bulan oyuncuyu dokunulmaz yapar.
        float absorbable = baseDamage * shieldAbsorbRatio;
        float absorbed = Mathf.Min(currentShield, absorbable);

        currentShield = Mathf.Max(0f, currentShield - absorbed);
        float healthDamage = baseDamage - absorbed;

        if (healthDamage > 0f)
            currentHealth = Mathf.Max(0f, currentHealth - healthDamage);

        // Hem vurulan hem vuran "catismada" sayilir. Kafa ustu can barlari
        // sadece bu pencere boyunca gorunur (bkz. WorldHealthBar).
        MarkCombatants(attacker);

        // Takim modunda arkadasi ayaktaysa olmez, yere duser.
        bool knocked = currentHealth <= 0f && CanBeKnockedDown();
        Rpc_ShowDamageNumber(baseDamage, shieldWasHit, attacker, currentHealth <= 0f && !knocked);

        if (knocked)
            KnockDown(attacker, attackerObject, weaponLabel);
        else if (currentHealth <= 0f)
            CompleteDeath(attacker, attackerObject, weaponLabel);
    }

    private void CompleteDeath(PlayerRef attacker, NetworkObject attackerObject, string weaponLabel)
    {
        IsDowned = false;
        ReviveProgress = 0f;
        currentHealth = 0f;
        RecordDeath(attacker, attackerObject, weaponLabel);
        AwardKill(attacker);
        Rpc_AnnounceDeath(attackerObject != null ? attackerObject.Id : default, weaponLabel ?? string.Empty);
        DropLoot();
        Die();
    }

    // ------------------------------------------------------------------
    // Yere dusme / kaldirma (sadece host)
    // ------------------------------------------------------------------

    private bool CanBeKnockedDown()
    {
        return knockdownEnabled &&
               NetworkBootstrap.ActiveMode == BattleGameMode.Squad &&
               NetworkBootstrap.ActiveTeamSize > 1 &&
               HasStandingTeammate();
    }

    private void KnockDown(PlayerRef attacker, NetworkObject attackerObject, string weaponLabel)
    {
        IsDowned = true;
        ReviveProgress = 0f;
        currentHealth = downedHealth;
        currentShield = 0f;

        KnockerPlayer = attacker;
        KnockerId = attackerObject != null ? attackerObject.Id : default;
        string weapon = weaponLabel ?? string.Empty;
        KnockerLabel = weapon.Length > 16 ? weapon.Substring(0, 16) : weapon;

        if (TryGetComponent(out PlayerLoadout held))
            held.UnequipThrowable();

        // Silah elden gider (yere duser, kalan mermisiyle); kaldirilinca elde silah olmaz.
        if (TryGetComponent(out PlayerLoadout loadout) && loadout.HasRangedWeapon)
        {
            LootSpawner spawner = Runner.GetComponent<LootSpawner>();
            if (spawner != null)
                spawner.DropDeathLoot(transform.position, loadout, supplies: false);
            loadout.DropWeapon();
        }

        Rpc_Knocked(attacker);
    }

    /// <summary>Yerdeyken vurulursa can daha hizli gider; biterse olur (vuran oldurmus sayilir).</summary>
    private void TakeDownedDamage(float baseDamage, PlayerRef attacker, NetworkObject attackerObject, string weaponLabel)
    {
        float damage = baseDamage * downedDamageMultiplier;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        MarkCombatants(attacker);
        Rpc_ShowDamageNumber(damage, false, attacker, currentHealth <= 0f);

        if (currentHealth <= 0f)
            FinishDowned(attacker, attackerObject, weaponLabel);
    }

    /// <summary>
    /// Yerdeki oyuncu olur. Vuran yoksa (kan kaybi, alan, takim elendi) yere seren oldurmus sayilir.
    /// </summary>
    private void FinishDowned(PlayerRef attacker, NetworkObject attackerObject, string weaponLabel)
    {
        if (attacker == PlayerRef.None && attackerObject == null)
        {
            attacker = KnockerPlayer;
            if (KnockerId.IsValid)
                Runner.TryFindObject(KnockerId, out attackerObject);
            weaponLabel = KnockerLabel.ToString();
        }

        CompleteDeath(attacker, attackerObject, weaponLabel);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !IsDowned || currentHealth <= 0f)
            return;

        // Takimda ayakta kimse kalmadi (oldu, yere dustu ya da cikti): kaldiracak kimse yok.
        if (!HasStandingTeammate())
        {
            FinishDowned(PlayerRef.None, null, null);
            return;
        }

        if (FindReviver() != null)
        {
            // Kaldirilirken kan kaybi durur.
            ReviveProgress = Mathf.Min(1f, ReviveProgress + Runner.DeltaTime / Mathf.Max(0.1f, reviveSeconds));
            if (ReviveProgress >= 1f)
                Revive();
            return;
        }

        ReviveProgress = 0f;
        currentHealth = Mathf.Max(0f, currentHealth - downedHealth / Mathf.Max(1f, bleedOutSeconds) * Runner.DeltaTime);
        if (currentHealth <= 0f)
            FinishDowned(PlayerRef.None, null, null);
    }

    private void Revive()
    {
        IsDowned = false;
        ReviveProgress = 0f;
        currentHealth = Mathf.Max(1f, maxHealth * reviveHealthFraction);
        currentShield = 0f;
        Rpc_Revived();
    }

    private bool HasStandingTeammate()
    {
        PlayerCombatStats own = OwnStats();
        if (own == null)
            return false;

        var registry = NewBattle.Gameplay.PlayerRegistry.All;
        for (int i = 0; i < registry.Count; i++)
        {
            NewBattle.Gameplay.PlayerRegistry entry = registry[i];
            if (IsStandingTeammate(entry, own))
                return true;
        }

        return false;
    }

    /// <summary>Kaldirma halkasinin icinde duran, ayaktaki takim arkadasi.</summary>
    private HealthController FindReviver()
    {
        PlayerCombatStats own = OwnStats();
        if (own == null)
            return null;

        Vector3 position = transform.position;
        var registry = NewBattle.Gameplay.PlayerRegistry.All;
        for (int i = 0; i < registry.Count; i++)
        {
            NewBattle.Gameplay.PlayerRegistry entry = registry[i];
            if (!IsStandingTeammate(entry, own))
                continue;

            Vector3 delta = entry.transform.position - position;
            if (Mathf.Abs(delta.y) > 2f)
                continue;

            delta.y = 0f;
            if (delta.sqrMagnitude <= reviveRadius * reviveRadius)
                return entry.Health;
        }

        return null;
    }

    private bool IsStandingTeammate(NewBattle.Gameplay.PlayerRegistry entry, PlayerCombatStats own)
    {
        return entry != null && entry.Health != null && entry.Health != this &&
               entry.Object != null && entry.Object.IsValid && entry.Runner == Runner &&
               entry.Health.IsStanding && own.IsTeammate(entry.Stats);
    }

    private PlayerCombatStats OwnStats()
    {
        if (_ownStats == null)
            _ownStats = GetComponent<PlayerCombatStats>();
        return _ownStats;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_Knocked(PlayerRef attacker)
    {
        if (HasInputAuthority)
        {
            NewBattle.Audio.Sfx.PlayHud("olum");
            NewBattle.Gameplay.ScreenAnnouncer.Show("YERE DÜŞTÜN!\nTakım arkadaşın seni kaldırabilir");
        }
        else if (Runner != null && attacker != PlayerRef.None && attacker == Runner.LocalPlayer)
        {
            NewBattle.Audio.Sfx.PlayHud("oldurme");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_Revived()
    {
        if (HasInputAuthority)
        {
            NewBattle.Audio.Sfx.PlayHud("odul");
            NewBattle.Gameplay.ScreenAnnouncer.Show("KALDIRILDIN!");
        }
    }

    /// <summary>
    /// Olumu tum cihazlara duyurur: oldurme akisi, oldurme afisi ve "son 3
    /// oyuncu" uyarisi bunu dinler (bkz. KillEvents). Oldureni bos ise alan oldurdu.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_AnnounceDeath(NetworkId killerId, string weaponLabel)
    {
        PlayerCombatStats victimStats = GetComponent<PlayerCombatStats>();
        PlayerCombatStats killerStats = null;
        if (killerId.IsValid && Runner != null && Runner.TryFindObject(killerId, out NetworkObject killerObject))
            killerStats = killerObject.GetComponent<PlayerCombatStats>();

        PlayerCombatStats local = FindLocalStats();

        int alive = 0;
        foreach (HealthController player in FindObjectsByType<HealthController>(FindObjectsSortMode.None))
        {
            if (player != this && player.Object != null && player.Object.IsValid && player.currentHealth > 0f)
                alive++;
        }

        NewBattle.Gameplay.KillEvents.Raise(new NewBattle.Gameplay.KillInfo
        {
            KillerName = killerStats != null ? killerStats.NameOrDefault : string.Empty,
            VictimName = victimStats != null ? victimStats.NameOrDefault : "Oyuncu",
            Weapon = NewBattle.Gameplay.KillEvents.WeaponDisplayName(weaponLabel),
            ByZone = killerStats == null,
            KillerIsLocal = killerStats != null && killerStats.HasInputAuthority,
            VictimIsLocal = HasInputAuthority,
            KillerIsTeammate = killerStats != null && local != null && killerStats != local && local.IsTeammate(killerStats),
            VictimIsTeammate = victimStats != null && local != null && victimStats != local && local.IsTeammate(victimStats),
            AliveAfter = alive,
            VictimPosition = transform.position
        });
    }

    private static PlayerCombatStats FindLocalStats()
    {
        foreach (PlayerCombatStats stats in FindObjectsByType<PlayerCombatStats>(FindObjectsSortMode.None))
        {
            if (stats.Object != null && stats.Object.IsValid && stats.HasInputAuthority)
                return stats;
        }

        return null;
    }

    private void RecordDeath(PlayerRef attacker, NetworkObject attackerObject, string weaponLabel)
    {
        bool byPlayer = attackerObject != null || attacker != PlayerRef.None;
        LastDeathCause = byPlayer ? DeathCause.Player : DeathCause.Zone;
        KillerObject = attackerObject != null ? attackerObject.Id : default;

        string weapon = weaponLabel ?? string.Empty;
        KillerWeapon = weapon.Length > 16 ? weapon.Substring(0, 16) : weapon;
    }

    private void MarkCombatants(PlayerRef attacker)
    {
        if (_presence == null)
            _presence = GetComponent<NewBattle.Gameplay.PlayerPresence>();

        if (_presence != null)
            _presence.MarkCombat();

        NewBattle.Gameplay.PlayerPresence attackerPresence =
            NewBattle.Gameplay.PlayerPresence.Find(attacker);

        if (attackerPresence != null)
            attackerPresence.MarkCombat();
    }

    private bool IsFriendlyFire(PlayerRef attacker)
    {
        if (attacker == PlayerRef.None)
            return false;

        if (_ownStats == null)
            _ownStats = GetComponent<PlayerCombatStats>();

        if (_ownStats == null)
            return false;

        // Sahne taramasi yerine O(1) kayit defteri: her mermi icin butun
        // sahneyi taramak mobilde olculebilir bir maliyetti.
        NewBattle.Gameplay.PlayerRegistry attackerEntry =
            NewBattle.Gameplay.PlayerRegistry.Find(attacker);

        return attackerEntry != null && _ownStats.IsTeammate(attackerEntry.Stats);
    }

    private void AwardKill(PlayerRef attacker)
    {
        if (attacker == PlayerRef.None || (Object != null && Object.InputAuthority == attacker))
            return;

        NewBattle.Gameplay.PlayerRegistry attackerEntry =
            NewBattle.Gameplay.PlayerRegistry.Find(attacker);

        if (attackerEntry != null && attackerEntry.Stats != null)
            attackerEntry.Stats.AddKill();
    }

    public void Heal(float healAmount)
    {
        if (!HasStateAuthority || healAmount <= 0f || IsDowned)
            return;

        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
    }

    public void AddShield(float shieldAmount)
    {
        if (!HasStateAuthority || shieldAmount <= 0f || IsDowned)
            return;

        currentShield = Mathf.Min(currentShield + shieldAmount, maxShield);
    }

    public override void Render()
    {
        if (_changeDetector == null)
            return;

        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(currentHealth) || change == nameof(currentShield) || change == nameof(IsDowned))
                OnHealthChanged?.Invoke();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ShowDamageNumber(float damage, bool shieldHit, PlayerRef attacker, bool killed)
    {
        DamageNumberPopup.Show(transform.position, damage, shieldHit);
        PlayHitSounds(attacker, killed);

        if (HasInputAuthority)
            NewBattle.Gameplay.KillEvents.RaiseLocalDamaged(damage);

        // Govdenin kendisi de tepki versin: hasar sayisi karakterin ustunde
        // belirirken goz nisan alirken govdede oluyor.
        NewBattle.Gameplay.HitFlash.Play(gameObject);

        if (_animationDriver != null)
            _animationDriver.PlayHitFlinch();
    }

    /// <summary>
    /// Vuran bensem isabet "tik"i (oldurduysem zil), vurulan bensem darbe
    /// (oldusem olum sesi). Baskalarinin birbirini vurmasi sessiz; onu
    /// zaten atis sesinden duyuyoruz.
    /// </summary>
    private void PlayHitSounds(PlayerRef attacker, bool killed)
    {
        if (Runner == null)
            return;

        if (HasInputAuthority)
        {
            NewBattle.Audio.Sfx.PlayHud(killed ? "olum" : "hasar_al");
            return;
        }

        if (attacker != PlayerRef.None && attacker == Runner.LocalPlayer)
            NewBattle.Audio.Sfx.PlayHud(killed ? "oldurme" : "isabet");
    }

    /// <summary>Oyuncu / bot olunce silahi, can ve kalkan paketi yere saçilir (host).</summary>
    private void DropLoot()
    {
        if (Runner == null || !Runner.IsServer || !TryGetComponent(out PlayerLoadout loadout))
            return;

        LootSpawner spawner = Runner.GetComponent<LootSpawner>();
        if (spawner != null)
            spawner.DropDeathLoot(transform.position, loadout);
    }

    private void Die()
    {
        OnDeath?.Invoke();
    }
}
