using Fusion;
using UnityEngine;

public class PlayerIdleState : PlayerStateBase
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    public override void EnterState()
    {
        if (stateMachine.Animator != null)
            stateMachine.Animator.SetBool(IsMovingHash, false);
    }

    public override void UpdateNetworkState(NetworkInputData input)
    {
        if (input.RightJoystickVector.sqrMagnitude > 0.01f)
        {
            Vector3 aimDirection = new Vector3(
                input.RightJoystickVector.x,
                0f,
                input.RightJoystickVector.y
            ).normalized;

            transform.rotation = Quaternion.LookRotation(aimDirection);
        }

        if (input.JoystickInput.sqrMagnitude > 0.01f)
            stateMachine.ChangeState(1);
    }

    public override void ExitState()
    {
    }
}
