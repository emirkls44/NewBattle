using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class DropPhaseController : NetworkBehaviour
{
    [Header("Harita Olcusu")]
    [Tooltip("Harita KARE. Bu deger merkezden kenara olan mesafe, yani kenar " +
             "uzunlugunun yarisi. 160x160 bir harita icin ~78 (kenardan biraz " +
             "iceride, oyuncu haritanin tam ucunda durmasin diye).")]
    [SerializeField, Min(5f)] private float playableMapExtent = 78f;

    [Networked] public NetworkBool SelectionStarted { get; private set; }
    [Networked] public NetworkBool GameplayStarted { get; private set; }

    private LobbyCountdownController _lobby;

    /// <summary>Merkezden kenara mesafe (kenar uzunlugunun yarisi).</summary>
    public float MapExtent => playableMapExtent;

    /// <summary>
    /// Bir noktayi haritanin KARE sinirlari icine tasir.
    ///
    /// Eskiden burada cembersel bir kirpma vardi. Kare haritada bu, dort
    /// kosenin - haritanin beste birinin - oyuncuya kapali olmasi demek
    /// olurdu: zemin gorunur ama gidilemez.
    /// </summary>
    public Vector2 ClampInside(Vector2 point, float margin = 0f)
    {
        float limit = Mathf.Max(0f, playableMapExtent - margin);

        return new Vector2(
            Mathf.Clamp(point.x, -limit, limit),
            Mathf.Clamp(point.y, -limit, limit));
    }

    /// <summary>Harita icinde rastgele bir nokta; kareye esit dagilir.</summary>
    public Vector2 RandomPointInside(float margin = 0f)
    {
        float limit = Mathf.Max(0f, playableMapExtent - margin);

        return new Vector2(
            Random.Range(-limit, limit),
            Random.Range(-limit, limit));
    }

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
