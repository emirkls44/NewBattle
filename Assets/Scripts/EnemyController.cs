using UnityEngine;
using UnityEngine.AI;
using Fusion;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : NetworkBehaviour
{
    public enum EnemyState { Patrolling, Chasing }
    [Networked] private EnemyState currentState { get; set; } = EnemyState.Patrolling;

    private NavMeshAgent agent;
    private Transform target;
    private PlayerHide playerHideScript;

    [Header("Can Sistemi")]
    public float maxHealth = 100f;
    [Networked, OnChangedRender(nameof(OnHealthChanged))]
    private float currentHealth { get; set; }

    [Header("Animasyon")]
    public Animator anim;

    [Header("Görüþ ve Devriye Ayarlarý")]
    public float detectionRadius = 12f;
    public float patrolRadius = 10f;
    public float patrolWaitTime = 4f;
    [Networked] private TickTimer patrolTimer { get; set; }

    [Header("Ateþ Etme Ayarlarý")]
    public NetworkPrefabRef enemyBulletPrefab; // DEÐÝÞÝKLÝK: Fusion Object Pool için NetworkPrefabRef kullanýldý.
    public Transform firePoint;
    public float shootDistance = 8f;
    public float fireRate = 0.8f;
    public float bulletSpeed = 15f;

    [Range(0f, 0.4f)]
    public float inaccuracySpread = 0.2f;
    [Networked] private TickTimer nextFireTimer { get; set; }

    private Collider[] _playerHitColliders = new Collider[5];

    public override void Spawned()
    {
        agent = GetComponent<NavMeshAgent>();

        if (HasStateAuthority)
        {
            currentHealth = maxHealth;
            patrolTimer = TickTimer.CreateFromSeconds(Runner, patrolWaitTime);
            SetRandomPatrolDestination();
        }
        else
        {
            // PERFORMANS: Ýstemcilerde NavMeshAgent kapatýlarak CPU yükü sýfýrlandý.
            agent.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // GÜVENLÝK VE PERFORMANS: Yapay zeka ve hareket hesaplamalarý sadece sunucuda (State Authority) yürütülür.
        if (!HasStateAuthority || currentHealth <= 0) return;

        FindClosestPlayer();

        if (target == null) return;

        float distanceToPlayerSqr = Vector3.SqrMagnitude(transform.position - target.position);
        bool isPlayerHidden = (playerHideScript != null && playerHideScript.isHidden);

        if (distanceToPlayerSqr <= (detectionRadius * detectionRadius) && !isPlayerHidden)
        {
            currentState = EnemyState.Chasing;
        }
        else
        {
            currentState = EnemyState.Patrolling;
        }

        if (currentState == EnemyState.Chasing)
        {
            ChaseAndShootLogic(Mathf.Sqrt(distanceToPlayerSqr));
        }
        else if (currentState == EnemyState.Patrolling)
        {
            PatrolLogic();
        }
    }

    private void FindClosestPlayer()
    {
        // PERFORMANS: Her kare FindGameObject yerine NonAlloc fizik taramasý kullanýldý.
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

        if (closestPlayer != target)
        {
            target = closestPlayer;
            if (target != null)
            {
                playerHideScript = target.GetComponent<PlayerHide>();
            }
        }
    }

    public void TakeDamage(float damageAmount)
    {
        if (!HasStateAuthority) return;

        currentHealth -= damageAmount;

        if (currentHealth <= 0)
        {
            DusmanOlum();
        }
    }

    private void OnHealthChanged()
    {
        // UI can barý görsel güncellemesi istemcilerde render anýnda tetiklenir.
    }

    private void DusmanOlum()
    {
        // PERFORMANS: Destroy() yerine að havuzlama (Despawn) kullanýldý.
        Runner.Despawn(Object);
    }

    private void PatrolLogic()
    {
        agent.updateRotation = true;
        agent.isStopped = false;

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
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }

    private void ChaseAndShootLogic(float distanceToPlayer)
    {
        if (distanceToPlayer <= shootDistance)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.updateRotation = false;

            Vector3 direction = (target.position - transform.position).normalized;
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
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.SetDestination(target.position);
        }
    }

    private void ShootWithSpread()
    {
        if (firePoint == null || !enemyBulletPrefab.IsValid) return;

        Vector3 baseDirection = firePoint.forward;
        Vector3 randomSpread = new Vector3(
            Random.Range(-inaccuracySpread, inaccuracySpread),
            0f,
            Random.Range(-inaccuracySpread, inaccuracySpread)
        );
        Vector3 finalDirection = (baseDirection + randomSpread).normalized;
        Quaternion shootRotation = Quaternion.LookRotation(finalDirection);

        // PERFORMANS: Instantiate yerine Fusion Object Pool (Runner.Spawn) kullanýldý.
        Runner.Spawn(enemyBulletPrefab, firePoint.position, shootRotation, Object.InputAuthority, (runner, spawnedBullet) =>
        {
            Rigidbody bulletRb = spawnedBullet.GetComponent<Rigidbody>();
            if (bulletRb != null)
            {
                bulletRb.linearVelocity = finalDirection * bulletSpeed;
            }
        });
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shootDistance);
    }
}