using UnityEngine;

namespace NewBattle.UI
{
    /// <summary>
    /// UI nesnesini yatayda yavasca kaydirir; ebeveynin disina cikinca diger
    /// kenardan geri girer (harita ustundeki bulutlar).
    /// </summary>
    public class UIDrift : MonoBehaviour
    {
        [Tooltip("Saniyede kac birim kaysin. Eksi = sola.")]
        [SerializeField] private float speed = 18f;
        [Tooltip("Hafif yukari asagi salinim (birim).")]
        [SerializeField] private float bob = 6f;

        private RectTransform _rect;
        private float _baseY;
        private float _phase;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _baseY = _rect.anchoredPosition.y;
            _phase = Random.value * 10f;
        }

        private void Update()
        {
            var parent = _rect.parent as RectTransform;
            if (parent == null)
                return;

            float half = parent.rect.width * 0.5f + _rect.rect.width * 0.5f;
            Vector2 position = _rect.anchoredPosition;
            position.x += speed * Time.unscaledDeltaTime;

            if (position.x > half)
                position.x = -half;
            else if (position.x < -half)
                position.x = half;

            position.y = _baseY + Mathf.Sin((Time.unscaledTime + _phase) * 0.6f) * bob;
            _rect.anchoredPosition = position;
        }
    }
}
