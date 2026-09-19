using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkTransform), typeof(PlayerStateMachine), typeof(PlayerShooting))]
public class PlayerController : NetworkBehaviour
{
    [Header("Yercekimi")]
    [Tooltip("Egimli araziye oturmak icin uygulanan dusus ivmesi.")]
    [SerializeField] private float gravity = -22f;
    [Tooltip("Yerdeyken zemine yapisik kalmak icin uygulanan sabit bastirma.")]
    [SerializeField] private float groundedStick = -2f;

    [Header("Inis")]
    [Tooltip("Inis noktasi ararken zemin sayilan katmanlar.")]
    [SerializeField] private LayerMask dropGroundMask = ~0;

    [Header("Silah Ayarlari")]
    public GameObject weaponInHand;
    public Animator anim;

    [Networked] public NetworkBool hasWeapon { get; set; }

    private PlayerStateMachine _stateMachine;
    private PlayerShooting _shooting;
    private ChangeDetector _changeDetector;
    private LobbyCountdownController _lobbyCountdown;
    private DropPhaseController _dropPhase;
    private SafeZoneController _safeZone;
    private CharacterController _characterController;
    private NewBattle.Gameplay.ParachuteDescent _descent;
    private float _verticalVelocity;
    private static readonly int HasWeaponHash = Animator.StringToHash("HasWeapon");

    [Networked] public NetworkBool HasSelectedDrop { get; private set; }
    [Networked] private NetworkBool DropApplied { get; set; }
    [Networked] private Vector3 SelectedDropPosition { get; set; }

    private void Awake()
    {
        _stateMachine = GetComponent<PlayerStateMachine>();
        _shooting = GetComponent<PlayerShooting>();
        _characterController = GetComponent<CharacterController>();
        _descent = GetComponent<NewBattle.Gameplay.ParachuteDescent>();

        if (weaponInHand != null)
            weaponInHand.SetActive(false);
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _lobbyCountdown = UnityEngine.Object.FindFirstObjectByType<LobbyCountdownController>();
        _dropPhase = UnityEngine.Object.FindFirstObjectByType<DropPhaseController>();
        _safeZone = UnityEngine.Object.FindFirstObjectByType<SafeZoneController>();

        if (HasStateAuthority)
        {
            HasSelectedDrop = false;
            DropApplied = false;
        }

        if (!HasInputAuthority)
            return;

        LocalPlayerHUD localHud = UnityEngine.Object.FindFirstObjectByType<LocalPlayerHUD>(
            FindObjectsInactive.Include
        );
        HealthController healthController = GetComponent<HealthController>();

        if (localHud != null && healthController != null)
            localHud.BindLocalPlayer(healthController);

        CameraFollow cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<CameraFollow>()
            : null;

        if (cameraFollow != null)
            cameraFollow.SetTarget(transform);

        MinimapCameraFollow minimapCamera = UnityEngine.Object.FindFirstObjectByType<MinimapCameraFollow>();
        if (minimapCamera != null)
            minimapCamera.SetTarget(transform);
    }

    public override void FixedUpdateNetwork()
    {
        var health = GetComponent<HealthController>();
        if (health != null && health.currentHealth <= 0f) return;
        if (_lobbyCountdown != null && !_lobbyCountdown.MatchStarted)
            return;

        if (_dropPhase != null && !_dropPhase.GameplayStarted)
            return;

        if (HasStateAuthority && !DropApplied)
            ApplyDropPosition();

        // Inis surerken kontrol ParachuteDescent'te: oyuncu ne yurur ne ates eder,
        // sadece suzulme yonunu degistirebilir.
        if (_descent != null && _descent.IsDescending)
            return;

        // Yercekimi girdiden bagimsiz calisir: girdi paketi dusen bir oyuncu da,
        // hic hareket etmeyen bir oyuncu da arazinin egimine oturmali. Duz zeminli
        // prototip arenada fark etmiyordu, gercek yukseltili haritada sart.
        if (HasStateAuthority)
            ApplyGravity();

        if (!GetInput<NetworkInputData>(out var input))
            return;

        _stateMachine.ProcessInput(input);

        // Saldiri state'e bagli degildir: oyuncu dururken de kosarken de saldirabilir.
        if (input.RightJoystickVector.sqrMagnitude > 0.01f)
            _shooting.ProcessShooting(input.RightJoystickVector);
    }

    private void ApplyGravity()
    {
        if (_characterController == null || !_characterController.enabled)
            return;

        if (_characterController.isGrounded && _verticalVelocity < 0f)
        {
            // Sifir yerine kucuk bir negatif deger: isGrounded'in her tick
            // dogru donmesi icin karakterin zemine bastirilmasi gerekiyor.
            _verticalVelocity = groundedStick;
        }
        else
        {
            _verticalVelocity += gravity * Runner.DeltaTime;
        }

        _characterController.Move(Vector3.up * (_verticalVelocity * Runner.DeltaTime));
    }

