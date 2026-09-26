using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject), typeof(Collider))]
public abstract class TimedLootPickup : NetworkBehaviour
{
    [Header("Yesil Halka")]
    [SerializeField, Min(0.05f)] private float collectionSeconds = 1f;
    [SerializeField, Min(0.2f)] private float collectionRadius = 1.25f;
    [SerializeField] private float ringWorldHeight = 0.04f;
    [Tooltip("Pickup'in merkezinden zemine olan mesafe. Loot havada durdugu icin " +
             "halkayi biraz asagi indirmek gerekir.")]
    [SerializeField] private float ringGroundOffset = -0.55f;
    [SerializeField] private Color progressColor = new Color(0.2f, 1f, 0.08f, 1f);
    [Tooltip("Toplama alanini gosteren halkanin rengi.")]
    [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.8f);

    [Tooltip("Beyaz halka ancak yerel oyuncu bu mesafedeyken cizilir. " +
             "Haritadaki tum loot'larin halkasi ayni anda gorunurse ekran " +
             "beyaz cemberlerden gorunmez hale gelir.")]
    [SerializeField, Min(0.5f)] private float ringVisibleDistance = 4.5f;

    [Header("Parilti")]
    [Tooltip("Esyanin altindaki yumusak leke. Esyanin turunu uzaktan belli eder: " +
             "kirmizi can, mavi kalkan, sari mermi. Alfa 0 kapatir.")]
    [SerializeField] private Color glowColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField, Min(0.1f)] private float glowRadius = 0.85f;

    [Networked, Capacity(32)] private NetworkDictionary<NetworkId, float> Progress => default;
    private static readonly HashSet<TimedLootPickup> Active = new();
    private readonly List<NetworkId> _remove = new();
    private readonly HashSet<NetworkId> _eligible = new();
    private MeshRenderer _track, _fill, _glow;
    private MaterialPropertyBlock _block;
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
        // Pickup'in altina bagli olsun: nesne sahne kokunde basibos durmaz ve
        // pickup ile birlikte yok olur. Konum ve donus her karede dunyaya gore
        // yeniden yaziliyor (bkz. Render).
        _rings.transform.SetParent(transform, false);

        // Halka ve parilti paylasilan overlay materyaliyle ciziliyor. Eskiden her
        // loot iki LineRenderer ve iki materyal kopyasi uretiyordu; 60 loot'lu
        // bir haritada bu 120 materyal demekti.
        _block = new MaterialPropertyBlock();
        _glow = CreateOverlay("LootGlow", NewBattle.Gameplay.OverlayMeshLibrary.SoftDisc, 0f, glowRadius);
        _track = CreateOverlay("LootRing", NewBattle.Gameplay.OverlayMeshLibrary.SoftRing, 0.004f, collectionRadius);
        _fill = CreateOverlay("LootProgress", NewBattle.Gameplay.OverlayMeshLibrary.SoftRing, 0.008f, collectionRadius);
    }

    private MeshRenderer CreateOverlay(string label, Mesh mesh, float lift, float radius)
    {
        MeshRenderer overlay = NewBattle.Gameplay.OverlayMeshLibrary.CreateOverlayObject(
            label, mesh, _rings.transform, new Vector3(0f, lift, 0f), radius);

        // Airdrop sandigi gibi havadan gelen loot'ta ilk Render'a kadar
        // gorunmesin; halka yere inmeden anlamsiz.
        overlay.enabled = false;
        return overlay;
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
        if (_rings == null || _track == null || _fill == null || _glow == null) return;
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

        // Halka pickup'in KENDI yuksekligine gore cizilir. Onceki surum mutlak
        // dunya Y'si kullaniyordu (y = 0.04); duz prototip arenada dogruydu ama
        // yukseltili bir haritada tepedeki loot'un halkasi yerin metrelerce
        // altinda kaliyor ve hic gorunmuyordu.
        //
        // Donus ve olcek de dunyaya sabitlenir: pickup olcekli ya da egik olsa
        // bile halka yere duz yatan, dogru yaricapli bir daire kalir.
        Transform rings = _rings.transform;
        Vector3 position = transform.position;
        rings.SetPositionAndRotation(
            new Vector3(position.x, position.y + ringGroundOffset + ringWorldHeight, position.z),
            Quaternion.identity);
        Vector3 parentScale = transform.lossyScale;
        rings.localScale = new Vector3(
            1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));

        SetOverlay(_glow, glowColor, glowColor.a > 0.001f ? 1f : 0f);
        SetOverlay(_track, ringColor, nearLocalPlayer ? 1f : 0f);
        SetOverlay(_fill, progressColor, progress);
    }

    /// <summary>fill 0 ise gizler; aksi halde radyal dolumu ve rengi yazar.</summary>
    private void SetOverlay(MeshRenderer overlay, Color color, float fill)
    {
        bool visible = fill > 0f;

        if (overlay.enabled != visible)
            overlay.enabled = visible;

        if (!visible)
            return;

        _block.SetColor(NewBattle.Gameplay.OverlayMeshLibrary.BaseColorId, color);
        _block.SetFloat(NewBattle.Gameplay.OverlayMeshLibrary.FillId, fill);
        overlay.SetPropertyBlock(_block);
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

    public override void Despawned(NetworkRunner runner, bool hasState) { Cleanup(); }
    private void OnDestroy() { Cleanup(); }
    private void Cleanup()
    {
        Active.Remove(this);
        // Materyal paylasilan overlay materyali; burada yok edilmez.
        if (_rings != null) Destroy(_rings);
        _rings = null; _track = _fill = _glow = null;
    }
}
