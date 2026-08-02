using Fusion;
using UnityEngine;

public class PlayerIdleState : PlayerStateBase
{
    public override void EnterState()
    {
    }

    public override void UpdateNetworkState(NetworkInputData input)
    {
        if (input.RightJoystickVector.sqrMagnitude > 0.01f)
        {
            Vector3 aimDirection = new Vector3(input.RightJoystickVector.x, 0, input.RightJoystickVector.y).normalized;
            transform.rotation = Quaternion.LookRotation(aimDirection);

            if (TryGetComponent<PlayerShooting>(out var shooter))
            {
                shooter.ProcessShooting(input.RightJoystickVector);
            }
        }

        if (input.JoystickInput.sqrMagnitude > 0.01f)
        {
            stateMachine.ChangeState(1);
        }
    }

    public override void ExitState()
    {
    }
}