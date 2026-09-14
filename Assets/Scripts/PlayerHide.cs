using Fusion;
using UnityEngine;

public class PlayerHide : PlayerStateBase
{
    [Header("Ayarlar")]
    public float revealDistance = 3f;
    public GameObject footprintIcon;

    [Networked, OnChangedRender(nameof(OnHiddenStatusChanged))]
    public NetworkBool isHidden { get; set; }

    [Networked] private TickTimer proximityScanTimer { get; set; }

    private Vector3 lastPosition;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _propBlock;
    private Collider[] _enemyColliders = new Collider[5];
    private readonly int baseColorID = Shader.PropertyToID("_BaseColor");

    public override void InitState(PlayerStateMachine stateMachine)
    {
        base.InitState(stateMachine);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _propBlock = new MaterialPropertyBlock();
        if (footprintIcon != null) footprintIcon.SetActive(false);
    }

    public override void EnterState()
    {
        lastPosition = transform.position;
        isHidden = true;
    }

    public override void UpdateNetworkState(NetworkInputData input)
    {
        float speedSq = (transform.position - lastPosition).sqrMagnitude;
        bool isMoving = speedSq > 0.01f;

        if (proximityScanTimer.ExpiredOrNotRunning(Runner))
        {
            isHidden = !CheckEnemyProximity();
            proximityScanTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
        }

        if (footprintIcon != null && footprintIcon.activeSelf != isMoving)
        {
            footprintIcon.SetActive(isMoving);
        }

        if (input.JoystickInput.sqrMagnitude > 0.01f)
        {
            Vector3 moveDirection = new Vector3(input.JoystickInput.x, 0, input.JoystickInput.y).normalized;
            transform.position += moveDirection * 3.5f * Runner.DeltaTime;

            // MÝMARÝ DÜZELTME: Karakterin çalý içinde yürüdüðü yöne dönmesini saðlayan rotasyon eklendi
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Runner.DeltaTime * 15f);
        }

        lastPosition = transform.position;
    }

    public override void ExitState()
    {
        isHidden = false;
        if (footprintIcon != null) footprintIcon.SetActive(false);
    }

    bool CheckEnemyProximity()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, revealDistance, _enemyColliders);
        for (int i = 0; i < hitCount; i++)
        {
            if (_enemyColliders[i].CompareTag("Enemy") || _enemyColliders[i].CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    private void OnHiddenStatusChanged()
    {
        SetVisible(!isHidden);
    }

    void SetVisible(bool isVisible)
    {
        float targetAlpha = isVisible ? 1f : 0.3f;

        foreach (Renderer r in _renderers)
        {
            if (footprintIcon != null && r.gameObject == footprintIcon) continue;

            r.GetPropertyBlock(_propBlock);
            Color baseColor = r.sharedMaterial.HasProperty(baseColorID) ? r.sharedMaterial.GetColor(baseColorID) : Color.white;
            baseColor.a = targetAlpha;

            _propBlock.SetColor(baseColorID, baseColor);
            r.SetPropertyBlock(_propBlock);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;

        if (other.CompareTag("Bush"))
        {
            stateMachine.ChangeState(2);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!HasStateAuthority) return;

        if (other.CompareTag("Bush"))
        {
            // MÝMARÝ DÜZELTME: Çýkýþ anýnda joystick durumuna göre doðru State'e yönlendirme yap.
            if (GetInput<NetworkInputData>(out var input) && input.JoystickInput.sqrMagnitude > 0.01f)
            {
                stateMachine.ChangeState(1); // Move
            }
            else
            {
                stateMachine.ChangeState(0); // Idle
            }
        }
    }
}