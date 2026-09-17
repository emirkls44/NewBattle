using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Haritayi gorunmez bolgelere bolen, loot'u oyuncular yaklastikca ureten sistem.
    ///
    /// Neden tek seferde hepsini spawn etmiyoruz (eski LootSpawner'in yaptigi):
    /// 180 m'lik bir haritada yuzlerce NetworkObject mac basinda yaratilir, hepsi
    /// her istemciye replike edilir ve mac boyunca tick alir. Mobilde bu hem bellek
    /// hem bant genisligi israfi. Bolgesel yukleme ile ayni anda sadece oyuncularin
    /// cevresindeki loot canli kalir.
    ///
    /// Kurallar:
    ///   - Bir bolge ilk kez ziyaret edildiginde loot'u uretilir.
    ///   - Ikinci bir oyuncu ayni bolgeye gelirse TEKRAR uretilmez.
    ///   - Herkes uzaklasinca loot hemen silinmez; unloadDelaySeconds kadar beklenir.
    ///   - Toplanan loot bir daha cikmaz. Istisna: lootTable'da respawnSeconds > 0
    ///     verilen turler o sure sonunda tekrar cikabilir.
    ///   - Bolgenin loot dizilimi SABIT TOHUMLUDUR: bolge bosaltilip tekrar yuklense
    ///     bile kalan esyalar ayni yerdedir.
    ///
    /// Tum karar sunucuda (state authority) verilir; istemciler sadece spawn edilen
    /// NetworkObject'leri gorur.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class LootZoneManager : NetworkBehaviour
    {
        [System.Serializable]
        public struct LootEntry
        {
            public string label;
            public NetworkPrefabRef prefab;

            [Min(0f), Tooltip("Cekilis agirligi. Buyuk olan daha sik cikar.")]
            public float weight;

            [Min(0f), Tooltip("0 = mac boyunca bir kez. >0 = alindiktan bu kadar saniye sonra tekrar cikar.")]
            public float respawnSeconds;
        }

        // Varsayilanlar mevcut ~29 m yaricapli prototip arenaya gore ayarlandi.
        // Harita buyudugunde (ornegin 180 m) zoneSize'i ~26, activationRadius'u ~45
        // yapmak gerekir; yoksa bolge sayisi gereksiz yere patlar.
        [Header("Bolge Izgarasi")]
        [SerializeField, Min(5f)] private float zoneSize = 12f;
        [Tooltip("0 ise harita yaricapi DropPhaseController'dan okunur.")]
        [SerializeField, Min(0f)] private float mapRadiusOverride;
        [SerializeField, Min(1f)] private float activationRadius = 16f;
        [SerializeField, Min(0f)] private float unloadDelaySeconds = 20f;
        [SerializeField, Min(0.1f)] private float scanInterval = 0.5f;
        [SerializeField] private int seed = 7714;

        [Header("Bolge Basina Loot")]
        [Tooltip("Bolge basina min/max esya. 12 m'lik bolgede 1-2 makul; " +
                 "haritanin tamaminda ~25-45 loot eder (eski toplu spawn 33 idi).")]
        [SerializeField] private Vector2Int lootPerZone = new(1, 2);
        [SerializeField, Min(0.5f)] private float minSpacing = 3f;
        [SerializeField, Min(0.1f)] private float spawnHeight = 0.65f;
        [SerializeField, Min(1)] private int placementAttempts = 24;
        [SerializeField, Min(0.05f)] private float obstacleCheckRadius = 0.45f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;
        [SerializeField] private float groundRaycastHeight = 40f;

        [Header("Loot Tablosu")]
        [SerializeField] private LootEntry[] lootTable;

        [Header("Teshis")]
        [SerializeField] private bool logZoneEvents;
        [SerializeField] private bool drawZoneGizmos = true;

        private struct Slot
        {
            public Vector3 Position;
            public int EntryIndex;
            public bool Taken;
            public float RespawnAt;
        }

        private class Zone
        {
            public Vector2 Center;
            public bool Populated;
            public float LastOccupied = float.NegativeInfinity;
            public Slot[] Slots;
            public readonly List<NetworkObject> Spawned = new();
        }

        private Zone[] _zones;
        private int _columns;
        private float _mapRadius;
        private float _totalWeight;

        /// <summary>Canli loot -> hangi bolgenin hangi yuvasi. Toplanan esyayi isaretlemek icin.</summary>
        private readonly Dictionary<NetworkObject, (int zone, int slot)> _spawnedLookup = new();

        private readonly HashSet<int> _occupiedThisScan = new();
        private TickTimer _scanTimer;
        private DropPhaseController _dropPhase;

        private static LootZoneManager _instance;

        public override void Spawned()
        {
            _instance = this;
            _dropPhase = FindFirstObjectByType<DropPhaseController>();

            BuildGrid();
            InheritLootTableIfEmpty();
            CacheWeights();

            if (HasStateAuthority && _totalWeight <= 0f)
            {
                Debug.LogError(
                    "LootZoneManager: Kullanilabilir loot prefabi yok. Sahnedeki LootSpawner'in " +
                    "Rifle/Health/Shield/Ammo prefab alanlari dolu mu?");
            }
        }

        /// <summary>
        /// Tablo elle doldurulmadiysa sahnedeki LootSpawner'in prefablarini devral.
        /// Ayni referanslari iki bilesende ayri ayri tutmak, birini guncelleyip
        /// otekini unutmaya davetiye cikarir.
        /// </summary>
        private void InheritLootTableIfEmpty()
        {
            if (lootTable != null && lootTable.Length > 0)
                return;

            LootSpawner spawner = FindFirstObjectByType<LootSpawner>(FindObjectsInactive.Include);

            if (spawner == null)
                return;

            lootTable = new[]
            {
                NewEntry("Mermi", spawner.AmmoPickupPrefab, 3.0f),
                NewEntry("Saglik", spawner.HealthPickupPrefab, 2.2f),
                NewEntry("Kalkan", spawner.ShieldPickupPrefab, 2.0f),
                NewEntry("Silah", spawner.RiflePickupPrefab, 1.4f)
            };

            Debug.Log("LootZoneManager: Loot tablosu LootSpawner prefablarindan devralindi.");
        }

        private static LootEntry NewEntry(string label, NetworkPrefabRef prefab, float weight)
        {
            return new LootEntry
            {
                label = label,
                prefab = prefab,
                weight = weight,
                respawnSeconds = 0f
            };
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_instance == this)
                _instance = null;
        }

        #region Izgara

        private void BuildGrid()
        {
            _mapRadius = mapRadiusOverride > 0f
                ? mapRadiusOverride
                : _dropPhase != null ? _dropPhase.PlayableMapRadius : 29f;

            _columns = Mathf.Max(1, Mathf.CeilToInt(_mapRadius * 2f / zoneSize));
            _zones = new Zone[_columns * _columns];

            float origin = -_columns * zoneSize * 0.5f;

            for (int z = 0; z < _columns; z++)
            {
                for (int x = 0; x < _columns; x++)
                {
                    _zones[z * _columns + x] = new Zone
                    {
                        Center = new Vector2(
                            origin + (x + 0.5f) * zoneSize,
                            origin + (z + 0.5f) * zoneSize)
                    };
                }
            }
        }

        private void CacheWeights()
        {
            _totalWeight = 0f;

            if (lootTable == null)
                return;

            foreach (LootEntry entry in lootTable)
            {
                if (entry.prefab.IsValid)
                    _totalWeight += Mathf.Max(0f, entry.weight);
            }
        }

        #endregion

        #region Ana dongu

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _zones == null)
                return;

            // Inis fazi bitmeden loot uretme: oyuncular henuz haritada degil.
            if (_dropPhase != null && !_dropPhase.GameplayStarted)
                return;

            if (!_scanTimer.ExpiredOrNotRunning(Runner))
                return;

            _scanTimer = TickTimer.CreateFromSeconds(Runner, scanInterval);

            float now = Runner.SimulationTime;

            MarkOccupiedZones(now);
            ProcessZones(now);
        }

        private void MarkOccupiedZones(float now)
        {
            _occupiedThisScan.Clear();

            foreach (PlayerPresence presence in PlayerPresence.All)
            {
                if (presence == null || presence.Object == null || !presence.Object.IsValid)
                    continue;

                Vector3 position = presence.transform.position;
                MarkZonesAround(new Vector2(position.x, position.z), now);
            }
        }

        private void MarkZonesAround(Vector2 position, float now)
        {
            // Aktivasyon yaricapina degen butun hucreleri isaretle.
            int span = Mathf.CeilToInt(activationRadius / zoneSize);
            if (!TryGetCell(position, out int centerX, out int centerZ))
            {
                // Oyuncu izgaranin disinda: en yakin hucreyi yine de ac.
                centerX = Mathf.Clamp(Mathf.FloorToInt((position.x + _columns * zoneSize * 0.5f) / zoneSize), 0, _columns - 1);
                centerZ = Mathf.Clamp(Mathf.FloorToInt((position.y + _columns * zoneSize * 0.5f) / zoneSize), 0, _columns - 1);
            }

            for (int dz = -span; dz <= span; dz++)
            {
                for (int dx = -span; dx <= span; dx++)
                {
                    int x = centerX + dx;
                    int z = centerZ + dz;

                    if (x < 0 || z < 0 || x >= _columns || z >= _columns)
                        continue;

                    int index = z * _columns + x;

                    if (Vector2.Distance(_zones[index].Center, position) > activationRadius + zoneSize * 0.5f)
                        continue;

                    _occupiedThisScan.Add(index);
                    _zones[index].LastOccupied = now;
                }
            }
        }

        private void ProcessZones(float now)
        {
            for (int i = 0; i < _zones.Length; i++)
            {
                Zone zone = _zones[i];
                bool occupied = _occupiedThisScan.Contains(i);

                if (occupied)
                {
                    if (!zone.Populated)
                        Populate(i, zone, now);

                    continue;
                }

                if (zone.Populated && now - zone.LastOccupied >= unloadDelaySeconds)
                    Unload(i, zone);
            }
        }

        #endregion

        #region Yukleme / bosaltma

        private void Populate(int zoneIndex, Zone zone, float now)
        {
            zone.Slots ??= GenerateSlots(zoneIndex, zone);
            zone.Populated = true;

            int spawned = 0;

            for (int i = 0; i < zone.Slots.Length; i++)
            {
                Slot slot = zone.Slots[i];

                if (slot.Taken)
                {
                    // respawnSeconds = 0 ise RespawnAt sonsuzdur: bir daha hic cikmaz.
                    if (now < slot.RespawnAt)
                        continue;

                    slot.Taken = false;
                    zone.Slots[i] = slot;
                }

                if (!TrySpawnSlot(zoneIndex, i, slot, zone))
                    continue;

                spawned++;
            }

            if (logZoneEvents)
                Debug.Log($"[LootZone] {zoneIndex} yuklendi, {spawned} loot.");
        }

        private bool TrySpawnSlot(int zoneIndex, int slotIndex, Slot slot, Zone zone)
        {
            if (slot.EntryIndex < 0 || slot.EntryIndex >= lootTable.Length)
                return false;

            NetworkPrefabRef prefab = lootTable[slot.EntryIndex].prefab;
            if (!prefab.IsValid)
                return false;

            NetworkObject spawnedObject = Runner.Spawn(
                prefab,
                slot.Position,
                Quaternion.Euler(0f, slot.Position.x * 37f % 360f, 0f));

            if (spawnedObject == null)
                return false;

            zone.Spawned.Add(spawnedObject);
            _spawnedLookup[spawnedObject] = (zoneIndex, slotIndex);
            return true;
        }

        private void Unload(int zoneIndex, Zone zone)
        {
            foreach (NetworkObject spawned in zone.Spawned)
            {
                if (spawned == null || !spawned.IsValid)
                    continue;

                // Bosaltma "alindi" demek degil: yuva bos kalir, bolge tekrar
                // yuklendiginde ayni esya ayni yerde geri gelir.
                _spawnedLookup.Remove(spawned);
                Runner.Despawn(spawned);
            }

            zone.Spawned.Clear();
            zone.Populated = false;

            if (logZoneEvents)
                Debug.Log($"[LootZone] {zoneIndex} bosaltildi.");
        }

        #endregion

        #region Yuva uretimi

        /// <summary>
        /// Bolgenin loot dizilimini uretir. Tohum bolge indeksine bagli oldugu icin
        /// ayni bolge her zaman ayni dizilimi verir - bosaltilip tekrar yuklense bile.
        /// </summary>
        private Slot[] GenerateSlots(int zoneIndex, Zone zone)
        {
            System.Random random = new(seed ^ (zoneIndex * 73856093));

            int count = random.Next(lootPerZone.x, Mathf.Max(lootPerZone.x, lootPerZone.y) + 1);
            List<Slot> slots = new(count);

            for (int i = 0; i < count; i++)
            {
                if (!TryFindSlotPosition(zone, random, slots, out Vector3 position))
                    continue;

                slots.Add(new Slot
                {
                    Position = position,
                    EntryIndex = PickEntry(random),
                    Taken = false,
                    RespawnAt = float.PositiveInfinity
                });
            }

            return slots.ToArray();
        }

        private bool TryFindSlotPosition(Zone zone, System.Random random, List<Slot> placed, out Vector3 position)
        {
            float half = zoneSize * 0.5f;

            for (int attempt = 0; attempt < placementAttempts; attempt++)
            {
                float offsetX = ((float)random.NextDouble() - 0.5f) * 2f * half * 0.88f;
                float offsetZ = ((float)random.NextDouble() - 0.5f) * 2f * half * 0.88f;

                Vector2 point = zone.Center + new Vector2(offsetX, offsetZ);

                // Harita disina tasan hucrelerin kose kisimlarina loot koyma.
                if (point.magnitude > _mapRadius * 0.96f)
                    continue;

                if (IsTooCloseToPlaced(placed, point))
                    continue;

                float groundY = SampleGroundHeight(point);
                Vector3 candidate = new(point.x, groundY + spawnHeight, point.y);

                if (Physics.CheckSphere(candidate, obstacleCheckRadius, obstacleLayerMask,
                        QueryTriggerInteraction.Ignore))
                    continue;

                position = candidate;
                return true;
            }

            position = default;
            return false;
        }

        private bool IsTooCloseToPlaced(List<Slot> placed, Vector2 candidate)
        {
            foreach (Slot slot in placed)
            {
                Vector2 existing = new(slot.Position.x, slot.Position.z);
                if (Vector2.Distance(existing, candidate) < minSpacing)
                    return true;
            }

            return false;
        }

        private float SampleGroundHeight(Vector2 point)
        {
            Vector3 origin = new(point.x, groundRaycastHeight, point.y);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    groundRaycastHeight * 2f, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return 0f;
        }

        private int PickEntry(System.Random random)
        {
            if (_totalWeight <= 0f)
                return 0;

            float roll = (float)random.NextDouble() * _totalWeight;

            for (int i = 0; i < lootTable.Length; i++)
            {
                if (!lootTable[i].prefab.IsValid)
                    continue;

                roll -= Mathf.Max(0f, lootTable[i].weight);
                if (roll <= 0f)
                    return i;
            }

            return 0;
        }

        #endregion

        #region Toplanma bildirimi

        /// <summary>
        /// Bir loot toplandiginda TimedLootPickup burayi cagirir.
        /// Yuva "alindi" olarak isaretlenir; bolge tekrar yuklendiginde bu esya cikmaz
        /// (respawnSeconds tanimliysa o sure sonunda cikar).
        /// </summary>
        public static void NotifyConsumed(NetworkObject pickup)
        {
            if (_instance != null)
                _instance.HandleConsumed(pickup);
        }

        private void HandleConsumed(NetworkObject pickup)
        {
            if (pickup == null || !_spawnedLookup.TryGetValue(pickup, out (int zone, int slot) location))
                return;

            _spawnedLookup.Remove(pickup);

            Zone zone = _zones[location.zone];
            zone.Spawned.Remove(pickup);

            Slot slot = zone.Slots[location.slot];
            slot.Taken = true;

            float respawnSeconds = slot.EntryIndex >= 0 && slot.EntryIndex < lootTable.Length
                ? lootTable[slot.EntryIndex].respawnSeconds
                : 0f;

            slot.RespawnAt = respawnSeconds > 0f
                ? Runner.SimulationTime + respawnSeconds
                : float.PositiveInfinity;

            zone.Slots[location.slot] = slot;

            if (logZoneEvents)
                Debug.Log($"[LootZone] {location.zone}/{location.slot} toplandi.");
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            if (!drawZoneGizmos || _zones == null)
                return;

            foreach (Zone zone in _zones)
            {
                Gizmos.color = zone.Populated
                    ? new Color(0.2f, 1f, 0.3f, 0.5f)
                    : new Color(1f, 1f, 1f, 0.12f);

                Gizmos.DrawWireCube(
                    new Vector3(zone.Center.x, 0.5f, zone.Center.y),
                    new Vector3(zoneSize, 0.2f, zoneSize));
            }
        }

        private bool TryGetCell(Vector2 position, out int x, out int z)
        {
            float origin = -_columns * zoneSize * 0.5f;

            x = Mathf.FloorToInt((position.x - origin) / zoneSize);
            z = Mathf.FloorToInt((position.y - origin) / zoneSize);

            return x >= 0 && z >= 0 && x < _columns && z < _columns;
        }
    }
}
