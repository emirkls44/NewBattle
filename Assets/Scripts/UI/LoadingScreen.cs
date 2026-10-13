using System.Collections;
using TMPro;
using UnityEngine;

namespace NewBattle.UI
{
    /// <summary>
    /// ASAMA 3: Mac aranirken gorunen yukleme ekrani (koyu mor, kosan karakter).
    ///
    /// Fusion maci baslatirken sahneyi bastan yukler; bu ekran sahne
    /// degisiminde yok olmasin diye DontDestroyOnLoad ile yasar. Harita secim
    /// ekrani hazir olunca (DropPhaseController agda dogunca) kendiliginden
    /// kaybolur. Baglanti hatasinda hatayi kisa sure gosterip kapanir.
    ///
    /// Gorunum bir prefab: Assets/Prefabs/UI/YuklemeEkrani.prefab
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Zamanlama")]
        [Tooltip("Ekran en az bu kadar acik kalir (bir an yanip sonmesin).")]
        [SerializeField, Min(0f)] private float minimumSeconds = 1.5f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.3f;
        [Tooltip("Hazir sinyali hic gelmezse oyuncu burada kilitli kalmasin.")]
        [SerializeField, Min(5f)] private float timeoutSeconds = 30f;
        [Tooltip("Baglanti hatasi yazisi ne kadar gorunsun.")]
        [SerializeField, Min(0f)] private float errorSeconds = 2f;

        private static LoadingScreen _current;

        private NetworkBootstrap _bootstrap;
        private float _shownAt;
        private bool _closing;
        private bool _failed;

        // Maci/odayi terk ederken: ana menu gelene kadar acik kalir.
        private bool _leaving;
        private bool _holdForRejoin;

        public static bool IsShowing => _current != null;

        /// <summary>Yukleme ekranini acar. Zaten aciksa onu dondurur.</summary>
        public static LoadingScreen Show(LoadingScreen prefab, NetworkBootstrap bootstrap)
        {
            if (_current != null)
            {
                // Ayrilma ekrani hala aciksa (kurucu ayrildi, yeni odaya baglaniliyor)
                // onu yeni baglanti icin devral; arada menu gorunmesin.
                if (_current._leaving && !_current._closing)
                    _current.Adopt(bootstrap);
                return _current;
            }

            if (prefab == null)
                return null;

            _current = Instantiate(prefab);
            _current.name = prefab.name;
            DontDestroyOnLoad(_current.gameObject);
            _current.Begin(bootstrap);
            return _current;
        }

        /// <summary>
        /// Odadan/mactan ayrilirken: mor yukleme ekrani acilir, sahne yeniden yuklenip
        /// ana menu gelince kapanir. holdForRejoin: menu hemen yeni odaya baglanacak,
        /// ekran onu bekler.
        /// </summary>
        public static void ShowLeaving(LoadingScreen prefab, string message, bool holdForRejoin = false)
        {
            if (_current == null)
            {
                if (prefab == null)
                    return;

                _current = Instantiate(prefab);
                _current.name = prefab.name;
                DontDestroyOnLoad(_current.gameObject);
                _current.Begin(null);
            }

            _current._leaving = true;
            _current._holdForRejoin = holdForRejoin;
            _current._shownAt = Time.unscaledTime;
            if (_current.statusText != null)
                _current.statusText.text = message;
        }

        private void Adopt(NetworkBootstrap bootstrap)
        {
            _leaving = false;
            _holdForRejoin = false;
            _shownAt = Time.unscaledTime;
            if (_bootstrap == null && bootstrap != null)
            {
                _bootstrap = bootstrap;
                _bootstrap.StatusChanged += OnStatusChanged;
                _bootstrap.ConnectionCompleted += OnConnectionCompleted;
            }
        }

        /// <summary>Acik yukleme ekranini kapatir.</summary>
        public static void HideCurrent()
        {
            if (_current != null)
                _current.Close();
        }

        private void Begin(NetworkBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            _shownAt = Time.unscaledTime;

            if (statusText != null)
                statusText.text = string.Empty;

            if (_bootstrap != null)
            {
                _bootstrap.StatusChanged += OnStatusChanged;
                _bootstrap.ConnectionCompleted += OnConnectionCompleted;
            }

            StartCoroutine(Fade(0f, 1f, null));
        }

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;

            if (_bootstrap != null)
            {
                _bootstrap.StatusChanged -= OnStatusChanged;
                _bootstrap.ConnectionCompleted -= OnConnectionCompleted;
            }
        }

        private void Update()
        {
            if (_closing || _failed)
                return;

            float elapsed = Time.unscaledTime - _shownAt;
            if (elapsed < minimumSeconds)
                return;

            if (_leaving)
            {
                if ((!_holdForRejoin && IsMenuReady()) || elapsed >= timeoutSeconds)
                    Close();
                return;
            }

            if (IsGameReady() || elapsed >= timeoutSeconds)
                Close();
        }

        private void Close()
        {
            if (_closing)
                return;

            _closing = true;
            StopAllCoroutines();
            StartCoroutine(Fade(group != null ? group.alpha : 1f, 0f, () => Destroy(gameObject)));
        }

        private void OnStatusChanged(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private void OnConnectionCompleted(bool success)
        {
            if (success)
                return;

            _failed = true;
            StartCoroutine(CloseAfter(errorSeconds));
        }

        private IEnumerator CloseAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _failed = false;
            Close();
        }

        /// <summary>Sahne yeniden yuklendi ve ana menu ekranda.</summary>
        private static bool IsMenuReady()
        {
            MainMenuScreen menu = FindFirstObjectByType<MainMenuScreen>();
            return menu != null && menu.isActiveAndEnabled;
        }

        /// <summary>
        /// Harita secim asamasinin ag nesnesi dogduysa oyun hazirdir. O andan
        /// sonra ekrani harita secim arayuzu devralir.
        /// </summary>
        private static bool IsGameReady()
        {
            DropPhaseController drop = FindFirstObjectByType<DropPhaseController>();
            return drop != null && drop.Object != null && drop.Object.IsValid && drop.Runner != null && drop.Runner.IsRunning;
        }

        private IEnumerator Fade(float from, float to, System.Action done)
        {
            if (group == null)
            {
                done?.Invoke();
                yield break;
            }

            group.blocksRaycasts = to > 0f;

            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }

            group.alpha = to;
            done?.Invoke();
        }
    }
}
