using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Haritaya gizlenme calilari serpistirir.
    ///
    /// Neden calisma aninda? Eski prototip arenasi (PrototypeIslandBuilder) zeminini
    /// Awake'te uretiyor - Editor'de ortada bir zemin yok, sahneye onceden cali koymak
    /// mumkun degil. Bu bilesen zemin olustuktan sonra devreye girer.
    ///
    /// Yerlesim SABIT TOHUMLUDUR: her istemcide birebir ayni cali dizilimi cikar.
    /// Bu onemli, cunku gizlenme kararini host verir ama oyuncu gordugu caliya
    /// guvenerek oynar; istemcide baska yerde duran bir cali oyunu yalanci yapar.
    /// </summary>
    [DefaultExecutionOrder(100)] // PrototypeIslandBuilder Awake'te zemini kurduktan sonra
    public class GrassPatchSpawner : MonoBehaviour
    {
        [Header("Gorsel")]
        [Tooltip("Assets/GameArt/Meshes/Grass altindaki uretilmis cali mesh'leri.")]
        [SerializeField] private Mesh[] patchMeshes;
        [SerializeField] private Material patchMaterial;

        [Header("Dagitim")]
        [SerializeField] private int seed = 91723;
        [SerializeField, Min(0)] private int patchCount = 26;
        [SerializeField] private Vector2 areaCenter = Vector2.zero;
        [SerializeField, Min(1f)] private float areaRadius = 32f;
        [SerializeField, Min(0.5f)] private float minSpacing = 4.5f;
        [SerializeField] private Vector2 scaleRange = new(0.9f, 1.45f);

        [Header("Gizlenme Hacmi")]
        [Tooltip("Oyuncunun gizlenmis sayildigi yaricap. Gorsel yaricaptan biraz kucuk olmali.")]
        [SerializeField, Min(0.5f)] private float hideRadiusFactor = 0.78f;
        [SerializeField] private float hideVolumeHeight = 2.2f;

        [Header("Zamanlama")]
        [Tooltip("Acikken cimenler ancak odaya baglanildiginda uretilir. " +
                 "Kapaliysa sahne acilir acilmaz uretilir (eski davranis).")]
        [SerializeField] private bool waitForMatch = true;

        [Header("Zemin")]
        [SerializeField] private float raycastHeight = 25f;
        [SerializeField] private float fallbackGroundY;
        [SerializeField] private LayerMask groundMask = ~0;

        private bool _spawned;

        private void Start()
        {
            if (!waitForMatch)
                TrySpawn();
        }

        /// <summary>
        /// Cimenler menude degil, mac baslayinca uretilir.
        ///
        /// Eskiden Start() sahne acilir acilmaz calisiyordu: oyuncu daha mod bile
        /// secmeden menunun arkasinda yuzlerce cali olusuyordu. Ayrica cimen
        /// zemine isin atarak yerlesiyor - harita henuz aktif degilse hepsi
        /// yanlis yukseklige dusuyordu.
        /// </summary>
        private void Update()
        {
            if (_spawned || !waitForMatch)
                return;

            Fusion.NetworkRunner runner = FindFirstObjectByType<Fusion.NetworkRunner>();

            if (runner == null || !runner.IsRunning)
                return;

            TrySpawn();
        }

        private void TrySpawn()
        {
            if (_spawned)
                return;

            _spawned = true;

            if (patchMeshes == null || patchMeshes.Length == 0 || patchMaterial == null)
            {
                Debug.LogWarning(
                    "GrassPatchSpawner: mesh veya materyal atanmamis. " +
                    "Tools > NewBattle > Cimen Alanlarini Kur komutunu calistir.");
                return;
            }

            SpawnPatches();
        }

        private void SpawnPatches()
        {
            System.Random random = new(seed);
            Vector2[] placed = new Vector2[patchCount];
            int placedCount = 0;

            int attempts = 0;
            int maxAttempts = patchCount * 40;

            while (placedCount < patchCount && attempts < maxAttempts)
            {
                attempts++;

                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = areaRadius * Mathf.Sqrt((float)random.NextDouble());
                Vector2 point = areaCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                if (IsTooClose(placed, placedCount, point))
                    continue;

                placed[placedCount++] = point;
                CreatePatch(point, random);
            }

            if (placedCount < patchCount)
            {
                Debug.Log(
                    $"GrassPatchSpawner: {placedCount}/{patchCount} cali yerlestirildi. " +
                    "Daha fazlasi icin minSpacing degerini dusur veya areaRadius'u buyut.");
            }
        }

        private bool IsTooClose(Vector2[] placed, int count, Vector2 candidate)
        {
            for (int i = 0; i < count; i++)
            {
                if (Vector2.Distance(placed[i], candidate) < minSpacing)
                    return true;
            }

            return false;
        }

        private void CreatePatch(Vector2 point, System.Random random)
        {
            float groundY = SampleGroundHeight(point);
            float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());

            GameObject patch = new($"GrassPatch_{point.x:0}_{point.y:0}");
            patch.transform.SetParent(transform, false);
            patch.transform.position = new Vector3(point.x, groundY, point.y);
            patch.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            patch.transform.localScale = Vector3.one * scale;

            Mesh mesh = patchMeshes[random.Next(patchMeshes.Length)];

            MeshFilter filter = patch.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = patch.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = patchMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            AddHideVolume(patch.transform, mesh);
        }

        /// <summary>
        /// Gizlenme tetikleyicisi. PlayerPresence bunu "Bush" etiketiyle taniyor;
        /// etiket eksikse gizlenme sessizce hic calismaz, o yuzden uyari basiyoruz.
        /// </summary>
        private void AddHideVolume(Transform patch, Mesh mesh)
        {
            GameObject volume = new("HideVolume");
            volume.transform.SetParent(patch, false);

            try
            {
                volume.tag = "Bush";
            }
            catch (UnityException)
            {
                Debug.LogError(
                    "GrassPatchSpawner: 'Bush' tag'i projede tanimli degil. " +
                    "Tools > NewBattle > Art Generator penceresini bir kez acip calistirmak " +
                    "bu tag'i otomatik ekler.");
            }

            Bounds bounds = mesh.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * hideRadiusFactor;

            CapsuleCollider collider = volume.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.6f, radius);
            collider.height = hideVolumeHeight;
            collider.center = new Vector3(0f, hideVolumeHeight * 0.5f, 0f);
        }

        private float SampleGroundHeight(Vector2 point)
        {
            Vector3 origin = new(point.x, raycastHeight, point.y);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return fallbackGroundY;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(new Vector3(areaCenter.x, 0f, areaCenter.y), areaRadius);
        }
    }
}
