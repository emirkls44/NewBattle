using Fusion;
using UnityEngine;

public class PlayerStateMachine : NetworkBehaviour
{
    [Networked] public int ActiveStateIndex { get; set; }

    private PlayerStateBase[] _states;
    private PlayerStateBase _currentState;
    private ChangeDetector _changeDetector;

    // Alt state'lerin erisebilecegi merkezi Animator referansi
    public Animator Animator { get; private set; }

    public override void Spawned()
    {
        _states = GetComponents<PlayerStateBase>();

        // Karakterin altindaki Animator bilesenini otomatik bul
        Animator = GetComponentInChildren<Animator>();

        foreach (var state in _states)
        {
            state.InitState(this);
        }

        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority && _states.Length > 0)
        {
            ChangeState(0);
        }
        else
        {
            // Proxy'ler durumu ag uzerinden alir. Bu olmadan uzak oyuncularda
            // EnterState hic calismaz: yurume animasyonu, olum tetigi ve
            // collider kapatma sadece yetkili tarafta gorunurdu.
            ApplyNetworkedState();
        }
    }

    public override void Render()
    {
        if (_changeDetector == null || HasStateAuthority)
            return;

        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(ActiveStateIndex))
                ApplyNetworkedState();
        }
    }

    /// <summary>
    /// Yetkisiz tarafta gorsel state gecisini uygular. Simulasyona dokunmaz:
    /// EnterState/ExitState icindeki yazma islemleri zaten HasStateAuthority
    /// ile korunuyor, geriye sadece animator ve gorsel etkiler kaliyor.
    /// </summary>
    private void ApplyNetworkedState()
    {
        if (_states == null || ActiveStateIndex < 0 || ActiveStateIndex >= _states.Length)
            return;

        PlayerStateBase target = _states[ActiveStateIndex];

        if (target == _currentState)
            return;

        if (_currentState != null)
            _currentState.ExitState();

        _currentState = target;
        _currentState.EnterState();
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