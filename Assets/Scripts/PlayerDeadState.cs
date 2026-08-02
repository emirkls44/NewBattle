using Fusion;
using UnityEngine;

public class PlayerDeadState : PlayerStateBase
{
    private Animator _animator;
    private readonly int deathHash = Animator.StringToHash("Death");

    public override void InitState(PlayerStateMachine stateMachine)
    {
        base.InitState(stateMachine);
        _animator = GetComponentInChildren<Animator>();
    }

    public override void EnterState()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(deathHash);
        }

        if (TryGetComponent<Collider>(out var col))
        {
            col.enabled = false;
        }

        if (HasStateAuthority)
        {
            // Örnek: Ölüm animasyonu veya süresi bittikten sonra objeyi aðdan kaldýr
            Runner.Despawn(Object);
        }
    }

    public override void UpdateNetworkState(NetworkInputData input)
    {
        // Ölü bir karakter hiçbir girdiyi iþleyemez.
    }

    public override void ExitState()
    {
    }
}