using UnityEngine;
using UnityEngine.EventSystems;

public class SimpleJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform background;
    public RectTransform handle;

    [HideInInspector] public Vector2 inputVector;

    // Önbellek deðiþkenleri
    private Vector2 _backgroundSize;
    private Vector2 _handleRadius;

    private void Start()
    {
        // DEÐÝÞÝKLÝK: Her drag olayýnda bölme yapmamak için deðerleri oyun baþýnda hesaplayýp saklýyoruz.
        _backgroundSize = background.sizeDelta;
        _handleRadius = _backgroundSize * 0.5f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // DEÐÝÞÝKLÝK: Dýþarýda Vector2 tanýmlamak yerine doðrudan metodun out parametresi içinde oluþturuldu.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out Vector2 position))
        {
            position.x = (position.x / _backgroundSize.x) * 2f - 1f;
            position.y = (position.y / _backgroundSize.y) * 2f - 1f;

            inputVector = position;

            // DEÐÝÞÝKLÝK: magnitude yerine karekök iþlemi gerektirmeyen sqrMagnitude kullanýldý (Mobil CPU için daha hafiftir).
            if (inputVector.sqrMagnitude > 1f)
            {
                inputVector.Normalize();
            }

            handle.anchoredPosition = new Vector2(inputVector.x * _handleRadius.x, inputVector.y * _handleRadius.y);
        }
    }

    public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }
}