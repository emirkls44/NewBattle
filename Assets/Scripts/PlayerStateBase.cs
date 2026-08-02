using Fusion;
using UnityEngine;

public abstract class PlayerStateBase : NetworkBehaviour
{
    protected PlayerStateMachine stateMachine;

    public virtual void InitState(PlayerStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }

    public abstract void EnterState();
    public abstract void UpdateNetworkState(NetworkInputData input);
    public abstract void ExitState();
}