using UnityEngine;

/// <summary>
/// Oyuncunun cali/uzun ot icinde oldugu durum.
///
/// NOT: Bu state artik GORUNURLUK ile ilgilenmiyor. Daha once burada butun
/// renderer'larin alfasi dusuruluyordu; bu yanlisti, cunku gorunurluk herkes icin
/// ayni oluyordu - takim arkadasin da seni kaybediyordu, dusman da.
///
/// Artik is bolumu soyle:
///   - PlayerPresence   : "cimende miyim / hareket ediyor muyum" (aga yazilan gercek)
///   - PlayerVisibility : "bu cihazdaki gozlemci beni goruyor mu" (yerel karar)
///   - PlayerHide (bu)  : sadece state makinesi ve cali icindeki hareket
/// </summary>
public class PlayerHide : PlayerStateBase
{
    [Header("Hareket")]
    [Tooltip("Cali icinde hareket normalden yavas: gizlenmenin bedeli.")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 3.5f;
    [SerializeField, Min(1f)] private float rotationSpeed = 15f;

    private CharacterController _characterController;
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    public override void InitState(PlayerStateMachine playerStateMachine)
    {
        base.InitState(playerStateMachine);
        _characterController = GetComponent<CharacterController>();
    }

    public override void EnterState()
    {
        SetMovingFlag(false);
    }

    public override void ExitState()
    {
        SetMovingFlag(false);
    }

    public override void UpdateNetworkState(NetworkInputData input)
    {
        bool hasInput = input.JoystickInput.sqrMagnitude > 0.01f;
        SetMovingFlag(hasInput);

        if (!hasInput)
            return;

        Vector3 moveDirection = new Vector3(input.JoystickInput.x, 0f, input.JoystickInput.y).normalized;

        // PlayerMoveState ile ayni yaklasim: transform'u dogrudan itmek yerine
        // CharacterController kullan, yoksa oyuncu calidayken duvardan gecebiliyor.
        Vector3 movement = moveDirection * (moveSpeed * Runner.DeltaTime);

        if (_characterController != null && _characterController.enabled)
            _characterController.Move(movement);
        else
            transform.position += movement;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Runner.DeltaTime * rotationSpeed);
    }

    private void SetMovingFlag(bool moving)
    {
        if (stateMachine != null && stateMachine.Animator != null)
            stateMachine.Animator.SetBool(IsMovingHash, moving);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority || !other.CompareTag("Bush"))
            return;

        stateMachine.ChangeState(2);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!HasStateAuthority || !other.CompareTag("Bush"))
            return;

        // Calidan cikarken joystick durumuna gore dogru state'e don.
        if (GetInput<NetworkInputData>(out NetworkInputData input) &&
            input.JoystickInput.sqrMagnitude > 0.01f)
        {
            stateMachine.ChangeState(1); // Move
        }
        else
        {
            stateMachine.ChangeState(0); // Idle
        }
    }
}
