using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("UI Kontrolleri")]
    public VirtualJoystick movementJoystick;
    public VirtualJoystick attackJoystick;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Fusion'ýn OnInput callback'inde doðrudan bu metot çaðrýlacak
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData data = new NetworkInputData();

        if (movementJoystick != null)
        {
            data.JoystickInput = movementJoystick.InputVector;
        }

        if (attackJoystick != null)
        {
            data.RightJoystickVector = attackJoystick.InputVector;
        }

        return data;
    }
}