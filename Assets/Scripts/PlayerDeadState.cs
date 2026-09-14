using Fusion;
using UnityEngine;

public class PlayerDeadState : PlayerStateBase
{
    [SerializeField] private float despawnDelay = 1.25f;

    [Networked] private TickTimer DespawnTimer { get; set; }

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
        if (HasStateAuthority && DespawnTimer.Expired(Runner))
            Runner.Despawn(Object);
    }

    public override void ExitState()
    {
    }
}
