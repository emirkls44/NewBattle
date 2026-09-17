using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Guvenli alan ilk kez daraldiginda haritaya airdrop sandigi birakir.
    ///
    /// Dusus noktasi, o anki guvenli cemberin ICINDEN rastgele secilir. Bu onemli:
    /// alan disina dusen bir sandik oyuncuyu hasar alanina cagirir ve odul yerine
    /// ceza olur. Ayrica sandigin altinda zemin oldugundan emin olmak icin isin
    /// atiyoruz; suya veya bosluga dusen sandik toplanamaz.
    ///
    /// Birden fazla dusus istenirse dropsPerMatch artirilabilir; her biri bir sonraki
    /// daralmada birakilir.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class AirdropController : NetworkBehaviour
    {
        [Header("Zamanlama")]
        [Tooltip("Ilk dusus icin beklenen daralma numarasi. 1 = ilk daralma.")]
        [SerializeField, Min(1)] private int firstDropAfterStage = 1;
        [SerializeField, Min(1)] private int dropsPerMatch = 1;
        [SerializeField, Min(0f)] private float dropDelaySeconds = 1.5f;

        [Header("Dusus")]
        [Tooltip("Airdrop sandigi prefabi (NetworkObject tasimali).")]
        [SerializeField] private NetworkObject cratePrefab;
        [SerializeField, Min(5f)] private float dropAltitude = 35f;
        [SerializeField, Range(0.1f, 0.95f)] private float safeAreaUsage = 0.75f;
        [SerializeField, Min(1)] private int placementAttempts = 20;
        [SerializeField, Min(0.1f)] private float clearanceRadius = 1.2f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Teshis")]
        [SerializeField] private bool logDrops = true;

        [Networked] private int DropsReleased { get; set; }
        [Networked] private int LastTriggeredStage { get; set; }
        [Networked] private TickTimer PendingDropTimer { get; set; }
        [Networked] private NetworkBool DropPending { get; set; }

        private SafeZoneController _safeZone;

        public override void Spawned()
        {
            _safeZone = FindFirstObjectByType<SafeZoneController>();

            if (HasStateAuthority)
            {
                DropsReleased = 0;
                LastTriggeredStage = 0;
                DropPending = false;
            }

            if (HasStateAuthority && cratePrefab == null)
            {
                Debug.LogError(
                    "AirdropController: Sandik prefabi atanmamis. " +
                    "Tools > NewBattle > Faz 3-5 Kurulumu komutunu calistir.");
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _safeZone == null || cratePrefab == null)
                return;

            if (DropsReleased >= dropsPerMatch)
                return;

            if (DropPending)
            {
                if (PendingDropTimer.ExpiredOrNotRunning(Runner))
                    ReleaseDrop();

                return;
            }

            // SafeZoneController'in StageNumber'i 1'den baslar ve her daralma
            // tamamlandiginda artar. firstDropAfterStage daralmasi bittiginde tetikleniyoruz.
            int stage = _safeZone.StageNumber;
            int requiredStage = firstDropAfterStage + DropsReleased;

            if (stage <= requiredStage || stage == LastTriggeredStage)
                return;

            LastTriggeredStage = stage;
            DropPending = true;
            PendingDropTimer = TickTimer.CreateFromSeconds(Runner, dropDelaySeconds);
        }

        private void ReleaseDrop()
        {
            DropPending = false;

            if (!TryFindDropPoint(out Vector3 groundPoint))
            {
                if (logDrops)
                    Debug.LogWarning("AirdropController: Uygun dusus noktasi bulunamadi, atlandi.");

                DropsReleased++;
                return;
            }

            Vector3 spawnPosition = groundPoint + Vector3.up * dropAltitude;

            NetworkObject crate = Runner.Spawn(cratePrefab, spawnPosition, Quaternion.identity);

            if (crate != null && crate.TryGetComponent(out AirdropCratePickup pickup))
                pickup.ConfigureDrop(groundPoint.y);

            DropsReleased++;

            if (logDrops)
                Debug.Log($"[Airdrop] Sandik birakildi: {groundPoint}");
        }

        /// <summary>
        /// Guvenli cemberin icinde, zemini saglam ve uzerinde engel olmayan bir nokta bul.
        /// </summary>
        private bool TryFindDropPoint(out Vector3 groundPoint)
        {
            Vector3 center = _safeZone.SafeCenter;
            float radius = _safeZone.SafeRadius * safeAreaUsage;

            for (int attempt = 0; attempt < placementAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 origin = new(center.x + offset.x, center.y + dropAltitude + 20f, center.z + offset.y);

                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                        dropAltitude + 100f, groundMask, QueryTriggerInteraction.Ignore))
                    continue;

                // Sandigin oturacagi yerde bina/agac olmasin.
                // Kureyi zeminden yukari kaldiriyoruz; aksi halde zeminin kendisi
                // engel sayilir ve hicbir aday gecmez.
                Vector3 clearanceCenter = hit.point + Vector3.up * (clearanceRadius + 0.6f);

                if (Physics.CheckSphere(clearanceCenter, clearanceRadius,
                        groundMask, QueryTriggerInteraction.Ignore))
                    continue;

                groundPoint = hit.point;
                return true;
            }

            // Hicbir aday tutmadiysa cemberin merkezine birak: hic dusmemekten iyidir.
            //
            // Merkezde de zemin olmayabilir (alan haritanin kenarina kaymis
            // olabilir), o yuzden dogrudan isin atmak yerine GroundSampler
            // kullaniyoruz: bulamazsa cevreyi tarayip en yakin zemini buluyor.
            // Aksi halde sandik y = 0'a, yani muhtemelen yerin altina duserdi.
            return GroundSampler.TryFindGround(
                new Vector3(center.x, 0f, center.z), radius, groundMask, out groundPoint);
        }
    }
}
