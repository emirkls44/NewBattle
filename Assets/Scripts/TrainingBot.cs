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
        {
            Vector3 difference = _target.position - transform.position;
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

                if (attackEnabled && distance <= attackDistance)
                    botInput.RightJoystickVector = new Vector2(direction.x, direction.z);
            }
        }

        _stateMachine.ProcessInput(botInput);

        if (botInput.RightJoystickVector.sqrMagnitude > 0.01f)
            _shooting.ProcessShooting(botInput.RightJoystickVector);
    }

    private void FindNearestHuman()
    {
        PlayerController[] players = UnityEngine.Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        float nearestDistanceSquared = float.MaxValue;
        Transform nearestTarget = null;

        foreach (PlayerController player in players)
        {
            if (player == null || player.Object == null)
                continue;

            if (!player.Object.IsValid || player.Runner != Runner || player.gameObject == gameObject) continue;
            var health = player.GetComponent<HealthController>();
            if (health == null || health.currentHealth <= 0f) continue;
            var ownStats = GetComponent<PlayerCombatStats>();
            var targetStats = player.GetComponent<PlayerCombatStats>();
            if (ownStats != null && ownStats.IsTeammate(targetStats)) continue;

            float distanceSquared = (player.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            nearestTarget = player.transform;
        }

        _target = nearestTarget;
    }

    private Vector3 CalculateSeparation()
    {
        TrainingBot[] bots = UnityEngine.Object.FindObjectsByType<TrainingBot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        Vector3 separation = Vector3.zero;

        foreach (TrainingBot other in bots)
        {
            if (other == null || other == this || other.Object == null)
                continue;

            if (other.Object.InputAuthority != PlayerRef.None)
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
