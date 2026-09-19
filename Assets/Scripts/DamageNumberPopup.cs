using TMPro;
using UnityEngine;

/// <summary>
/// Isabet aninda dusmanin uzerinde beliren hasar sayisi.
///
/// Havuzlu: onceki surum her isabette GameObject yaratip yok ediyordu.
/// Seri ateste bu saniyede onlarca tahsis demek; cop toplama devreye
/// girince oyun tam catisma aninda takiliyordu. Havuz sabit kaliyor.
///
/// Gorunum ayarlari (font, boyut, renk) Inspector'da. Sahneye eklemek
/// icin: Tools > NewBattle > HUD Kurulumu > "Hasar Sayisini Sahneye Ekle".
/// Sahnede yoksa kendini varsayilan ayarlarla olusturur.
/// </summary>
public class DamageNumberPopup : MonoBehaviour
{
    private const int PoolSize = 24;

    [Header("Yazi")]
    [Tooltip("Bos birakilirsa TextMeshPro varsayilan fontu kullanilir.")]
    [SerializeField] private TMP_FontAsset font;

    [Tooltip("Dunya birimi cinsinden yazi boyutu. Kamera uzaklastikca buyutmek gerekir.")]
    [SerializeField, Min(0.5f)] private float fontSize = 4.5f;

    [SerializeField] private FontStyles fontStyle = FontStyles.Bold;

    [Header("Renkler")]
    [Tooltip("Cana isabet edince.")]
    [SerializeField] private Color healthColor = new(1f, 0.9f, 0.15f, 1f);

    [Tooltip("Kalkana isabet edince.")]
    [SerializeField] private Color shieldColor = new(0.2f, 0.72f, 1f, 1f);

    [Header("Okunabilirlik")]
    [Tooltip("Yazinin cevresindeki kontur. Parlak zeminde sayiyi ayirir.")]
    [SerializeField, Range(0f, 0.5f)] private float outlineWidth = 0.2f;

    [SerializeField] private Color outlineColor = new(0f, 0f, 0f, 0.85f);

    [Header("Hareket")]
    [Tooltip("Sayinin ekranda kalma suresi.")]
    [SerializeField, Min(0.1f)] private float lifetime = 0.85f;

    [Tooltip("Yukari suzulme hizi.")]
    [SerializeField] private float riseSpeed = 1.4f;

    [Tooltip("Karakterin ne kadar ustunde belirsin.")]
    [SerializeField] private float heightOffset = 2.35f;

    private struct Entry
    {
        public TextMeshPro Text;
        public Transform Body;
        public float Remaining;
        public Color BaseColor;
        public bool Active;
    }

    private static DamageNumberPopup _instance;

    private Entry[] _pool;
    private int _nextIndex;
    private Camera _camera;

    public static void Show(Vector3 worldPosition, float damage, bool shieldHit)
    {
        Ensure();
        _instance.Emit(worldPosition, damage, shieldHit);
    }

    private static void Ensure()
    {
        if (_instance != null)
            return;

        GameObject host = new("DamageNumberPool");
        _instance = host.AddComponent<DamageNumberPopup>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        BuildPool();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void BuildPool()
    {
        if (_pool != null)
            return;

        _pool = new Entry[PoolSize];

        for (int i = 0; i < PoolSize; i++)
        {
            GameObject popup = new($"DamageNumber_{i}");
            popup.transform.SetParent(transform, false);

            TextMeshPro text = popup.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.sortingOrder = 100;
            text.enableWordWrapping = false;

            // Yazinin kendi RectTransform'u genis olmali, yoksa uc haneli
            // hasar ("100") satir sonuna sikisip kayboluyor.
            text.rectTransform.sizeDelta = new Vector2(4f, 1.5f);

            popup.SetActive(false);

            _pool[i] = new Entry { Text = text, Body = popup.transform };
        }

        ApplyStyle();
    }

    /// <summary>
    /// Inspector ayarlarini havuzdaki tum yazilara yazar.
    ///
    /// OnValidate'ten de cagriliyor: Play modunda font boyutunu veya rengi
    /// degistirince sonucu hemen goruyorsun, tekrar baslatmaya gerek yok.
    /// </summary>
    private void ApplyStyle()
    {
        if (_pool == null)
            return;

        for (int i = 0; i < _pool.Length; i++)
        {
            TextMeshPro text = _pool[i].Text;

            if (text == null)
                continue;

            if (font != null)
                text.font = font;

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.outlineWidth = outlineWidth;
            text.outlineColor = outlineColor;
        }
    }

    private void OnValidate()
    {
        ApplyStyle();
    }

    private void Emit(Vector3 worldPosition, float damage, bool shieldHit)
    {
        BuildPool();

        int index = _nextIndex;
        _nextIndex = (_nextIndex + 1) % _pool.Length;

        // Ayni noktaya ust uste gelen sayilar tek bir bulanik yigin olur;
        // kucuk bir sapma her vurusu ayri ayri okunur yapiyor.
        Vector3 jitter = new(
            Random.Range(-0.18f, 0.18f),
            0f,
            Random.Range(-0.08f, 0.08f));

        _pool[index].Body.position = worldPosition + Vector3.up * heightOffset + jitter;
        _pool[index].Text.text = Mathf.CeilToInt(damage).ToString();
        _pool[index].BaseColor = shieldHit ? shieldColor : healthColor;
        _pool[index].Text.color = _pool[index].BaseColor;
        _pool[index].Remaining = lifetime;
        _pool[index].Active = true;
        _pool[index].Body.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (_pool == null)
            return;

        if (_camera == null)
            _camera = Camera.main;

        float delta = Time.deltaTime;
        Quaternion facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;

        for (int i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].Active)
                continue;

            _pool[i].Remaining -= delta;

            if (_pool[i].Remaining <= 0f)
            {
                _pool[i].Active = false;
                _pool[i].Body.gameObject.SetActive(false);
                continue;
            }

            _pool[i].Body.position += Vector3.up * riseSpeed * delta;
            _pool[i].Body.rotation = facing;

            float alpha = Mathf.Clamp01(_pool[i].Remaining / lifetime);
            Color color = _pool[i].BaseColor;
            color.a = alpha;
            _pool[i].Text.color = color;
        }
    }
}
