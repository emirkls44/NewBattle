using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject), typeof(Collider))]
public abstract class TimedLootPickup : NetworkBehaviour
{
    [Header("Yesil Halka")]
    [SerializeField, Min(0.05f)] private float collectionSeconds = 1f;
    [SerializeField, Min(0.2f)] private float collectionRadius = 1.25f;
    [SerializeField, Min(0.01f)] private float ringWidth = 0.055f;
    [SerializeField] private float ringWorldHeight = 0.04f;
    [Tooltip("Pickup'in merkezinden zemine olan mesafe. Loot havada durdugu icin " +
             "halkayi biraz asagi indirmek gerekir.")]
    [SerializeField] private float ringGroundOffset = -0.55f;
    [SerializeField] private Color progressColor = new Color(0.2f, 1f, 0.08f, 1f);

    [Tooltip("Beyaz halka ancak yerel oyuncu bu mesafedeyken cizilir. " +
             "Haritadaki tum loot'larin halkasi ayni anda gorunurse ekran " +
             "beyaz cemberlerden gorunmez hale gelir.")]
    [SerializeField, Min(0.5f)] private float ringVisibleDistance = 4.5f;
    [Networked, Capacity(32)] private NetworkDictionary<NetworkId, float> Progress => default;
    private static readonly HashSet<TimedLootPickup> Active = new();
    private readonly List<NetworkId> _remove = new();
    private readonly HashSet<NetworkId> _eligible = new();
    private LineRenderer _white, _green;
    private Material _material;
    private GameObject _rings;
    private LobbyCountdownController _lobby;
    private bool _consumed;
    protected abstract bool CanCollect(PlayerLoadout player);
    protected abstract void GiveTo(PlayerLoadout player);

    public override void Spawned()
    {
        _consumed = false;
        Active.Add(this);
        // Spawn'da bir kez: burada sahne taramasi kabul edilebilir.
        foreach (var lobby in FindObjectsByType<LobbyCountdownController>(FindObjectsSortMode.None))
            if (lobby.Runner == Runner) { _lobby = lobby; break; }
        _rings = new GameObject(name + "_LootRings");
        // Pickup'in altina bagli olsun: LineRenderer zaten world-space
        // calistigi icin konum degismez, ama nesne artik sahne kokunde
        // basibos durmaz ve pickup ile birlikte taranabilir.
        _rings.transform.SetParent(transform, true);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        _material = new Material(shader);
        _white = MakeRing("WhiteRing", new Color(0.8f, 0.85f, 0.8f));
        _green = MakeRing("GreenProgress", progressColor);
    }

    private LineRenderer MakeRing(string label, Color color)
    {
        var go = new GameObject(label);
        go.transform.SetParent(_rings.transform);
        var line = go.AddComponent<LineRenderer>();
        // Separate material colors also work with URP shaders which ignore vertex colors.
        line.material = new Material(_material);
        line.material.color = color;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = ringWidth;
        line.useWorldSpace = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private bool Eligible(PlayerLoadout player)
    {
        if (player == null || player.Object == null || !player.Object.IsValid || player.Runner != Runner) return false;
        var health = player.GetComponent<HealthController>();
        if (health == null || health.Object == null || !health.Object.IsValid || health.currentHealth <= 0f) return false;
        Vector3 delta = player.transform.position - transform.position;
        if (Mathf.Abs(delta.y) > 2.5f) return false;
        delta.y = 0;
        return delta.sqrMagnitude <= collectionRadius * collectionRadius && CanCollect(player);
    }

    private static TimedLootPickup Nearest(PlayerLoadout player)
    {
        TimedLootPickup best = null;
        float distance = float.PositiveInfinity;
        foreach (var pickup in Active)
        {
            if (pickup == null || pickup._consumed || pickup.Object == null || !pickup.Object.IsValid || !pickup.Eligible(player)) continue;
            var delta = player.transform.position - pickup.transform.position;
            delta.y = 0;
            float candidate = delta.sqrMagnitude;
            if (candidate < distance || (candidate == distance && best != null && pickup.Object.Id.Raw < best.Object.Id.Raw))
            { best = pickup; distance = candidate; }
        }
        return best;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _consumed) return;
        if (_lobby != null && (_lobby.Object == null || !_lobby.Object.IsValid || !_lobby.MatchStarted)) return;
        _eligible.Clear();
        PlayerLoadout winner = null;
        float winnerTime = float.NegativeInfinity;
        var registry = NewBattle.Gameplay.PlayerRegistry.All;
        for (int i = 0; i < registry.Count; i++)
        {
            PlayerLoadout player = registry[i] != null ? registry[i].Loadout : null;
            if (player == null) continue;
            if (!Eligible(player) || !player.HasStateAuthority || Nearest(player) != this) continue;
            NetworkId id = player.Object.Id;
            _eligible.Add(id);
            Progress.TryGet(id, out float elapsed);
            elapsed += Runner.DeltaTime;
            Progress.Set(id, elapsed);
            if (elapsed >= Mathf.Max(0.05f, collectionSeconds) &&
                (winner == null || elapsed > winnerTime || (elapsed == winnerTime && id.Raw < winner.Object.Id.Raw)))
            { winner = player; winnerTime = elapsed; }
        }
        _remove.Clear();
        foreach (var entry in Progress) if (!_eligible.Contains(entry.Key)) _remove.Add(entry.Key);
        foreach (var id in _remove) Progress.Remove(id);
        if (winner != null)
        {
            _consumed = true;
            GiveTo(winner);

            // Bolge yoneticisine "bu yuva alindi" de. Bolge bosaltilip tekrar
            // yuklendiginde bu esya geri gelmesin diye gerekli; despawn tek basina
            // yeterli degil cunku bosaltma da despawn ediyor.
            NewBattle.Gameplay.LootZoneManager.NotifyConsumed(Object);

            Runner.Despawn(Object);
        }
    }

