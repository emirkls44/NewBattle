using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>Basili tutulan butonu hafifce kucultur (mobil oyun hissi).</summary>
    public class UIPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.93f;
        [SerializeField, Min(1f)] private float speed = 22f;

        private Selectable _selectable;
        private float _target = 1f;

        private void Awake() => _selectable = GetComponent<Selectable>();

        private void OnDisable()
        {
            _target = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float current = transform.localScale.x;
            if (Mathf.Approximately(current, _target))
                return;

            float next = Mathf.MoveTowards(current, _target, speed * Time.unscaledDeltaTime * 0.1f);
            transform.localScale = new Vector3(next, next, 1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable == null || _selectable.IsInteractable())
                _target = pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData) => _target = 1f;

        public void OnPointerExit(PointerEventData eventData) => _target = 1f;
    }
}
