using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Bir oyuncunun bu cihazdaki gozlemciye gorunup gorunmeyecegine karar verir.
    ///
    /// Kurallar (istenen davranis):
    ///   - Kendisi ve takim arkadaslari her zaman gorunur.
    ///   - Cimenin disindaysa herkese gorunur.
    ///   - Cimende ve hareketsizse dusmanlara gorunmez, ayak izi de birakmaz.
    ///   - Cimende hareket ediyorsa:
    ///       * dusman cok yakinsa       -> govde gorunur
    ///       * orta mesafedeyse         -> govde gizli, SADECE ayak izleri gorunur
    ///       * daha uzaktaysa           -> hicbir sey gorunmez
    ///
    /// ONEMLI SINIR: bu katman gorsel bir cozumdur. Gizli oyuncunun konumu hala
    /// istemciye replike edilir, yani hile yapan bir istemci onu yine de gorebilir.
    /// Gercek cozum Fusion'un interest management'i ile konumu hic gondermemektir;
    /// sahnede PlayerAoIController var ama takim arkadasi istisnasini desteklemiyor.
    /// </summary>
    [RequireComponent(typeof(PlayerPresence))]
    [RequireComponent(typeof(PlayerCombatStats))]
    public class PlayerVisibility : MonoBehaviour
    {
        [Header("Mesafeler")]
        [Tooltip("Cimendeki hareketli oyuncunun govdesinin ifsa oldugu mesafe.")]
        [SerializeField, Min(0.5f)] private float closeRevealDistance = 6f;

        [Tooltip("Bu mesafeye kadar govde degil, sadece ayak izleri gorunur.")]
        [SerializeField, Min(1f)] private float footprintRevealDistance = 20f;

        [Header("Kural Secenekleri")]
        [Tooltip("Acikken, yakin mesafe ifsasi icin gozlemcinin de hareket ediyor olmasi gerekir.")]
        [SerializeField] private bool requireObserverMovement;

        [Header("Kendi Gizlenme Geri Bildirimi")]
        [Tooltip("Yerel oyuncu cimene girdiginde kendi govdesini yari saydam gorur.")]
        [SerializeField] private bool fadeSelfInGrass = true;
        [SerializeField, Range(0.1f, 1f)] private float selfGrassAlpha = 0.78f;

        private PlayerPresence _presence;
        private PlayerCombatStats _stats;
        private FootprintTrail _footprints;
        private readonly List<Renderer> _bodyRenderers = new();

        private bool _bodyVisible = true;
        private bool _footprintsVisible = true;
        private bool _selfFaded;
        private Material[] _originalMaterials;

        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        /// <summary>Alfasi degistirilecek renk ozellikleri.</summary>
        private static readonly int[] CopiedColorIds =
        {
            Shader.PropertyToID("_BaseColor"),
            Shader.PropertyToID("_ShadowTint")
        };

        /// <summary>Opak materyal -> saydam esi. Tum oyuncular arasinda paylasilir.</summary>
        private static readonly Dictionary<Material, Material> FadeMaterialCache = new();

        /// <summary>Govde su an bu cihazdaki gozlemciye gorunuyor mu.</summary>
        public bool IsBodyVisible => _bodyVisible;

        public PlayerRelation RelationToLocalPlayer { get; private set; } = PlayerRelation.Unknown;

        private void Awake()
        {
            _presence = GetComponent<PlayerPresence>();
            _stats = GetComponent<PlayerCombatStats>();
            _footprints = GetComponent<FootprintTrail>();

            CollectBodyRenderers();
        }

        /// <summary>
        /// Govde renderer'larini bir kez topla. Gosterge katmanlari (cember, ayak izi)
        /// OverlayVisual tasidigi icin bu listeye girmez ve gizlenmeden etkilenmez.
        /// </summary>
        private void CollectBodyRenderers()
        {
            _bodyRenderers.Clear();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<OverlayVisual>() != null)
                    continue;

                _bodyRenderers.Add(renderer);
            }
        }

        private void LateUpdate()
        {
            RelationToLocalPlayer = LocalPlayerContext.GetRelation(_stats);

            EvaluateVisibility(out bool bodyVisible, out bool footprintsVisible);

            ApplyBodyVisibility(bodyVisible);
            ApplyFootprintVisibility(footprintsVisible);
            ApplySelfFade();
        }

        /// <summary>
        /// Kendi karakterin cimendeyken yari saydam gorunur. Bu tamamen yereldir:
        /// baska hicbir oyuncu seni boyle gormez. Amac "su an gizlisin" bilgisini
        /// ayri bir ikon koymadan, dogrudan karakterin uzerinden vermek.
        /// </summary>
        private void ApplySelfFade()
        {
            if (!fadeSelfInGrass)
                return;

            bool shouldFade = RelationToLocalPlayer == PlayerRelation.Self
                              && _presence != null
                              && _presence.InGrass;

            if (_selfFaded == shouldFade)
                return;

            _selfFaded = shouldFade;

            if (shouldFade)
                CacheOriginalMaterials();

            for (int i = 0; i < _bodyRenderers.Count; i++)
            {
                Renderer renderer = _bodyRenderers[i];
                if (renderer == null)
                    continue;

                renderer.sharedMaterial = shouldFade
                    ? FadeMaterialFor(_originalMaterials[i])
                    : _originalMaterials[i];
            }
        }

        private void CacheOriginalMaterials()
        {
            if (_originalMaterials != null && _originalMaterials.Length == _bodyRenderers.Count)
                return;

            _originalMaterials = new Material[_bodyRenderers.Count];
            for (int i = 0; i < _bodyRenderers.Count; i++)
                _originalMaterials[i] = _bodyRenderers[i] != null ? _bodyRenderers[i].sharedMaterial : null;
        }

        /// <summary>
        /// Opak materyalin saydam esini. Materyal basina bir kez uretilip paylasilir;
        /// her renderer icin yeni materyal yaratmak batch'leri kirar ve sizinti yapar.
        /// </summary>
        private Material FadeMaterialFor(Material source)
        {
            if (source == null)
                return null;

            if (FadeMaterialCache.TryGetValue(source, out Material cached) && cached != null)
                return cached;

            // Materyalin KOPYASINI aliyoruz, baska bir shader'a gecmiyoruz.
            //
            // Onceki surum her materyali "ToonVertexColorFade" shader'ina
            // ceviriyordu. Bu sadece toon materyalleri icin dogruydu: karakter
            // modeli disaridan geldigi ve URP Lit kullandigi icin, shader
            // degisince model saydamlasmiyor, BASKA bir gorunume atliyordu -
            // dokusunu ve rengini kaybedip soluk gri bir sekle donusuyordu.
            //
            // Kopya + saydam yuzey ayari, shader ne olursa olsun gorunumu
            // aynen korur ve yalnizca opakligi degistirir.
            Material fade = new(source)
            {
                name = source.name + "_Fade",
                hideFlags = HideFlags.DontSave
            };

            ApplyAlpha(fade, selfGrassAlpha);
            MakeTransparent(fade);

            FadeMaterialCache[source] = fade;
            return fade;
        }

        /// <summary>
        /// Materyalin hangi ozelligi rengi tutuyorsa onun alfasini ayarlar.
        /// URP Lit _BaseColor, eski shader'lar _Color, bizim toon shader _Alpha
        /// kullaniyor - hepsini deniyoruz.
        /// </summary>
        private static void ApplyAlpha(Material material, float alpha)
        {
            foreach (int colorId in CopiedColorIds)
            {
                if (!material.HasProperty(colorId))
                    continue;

                Color color = material.GetColor(colorId);
                color.a = alpha;
                material.SetColor(colorId, color);
            }

            if (material.HasProperty(LegacyColorId))
            {
                Color color = material.GetColor(LegacyColorId);
                color.a = alpha;
                material.SetColor(LegacyColorId, color);
            }

            if (material.HasProperty(AlphaId))
                material.SetFloat(AlphaId, alpha);
        }

        /// <summary>URP'nin opak/saydam gecisi icin gereken tum ayarlar.</summary>
        private static void MakeTransparent(Material material)
        {
            if (material.HasProperty(SurfaceId))
                material.SetFloat(SurfaceId, 1f);

            if (material.HasProperty(SrcBlendId))
                material.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (material.HasProperty(DstBlendId))
                material.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (material.HasProperty(ZWriteId))
                material.SetFloat(ZWriteId, 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void EvaluateVisibility(out bool bodyVisible, out bool footprintsVisible)
        {
            // Yerel oyuncu henuz yoksa (inis fazi / seyirci) kimseyi gizleme.
            if (RelationToLocalPlayer != PlayerRelation.Enemy)
            {
                bodyVisible = true;
                footprintsVisible = true;
                return;
            }

            if (!_presence.InGrass)
            {
                bodyVisible = true;
                footprintsVisible = true;
                return;
            }

            if (!_presence.IsMoving)
            {
                // Cimende durup bekleyen oyuncu tamamen kaybolur - iz de birakmaz.
                bodyVisible = false;
                footprintsVisible = false;
                return;
            }

            float distance = LocalPlayerContext.DistanceTo(transform.position);

            if (distance <= closeRevealDistance && ObserverMovementAllowsReveal())
            {
                bodyVisible = true;
                footprintsVisible = true;
                return;
            }

            bodyVisible = false;
            footprintsVisible = distance <= footprintRevealDistance;
        }

        /// <summary>
        /// Rastgele bir gozlemci (ornegin bir bot) bu oyuncunun govdesini gorebilir mi.
        ///
        /// Botlarin ayri bir "gizli mi" mantigi olmamali; oyuncuyla botun ayni kurala
        /// uymasi, gizlenmenin oyunda tutarli hissedilmesinin sarti.
        /// </summary>
        public bool IsBodyVisibleFrom(Vector3 observerPosition, bool observerIsEnemy = true)
        {
            if (!observerIsEnemy)
                return true;

            if (_presence == null || !_presence.InGrass)
                return true;

            if (!_presence.IsMoving)
                return false;

            return Vector3.Distance(observerPosition, transform.position) <= closeRevealDistance;
        }

        private bool ObserverMovementAllowsReveal()
        {
            if (!requireObserverMovement)
                return true;

            if (!LocalPlayerContext.IsReady)
                return true;

            PlayerPresence observer = LocalPlayerContext.Transform.GetComponent<PlayerPresence>();
            return observer == null || observer.IsMoving;
        }

        private void ApplyBodyVisibility(bool visible)
        {
            if (_bodyVisible == visible)
                return;

            _bodyVisible = visible;

            foreach (Renderer renderer in _bodyRenderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }

        private void ApplyFootprintVisibility(bool visible)
        {
            if (_footprintsVisible == visible)
                return;

            _footprintsVisible = visible;

            if (_footprints != null)
                _footprints.SetVisible(visible);
        }
    }
}
