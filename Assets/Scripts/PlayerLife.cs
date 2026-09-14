using Fusion;
using UnityEngine;

[RequireComponent(typeof(HealthController), typeof(PlayerStateMachine))]
public class PlayerLife : NetworkBehaviour
{
    private HealthController _health;
    private PlayerStateMachine _stateMachine;

    private void Awake()
    {
        _health = GetComponent<HealthController>();
        _stateMachine = GetComponent<PlayerStateMachine>();
    }

    public override void Spawned()
    {
        _health.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (HasStateAuthority)
            _stateMachine.ChangeState(3);
    }
}
