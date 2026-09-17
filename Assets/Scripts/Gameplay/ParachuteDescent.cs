using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Mac basindaki haritaya inis: serbest dusus -> parasut -> yere temas.
    ///
    /// Inis fizik simulasyonu ile degil, normalize edilmis ilerlemeyle yapilir.
    /// Sebebi: fizikle yapilinca inis suresi hiz/yercekimi/ruzgar ayarlarina gore
    /// kayar ve "2 saniye sonra oyuncu yerde" garantisi bozulur. Ilerleme tabanli
    /// cozumde toplam sure sabittir, egri sadece hissi belirler.
    ///
    /// Yukseklik profili:
    ///   - Serbest dusus evresi irtifanin %68'ini hizlica yer (ease-in)
    ///   - Parasut evresi kalan %32'yi sabit ve yavas ilerler
    /// Boylece once "dusuyorum", sonra "suzuluyorum" hissi olusur.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ParachuteDescent : NetworkBehaviour
    {
        public enum DescentPhase : byte
        {
            None = 0,
            Freefall = 1,
            Parachute = 2,
            Landed = 3
        }

        [Header("Zamanlama")]
        [SerializeField, Min(0f)] private float freefallSeconds = 0.8f;
        [SerializeField, Min(0.1f)] private float parachuteSeconds = 2f;

        [Header("Yukseklik")]
        [SerializeField, Min(5f)] private float startAltitude = 48f;
        [Tooltip("Irtifanin ne kadari serbest dususte yenilsin.")]
        [SerializeField, Range(0.3f, 0.9f)] private float freefallAltitudeShare = 0.68f;

        [Header("Yonlendirme")]
        [Tooltip("Parasutteyken joystick ile saniyede kac metre kayabilir.")]
        [SerializeField, Min(0f)] private float driftSpeed = 6f;
        [SerializeField, Min(0f)] private float freefallDriftSpeed = 2f;

        [Header("Zemin")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Gorsel")]
        [SerializeField] private Mesh parachuteMesh;
        [SerializeField] private Material parachuteMaterial;
        [SerializeField] private Vector3 parachuteOffset = new(0f, 2.15f, 0f);
        [SerializeField] private float parachuteScale = 1f;

        [Networked] public DescentPhase Phase { get; private set; }
        [Networked] private float PhaseElapsed { get; set; }
        [Networked] private Vector3 GroundPoint { get; set; }

        public bool IsDescending => Phase == DescentPhase.Freefall || Phase == DescentPhase.Parachute;

        private CharacterController _characterController;
        private GameObject _parachuteVisual;
        private float _mapRadius = 29f;

        public override void Spawned()
        {
            _characterController = GetComponent<CharacterController>();

            DropPhaseController dropPhase = FindFirstObjectByType<DropPhaseController>();
            if (dropPhase != null)
                _mapRadius = dropPhase.PlayableMapRadius;

            CreateParachuteVisual();
        }

        /// <summary>
        /// Sunucu cagirir: oyuncuyu secilen noktanin uzerine koyup inisi baslatir.
        /// </summary>
        public void BeginDescent(Vector3 targetGroundPoint)
        {
            if (!HasStateAuthority)
                return;

            // Hedef noktanin zemini burada bir kez, guvenli sekilde bulunur.
            // Zemin yoksa en yakin gecerli zemine kayar - oyuncu bosluga dusmez.
            NewBattle.Gameplay.GroundSampler.TryFindGround(
                targetGroundPoint, _mapRadius, groundMask, out Vector3 resolved);

            GroundPoint = resolved;
            PhaseElapsed = 0f;
            Phase = DescentPhase.Freefall;

            // Inis boyunca konumu dogrudan biz suruyoruz. CharacterController acik
            // kalirsa her Move'da eski konumu geri dayatir; bu yuzden bir kez
            // kapatip yere degdigimizde tekrar aciyoruz.
            SetControllerEnabled(false);
            transform.position = new Vector3(GroundPoint.x, GroundPoint.y + startAltitude, GroundPoint.z);
        }

        private void SetControllerEnabled(bool enabled)
        {
            if (_characterController != null)
                _characterController.enabled = enabled;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !IsDescending)
                return;

            float deltaTime = Runner.DeltaTime;
            PhaseElapsed += deltaTime;

            Vector2 steer = ReadSteerInput();
            AdvanceDescent(deltaTime, steer);
        }

        private Vector2 ReadSteerInput()
        {
            // Inis sirasinda sol joystick sadece suzulme yonunu degistirir.
            return GetInput<NetworkInputData>(out NetworkInputData input)
                ? Vector2.ClampMagnitude(input.JoystickInput, 1f)
                : Vector2.zero;
        }

        private void AdvanceDescent(float deltaTime, Vector2 steer)
        {
            float freefallAltitude = startAltitude * freefallAltitudeShare;
            float parachuteAltitude = startAltitude - freefallAltitude;

            float height;
            float currentDriftSpeed;

            if (Phase == DescentPhase.Freefall)
            {
                float duration = Mathf.Max(0.0001f, freefallSeconds);
                float t = Mathf.Clamp01(PhaseElapsed / duration);

                // Ease-in: basta yavas, sonra hizlanan dusus.
                height = startAltitude - freefallAltitude * (t * t);
                currentDriftSpeed = freefallDriftSpeed;

                if (t >= 1f)
                {
                    Phase = DescentPhase.Parachute;
                    PhaseElapsed = 0f;
                }
            }
            else
            {
                float duration = Mathf.Max(0.0001f, parachuteSeconds);
                float t = Mathf.Clamp01(PhaseElapsed / duration);

                height = parachuteAltitude * (1f - t);
                currentDriftSpeed = driftSpeed;

                if (t >= 1f)
                {
                    Land();
                    return;
                }
            }

            Vector3 position = transform.position;
            Vector3 drift = new Vector3(steer.x, 0f, steer.y) * (currentDriftSpeed * deltaTime);
            Vector2 horizontal = new(position.x + drift.x, position.z + drift.z);

            // Suzulurken harita disina cikilamaz.
            horizontal = Vector2.ClampMagnitude(horizontal, _mapRadius * 0.96f);

            transform.position = new Vector3(horizontal.x, GroundPoint.y + height, horizontal.y);
        }

        private void Land()
        {
            Phase = DescentPhase.Landed;

            Vector3 position = transform.position;
            transform.position = new Vector3(position.x, SampleGroundHeight(position), position.z);

            // Kontrol oyuncuya geri veriliyor: bu andan itibaren yurur ve ates eder.
            SetControllerEnabled(true);
        }

        /// <summary>
        /// Zemin yuksekligi.
        ///
        /// Onceki surum y = 60'tan isin atiyor, carpmazsa 0 donduruyordu. Iki
        /// ayri sekilde kiriliyordu: arazi 60 metrenin uzerindeyse isin zaten
        /// zeminin altindan basliyordu, ve harita disina suzulen oyuncu y = 0'a
        /// yani yerin altina konuluyordu. GroundSampler her iki durumu da cozer.
        /// </summary>
        private float SampleGroundHeight(Vector3 position)
        {
            return NewBattle.Gameplay.GroundSampler.HeightAt(
                position.x, position.z, groundMask, GroundPoint.y);
        }

        #region Gorsel

        private void CreateParachuteVisual()
        {
            if (parachuteMesh == null || parachuteMaterial == null)
                return;

            _parachuteVisual = new GameObject("Parachute");
            _parachuteVisual.transform.SetParent(transform, false);
            _parachuteVisual.transform.localPosition = parachuteOffset;
            _parachuteVisual.transform.localScale = Vector3.one * parachuteScale;

            MeshFilter filter = _parachuteVisual.AddComponent<MeshFilter>();
            filter.sharedMesh = parachuteMesh;

            MeshRenderer renderer = _parachuteVisual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = parachuteMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _parachuteVisual.SetActive(false);
        }

        public override void Render()
        {
            if (_parachuteVisual == null)
                return;

            bool shouldShow = Phase == DescentPhase.Parachute;

            if (_parachuteVisual.activeSelf != shouldShow)
                _parachuteVisual.SetActive(shouldShow);

            if (shouldShow)
            {
                // Hafif salinim: parasut sabit dursa cansiz gorunuyor.
                float sway = Mathf.Sin(Time.time * 1.6f) * 6f;
                _parachuteVisual.transform.localRotation = Quaternion.Euler(sway * 0.4f, 0f, sway);
            }
        }

        #endregion
    }
}
