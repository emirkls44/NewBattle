using TMPro;
using UnityEngine;

public class DamageNumberPopup : MonoBehaviour
{
    private const float Lifetime = 0.85f;
    private const float RiseSpeed = 1.4f;

    private TextMeshPro _text;
    private Camera _camera;
    private float _remainingTime;
    private Color _startColor;

    public static void Show(Vector3 worldPosition, float damage, bool shieldHit)
    {
        GameObject popupObject = new("DamageNumber");
        popupObject.transform.position = worldPosition
            + Vector3.up * 2.35f
            + new Vector3(Random.Range(-0.18f, 0.18f), 0f, Random.Range(-0.08f, 0.08f));

        TextMeshPro text = popupObject.AddComponent<TextMeshPro>();
        text.text = Mathf.CeilToInt(damage).ToString();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 4.5f;
        text.fontStyle = FontStyles.Bold;
        text.color = shieldHit
            ? new Color(0.2f, 0.72f, 1f, 1f)
            : new Color(1f, 0.9f, 0.15f, 1f);
        text.sortingOrder = 100;

        DamageNumberPopup popup = popupObject.AddComponent<DamageNumberPopup>();
        popup.Initialize(text);
    }

    private void Initialize(TextMeshPro text)
    {
        _text = text;
        _camera = Camera.main;
        _remainingTime = Lifetime;
        _startColor = text.color;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        _remainingTime -= deltaTime;
        transform.position += Vector3.up * RiseSpeed * deltaTime;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera != null)
            transform.rotation = _camera.transform.rotation;

        float alpha = Mathf.Clamp01(_remainingTime / Lifetime);
        _text.color = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);

        if (_remainingTime <= 0f)
            Destroy(gameObject);
    }
}
