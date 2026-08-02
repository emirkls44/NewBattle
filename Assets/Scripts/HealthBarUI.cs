using Fusion;
using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [Header("UI Elemanlarý")]
    public Transform targetTransform;
    public UnityEngine.UI.Slider healthSlider;
    public UnityEngine.UI.Slider shieldSlider;
    public Vector3 worldOffset = new Vector3(0, 2f, 0);

    private HealthController _healthController;
    private Camera _mainCamera;

    void Start()
    {
        _mainCamera = Camera.main;

        if (targetTransform != null)
        {
            _healthController = targetTransform.GetComponent<HealthController>();
            if (_healthController != null)
            {
                _healthController.OnHealthChanged += UpdateUIValues;
                UpdateUIValues();
            }
        }
    }

    void OnDestroy()
    {
        if (_healthController != null)
        {
            _healthController.OnHealthChanged -= UpdateUIValues;
        }
    }

    void LateUpdate()
    {
        if (targetTransform == null) return;

        transform.position = _mainCamera.WorldToScreenPoint(targetTransform.position + worldOffset);

        if (healthSlider != null && _healthController != null)
        {
            healthSlider.value = _healthController.currentHealth / _healthController.maxHealth;
        }

        if (shieldSlider != null && _healthController != null)
        {
            shieldSlider.value = _healthController.currentShield / _healthController.maxShield;
            shieldSlider.gameObject.SetActive(_healthController.currentShield > 0);
        }
    }

    private void UpdateUIValues()
    {
        if (_healthController == null) return;

        if (healthSlider != null)
        {
            healthSlider.value = _healthController.currentHealth / _healthController.maxHealth;
        }

        if (shieldSlider != null)
        {
            shieldSlider.value = _healthController.currentShield / _healthController.maxShield;
            shieldSlider.gameObject.SetActive(_healthController.currentShield > 0);
        }
    }

    public void Setup(Transform target)
    {
        targetTransform = target;
        _healthController = target.GetComponent<HealthController>();
        if (_healthController != null)
        {
            _healthController.OnHealthChanged += UpdateUIValues;
            UpdateUIValues();
        }
    }
}