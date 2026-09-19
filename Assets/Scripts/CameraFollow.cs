using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Takip Ayarlari")]
    public Transform target;
    public Vector3 offset = new(0f, 15f, -10f);
    public float followSpeed = 25f;

    [Header("Harita Kamera Siniri")]
    public Vector2 mapCenter = Vector2.zero;
    public float cameraFocusRadius = 25f;
    public float cameraAngle = 55f;

    [Header("Atis Sarsintisi")]
    [Tooltip("Sarsintinin sonumlenme hizi. Buyudukce daha cabuk durulur.")]
    [SerializeField, Min(0.1f)] private float traumaDecay = 1.8f;

    [Tooltip("Titremenin saniyedeki salinim sayisi.")]
    [SerializeField, Min(1f)] private float shakeFrequency = 24f;

    [Tooltip("En siddetli sarsintida kameranin kayacagi mesafe (dunya birimi).")]
    [SerializeField, Min(0f)] private float maxShakeOffset = 0.5f;

    [Tooltip("En siddetli sarsintida kameranin yatacagi aci (derece).")]
    [SerializeField, Min(0f)] private float maxShakeRoll = 1.4f;

    /// <summary>
    /// Sahnedeki etkin oyun kamerasi.
    ///
    /// Atis kodunun kameraya referans tasimasi gerekmesin diye burada
    /// duruyor: silah ates ettiginde <see cref="Shake"/> cagirmasi yeterli.
    /// </summary>
    public static CameraFollow Active { get; private set; }

    /// <summary>
    /// Birikmis sarsinti miktari (0..1).
    ///
    /// Her atis bunu ARTIRIYOR, sifirlamiyor. Sifirlasaydi seri ateste her
    /// mermi sarsintiyi bastan baslatir ve titreme duzenli bir tik tik
    /// hissi verirdi; birikince seri ates suren bir gerginlige donusuyor.
    /// </summary>
    private float _trauma;

    private Quaternion _baseRotation;
    private float _noiseSeed;

    private void Awake()
    {
        // Her kameranin kendi gurultu tohumu olsun ki iki kamera ayni anda
        // birebir ayni titremeyi uretmesin.
        _noiseSeed = Random.value * 1000f;
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    private void Start()
    {
        _baseRotation = Quaternion.Euler(cameraAngle, 0f, 0f);
        transform.rotation = _baseRotation;
    }

    /// <summary>Etkin kamerayi sarsar. Kamera yoksa sessizce gecer.</summary>
    public static void Shake(float amount)
    {
        if (Active != null)
            Active.AddTrauma(amount);
    }

    public void AddTrauma(float amount)
    {
        _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = GetDesiredPosition();
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            Time.deltaTime * followSpeed
        );

        // Sarsinti takipten SONRA uygulaniyor. Once uygulansaydi Lerp bir
        // sonraki karede sarsintiyi hedef konum sanip ona dogru yumusatir,
        // titreme kaybolurdu.
        ApplyShake();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
            transform.position = GetDesiredPosition();
    }

    /// <summary>
    /// Birikmis sarsintiyi kameranin konumuna ve yatisina yansitir.
    ///
    /// Genlik trauma'nin KARESI ile oluyor: hafif sarsintilar neredeyse
    /// gorunmez kalirken siddetli olanlar belirginlesiyor. Dogrusal olsaydi
    /// tek bir mermi bile kamerayi gozle secilir sekilde oynatirdi.
    ///
    /// Rastgele deger yerine Perlin gurultusu kullaniliyor: rastgele deger
    /// kare kare zipladigi icin ucuz bir parazit gibi durur, Perlin ise
    /// surekli oldugu icin gercek bir geri tepme gibi okunuyor.
    /// </summary>
    private void ApplyShake()
    {
        if (_trauma <= 0f)
        {
            transform.rotation = _baseRotation;
            return;
        }

        _trauma = Mathf.Max(0f, _trauma - traumaDecay * Time.deltaTime);

        float shake = _trauma * _trauma;
        float time = Time.time * shakeFrequency;

        float sideways = (Mathf.PerlinNoise(_noiseSeed, time) * 2f - 1f) * maxShakeOffset * shake;
        float vertical = (Mathf.PerlinNoise(_noiseSeed + 17f, time) * 2f - 1f) * maxShakeOffset * shake;
        float roll = (Mathf.PerlinNoise(_noiseSeed + 41f, time) * 2f - 1f) * maxShakeRoll * shake;

        // Kameranin KENDI eksenlerinde kaydiriyoruz; dunya eksenlerinde
        // kaydirsaydik egik kamerada titreme zemine dogru kayarak garip
        // gorunurdu.
        transform.position += transform.right * sideways + transform.up * vertical;
        transform.rotation = _baseRotation * Quaternion.Euler(0f, 0f, roll);
    }

    private Vector3 GetDesiredPosition()
    {
        Vector2 targetFromCenter = new(
            target.position.x - mapCenter.x,
            target.position.z - mapCenter.y
        );

        // Harita kare: kamerayi daire icinde tutmak, kosedeki oyuncunun
        // ekranin kenarina kaymasina yol acardi.
        targetFromCenter = new Vector2(
            Mathf.Clamp(targetFromCenter.x, -cameraFocusRadius, cameraFocusRadius),
            Mathf.Clamp(targetFromCenter.y, -cameraFocusRadius, cameraFocusRadius));

        Vector3 focusPosition = new(
            mapCenter.x + targetFromCenter.x,
            target.position.y,
            mapCenter.y + targetFromCenter.y
        );

        return focusPosition + offset;
    }
}
