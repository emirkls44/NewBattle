using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Haritaya elle belirlenmis noktalara loot birakir.
    ///
    /// Test icin: LootZoneManager'i kapatmadan, haritanin belirli bir
    /// yerinde tek bir silah isteniyorsa bu kullanilir. Loot prefablari
    /// ag nesnesi oldugu icin sahneye elle surukleyemiyoruz - host'un
    /// calisma aninda dogurmasi gerekiyor, bu bilesen de onu yapiyor.
    ///
    /// Prefab referanslarini sahnedeki LootSpawner'dan devraliyor; ayri
    /// bir yerde ikinci bir prefab listesi tutmak, biri degisince digerinin
    /// sessizce eskimesine yol acardi.
    /// </summary>
    public class TestLootPlacer : NetworkBehaviour
    {
        public enum LootKind
        {
            Silah,
            Can,
            Kalkan,
            Mermi
        }

        [System.Serializable]
        public struct Placement
        {
            [Tooltip("Ne birakilacak.")]
            public LootKind kind;

            [Tooltip("Haritadaki konum (X, Z). Harita merkezi 0,0.")]
            public Vector2 position;
        }

        [Tooltip("Birakilacak loot listesi. Bos birakilirsa merkeze bir silah konur.")]
        [SerializeField] private Placement[] placements;

        [Tooltip("Zeminin uzerinde ne kadar yukseklikte dursun.")]
        [SerializeField] private float heightAboveGround = 0.65f;

        [Tooltip("Zemin aranirken hangi katmanlar dikkate alinsin.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("Konsola ne birakildigini yazar.")]
        [SerializeField] private bool logPlacements = true;

        public override void Spawned()
        {
            // Sadece host doguruyor: her istemci kendi kopyasini dogursaydi
            // haritada oyuncu sayisi kadar ayni silah olurdu.
            if (!HasStateAuthority)
                return;

            LootSpawner source = FindFirstObjectByType<LootSpawner>();

            if (source == null)
            {
                Debug.LogWarning(
                    "TestLootPlacer: Sahnede LootSpawner yok, prefab referanslari alinamiyor.");
                return;
            }

            Placement[] list = placements != null && placements.Length > 0
                ? placements
                : new[] { new Placement { kind = LootKind.Silah, position = Vector2.zero } };

            foreach (Placement placement in list)
                Place(source, placement);
        }

        private void Place(LootSpawner source, Placement placement)
        {
            NetworkPrefabRef prefab = placement.kind switch
            {
                LootKind.Can => source.HealthPickupPrefab,
                LootKind.Kalkan => source.ShieldPickupPrefab,
                LootKind.Mermi => source.AmmoPickupPrefab,
                _ => source.RiflePickupPrefab
            };

            if (!prefab.IsValid)
            {
                Debug.LogWarning(
                    $"TestLootPlacer: {placement.kind} prefabi LootSpawner'da bos.");
                return;
            }

            // Zemini olcuyoruz: harita yuksekligi degisirse (ya da bolgeler
            // farkli seviyelerde olursa) sabit bir Y degeri loot'u havada
            // ya da zeminin altinda birakirdi.
            float groundY = GroundSampler.HeightAt(
                placement.position.x, placement.position.y, groundMask);

            Vector3 spawnPosition = new(
                placement.position.x,
                groundY + heightAboveGround,
                placement.position.y);

            Runner.Spawn(prefab, spawnPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            if (logPlacements)
                Debug.Log($"[TestLoot] {placement.kind} birakildi: {spawnPosition}");
        }
    }
}
