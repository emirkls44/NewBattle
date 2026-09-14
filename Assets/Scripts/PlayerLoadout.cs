using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerLoadout : NetworkBehaviour
{
    [SerializeField, Min(1)] private int maxRifleAmmo = 120;
    public int MaxRifleAmmo => Mathf.Max(1, maxRifleAmmo);

    [Header("Gorsel")]
    [SerializeField] private GameObject rifleVisual;

    [Networked] public NetworkBool HasRifle { get; private set; }
    [Networked] public int RifleAmmo { get; private set; }
    [Networked] public int SelectedSlot { get; private set; }

    public bool IsRifleSelected => HasRifle && SelectedSlot == 1;

    private PlayerController _playerController;
    private Animator _animator;
    private static readonly int HasWeaponHash = Animator.StringToHash("HasWeapon");

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _animator = GetComponentInChildren<Animator>();

        if (rifleVisual != null)
            rifleVisual.SetActive(false);
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            HasRifle = false;
            RifleAmmo = 0;
            SelectedSlot = 0;
        }

        ApplyVisualState();
    }

    public override void Render()
    {
        ApplyVisualState();
    }

    public void SelectFist()
    {
        if (HasInputAuthority)
            Rpc_RequestSlot(0);
    }

    public void SelectRifle()
    {
        if (HasInputAuthority && HasRifle)
            Rpc_RequestSlot(1);
    }

    public void GrantRifle(int ammo)
    {
        if (!HasStateAuthority)
            return;

        HasRifle = true;
        RifleAmmo = Mathf.Min(MaxRifleAmmo, Mathf.Max(RifleAmmo, 0) + Mathf.Max(0, ammo));
        SelectedSlot = 1;
        ApplyVisualState();
    }

    public void AddRifleAmmo(int amount)
    {
        if (HasStateAuthority && HasRifle) RifleAmmo = Mathf.Min(MaxRifleAmmo, RifleAmmo + Mathf.Max(0, amount));
    }

    public bool TryConsumeRifleAmmo()
    {
        if (!HasStateAuthority || !IsRifleSelected || RifleAmmo <= 0)
            return false;

        RifleAmmo--;
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RequestSlot(int slot)
    {
        if (slot == 0)
            SelectedSlot = 0;
        else if (slot == 1 && HasRifle)
            SelectedSlot = 1;

        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        bool rifleSelected = IsRifleSelected;

        if (rifleVisual != null && rifleVisual.activeSelf != rifleSelected)
            rifleVisual.SetActive(rifleSelected);

        if (_animator != null)
            _animator.SetBool(HasWeaponHash, rifleSelected);

        if (HasStateAuthority && _playerController != null)
            _playerController.hasWeapon = rifleSelected;
    }
}
