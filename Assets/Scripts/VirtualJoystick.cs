using UnityEngine;
using UnityEngine.EventSystems;

// IPointer arayüzleri, Unity'nin standart Event System'i üzerinden Update() kullanmadan çalýþýr.
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    private RectTransform _bgRect;
    private RectTransform _handleRect;

    // Fusion'ýn okuyacaðý sýfýr tahsisli (zero-allocation) yön verisi
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
        Vector2 position;
        // Dokunulan pikseli, UI objesinin yerel koordinatlarýna çeviriyoruz
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_bgRect, eventData.position, eventData.pressEventCamera, out position))
        {
            // Pozisyonu -1 ile 1 arasýna normalize ediyoruz
            position.x = (position.x / _bgRect.sizeDelta.x) * 2f;
            position.y = (position.y / _bgRect.sizeDelta.y) * 2f;

            InputVector = new Vector2(position.x, position.y);
            InputVector = (InputVector.magnitude > 1.0f) ? InputVector.normalized : InputVector;

            // Handle (Tutamaç) görselini parmaðýn olduðu konuma sýnýrlandýrarak taþýyoruz
            _handleRect.anchoredPosition = new Vector2(InputVector.x * (_bgRect.sizeDelta.x / 3f), InputVector.y * (_bgRect.sizeDelta.y / 3f));
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Parmak çekildiðinde vektörü ve görseli merkeze (sýfýra) sýfýrla
        InputVector = Vector2.zero;
        _handleRect.anchoredPosition = Vector2.zero;
    }
}