    public override void Render()
    {
        if (_white == null || _green == null) return;
        float progress = 0;
        var registry = NewBattle.Gameplay.PlayerRegistry.All;
        for (int i = 0; i < registry.Count; i++)
        {
            PlayerLoadout player = registry[i] != null ? registry[i].Loadout : null;
            if (player == null || player.Object == null || !player.Object.IsValid || player.Runner != Runner || !player.HasInputAuthority) continue;
            if (Progress.TryGet(player.Object.Id, out float elapsed)) progress = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, collectionSeconds));
            break;
        }
        // Halkalar sadece yakindayken cizilir.
        //
        // Onceki surum her loot'un beyaz halkasini her zaman ciziyordu. Loot
        // yogunlugu artinca ekranda ayni anda 30+ beyaz cember oluyor ve harita
        // okunmaz hale geliyordu. Battlelands'te de halka ancak uzerine
        // yaklasinca beliriyor - halka bir "burada esya var" isareti degil,
        // "toplamaya baslayabilirsin" isareti.
        bool nearLocalPlayer = IsLocalPlayerNear();

        Draw(_white, nearLocalPlayer ? 1f : 0f, 0f);
        Draw(_green, progress, 0.008f);
    }

    /// <summary>Yerel oyuncu halkanin gorunecegi mesafede mi.</summary>
    private bool IsLocalPlayerNear()
    {
        Transform localPlayer = NewBattle.Gameplay.LocalPlayerContext.Transform;

        if (localPlayer == null)
            return false;

        Vector3 delta = localPlayer.position - transform.position;
        delta.y = 0f;

        return delta.sqrMagnitude <= ringVisibleDistance * ringVisibleDistance;
    }

    private void Draw(LineRenderer line, float fraction, float lift)
    {
        line.enabled = fraction > 0;
        if (!line.enabled) return;
        int steps = Mathf.Max(1, Mathf.CeilToInt(64 * fraction));
        line.positionCount = steps + 1;
        // Halka pickup'in KENDI yuksekligine gore cizilir. Onceki surum mutlak
        // dunya Y'si kullaniyordu (y = 0.04); duz prototip arenada dogruydu ama
        // yukseltili bir haritada tepedeki loot'un halkasi yerin metrelerce
        // altinda kaliyor ve hic gorunmuyordu.
        Vector3 center = new Vector3(
            transform.position.x,
            transform.position.y + ringGroundOffset + ringWorldHeight + lift,
            transform.position.z);
        for (int i = 0; i <= steps; i++)
        {
            float angle = i / (float)steps * fraction * Mathf.PI * 2;
            line.SetPosition(i, center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * collectionRadius);
        }
    }
    public override void Despawned(NetworkRunner runner, bool hasState) { Cleanup(); }
    private void OnDestroy() { Cleanup(); }
    private void Cleanup()
    {
        Active.Remove(this);
        if (_white != null) Destroy(_white.sharedMaterial);
        if (_green != null) Destroy(_green.sharedMaterial);
        if (_rings != null) Destroy(_rings);
        if (_material != null) Destroy(_material);
        _rings = null; _white = _green = null; _material = null;
    }
}
