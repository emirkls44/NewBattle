using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Yuzen joystick'in dokunma alani (gorunmez, ekranin bir bolgesi). Alana
/// dokunan parmak joystick'i oraya getirir ve onu surer; birakinca joystick
/// dinlenme yerine doner. Coklu dokunus: her alan kendi parmagini izler, ayni
/// anda hem hareket hem saldiri yapilabilir.
///
/// VirtualJoystick (touchZone dolu) Awake'te bu bileseni alana ekleyip baglar.
/// Alanin bir Image'i olmali (renk saydam, Raycast Target acik).
/// </summary>
public class FloatingJoystickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler,
    IInitializePotentialDragHandler
{
    [SerializeField] private VirtualJoystick joystick;

    private int _pointerId = int.MinValue;

    public void Bind(VirtualJoystick target) => joystick = target;

    private bool Usable => joystick != null && joystick.isActiveAndEnabled;

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Usable || _pointerId != int.MinValue)
            return;

        _pointerId = eventData.pointerId;
        joystick.BeginTouch(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId || !Usable)
            return;

        joystick.OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId)
            return;

        _pointerId = int.MinValue;
        if (joystick != null)
            joystick.EndTouch();
    }

    private void OnDisable()
    {
        if (_pointerId == int.MinValue)
            return;

        _pointerId = int.MinValue;
        if (joystick != null)
            joystick.EndTouch();
    }
}
