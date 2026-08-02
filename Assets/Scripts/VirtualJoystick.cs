using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    private RectTransform _bgRect;
    private RectTransform _handleRect;

    public Vector2 InputVector { get; private set; }

    private void Start()
    {
        _bgRect = GetComponent<RectTransform>();
        _handleRect = transform.GetChild(0).GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_bgRect, eventData.position, eventData.pressEventCamera, out Vector2 position))
        {
            position.x = (position.x / _bgRect.sizeDelta.x) * 2f;
            position.y = (position.y / _bgRect.sizeDelta.y) * 2f;

            InputVector = new Vector2(position.x, position.y);

            // Karekök maliyetinden kaçýnmak için sqrMagnitude kullanýldý
            InputVector = (InputVector.sqrMagnitude > 1.0f) ? InputVector.normalized : InputVector;

            _handleRect.anchoredPosition = new Vector2(InputVector.x * (_bgRect.sizeDelta.x / 3f), InputVector.y * (_bgRect.sizeDelta.y / 3f));
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        InputVector = Vector2.zero;
        _handleRect.anchoredPosition = Vector2.zero;
    }
}