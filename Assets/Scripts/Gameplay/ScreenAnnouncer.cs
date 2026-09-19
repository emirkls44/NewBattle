using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Ekranin ortasinda belirip solan duyuru yazisi ("Yeni Tedarik Paketi").
    ///
    /// Kendi Canvas'ini kendisi kuruyor. Sebebi: bu yazi mac sirasinda
    /// herhangi bir andan tetiklenebiliyor (tedarik paketi, alan daralmasi,
    /// mac sonu) ve her tetikleyicinin sahnede bir referans tasimasi
    /// gerekseydi, sahnedeki tek bir kopuk baglanti yaziyi sessizce yok
    /// ederdi. Boylece hicbir Inspector ayari olmadan da calisiyor.
    /// </summary>
    public class ScreenAnnouncer : MonoBehaviour
    {
        [Header("Zamanlama")]
        [Tooltip("Yazinin gorunur hale gelme suresi.")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.25f;

        [Tooltip("Tam gorunur kaldigi sure.")]
        [SerializeField, Min(0f)] private float holdSeconds = 1.1f;

        [Tooltip("Solup kaybolma suresi.")]
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.9f;

        [Header("Gorunum")]
        [Tooltip("En yuksek saydamsizlik. Videoda yazi tam beyaz degil, yari saydam.")]
        [SerializeField, Range(0.1f, 1f)] private float peakAlpha = 0.72f;

        [Tooltip("Ekran yuksekliginin yuzdesi olarak yazi boyutu.")]
        [SerializeField, Range(0.02f, 0.15f)] private float heightFraction = 0.062f;

        [Tooltip("Ekranin dikey ortasindan ne kadar yukarida dursun (yukseklik yuzdesi).")]
        [SerializeField, Range(-0.4f, 0.4f)] private float verticalOffset = 0.14f;

        [Tooltip("Bos birakilirsa TextMeshPro varsayilan fontu kullanilir.")]
        [SerializeField] private TMP_FontAsset font;

        private static ScreenAnnouncer _instance;

        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private RectTransform _labelRect;
        private float _elapsed;
        private bool _playing;

        /// <summary>
        /// Duyuruyu gosterir. Sahnede bilesen yoksa kendisi olusturur, bu
        /// yuzden cagiran tarafin hicbir sey hazirlamasi gerekmiyor.
        /// </summary>
        public static void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (_instance == null)
            {
                GameObject host = new("ScreenAnnouncer");
                _instance = host.AddComponent<ScreenAnnouncer>();
            }

            _instance.Play(message);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            BuildIfNeeded();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Canvas ve yazi nesnesini kurar.
        ///
        /// Sorting order yuksek: duyuru, can barlari ve joystick dahil her
        /// seyin ustunde kalmali. raycastTarget kapali, yoksa yazi ekranin
        /// ortasinda gorunmez bir dokunma engeli olusturup nisan almayi
        /// bozardi.
        /// </summary>
        private void BuildIfNeeded()
        {
            if (_group != null)
                return;

            Canvas canvas = gameObject.GetComponent<Canvas>();

            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;

                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();

            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            GameObject textObject = new("Mesaj");
            textObject.transform.SetParent(transform, false);

            _label = textObject.AddComponent<TextMeshProUGUI>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;
            _label.raycastTarget = false;
            _label.color = Color.white;
            _label.fontStyle = FontStyles.Bold;

            if (font != null)
                _label.font = font;

            _labelRect = _label.rectTransform;
            _labelRect.anchorMin = new Vector2(0f, 0.5f);
            _labelRect.anchorMax = new Vector2(1f, 0.5f);
            _labelRect.pivot = new Vector2(0.5f, 0.5f);
            _labelRect.sizeDelta = new Vector2(0f, 200f);
        }

        private void Play(string message)
        {
            BuildIfNeeded();

            _label.text = message;
            _elapsed = 0f;
            _playing = true;

            // Boyut ve konum her gosterimde yeniden hesaplaniyor: telefon
            // yatay/dikey cevrildiginde ya da pencere olcusu degistiginde
            // yazi orantisini korusun.
            _label.fontSize = Mathf.Max(18f, Screen.height * heightFraction);
            _labelRect.anchoredPosition = new Vector2(0f, Screen.height * verticalOffset * 0.5f);
        }

        private void Update()
        {
            if (!_playing)
                return;

            _elapsed += Time.deltaTime;

            float total = fadeInSeconds + holdSeconds + fadeOutSeconds;
            float alpha;

            if (_elapsed < fadeInSeconds)
            {
                alpha = fadeInSeconds <= 0f ? 1f : _elapsed / fadeInSeconds;
            }
            else if (_elapsed < fadeInSeconds + holdSeconds)
            {
                alpha = 1f;
            }
            else if (_elapsed < total)
            {
                float t = (_elapsed - fadeInSeconds - holdSeconds) / Mathf.Max(0.0001f, fadeOutSeconds);
                alpha = 1f - t;
            }
            else
            {
                alpha = 0f;
                _playing = false;
            }

            _group.alpha = alpha * peakAlpha;
        }
    }
}
