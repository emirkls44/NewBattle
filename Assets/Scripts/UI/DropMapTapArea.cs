using UnityEngine;
using UnityEngine.EventSystems;

namespace NewBattle.UI
{
    /// <summary>Harita resmine dokunmayi harita secim ekranina iletir.</summary>
    public class DropMapTapArea : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private DropSelectionScreen screen;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (screen != null)
                screen.SelectAt(eventData.position, eventData.pressEventCamera);
        }
    }
}
