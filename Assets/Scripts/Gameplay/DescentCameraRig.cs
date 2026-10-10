using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Inis sirasinda oyun kamerasini yonetir (sadece kendi oyuncumuz icin).
    ///
    ///   Serbest dusus : kamera karakterin tam tepesinden, YAKINDAN ve
    ///                   PERSPEKTIF bakar. Karakter ekrani doldurur, arkada mavi
    ///                   gokyuzu vardir; yere yaklastikca gokyuzu sis gibi
    ///                   acilir ve zemin gercekten buyuyerek yaklasir.
    ///   Parasut       : kamera yumusakca oyunun normal (ortografik, egik)
    ///                   gorunumune doner.
    ///
    /// Oyun kamerasi ortografik oldugu icin perspektif gecisi projeksiyon
    /// matrislerinin karistirilmasiyla yapilir; boylece ani bir sicrama olmaz.
    /// CameraFollow normal takibi yapmaya devam eder; bu bilesen ondan SONRA
    /// calisip sonucu inis gorunumuyle karistirir.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(Camera))]
    public class DescentCameraRig : MonoBehaviour
    {
        [Header("Serbest Dusus Kamerasi")]
        [Tooltip("Dususun basinda kameranin karaktere uzakligi. Kucuk = karakter daha buyuk.")]
        [SerializeField, Min(1f)] private float closeDistance = 5.5f;
        [Tooltip("Dususun sonunda kameranin karaktere uzakligi.")]
        [SerializeField, Min(1f)] private float farDistance = 9f;
        [Tooltip("Serbest dususteki perspektif gorus acisi.")]
        [SerializeField, Range(20f, 90f)] private float freefallFieldOfView = 50f;

        [Header("Parasut Gecisi")]
        [Tooltip("Parasut acildiktan sonra normal kameraya donus suresi.")]
        [SerializeField, Min(0.05f)] private float parachuteBlendSeconds = 0.9f;

        [Header("Gokyuzu")]
        [SerializeField] private Sprite skySprite;
        [Tooltip("Gokyuzunun gorunurlugu bu noktada azalmaya baslar (0-1).")]
        [SerializeField, Range(0f, 1f)] private float skyFadeStart = 0.45f;
        [Tooltip("Bu noktada gokyuzu tamamen kaybolur ve harita gorunur.")]
        [SerializeField, Range(0f, 1f)] private float skyFadeEnd = 0.85f;
        [Tooltip("Gokyuzu karakterin bu kadar arkasinda durur.")]
        [SerializeField, Min(0.5f)] private float skyGap = 3f;

        private Camera _camera;
        private CameraFollow _follow;
        private Transform _cachedTarget;
        private ParachuteDescent _descent;
        private SpriteRenderer _sky;
        private bool _customProjection;
        private bool _baseOrthographic;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _follow = GetComponent<CameraFollow>();
            _baseOrthographic = _camera.orthographic;
            CreateSky();
        }

        private void OnDisable() => RestoreProjection();

        private void OnDestroy()
        {
            if (_sky != null)
                Destroy(_sky.gameObject);
        }

        private void LateUpdate()
        {
            Transform target = _follow != null ? _follow.target : null;
            if (target != _cachedTarget)
            {
                _cachedTarget = target;
                _descent = target != null ? target.GetComponent<ParachuteDescent>() : null;
            }

            float weight = 0f;
            float perspective = 0f;
            float skyAlpha = 0f;
            float distance = farDistance;

            if (_descent != null && _descent.Object != null && _descent.Object.IsValid)
            {
                switch (_descent.Phase)
                {
                    case ParachuteDescent.DescentPhase.Freefall:
                    {
                        float t = _descent.FreefallProgress;
                        distance = Mathf.Lerp(closeDistance, farDistance, t);
                        weight = 1f;
                        perspective = 1f;
                        skyAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(skyFadeStart, skyFadeEnd, t));
                        break;
                    }

                    case ParachuteDescent.DescentPhase.Parachute:
                    {
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_descent.ParachuteElapsed / parachuteBlendSeconds));
                        weight = 1f - k;
                        perspective = 1f - k;
                        break;
                    }
                }
            }

            if (weight > 0.001f && target != null)
            {
                Vector3 overridePosition = target.position + Vector3.up * distance;
                Quaternion overrideRotation = Quaternion.Euler(90f, 0f, 0f); // tam tepeden, kuzey yukari
                transform.SetPositionAndRotation(
                    Vector3.Lerp(transform.position, overridePosition, weight),
                    Quaternion.Slerp(transform.rotation, overrideRotation, weight));
            }

            ApplyProjection(perspective);
            UpdateSky(skyAlpha, distance);
        }

        /// <summary>0 = oyunun normal kamerasi, 1 = serbest dusus perspektifi.</summary>
        private void ApplyProjection(float perspective)
        {
            if (perspective <= 0.001f)
            {
                RestoreProjection();
                return;
            }

            float aspect = _camera.aspect;
            float near = _camera.nearClipPlane;
            float far = _camera.farClipPlane;
            float size = _camera.orthographicSize;

            Matrix4x4 perspectiveMatrix = Matrix4x4.Perspective(freefallFieldOfView, aspect, near, far);
            Matrix4x4 normalMatrix = _baseOrthographic
                ? Matrix4x4.Ortho(-size * aspect, size * aspect, -size, size, near, far)
                : Matrix4x4.Perspective(_camera.fieldOfView, aspect, near, far);

            float e = perspective * perspective * (3f - 2f * perspective);
            var blended = new Matrix4x4();
            for (int i = 0; i < 16; i++)
                blended[i] = Mathf.Lerp(normalMatrix[i], perspectiveMatrix[i], e);

            // Golgeler/sis gibi sistemler kameranin turune bakar: cogunlukla
            // perspektifteyken perspektif saysinlar.
            _camera.orthographic = _baseOrthographic && e < 0.5f;
            _camera.projectionMatrix = blended;
            _customProjection = true;
        }

        private void RestoreProjection()
        {
            if (!_customProjection || _camera == null)
                return;

            _camera.orthographic = _baseOrthographic;
            _camera.ResetProjectionMatrix();
            _customProjection = false;
        }

        private void UpdateSky(float alpha, float distance)
        {
            if (_sky == null)
                return;

            bool visible = alpha > 0.001f;
            if (_sky.gameObject.activeSelf != visible)
                _sky.gameObject.SetActive(visible);

            if (!visible)
                return;

            // Karakterin arkasinda, ekrani tamamen kaplayacak buyuklukte.
            float depth = distance + skyGap;
            float height = 2f * depth * Mathf.Tan(freefallFieldOfView * 0.5f * Mathf.Deg2Rad);
            float size = Mathf.Max(height, height * _camera.aspect) * 1.3f;
            float spriteSize = _sky.sprite != null ? Mathf.Max(0.01f, _sky.sprite.bounds.size.y) : 1f;

            _sky.transform.SetPositionAndRotation(transform.position + transform.forward * depth, transform.rotation);
            _sky.transform.localScale = Vector3.one * (size / spriteSize);
            _sky.color = new Color(1f, 1f, 1f, alpha);
        }

        private void CreateSky()
        {
            var sky = new GameObject("InisGokyuzu");
            _sky = sky.AddComponent<SpriteRenderer>();
            _sky.sprite = skySprite != null ? skySprite : CreateFallbackSky();
            _sky.sortingOrder = -100;
            _sky.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sky.receiveShadows = false;
            sky.SetActive(false);
        }

        private static Sprite CreateFallbackSky()
        {
            var texture = new Texture2D(2, 2) { wrapMode = TextureWrapMode.Clamp };
            Color sky = new(0.72f, 0.88f, 0.97f, 1f);
            texture.SetPixels(new[] { sky, sky, sky, sky });
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }
    }
}
