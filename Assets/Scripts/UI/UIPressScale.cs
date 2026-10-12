using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Basili tutulan butonu hafifce kucultur (mobil oyun hissi).
    ///
    /// Kucultme, nesnenin Hierarchy'de AYARLANMIS olcegine gore yapilir:
    /// karti 0.7'ye kucultmussen basinca 0.7 x 0.93 olur, birakinca 0.7'ye
    /// doner. (Eskiden olcegi her zaman 1'e cekiyordu ve senin ayarini
    /// bozuyordu.)
    /// </summary>
    public class UIPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.93f;
        [SerializeField, Min(1f)] private float speed = 22f;

        private Selectable _selectable;
        private Vector3 _baseScale = Vector3.one;
        private float _current = 1f;
        private float _target = 1f;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            _target = 1f;
            _current = 1f;
            transform.localScale = _baseScale;
        }

        private void Update()
        {
            if (Mathf.Approximately(_current, _target))
                return;

            _current = Mathf.MoveTowards(_current, _target, speed * Time.unscaledDeltaTime * 0.1f);
            transform.localScale = _baseScale * _current;
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
