using UnityEngine;
using UnityEngine.EventSystems;

namespace NewBattle.UI
{
    /// <summary>
    /// Menudeki karakter goruntusunu baska bir ekranda (Karakter ekrani) gosteren resme
    /// eklenir: oradaki surukleme de menudeki karakteri dondurur (ayni kaygan donus).
    /// </summary>
    public class CharacterDragForwarder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private MenuCharacterStage stage;

        public MenuCharacterStage Stage
        {
            get => stage;
            set => stage = value;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (stage != null)
                stage.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (stage != null)
                stage.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (stage != null)
                stage.OnEndDrag(eventData);
        }
    }
}
