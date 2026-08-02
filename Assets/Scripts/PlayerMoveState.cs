using Fusion;
using UnityEngine;

public class PlayerMoveState : PlayerStateBase
{
    [Header("Hareket Ayarlarý")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 15f;

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

        if (input.JoystickInput.sqrMagnitude <= 0.01f)
        {
            stateMachine.ChangeState(0);
            return;
        }

        Vector3 moveDirection = new Vector3(input.JoystickInput.x, 0, input.JoystickInput.y).normalized;
        transform.position += moveDirection * moveSpeed * Runner.DeltaTime;

        if (moveDirection != Vector3.zero && input.RightJoystickVector.sqrMagnitude <= 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Runner.DeltaTime * rotationSpeed);
        }
    }

    public override void ExitState()
    {
    }
}