using Fusion;
using UnityEngine;

public class PlayerDeadState : PlayerStateBase
{
    [SerializeField] private float despawnDelay = 1.25f;

    [Networked] private TickTimer DespawnTimer { get; set; }

    /// <summary>PlayerLife bu state'e 3 numarayla geciyor.</summary>
    private const int DeadStateIndex = 3;

    private Animator _animator;
    private Collider _collider;
    private bool _hasDeathTrigger;
    private static readonly int DeathHash = Animator.StringToHash("Death");

    public override void InitState(PlayerStateMachine playerStateMachine)
    {
        base.InitState(playerStateMachine);
        _animator = GetComponentInChildren<Animator>();
        _collider = GetComponent<Collider>();

        if (_animator != null)
        {
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == DeathHash && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    _hasDeathTrigger = true;
                    break;
                }
            }
        }
    }

    public override void EnterState()
    {
        if (_animator != null && _hasDeathTrigger)
            _animator.SetTrigger(DeathHash);

        if (_collider != null)
            _collider.enabled = false;

        if (HasStateAuthority)
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, despawnDelay);
    }

    public override void UpdateNetworkState(NetworkInputData input)
    {
        // Olu oyuncu girdi islemez. Despawn sayaci FixedUpdateNetwork'te doner:
        // PlayerController olen oyuncu icin ProcessInput'u hic cagirmadigi icin
        // sayaci buraya baglamak oyuncunun sonsuza kadar sahnede kalmasina yol acardi.
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || stateMachine == null)
            return;

        if (stateMachine.ActiveStateIndex != DeadStateIndex)
            return;

        if (DespawnTimer.Expired(Runner))
            Runner.Despawn(Object);
    }

    public override void ExitState()
    {
    }
}
