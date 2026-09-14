using Fusion;
using UnityEngine;

public class PlayerMoveState : PlayerStateBase
{
    [Header("Hareket Ayarlari")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 15f;

    [Header("Ada Siniri")]
    public Vector2 arenaCenter = Vector2.zero;
    public float arenaRadius = 33f;

    private CharacterController _characterController;
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    public override void InitState(PlayerStateMachine playerStateMachine)
    {
        base.InitState(playerStateMachine);
        _characterController = GetComponent<CharacterController>();
    }

    public override void EnterState()
    {
        if (stateMachine.Animator != null)
            stateMachine.Animator.SetBool(IsMovingHash, true);
    }

    public override void UpdateNetworkState(NetworkInputData input)
    {
        if (input.RightJoystickVector.sqrMagnitude > 0.01f)
        {
            Vector3 aimDirection = new(
                input.RightJoystickVector.x,
                0f,
                input.RightJoystickVector.y
            );

            transform.rotation = Quaternion.LookRotation(aimDirection.normalized);
        }

        if (input.JoystickInput.sqrMagnitude <= 0.01f)
        {
            stateMachine.ChangeState(0);
            return;
        }

        Vector3 moveDirection = new(
            input.JoystickInput.x,
            0f,
            input.JoystickInput.y
        );
        moveDirection.Normalize();

        Vector3 movement = moveDirection * moveSpeed * Runner.DeltaTime;

        // Transform tasimak yerine CharacterController kullanarak carpismalari koru.
        if (_characterController != null && _characterController.enabled)
            _characterController.Move(movement);
        else
            transform.position += movement;

        KeepInsideArena();

        if (input.RightJoystickVector.sqrMagnitude <= 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Runner.DeltaTime * rotationSpeed
            );
        }
    }

    public override void ExitState()
    {
    }

    private void KeepInsideArena()
    {
        Vector3 position = transform.position;
        Vector2 fromCenter = new(
            position.x - arenaCenter.x,
            position.z - arenaCenter.y
        );

        if (fromCenter.sqrMagnitude <= arenaRadius * arenaRadius)
            return;

        Vector2 clamped = fromCenter.normalized * arenaRadius;
        Vector3 allowedPosition = new(
            arenaCenter.x + clamped.x,
            position.y,
            arenaCenter.y + clamped.y
        );

        Vector3 correction = allowedPosition - position;

        if (_characterController != null && _characterController.enabled)
            _characterController.Move(correction);
        else
            transform.position = allowedPosition;
    }
}
