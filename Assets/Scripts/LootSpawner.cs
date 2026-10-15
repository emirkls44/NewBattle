using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

[RequireComponent(typeof(NetworkRunner))]
public class LootSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network Prefablari")]
    [SerializeField] private NetworkPrefabRef riflePickupPrefab;
    [SerializeField] private NetworkPrefabRef healthPickupPrefab;
    [SerializeField] private NetworkPrefabRef shieldPickupPrefab;

    [SerializeField] private NetworkPrefabRef ammoPickupPrefab;
    [SerializeField, Range(0, 32)] private int ammoCount = 8;

    [Header("Kayisi (atilabilir iyilestirme)")]
    [SerializeField] private NetworkPrefabRef apricotPickupPrefab;
    [SerializeField, Range(0, 32)] private int apricotCount = 6;
    [Tooltip("Bolgesel loot tablosunda kayisinin agirligi (mermi 3, can 2.2, kalkan 2, silah 1.4).")]
    [SerializeField, Min(0f)] private float apricotWeight = 0.8f;

    [Header("Findik (atilabilir yavaslatma)")]
    [SerializeField] private NetworkPrefabRef hazelnutPickupPrefab;
    [SerializeField, Range(0, 32)] private int hazelnutCount = 6;
    [Tooltip("Bolgesel loot tablosunda findigin agirligi.")]
    [SerializeField, Min(0f)] private float hazelnutWeight = 0.7f;

    [Header("Adetler")]
    [SerializeField, Range(0, 32)] private int rifleCount = 8;
    [SerializeField, Range(0, 32)] private int healthCount = 10;
    [SerializeField, Range(0, 32)] private int shieldCount = 8;

    [Header("Dagitim Alani")]
    [SerializeField] private Vector2 arenaCenter = Vector2.zero;
    [Tooltip("Merkezden kenara mesafe. Harita kare oldugu icin loot da kareye " +
             "dagitiliyor; daire kullanmak dort koseyi bos birakirdi.")]
    [SerializeField, Min(1f)] private float spawnExtent = 74f;
    [SerializeField, Min(0.1f)] private float spawnHeight = 0.65f;
    [SerializeField, Min(1)] private int placementAttempts = 30;
    [SerializeField, Min(0.1f)] private float obstacleCheckRadius = 0.45f;
    [SerializeField] private LayerMask obstacleLayerMask = ~0;

    [Header("Olum Lootu")]
    [Tooltip("Olen oyuncu (bot dahil) elindeki silahi (kalan mermisiyle) ve can / kalkan paketi birakir.")]
    [SerializeField] private bool dropOnDeath = true;
    [SerializeField] private bool dropWeapon = true;
    [SerializeField] private bool dropHealth = true;
    [SerializeField] private bool dropShield = true;
    [Tooltip("Esyalar oldugu yerin etrafina bu uzaklikta saçilir.")]
    [SerializeField, Min(0.3f)] private float dropRadius = 1.4f;

    private NetworkRunner _runner;
    private bool _lootSpawned;

    // LootZoneManager kendi tablosu bos kaldiginda bu prefablari devralir.
    // Boylece ayni prefab referanslarini iki yerde elle doldurmak gerekmez.
    public NetworkPrefabRef RiflePickupPrefab => riflePickupPrefab;
    public NetworkPrefabRef HealthPickupPrefab => healthPickupPrefab;
    public NetworkPrefabRef ShieldPickupPrefab => shieldPickupPrefab;
    public NetworkPrefabRef AmmoPickupPrefab => ammoPickupPrefab;
    public NetworkPrefabRef ApricotPickupPrefab => apricotPickupPrefab;
    public float ApricotWeight => apricotWeight;
    public NetworkPrefabRef HazelnutPickupPrefab => hazelnutPickupPrefab;
    public float HazelnutWeight => hazelnutWeight;

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
        _runner.AddCallbacks(this);
    }

    private void OnDestroy()
    {
        if (_runner != null)
            _runner.RemoveCallbacks(this);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer || _lootSpawned)
            return;

        // Bolgesel loot sistemi devredeyse toplu spawn yapma: ikisi birden calisirsa
        // harita iki kat loot ile dolar ve bolgesel yuklemenin anlami kalmaz.
        if (UnityEngine.Object.FindFirstObjectByType<NewBattle.Gameplay.LootZoneManager>(
                FindObjectsInactive.Include) != null)
        {
            _lootSpawned = true;
            Debug.Log("[Loot] LootZoneManager bulundu; toplu spawn devre disi.");
            return;
        }

        if (!AllPrefabsValid())
        {
            Debug.LogError("LootSpawner: Rifle, Health ve Shield prefab alanlarini doldur.");
            return;
        }

        _lootSpawned = true;
        SpawnGroup(runner, riflePickupPrefab, rifleCount);
        SpawnGroup(runner, healthPickupPrefab, healthCount);
        SpawnGroup(runner, shieldPickupPrefab, shieldCount);
        if (ammoPickupPrefab.IsValid) SpawnGroup(runner, ammoPickupPrefab, ammoCount);
        if (apricotPickupPrefab.IsValid) SpawnGroup(runner, apricotPickupPrefab, apricotCount);
        if (hazelnutPickupPrefab.IsValid) SpawnGroup(runner, hazelnutPickupPrefab, hazelnutCount);

        Debug.Log($"[Loot] {rifleCount + healthCount + shieldCount} pickup olusturuldu.");
    }

    private bool AllPrefabsValid()
    {
        return riflePickupPrefab.IsValid
            && healthPickupPrefab.IsValid
            && shieldPickupPrefab.IsValid;
    }

    private void SpawnGroup(NetworkRunner runner, NetworkPrefabRef prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryFindSpawnPosition(out Vector3 position))
            {
                Debug.LogWarning($"LootSpawner: {i + 1}. pickup icin bos yer bulunamadi.");
                continue;
            }

            float angle = UnityEngine.Random.Range(0f, 360f);
            runner.Spawn(prefab, position, Quaternion.Euler(0f, angle, 0f));
        }
    }

    /// <summary>
    /// Host: oyuncu oldu. Silahi (yumruk degilse, kalan mermi ve nadirligiyle) ve
    /// can / kalkan paketi oldugu yerin etrafina saçilir; herkes alabilir.
    /// </summary>
    public void DropDeathLoot(Vector3 deathPosition, PlayerLoadout loadout, bool supplies = true)
    {
        if (!dropOnDeath || _runner == null || !_runner.IsServer)
            return;

        var drops = new List<(NetworkPrefabRef prefab, NetworkRunner.OnBeforeSpawned setup)>();

        if (dropWeapon && loadout != null && loadout.HasRangedWeapon && loadout.Ammo > 0 && riflePickupPrefab.IsValid)
        {
            int weaponId = loadout.WeaponId, rarity = loadout.Rarity, ammo = loadout.Ammo;
            drops.Add((riflePickupPrefab, (runner, obj) =>
            {
                if (obj.TryGetComponent(out RiflePickup pickup))
                    pickup.Configure(weaponId, rarity, ammo);
            }));
        }

        // supplies=false: takim modunda yere dusunce sadece silah birakilir.
        if (supplies && dropHealth && healthPickupPrefab.IsValid)
            drops.Add((healthPickupPrefab, null));
        if (supplies && dropShield && shieldPickupPrefab.IsValid)
            drops.Add((shieldPickupPrefab, null));

        if (drops.Count == 0)
            return;

        float baseAngle = UnityEngine.Random.Range(0f, 360f);
        float step = 360f / drops.Count;
        for (int i = 0; i < drops.Count; i++)
        {
            Vector3 position = FindDropPosition(deathPosition, baseAngle + i * step);
            float yaw = UnityEngine.Random.Range(0f, 360f);
            _runner.Spawn(drops[i].prefab, position, Quaternion.Euler(0f, yaw, 0f), null, drops[i].setup);
        }
    }

    /// <summary>Olum yerinin etrafinda engelsiz bir nokta (yoksa olum yeri).</summary>
    private Vector3 FindDropPosition(Vector3 center, float angle)
    {
        float y = center.y + spawnHeight;
        float[] radii = { dropRadius, dropRadius * 0.6f, dropRadius * 1.4f };
        foreach (float radius in radii)
        {
            for (int turn = 0; turn < 4; turn++)
            {
                float a = (angle + turn * 25f) * Mathf.Deg2Rad;
                Vector3 candidate = new(center.x + Mathf.Cos(a) * radius, y, center.z + Mathf.Sin(a) * radius);
                if (!Physics.CheckSphere(candidate, obstacleCheckRadius, obstacleLayerMask, QueryTriggerInteraction.Ignore))
                    return candidate;
            }
        }

        return new Vector3(center.x, y, center.z);
    }

    private bool TryFindSpawnPosition(out Vector3 position)
    {
        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            Vector2 randomPoint = new(
                UnityEngine.Random.Range(-spawnExtent, spawnExtent),
                UnityEngine.Random.Range(-spawnExtent, spawnExtent));
            Vector3 candidate = new(
                arenaCenter.x + randomPoint.x,
                spawnHeight,
                arenaCenter.y + randomPoint.y
            );

            bool blocked = Physics.CheckSphere(
                candidate,
                obstacleCheckRadius,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            if (blocked)
                continue;

            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
