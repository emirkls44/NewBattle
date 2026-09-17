using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Nisan alirken oyuncunun onunde beliren nokta nokta hiza cizgisi.
    ///
    /// Sadece YEREL oyuncuda calisir ve tamamen yereldir: aga hicbir sey gonderilmez.
    /// Girdiyi dogrudan joystick'ten okur, agdan gelen girdiyi beklemez - aradaki
    /// gecikme nisan alirken dogrudan "sunucu benden geri kaliyor" hissi yaratirdi.
    ///
    /// Noktalar havuzludur; nisan alirken her karede nokta yaratilmaz.
    /// </summary>
    public class AimLineIndicator : MonoBehaviour
    {
        [Header("Cizgi")]
        [SerializeField, Min(2)] private int maxDots = 16;
        [SerializeField, Min(0.2f)] private float dotSpacing = 0.85f;
        [SerializeField, Min(0.05f)] private float startOffset = 1.1f;
        [SerializeField, Min(0.02f)] private float dotSize = 0.13f;
        [SerializeField] private float groundHeight = 0.12f;

        [Tooltip("Acikken noktalar merminin gercek cikis noktasindan, mermiyle " +
                 "ayni yukseklikte cizilir. Kapaliysa karakterin merkezinden " +
                 "zemin hizasinda cizilir (eski davranis).")]
        [SerializeField] private bool alignWithBullets = true;

        [Header("Renk")]
        [SerializeField] private Color nearColor = new(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color farColor = new(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color blockedColor = new(1f, 0.42f, 0.36f, 0.75f);

        [Header("Davranis")]
        [Tooltip("Cizgi engele carpinca kisalsin mi.")]
        [SerializeField] private bool stopAtObstacle = true;
        [SerializeField] private LayerMask obstacleMask = ~0;

        private HealthController _health;
        private NewBattle.Gameplay.ParachuteDescent _descent;
        private PlayerShooting _shooting;

        /// <summary>
        /// firePoint'in karakter kokune gore konumu. Her karede firePoint.position
        /// okumak YANLIS olurdu: govde donusu sunucu tarafinda, bir tick gecikmeyle
        /// uygulaniyor; joystick'i hizli cevirince cizgi govdenin arkasinda kalirdi.
        /// Sabit yerel ofseti nisan yonuyle dondurerek cizgi aninda tepki verir.
        /// </summary>
        private Vector3 _fireLocalOffset;
        private bool _hasFireOffset;

        private Transform _container;
        private MeshRenderer[] _dots;
        private MaterialPropertyBlock _propertyBlock;
        private bool _visible;

        private void Start()
        {
            _health = GetComponent<HealthController>();
            _descent = GetComponent<ParachuteDescent>();

            // Hiza cizgisi yalnizca kendi ekraninda anlamli; rakibin nereye nisan
            // aldigini gostermek oyunu bozardi.
            if (_health == null || !_health.HasInputAuthority)
            {
                enabled = false;
                return;
            }

            _shooting = GetComponent<PlayerShooting>();

            if (_shooting != null && _shooting.FirePoint != null)
            {
                _fireLocalOffset = transform.InverseTransformPoint(_shooting.FirePoint.position);
                _hasFireOffset = true;
            }

            _propertyBlock = new MaterialPropertyBlock();
            BuildDots();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_container != null)
                Destroy(_container.gameObject);
        }

        private void BuildDots()
        {
            GameObject containerObject = new("AimLine");
            _container = containerObject.transform;

            _dots = new MeshRenderer[maxDots];

            for (int i = 0; i < maxDots; i++)
            {
                _dots[i] = OverlayMeshLibrary.CreateOverlayObject(
                    $"Dot_{i}", OverlayMeshLibrary.Disc, _container, Vector3.zero, dotSize);
            }
        }

        private void Update()
        {
            if (!ShouldShowLine(out Vector2 aim))
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            UpdateDots(aim);
        }

        private bool ShouldShowLine(out Vector2 aim)
        {
            aim = Vector2.zero;

            if (_health == null || _health.currentHealth <= 0f)
                return false;

            // Parasutle inerken nisan alinmaz.
            if (_descent != null && _descent.IsDescending)
                return false;

            if (InputManager.Instance == null || InputManager.Instance.attackJoystick == null)
                return false;

            aim = InputManager.Instance.attackJoystick.InputVector;
            return aim.sqrMagnitude > 0.01f;
        }

        private void UpdateDots(Vector2 aim)
        {
            Vector3 direction = new Vector3(aim.x, 0f, aim.y).normalized;
            Vector3 origin;

            if (alignWithBullets && _hasFireOffset)
            {
                // Merminin cikacagi nokta: yerel ofseti nisan yonune dondur.
                // Boylece nokta nokta cizgi merminin gercek isini gosterir.
                origin = transform.position + Quaternion.LookRotation(direction) * _fireLocalOffset;
            }
            else
            {
                origin = transform.position + Vector3.up * groundHeight;
            }

            float maxDistance = startOffset + (maxDots - 1) * dotSpacing;
            float blockedDistance = maxDistance;

            if (stopAtObstacle && Physics.Raycast(
                    origin, direction, out RaycastHit hit,
                    maxDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                // Kendi collider'imiz sayilmaz.
                if (hit.collider.GetComponentInParent<HealthController>() != _health)
                    blockedDistance = hit.distance;
            }

            for (int i = 0; i < _dots.Length; i++)
            {
                float distance = startOffset + i * dotSpacing;
                bool beyondBlock = distance > blockedDistance;

                if (beyondBlock && stopAtObstacle)
                {
                    _dots[i].gameObject.SetActive(false);
                    continue;
                }

                if (!_dots[i].gameObject.activeSelf)
                    _dots[i].gameObject.SetActive(true);

                _dots[i].transform.position = origin + direction * distance;

                // Uzaklastikca soluklasan noktalar mesafe hissini veriyor.
                float t = i / Mathf.Max(1f, _dots.Length - 1f);
                Color color = Color.Lerp(nearColor, farColor, t);

                // Hedefe cok yakin bir engel varsa son nokta uyari rengine doner.
                if (stopAtObstacle && distance + dotSpacing > blockedDistance)
                    color = blockedColor;

                _dots[i].GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(OverlayMeshLibrary.BaseColorId, color);
                _propertyBlock.SetFloat(OverlayMeshLibrary.FillId, 1f);
                _dots[i].SetPropertyBlock(_propertyBlock);
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible || _dots == null)
                return;

            _visible = visible;

            foreach (MeshRenderer dot in _dots)
            {
                if (dot != null)
                    dot.gameObject.SetActive(visible);
            }
        }
    }
}
