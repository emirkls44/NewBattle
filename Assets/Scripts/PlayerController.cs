using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [Header("Hareket Ayarlarý")]
    public float moveSpeed = 5f;

    [Header("Silah ve Animasyon Ayarlarý")]
    public Animator anim;
    public GameObject weaponInHand;

    [Networked] public NetworkBool hasWeapon { get; set; }

    private Rigidbody rb;
    private ChangeDetector _changeDetector;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (weaponInHand != null) weaponInHand.SetActive(false);
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
            if (camFollow != null)
            {
                camFollow.SetTarget(this.transform);
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput<NetworkInputData>(out var input))
        {
            HareketVeYonHesapla(input);
            FizikselHareketUygula(input);
        }
    }

    void HareketVeYonHesapla(NetworkInputData input)
    {
        Vector3 moveVector = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y).normalized;
        Vector3 aimVector = new Vector3(input.RightJoystickVector.x, 0f, input.RightJoystickVector.y);

        if (aimVector.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(aimVector);
        else if (moveVector.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(moveVector);

        if (anim != null) anim.SetFloat("Speed", moveVector.magnitude);
    }

    void FizikselHareketUygula(NetworkInputData input)
    {
        Vector3 moveVector = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y).normalized;
        Vector3 finalVelocity = moveVector * moveSpeed;

        rb.linearVelocity = new Vector3(finalVelocity.x, rb.linearVelocity.y, finalVelocity.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;

        if (other.CompareTag("WeaponLoot"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                Runner.Despawn(netObj);
            }

            hasWeapon = true;
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(hasWeapon):
                    if (weaponInHand != null) weaponInHand.SetActive(hasWeapon);
                    if (anim != null) anim.SetBool("hasWeapon", hasWeapon);
                    break;
            }
        }
    }
}