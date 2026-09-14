using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkRunner))]
public class TrainingBotSpawner : MonoBehaviour
{
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private float spawnRadius = 12f;
    private NetworkRunner _runner;
    private NetworkBootstrap _bootstrap;
    private bool _reportedError;
    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
        _bootstrap = GetComponent<NetworkBootstrap>();
    }

    public bool FillToCapacity(int capacity, out int bots, out int total)
    {
        bots = total = 0;
        if (_runner == null || !_runner.IsRunning || !_runner.IsServer || _bootstrap == null) return false;
        foreach (var stats in UnityEngine.Object.FindObjectsByType<PlayerCombatStats>(FindObjectsSortMode.None))
        {
            if (stats.Object == null || !stats.Object.IsValid || stats.Runner != _runner) continue;
            total++;
            if (stats.Object.InputAuthority == PlayerRef.None) bots++;
        }
        var prefab = playerPrefab.IsValid ? playerPrefab : _bootstrap.PlayerPrefab;
        if (!prefab.IsValid)
        {
            if (!_reportedError) Debug.LogError("TrainingBotSpawner: Player Prefab eksik; mac baslatilmadi.");
            _reportedError = true;
            return false;
        }
        while (total < capacity)
        {
            float angle = total * 137.5f * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
            var bot = _runner.Spawn(prefab, position, Quaternion.identity, PlayerRef.None);
            if (bot == null) return false;
            var stats = bot.GetComponent<PlayerCombatStats>();
            if (stats == null)
            {
                _runner.Despawn(bot);
                if (!_reportedError) Debug.LogError("PlayerOnline prefabinda PlayerCombatStats eksik.");
                _reportedError = true;
                return false;
            }
            stats.AssignTeam(_bootstrap.FindAvailableTeam(bot));
            bots++;
            total++;
        }
        return true;
    }
}
