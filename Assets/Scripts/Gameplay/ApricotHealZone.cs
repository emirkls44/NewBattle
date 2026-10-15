using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Atilan kayisi. Once atanin elinden hedefe egri cizerek ucar; dustugu yerde
    /// yesil bir halka, icinde yesilimsi duman ve yukari suzulup kaybolan kayisi
    /// siluetleri belirir. Alan suresince icindeki takim arkadaslari (ve atan) iyilesir;
    /// rakipler etkilenmez. Ayarlar: Resources/KayisiAyarlari.
    /// </summary>
    public class ApricotHealZone : NetworkBehaviour
    {
        [Networked] private Vector3 Origin { get; set; }
        [Networked] private Vector3 Target { get; set; }
        [Networked] private int TeamId { get; set; }
        [Networked] private NetworkId Thrower { get; set; }
        [Networked] private TickTimer LandTimer { get; set; }
        [Networked] private TickTimer LifeTimer { get; set; }
        [Networked] private float FlightSeconds { get; set; }
        [Networked] private float Duration { get; set; }

        private ApricotSettings _settings;
        private GameObject _flying;
        private GameObject _area;
        private MeshRenderer _ring, _fill;
        private MaterialPropertyBlock _block;
        private readonly List<SpriteRenderer> _smoke = new();
        private readonly List<Floater> _floaters = new();
        private float _nextFloater;
        private bool _landedShown;

        private class Floater
        {
            public SpriteRenderer Renderer;
            public float Age;
            public float Life;
            public Vector3 Start;
            public float Size;
            public Color Color;
        }

        /// <summary>Host, spawn'dan once.</summary>
        public void Init(NetworkRunner runner, Vector3 origin, Vector3 target, int teamId, NetworkId thrower)
        {
            ApricotSettings settings = ApricotSettings.Instance;
            Origin = origin;
            Target = target;
            TeamId = teamId;
            Thrower = thrower;
            FlightSeconds = settings != null ? settings.flightSeconds : 0.6f;
            Duration = settings != null ? settings.duration : 6f;
            LandTimer = TickTimer.CreateFromSeconds(runner, FlightSeconds);
        }

        public override void Spawned()
        {
            _settings = ApricotSettings.Instance;
            transform.SetPositionAndRotation(Target, Quaternion.identity);
            _block = new MaterialPropertyBlock();
            PlayThrowGesture();

            if (_settings != null && _settings.model != null)
            {
                _flying = Instantiate(_settings.model);
                _flying.name = "UcanKayisi";
                _flying.transform.localScale = Vector3.one * _settings.modelSize;
                _flying.transform.position = Origin;
            }
        }

        /// <summary>Atan oyuncu atis hareketi yapar (yumruk animasyonu).</summary>
        private void PlayThrowGesture()
        {
            if (Runner == null || !Thrower.IsValid || !Runner.TryFindObject(Thrower, out NetworkObject thrower) || thrower == null)
                return;

            Animator animator = thrower.GetComponentInChildren<Animator>();
            if (animator == null)
                return;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == "Punch" && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger("Punch");
                    return;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !LandTimer.Expired(Runner))
                return;

            if (!LifeTimer.IsRunning)
                LifeTimer = TickTimer.CreateFromSeconds(Runner, Duration);

            if (LifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            HealTeam();
        }

        private void HealTeam()
        {
            if (_settings == null)
                return;

            float amount = _settings.healPerSecond * Runner.DeltaTime;
            float radiusSqr = _settings.radius * _settings.radius;
            Vector3 center = Target;

            var registry = PlayerRegistry.All;
            for (int i = 0; i < registry.Count; i++)
            {
                PlayerRegistry entry = registry[i];
                if (entry == null || entry.Health == null || entry.Stats == null || entry.Object == null ||
                    !entry.Object.IsValid || entry.Runner != Runner || !entry.Health.IsStanding)
                    continue;

                // Sadece atanin takimi (tek modda sadece atan).
                if (entry.Stats.TeamId != TeamId)
                    continue;

                Vector3 delta = entry.transform.position - center;
                if (Mathf.Abs(delta.y) > 2.5f)
                    continue;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                    entry.Health.Heal(amount);
            }
        }

        // ------------------------------------------------------------------
        // Gorunum (her cihazda)
        // ------------------------------------------------------------------

        public override void Render()
        {
            if (_settings == null)
                return;

            float? flightLeft = LandTimer.RemainingTime(Runner);
            bool landed = LandTimer.Expired(Runner) || (flightLeft.HasValue && flightLeft.Value <= 0f);

            if (!landed)
            {
                UpdateFlight(flightLeft ?? 0f);
                return;
            }

            if (!_landedShown)
                ShowLanded();

            float lifeLeft = LifeTimer.IsRunning ? LifeTimer.RemainingTime(Runner) ?? Duration : Duration;
            float fade = Mathf.Clamp01(lifeLeft / 0.6f); // son yarim saniyede soner
            UpdateArea(fade);
        }

        private void UpdateFlight(float remaining)
        {
            if (_flying == null)
                return;

            float t = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.05f, FlightSeconds));
            _flying.transform.position = _settings.ArcPoint(Origin, Target, t);
            _flying.transform.Rotate(new Vector3(260f, 140f, 0f) * Time.deltaTime, Space.Self);
        }

        private void ShowLanded()
        {
            _landedShown = true;
            if (_flying != null)
                Destroy(_flying);

            NewBattle.Audio.Sfx.Play("toplama", Target);

            _area = new GameObject("KayisiAlani");
            _area.transform.SetPositionAndRotation(Target + Vector3.up * 0.06f, Quaternion.identity);

            float radius = _settings.radius;
            _fill = OverlayMeshLibrary.CreateOverlayObject("Zemin", OverlayMeshLibrary.SoftDisc, _area.transform, Vector3.zero, radius);
            _ring = OverlayMeshLibrary.CreateOverlayObject("Halka", OverlayMeshLibrary.SoftRing, _area.transform, new Vector3(0f, 0.01f, 0f), radius);

            if (_settings.smoke != null)
            {
                var random = new System.Random(Object.Id.GetHashCode());
                for (int i = 0; i < 7; i++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float distance = (float)random.NextDouble() * radius * 0.6f;
                    SpriteRenderer puff = NewSprite("Duman", _settings.smoke, _settings.smokeColor);
                    puff.transform.localPosition = new Vector3(Mathf.Cos(angle) * distance, 0.35f + (float)random.NextDouble() * 0.6f,
                        Mathf.Sin(angle) * distance);
                    puff.transform.localScale = Vector3.one * radius * (0.7f + (float)random.NextDouble() * 0.5f);
                    _smoke.Add(puff);
                }
            }
        }

        private SpriteRenderer NewSprite(string label, Sprite sprite, Color color)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_area.transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void UpdateArea(float fade)
        {
            if (_area == null)
                return;

            SetOverlay(_ring, _settings.ringColor, fade);
            SetOverlay(_fill, _settings.fillColor, fade);

            Camera camera = Camera.main;
            Quaternion facing = camera != null ? camera.transform.rotation : Quaternion.identity;
            float time = Time.time;

            for (int i = 0; i < _smoke.Count; i++)
            {
                SpriteRenderer puff = _smoke[i];
                puff.transform.rotation = facing * Quaternion.Euler(0f, 0f, time * (12f + i * 5f) * (i % 2 == 0 ? 1f : -1f));
                Color color = _settings.smokeColor;
                color.a *= fade * (0.75f + 0.25f * Mathf.Sin(time * 1.7f + i));
                puff.color = color;
            }

            UpdateFloaters(facing, fade);
        }

        /// <summary>Halkadan yukari suzulup kaybolan kayisi ikonlari ve siluetleri.</summary>
        private void UpdateFloaters(Quaternion facing, float fade)
        {
            if (fade > 0.3f && Time.time >= _nextFloater && (_settings.icon != null || _settings.silhouette != null))
            {
                _nextFloater = Time.time + 0.16f;
                SpawnFloater();
            }

            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                Floater floater = _floaters[i];
                floater.Age += Time.deltaTime;
                float t = floater.Age / floater.Life;
                if (t >= 1f)
                {
                    Destroy(floater.Renderer.gameObject);
                    _floaters.RemoveAt(i);
                    continue;
                }

                Transform tr = floater.Renderer.transform;
                tr.position = floater.Start + Vector3.up * (t * 2.6f) + Vector3.right * (Mathf.Sin(t * 6f + i) * 0.12f);
                tr.rotation = facing;
                tr.localScale = Vector3.one * floater.Size * (0.6f + 0.4f * Mathf.Sin(Mathf.Min(1f, t * 3f) * Mathf.PI * 0.5f));
                Color color = floater.Color;
                color.a *= Mathf.Sin(t * Mathf.PI) * fade; // belirip kaybolur
                floater.Renderer.color = color;
            }
        }

        private void SpawnFloater()
        {
            bool silhouette = _settings.silhouette != null && (_settings.icon == null || Random.value < 0.55f);
            Sprite sprite = silhouette ? _settings.silhouette : _settings.icon;
            Color color = silhouette ? _settings.silhouetteColor : new Color(1f, 1f, 1f, 0.95f);

            Vector2 circle = Random.insideUnitCircle * _settings.radius * 0.8f;
            SpriteRenderer renderer = NewSprite("YukselenKayisi", sprite, color);
            var floater = new Floater
            {
                Renderer = renderer,
                Life = Random.Range(1.2f, 1.9f),
                Start = Target + new Vector3(circle.x, 0.2f, circle.y),
                Size = Random.Range(0.35f, 0.6f),
                Color = color
            };
            renderer.transform.position = floater.Start;
            renderer.transform.localScale = Vector3.zero;
            _floaters.Add(floater);
        }

        private void SetOverlay(MeshRenderer overlay, Color color, float fade)
        {
            if (overlay == null)
                return;

            color.a *= fade;
            _block.SetColor(OverlayMeshLibrary.BaseColorId, color);
            _block.SetFloat(OverlayMeshLibrary.FillId, 1f);
            overlay.SetPropertyBlock(_block);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_flying != null)
                Destroy(_flying);
            if (_area != null)
                Destroy(_area);
            _floaters.Clear();
            _smoke.Clear();
        }
    }
}
