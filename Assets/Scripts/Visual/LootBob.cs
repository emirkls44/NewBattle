using UnityEngine;

/// <summary>
/// Yerdeki loot'un yavasca donup asagi yukari suzulmesini saglar.
/// Referans gorsellerde esyalar canli durur; sabit duran bir model olu gorunur.
///
/// Update yerine sadece gorsel transform'u oynatir: agda senkronize edilen bir sey yok,
/// bu yuzden her istemcide bagimsiz calismasi sorun degil.
/// </summary>
[DisallowMultipleComponent]
public class LootBob : MonoBehaviour
{
    [SerializeField, Min(0f)] private float rotationSpeed = 45f;
    [SerializeField, Min(0f)] private float bobHeight = 0.09f;
    [SerializeField, Min(0.1f)] private float bobSpeed = 1.8f;

    private Vector3 _basePosition;
    private float _phaseOffset;

    private void Start()
    {
        _basePosition = transform.localPosition;

        // Ayni anda spawn olan loot'lar senkron ziplamasin diye faz kaydirmasi.
        _phaseOffset = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        float time = Time.time;

        transform.localPosition = _basePosition +
            Vector3.up * (Mathf.Sin(time * bobSpeed + _phaseOffset) * bobHeight);

        transform.localRotation = Quaternion.Euler(0f, time * rotationSpeed, 0f);
    }
}
