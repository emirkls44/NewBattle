using UnityEngine;
using UnityEngine.AI;
using Fusion;

[RequireComponent(typeof(NavMeshAgent), typeof(HealthController))]
public class EnemyController : NetworkBehaviour
{
    public enum EnemyState { Patrolling, Chasing }
    [Networked] private EnemyState currentState { get; set; } = EnemyState.Patrolling;

    private NavMeshAgent _agent;
    private HealthController _healthController;
    private Transform _target;
    private NewBattle.Gameplay.PlayerVisibility _targetVisibility;
    private GameManager _gameManager;

    [Header("Görüþ ve Devriye Ayarlarý")]
    public float detectionRadius = 12f;
    public float patrolRadius = 10f;
    public float patrolWaitTime = 4f;

    [Networked] private TickTimer patrolTimer { get; set; }
    [Networked] private TickTimer scanTimer { get; set; }

    [Header("Ateþ Etme Ayarlarý")]
    public NetworkPrefabRef enemyBulletPrefab;
    public Transform firePoint;
    public float shootDistance = 8f;
    public float fireRate = 0.8f;
    public float bulletSpeed = 15f;
    public float inaccuracySpread = 0.2f;

    [Networked] private TickTimer nextFireTimer { get; set; }

    private Collider[] _playerHitColliders = new Collider[5];

    public override void Spawned()
    {
        _agent = GetComponent<NavMeshAgent>();
        _healthController = GetComponent<HealthController>();
        _gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();

        if (HasStateAuthority)
        {
            _healthController.OnDeath += HandleDeath;
            patrolTimer = TickTimer.CreateFromSeconds(Runner, patrolWaitTime);
            SetRandomPatrolDestination();
        }
        else
        {
            _agent.enabled = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_healthController != null) _healthController.OnDeath -= HandleDeath;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _healthController.currentHealth <= 0) return;

        if (scanTimer.ExpiredOrNotRunning(Runner))
        {
            FindClosestPlayer();
            scanTimer = TickTimer.CreateFromSeconds(Runner, 0.3f);
        }

        if (_target == null) return;

        float distanceToPlayerSqr = Vector3.SqrMagnitude(transform.position - _target.position);
        // Gizlenme kurali tek yerde yasiyor: bot da oyuncuyla ayni kurala uyar.
        bool isPlayerHidden = _targetVisibility != null &&
                              !_targetVisibility.IsBodyVisibleFrom(transform.position);

        if (distanceToPlayerSqr <= (detectionRadius * detectionRadius) && !isPlayerHidden)
            currentState = EnemyState.Chasing;
        else
            currentState = EnemyState.Patrolling;

        if (currentState == EnemyState.Chasing)
            ChaseAndShootLogic(Mathf.Sqrt(distanceToPlayerSqr));
        else
            PatrolLogic();
    }

    private void FindClosestPlayer()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _playerHitColliders);
        float closestDistSqr = Mathf.Infinity;
        Transform closestPlayer = null;

        for (int i = 0; i < hitCount; i++)
        {
            if (_playerHitColliders[i].CompareTag("Player"))
            {
                float distSqr = Vector3.SqrMagnitude(transform.position - _playerHitColliders[i].transform.position);
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    closestPlayer = _playerHitColliders[i].transform;
                }
            }
        }

        if (closestPlayer != _target)
        {
            _target = closestPlayer;
            if (_target != null) _target.TryGetComponent(out _targetVisibility);
        }
    }

    private void HandleDeath()
    {
        if (_gameManager != null) _gameManager.RegisterEnemyDeath();
        Runner.Despawn(Object);
    }

    private void PatrolLogic()
    {
        _agent.updateRotation = true;
        _agent.isStopped = false;

        if (patrolTimer.ExpiredOrNotRunning(Runner))
        {
            SetRandomPatrolDestination();
            patrolTimer = TickTimer.CreateFromSeconds(Runner, patrolWaitTime);
        }
    }

    private void SetRandomPatrolDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;
        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, patrolRadius, 1))
        {
            _agent.SetDestination(hit.position);
        }
    }

    private void ChaseAndShootLogic(float distanceToPlayer)
    {
        if (distanceToPlayer <= shootDistance)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.updateRotation = false;

            Vector3 direction = (_target.position - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Runner.DeltaTime * 10f);
            }

            if (nextFireTimer.ExpiredOrNotRunning(Runner))
            {
                ShootWithSpread();
                nextFireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
            }
        }
        else
        {
            _agent.isStopped = false;
            _agent.updateRotation = true;
            _agent.SetDestination(_target.position);
        }
    }

    private void ShootWithSpread()
    {
        if (firePoint == null || !enemyBulletPrefab.IsValid) return;

        Vector3 baseDirection = firePoint.forward;
        Vector3 randomSpread = new Vector3(
            Random.Range(-inaccuracySpread, inaccuracySpread), 0f, Random.Range(-inaccuracySpread, inaccuracySpread)
        );
        Vector3 finalDirection = (baseDirection + randomSpread).normalized;
        Quaternion shootRotation = Quaternion.LookRotation(finalDirection);

        Runner.Spawn(enemyBulletPrefab, firePoint.position, shootRotation, Object.InputAuthority, (runner, spawnedBullet) =>
        {
            if (spawnedBullet.TryGetComponent<Rigidbody>(out var bulletRb))
            {
                bulletRb.linearVelocity = finalDirection * bulletSpeed;
            }
        });
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, shootDistance);
    }
}