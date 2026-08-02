using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkTransform), typeof(PlayerStateMachine))]
public class PlayerController : NetworkBehaviour
{
    [Header("Silah Ayarlarý")]
    public GameObject weaponInHand;

    [Networked] public NetworkBool hasWeapon { get; set; }

    private PlayerStateMachine _stateMachine;
    private ChangeDetector _changeDetector;
    public Animator anim;

    void Awake()
    {
        _stateMachine = GetComponent<PlayerStateMachine>();
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
            // Tüm karar mekanizmasý State Machine'e devredildi
            _stateMachine.ProcessInput(input);
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