using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerStateMachine), typeof(PlayerShooting))]
public class TrainingBot : NetworkBehaviour
{
    [Header("Bot Ayarlari")]
    [SerializeField] private float stoppingDistance = 1.6f;
    [SerializeField] private float targetSearchInterval = 0.5f;
    [SerializeField] private bool attackEnabled;
    [SerializeField] private float attackDistance = 1.8f;
    [SerializeField] private float separationRadius = 1.1f;
    [SerializeField] private float separationWeight = 1.5f;

    private PlayerStateMachine _stateMachine;
    private PlayerShooting _shooting;
    private Transform _target;
    private PlayerCombatStats _ownStats;
    private TickTimer _targetSearchTimer;
    private LobbyCountdownController _lobbyCountdown;
    private DropPhaseController _dropPhase;

    private void Awake()
    {
        _stateMachine = GetComponent<PlayerStateMachine>();
        _shooting = GetComponent<PlayerShooting>();
    }

    public override void Spawned()
    {
        _lobbyCountdown = UnityEngine.Object.FindFirstObjectByType<LobbyCountdownController>();
        _dropPhase = UnityEngine.Object.FindFirstObjectByType<DropPhaseController>();

        // InputAuthority'si olan nesne gercek oyuncudur; AI sadece botlarda calisir.
        enabled = Object.InputAuthority == PlayerRef.None;

        if (HasStateAuthority && enabled)
            FindNearestHuman();
    }

    public override void FixedUpdateNetwork()
    {
        var ownHealth = GetComponent<HealthController>();
        if (ownHealth != null && ownHealth.currentHealth <= 0f) return;
        if (!HasStateAuthority || Object.InputAuthority != PlayerRef.None)
            return;

        if (_lobbyCountdown != null && !_lobbyCountdown.MatchStarted)
            return;

        if (_dropPhase != null && !_dropPhase.GameplayStarted)
            return;

        if (_targetSearchTimer.ExpiredOrNotRunning(Runner))
        {
            FindNearestHuman();
            _targetSearchTimer = TickTimer.CreateFromSeconds(Runner, targetSearchInterval);
        }

        NetworkInputData botInput = default;

        if (_target != null)
            _lastKnownTargetPosition = _target.position;

        Vector3? destination = _target != null ? _target.position : _lastKnownTargetPosition;

        if (destination.HasValue)
        {
            Vector3 difference = destination.Value - transform.position;
            difference.y = 0f;
            float distance = difference.magnitude;

            if (distance > 0.01f)
            {
                Vector3 direction = difference / distance;

                Vector3 desiredDirection = distance > stoppingDistance
                    ? direction
                    : Vector3.zero;

                // Botlar ayni hedefe giderken birbirlerinin icine girmesin.
                Vector3 moveDirection = desiredDirection + CalculateSeparation() * separationWeight;
                if (moveDirection.sqrMagnitude > 0.01f)
                {
                    moveDirection.Normalize();
                    botInput.JoystickInput = new Vector2(moveDirection.x, moveDirection.z);
                }

                // Ates sadece hedef GERCEKTEN gorunuyorken: son bilinen konuma
                // yurumek serbest, gorunmeyen oyuncuya ates etmek degil.
                if (attackEnabled && _target != null && distance <= attackDistance)
                    botInput.RightJoystickVector = new Vector2(direction.x, direction.z);

                // Son bilinen konuma varildiysa arayisi birak.
                if (_target == null && distance <= stoppingDistance)
                    _lastKnownTargetPosition = null;
            }
        }

        _stateMachine.ProcessInput(botInput);

        if (botInput.RightJoystickVector.sqrMagnitude > 0.01f)
            _shooting.ProcessShooting(botInput.RightJoystickVector);
    }

    private void FindNearestHuman()
    {
        if (_ownStats == null)
            _ownStats = GetComponent<PlayerCombatStats>();

        // requireVisible: bot cimen gizlenmesine oyuncuyla AYNI kurala uyar.
        // Onceki surum bu kontrolu hic yapmiyordu - oyuncu caliya girip gorunmez
        // oluyor ama botlar yine de uzerine kosuyordu; gizlenmeyi tamamen anlamsiz
        // kilan sey buydu.
        NewBattle.Gameplay.PlayerRegistry nearest =
            NewBattle.Gameplay.PlayerRegistry.FindNearestEnemy(
                transform.position,
                _ownStats,
                Runner,
                exclude: gameObject,
                humansOnly: true,
                requireVisible: true);

        _target = nearest != null ? nearest.transform : null;
    }

    /// <summary>
    /// Hedefini kaybeden bot en son gordugu yere gider, oraya varinca durur.
    /// Oyuncunun caliya girip yok olmasi botu aninda dondurmemeli; aksi halde
    /// gizlenmek "botlar beni unuttu" gibi yapay gorunur.
    /// </summary>
    private Vector3? _lastKnownTargetPosition;

    private Vector3 CalculateSeparation()
    {
        var registry = NewBattle.Gameplay.PlayerRegistry.All;
        Vector3 separation = Vector3.zero;

        for (int i = 0; i < registry.Count; i++)
        {
            NewBattle.Gameplay.PlayerRegistry other = registry[i];

            if (other == null || other.gameObject == gameObject || other.Object == null)
                continue;

            // Sadece botlar birbirinden kacinir; oyuncudan kacmak istemiyoruz.
            if (other.IsHuman)
                continue;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float distance = away.magnitude;

            if (distance <= 0.001f || distance >= separationRadius)
                continue;

            separation += away.normalized * (1f - distance / separationRadius);
        }

        return Vector3.ClampMagnitude(separation, 1f);
    }
}