    public void RequestDropPosition(Vector3 worldPosition)
    {
        if (!HasInputAuthority || _dropPhase == null || _dropPhase.GameplayStarted)
            return;

        RPC_RequestDropPosition(worldPosition);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDropPosition(Vector3 worldPosition)
    {
        SelectedDropPosition = ClampToSafeArea(worldPosition);
        HasSelectedDrop = true;
    }

    /// <summary>
    /// Secilen inis noktasini once haritanin, sonra GUVENLI ALANIN icine tasir.
    ///
    /// Oyuncu ilk cemberin disina dokunabilir - bu bir hata degil, harita
    /// tamamen gorunuyor ve orasi da haritanin bir parcasi. Ama oraya inmek
    /// demek, ayaklari yere deger degmez can kaybetmeye baslamak demek. O yuzden
    /// secimi reddetmek yerine cemberin icindeki EN YAKIN noktaya kaydiriyoruz:
    /// oyuncu nereye inmek istedigini soylemis oluyor, oyun da onu cezalandirmadan
    /// oraya en yakin gecerli yere birakiyor.
    /// </summary>
    private Vector3 ClampToSafeArea(Vector3 worldPosition)
    {
        float mapExtent = _dropPhase != null ? _dropPhase.MapExtent : 78f;

        Vector2 point = new(worldPosition.x, worldPosition.z);

        // Harita kare: kirpma da kare olmali, yoksa koseler secilemez.
        point = _dropPhase != null
            ? _dropPhase.ClampInside(point, mapExtent * 0.04f)
            : point;

        SafeZoneController safeZone = _safeZone != null
            ? _safeZone
            : _safeZone = UnityEngine.Object.FindFirstObjectByType<SafeZoneController>();

        if (safeZone != null && safeZone.Object != null && safeZone.Object.IsValid &&
            safeZone.SafeRadius > 0f)
        {
            Vector2 center = new(safeZone.SafeCenter.x, safeZone.SafeCenter.z);
            Vector2 fromCenter = point - center;

            // Kenara yapismasin: cemberin hemen icine, biraz pay birakarak koy.
            float allowed = safeZone.SafeRadius * 0.92f;

            if (fromCenter.magnitude > allowed)
                point = center + fromCenter.normalized * allowed;
        }

        // SON VE EN ONEMLI ADIM: secilen yerin altinda gercekten zemin var mi.
        //
        // Yukaridaki iki kirpma noktayi haritanin ve alanin "matematiksel"
        // icine tasiyor, ama harita dairesel degil - cemberin icinde kalan bir
        // nokta yine de arazinin bittigi bir bosluga denk gelebilir. Oraya
        // birakilan oyuncu yercekimiyle sonsuza kadar duser.
        //
        // GroundSampler bulamazsa cevreyi tarayip EN YAKIN gecerli zemine tasir.
        Vector3 candidate = new(point.x, 0f, point.y);

        if (NewBattle.Gameplay.GroundSampler.TryFindGround(
                candidate, mapExtent, dropGroundMask, out Vector3 ground))
        {
            return ground;
        }

        // Hicbir yerde zemin yoksa (harita henuz aktif degil) alanin merkezine birak.
        return safeZone != null ? safeZone.SafeCenter : Vector3.zero;
    }

    private void ApplyDropPosition()
    {
        Vector3 targetPosition = SelectedDropPosition;

        if (!HasSelectedDrop)
        {
            // Yer secmeden oyuna giren oyuncu haritanin HER yerine dusebilmeli.
            // Daire icinde rastgele secmek koseleri hic kullanmamak demekti.
            Vector2 randomPoint = _dropPhase != null
                ? _dropPhase.RandomPointInside(_dropPhase.MapExtent * 0.1f)
                : Random.insideUnitCircle * 26f;

            targetPosition = ClampToSafeArea(new Vector3(randomPoint.x, 0f, randomPoint.y));
        }

        DropApplied = true;

        // Parasut bileseni varsa oyuncu haritaya suzulerek iner; yoksa eski
        // davranisa (anlik yerlestirme) duseriz, boylece prefab guncellenmemis
        // bir projede oyun yine de calisir.
        if (_descent != null)
        {
            _descent.BeginDescent(targetPosition);
            return;
        }

        bool controllerWasEnabled = _characterController != null && _characterController.enabled;
        if (controllerWasEnabled)
            _characterController.enabled = false;

        transform.position = targetPosition;

        if (controllerWasEnabled)
            _characterController.enabled = true;
    }

    public override void Render()
    {
        if (_changeDetector == null)
            return;

        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change != nameof(hasWeapon))
                continue;

            if (weaponInHand != null)
                weaponInHand.SetActive(hasWeapon);

            if (anim != null)
                anim.SetBool(HasWeaponHash, hasWeapon);
        }
    }
}
