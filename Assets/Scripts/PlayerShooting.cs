using Fusion;
using UnityEngine;

public class PlayerShooting : NetworkBehaviour
{
    [Header("Bileþenler")]
    public Transform firePoint;
    public float meleeRange = 1.5f;
    public LayerMask meleeHitLayerMask;

    private PlayerController _playerController;
    private Animator _animator;
    private Collider[] _hitColliders = new Collider[10];
    private IWeapon _activeWeapon;

    private readonly int punchHash = Animator.StringToHash("Punch");
    private readonly int hasWeaponHash = Animator.StringToHash("HasWeapon");

    public override void Spawned()
    {
        TryGetComponent(out _playerController);
        _animator = GetComponentInChildren<Animator>();
        _activeWeapon = GetComponentInChildren<IWeapon>();
    }

    // MÝMARÝ MÜDAHALE: Update döngüsü çýkarýldý. Sadece State'ler bu metodu çaðýrabilir.
    public void ProcessShooting(Vector2 aimInput)
    {
        if (_playerController != null && _playerController.hasWeapon && _activeWeapon != null)
        {
            _activeWeapon.Shoot(firePoint.position, aimInput);
        }
        else if (_playerController != null && !_playerController.hasWeapon)
        {
            ExecuteMelee();
        }
    }

    private void ExecuteMelee()
    {
        if (_animator != null) _animator.SetTrigger(punchHash);

        Vector3 hitPoint = transform.position + transform.forward * 1f;
        int hitCount = Physics.OverlapSphereNonAlloc(hitPoint, meleeRange, _hitColliders, meleeHitLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            if (_hitColliders[i].TryGetComponent<HealthController>(out var enemyHealth))
            {
                if (HasStateAuthority)
                {
                    enemyHealth.TakeDamage(25);
                }
            }
        }
    }

    public void EquipWeapon(IWeapon newWeapon)
    {
        _activeWeapon = newWeapon;
        _activeWeapon.SetWeaponVisibility(true);
        SetAnimatorWeaponState(true);
    }

    public void HolsterWeapon()
    {
        if (_activeWeapon != null)
        {
            _activeWeapon.SetWeaponVisibility(false);
        }
        SetAnimatorWeaponState(false);
    }

    private void SetAnimatorWeaponState(bool hasWeapon)
    {
        if (_animator != null) _animator.SetBool(hasWeaponHash, hasWeapon);
        if (_playerController != null) _playerController.hasWeapon = hasWeapon;
    }
    public void DrawWeapon()
    {
        if (_activeWeapon != null)
        {
            _activeWeapon.SetWeaponVisibility(true);
            SetAnimatorWeaponState(true);
        }
    }
}