using System.Collections;
using TMPro;
using UnityEngine;

namespace NewBattle.UI
{
    /// <summary>
    /// Ekranin ortasinda kisa sure gorunen bildirim ("YAKINDA!").
    /// Harita secimi ve mac sonu ekranlari kendi bildirimini bununla gosterir.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.15f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 1.2f;

        private CanvasGroup _group;
        private Coroutine _routine;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        public void Show(string message)
        {
            if (text != null)
                text.text = message;

            transform.SetAsLastSibling();

            if (_routine != null)
                StopCoroutine(_routine);

            if (isActiveAndEnabled)
                _routine = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = t / fadeSeconds;
                yield return null;
            }

            _group.alpha = 1f;
            yield return new WaitForSecondsRealtime(holdSeconds);

            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - t / fadeSeconds;
                yield return null;
            }

            _group.alpha = 0f;
            _routine = null;
        }
    }
}
