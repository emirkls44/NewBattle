using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class DropPhaseController : NetworkBehaviour
{
    [Header("Haritaya Inis")]
    [SerializeField, Min(5f)] private float playableMapRadius = 29f;

    [Networked] public NetworkBool SelectionStarted { get; private set; }
    [Networked] public NetworkBool GameplayStarted { get; private set; }

    private LobbyCountdownController _lobby;

    public float PlayableMapRadius => playableMapRadius;

    public float RemainingSeconds
    {
        get
        {
            if (Runner == null || _lobby == null || !SelectionStarted || GameplayStarted)
                return 0f;

            return _lobby.RemainingSeconds;
        }
    }

    public override void Spawned()
    {
        _lobby = GetComponent<LobbyCountdownController>();

        if (!HasStateAuthority)
            return;

        // Oyuncu odaya girer girmez haritadan inis yerini secebilsin.
        // Diger oyuncular bu ekran acikken odaya katilmaya devam eder.
        SelectionStarted = true;
        GameplayStarted = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || GameplayStarted)
            return;

        if (_lobby == null || !_lobby.MatchStarted)
            return;

        // Lobby sayaci bitti: ikinci bir sayac baslatmadan oyuna gec.
        GameplayStarted = true;
    }
}
