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

    private NetworkRunner _runner;
    private bool _lootSpawned;

    // LootZoneManager kendi tablosu bos kaldiginda bu prefablari devralir.
    // Boylece ayni prefab referanslarini iki yerde elle doldurmak gerekmez.
    public NetworkPrefabRef RiflePickupPrefab => riflePickupPrefab;
    public NetworkPrefabRef HealthPickupPrefab => healthPickupPrefab;
    public NetworkPrefabRef ShieldPickupPrefab => shieldPickupPrefab;
    public NetworkPrefabRef AmmoPickupPrefab => ammoPickupPrefab;

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
