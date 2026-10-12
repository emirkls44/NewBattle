using NewBattle.Audio;
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

        [Header("Ucus")]
        [Tooltip("Oyuncu secilen noktanin tam ustunde degil, bu kadar uzaktan baslayip " +
                 "serbest dususte noktaya suzulur (harita merkezi tarafindan gelir).")]
        [SerializeField, Min(0f)] private float flightDistance = 18f;

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
        [Tooltip("Parasutun acilma animasyonu (saniye).")]
        [SerializeField, Min(0.05f)] private float chuteOpenSeconds = 0.45f;
        [Tooltip("Yere degince parasutun kaybolma animasyonu (saniye).")]
        [SerializeField, Min(0.05f)] private float chuteCloseSeconds = 0.25f;

        [Networked] public DescentPhase Phase { get; private set; }
        [Networked] private float PhaseElapsed { get; set; }
        [Networked] private Vector3 GroundPoint { get; set; }
        [Networked] private Vector2 FlightOffset { get; set; }
        [Networked] private Vector2 DriftOffset { get; set; }

        public bool IsDescending => Phase == DescentPhase.Freefall || Phase == DescentPhase.Parachute;

        /// <summary>Serbest dususun ilerlemesi (0-1). Parasut acildiysa 1.</summary>
        public float FreefallProgress => Phase switch
        {
            DescentPhase.Freefall => Mathf.Clamp01(PhaseElapsed / Mathf.Max(0.0001f, freefallSeconds)),
            DescentPhase.None => 0f,
            _ => 1f
        };

        /// <summary>Parasut acildiktan sonra gecen sure (saniye).</summary>
        public float ParachuteElapsed => Phase == DescentPhase.Parachute ? PhaseElapsed : 0f;

        private DescentPhase _renderedPhase;
        private float _chuteTimer;
        private bool _chuteClosing;

        private CharacterController _characterController;
        private GameObject _parachuteVisual;
        private float _mapRadius = 29f;

        public override void Spawned()
        {
            _characterController = GetComponent<CharacterController>();

            DropPhaseController dropPhase = FindFirstObjectByType<DropPhaseController>();
            if (dropPhase != null)
                _mapRadius = dropPhase.MapExtent;

            CreateParachuteVisual();

            // Dusus pozu (yuzustu, donerek) her istemcide gorunsun. Prefab'a elle
            // eklenmisse onun ayarlari kullanilir.
            if (!TryGetComponent(out DescentPoseVisual _))
                gameObject.AddComponent<DescentPoseVisual>();
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
            DriftOffset = Vector2.zero;

            // Ucus: harita merkezi tarafindan noktaya dogru suzulerek gel.
            Vector2 target = new(GroundPoint.x, GroundPoint.z);
            Vector2 toCenter = -target;
            Vector2 direction = toCenter.sqrMagnitude > 1f ? toCenter.normalized : Vector2.up;
            FlightOffset = direction * flightDistance;

            // Inis boyunca konumu dogrudan biz suruyoruz. CharacterController acik
            // kalirsa her Move'da eski konumu geri dayatir; bu yuzden bir kez
            // kapatip yere degdigimizde tekrar aciyoruz.
            SetControllerEnabled(false);
            Vector2 start = ClampToMap(target + FlightOffset);
            transform.position = new Vector3(start.x, GroundPoint.y + startAltitude, start.y);
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
            float flightRemaining = 0f;

            if (Phase == DescentPhase.Freefall)
            {
                float duration = Mathf.Max(0.0001f, freefallSeconds);
                float t = Mathf.Clamp01(PhaseElapsed / duration);

                // Ease-in: basta yavas, sonra hizlanan dusus.
                height = startAltitude - freefallAltitude * (t * t);
                currentDriftSpeed = freefallDriftSpeed;

                // Ease-out: noktaya hizla yaklasir, sonda yavaslar.
                flightRemaining = (1f - t) * (1f - t);

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

            // Joystick ile yapilan kayma ayri birikir; ucus payi ondan bagimsiz azalir.
            DriftOffset += steer * (currentDriftSpeed * deltaTime);

            Vector2 target = new(GroundPoint.x, GroundPoint.z);
            Vector2 horizontal = ClampToMap(target + FlightOffset * flightRemaining + DriftOffset);
            DriftOffset = horizontal - target - FlightOffset * flightRemaining;

            transform.position = new Vector3(horizontal.x, GroundPoint.y + height, horizontal.y);
        }

        /// <summary>Suzulurken kare haritanin disina cikilamaz.</summary>
        private Vector2 ClampToMap(Vector2 point)
        {
            float limit = _mapRadius * 0.96f;
            return new Vector2(Mathf.Clamp(point.x, -limit, limit), Mathf.Clamp(point.y, -limit, limit));
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
            DescentPhase phase = Phase;

            UpdateWindSound(phase);

            if (phase != _renderedPhase)
            {
                if (phase == DescentPhase.Parachute)
                {
                    _chuteTimer = 0f;
                    _chuteClosing = false;
                    Sfx.Play("parasut", transform.position);
                }
                else if (phase == DescentPhase.Landed && _renderedPhase == DescentPhase.Parachute)
                {
                    _chuteTimer = 0f;
                    _chuteClosing = true;

                    // Yere degince hafif sarsinti (sadece kendi kameramizda).
                    if (HasInputAuthority)
                        CameraFollow.Shake(0.35f);

                    Sfx.Play("inis", transform.position);
                }

                _renderedPhase = phase;
            }

            if (_parachuteVisual == null)
                return;

            if (phase == DescentPhase.Parachute)
            {
                // Acilis: kucukten baslayip biraz tasarak (pop) tam boyuna oturur.
                _chuteTimer += Time.deltaTime;
                float open = EaseOutBack(Mathf.Clamp01(_chuteTimer / chuteOpenSeconds));
                SetChute(true, Mathf.LerpUnclamped(0.15f, 1f, open));

                // Hafif salinim: parasut sabit dursa cansiz gorunuyor.
                float sway = Mathf.Sin(Time.time * 1.6f) * 6f;
                _parachuteVisual.transform.localRotation = Quaternion.Euler(sway * 0.4f, 0f, sway);
                return;
            }

            if (_chuteClosing)
            {
                // Inis: parasut buzulerek kaybolur.
                _chuteTimer += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(_chuteTimer / chuteCloseSeconds);
                SetChute(k > 0f, k);
                if (k <= 0f)
                    _chuteClosing = false;
                return;
            }

            SetChute(false, 1f);
        }

        private int _windLoop;

        /// <summary>
        /// Serbest duseste sadece KENDI oyuncumuz ruzgar duyar; paraşut
        /// acilinca ruzgar kisilip biter.
        /// </summary>
        private void UpdateWindSound(DescentPhase phase)
        {
            bool wantWind = HasInputAuthority && (phase == DescentPhase.Freefall || phase == DescentPhase.Parachute);

            if (wantWind && _windLoop == 0)
                _windLoop = Sfx.StartLoop("ruzgar");

            if (_windLoop == 0)
                return;

            if (!wantWind)
            {
                Sfx.StopLoop(_windLoop);
                _windLoop = 0;
                return;
            }

            // Dususte tam, parasutte yavasca kisilir.
            float volume = phase == DescentPhase.Freefall ? 1f : Mathf.Clamp01(1f - _chuteTimer / 1.2f) * 0.6f;
            Sfx.SetLoopVolume(_windLoop, volume);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_windLoop != 0)
            {
                Sfx.StopLoop(_windLoop);
                _windLoop = 0;
            }
        }

        private void SetChute(bool visible, float scale)
        {
            if (_parachuteVisual.activeSelf != visible)
                _parachuteVisual.SetActive(visible);

            if (visible)
                _parachuteVisual.transform.localScale = Vector3.one * (parachuteScale * Mathf.Max(0.01f, scale));
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        #endregion
    }
}
