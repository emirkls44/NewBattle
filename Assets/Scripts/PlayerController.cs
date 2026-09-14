using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkTransform), typeof(PlayerStateMachine), typeof(PlayerShooting))]
public class PlayerController : NetworkBehaviour
{
    [Header("Silah Ayarlari")]
    public GameObject weaponInHand;
    public Animator anim;

    [Networked] public NetworkBool hasWeapon { get; set; }

    private PlayerStateMachine _stateMachine;
    private PlayerShooting _shooting;
    private ChangeDetector _changeDetector;
    private LobbyCountdownController _lobbyCountdown;
    private DropPhaseController _dropPhase;
    private CharacterController _characterController;
    private static readonly int HasWeaponHash = Animator.StringToHash("HasWeapon");

    [Networked] public NetworkBool HasSelectedDrop { get; private set; }
    [Networked] private NetworkBool DropApplied { get; set; }
    [Networked] private Vector3 SelectedDropPosition { get; set; }

    private void Awake()
    {
        _stateMachine = GetComponent<PlayerStateMachine>();
        _shooting = GetComponent<PlayerShooting>();
        _characterController = GetComponent<CharacterController>();

        if (weaponInHand != null)
            weaponInHand.SetActive(false);
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _lobbyCountdown = UnityEngine.Object.FindFirstObjectByType<LobbyCountdownController>();
        _dropPhase = UnityEngine.Object.FindFirstObjectByType<DropPhaseController>();

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

        if (!GetInput<NetworkInputData>(out var input))
            return;

        _stateMachine.ProcessInput(input);

        // Saldiri state'e bagli degildir: oyuncu dururken de kosarken de saldirabilir.
        if (input.RightJoystickVector.sqrMagnitude > 0.01f)
            _shooting.ProcessShooting(input.RightJoystickVector);
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
        float radius = _dropPhase != null ? _dropPhase.PlayableMapRadius : 29f;
        Vector2 horizontal = Vector2.ClampMagnitude(
            new Vector2(worldPosition.x, worldPosition.z),
            radius * 0.96f
        );

        SelectedDropPosition = new Vector3(horizontal.x, 0f, horizontal.y);
        HasSelectedDrop = true;
    }

    private void ApplyDropPosition()
    {
        float radius = _dropPhase != null ? _dropPhase.PlayableMapRadius : 29f;
        Vector3 targetPosition = SelectedDropPosition;

        if (!HasSelectedDrop)
        {
            Vector2 randomPoint = Random.insideUnitCircle * radius * 0.9f;
            targetPosition = new Vector3(randomPoint.x, 0f, randomPoint.y);
        }

        bool controllerWasEnabled = _characterController != null && _characterController.enabled;
        if (controllerWasEnabled)
            _characterController.enabled = false;

        transform.position = targetPosition;

        if (controllerWasEnabled)
            _characterController.enabled = true;

        DropApplied = true;
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
