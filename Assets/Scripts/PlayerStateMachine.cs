using Fusion;
using UnityEngine;

public class PlayerStateMachine : NetworkBehaviour
{
    [Networked] public int ActiveStateIndex { get; set; }

    private PlayerStateBase[] _states;
    private PlayerStateBase _currentState;

    // MÝMARÝ MÜDAHALE: Alt state'lerin eriþebileceði merkezi Animator referansý
    public Animator Animator { get; private set; }

    public override void Spawned()
    {
        _states = GetComponents<PlayerStateBase>();

        // Karakterin altýndaki Animator bileþenini otomatik bul
        Animator = GetComponentInChildren<Animator>();

        foreach (var state in _states)
        {
            state.InitState(this);
        }

        if (HasStateAuthority && _states.Length > 0)
        {
            ChangeState(0);
        }
    }

    public void ChangeState(int newStateIndex)
    {
        if (!HasStateAuthority || newStateIndex < 0 || newStateIndex >= _states.Length) return;

        if (_currentState != null)
        {
            _currentState.ExitState();
        }

        ActiveStateIndex = newStateIndex;
        _currentState = _states[ActiveStateIndex];
        _currentState.EnterState();
    }

    public void ProcessInput(NetworkInputData input)
    {
        if (_currentState != null)
        {
            _currentState.UpdateNetworkState(input);
        }
    }
}