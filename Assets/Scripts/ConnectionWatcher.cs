using Fusion;
using NewBattle.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Oturum beklenmedik sekilde biterse (oyun kurucusu ayrildi, internet koptu, uygulama
/// arka planda kalip zaman asimina ugradi) oyuncu donmus ekranda kalmasin: mor yukleme
/// ekraninda nedeni yazilir ve ana menuye donulur.
///   - Mac baslamadan (eslesme lobisinde) kurucu ayrilirsa menu hemen yeni mac arar.
///   - Takim odasi (BASLAT'a basilmadan once) TeamRoomScreen'in isidir, burada atlanir.
///   - Mac sonu ekrani aciksa dokunulmaz; DEVAM ile zaten menuye donulur.
/// Sahneye eklenmesi gerekmez: oyun acilirken kendini kurar.
/// </summary>
public class ConnectionWatcher : MonoBehaviour
{
    private const float CheckSeconds = 0.25f;

    private NetworkRunner _runner;
    private float _nextCheck;

    // Son kontrolde oturumun durumu (oturum bittiginde karar bunlara gore verilir).
    private bool _watching;
    private bool _wasServer;
    private bool _matchStarted;
    private BattleGameMode _mode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var host = new GameObject("BaglantiIzleyici") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(host);
        host.AddComponent<ConnectionWatcher>();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextCheck)
            return;

        _nextCheck = Time.unscaledTime + CheckSeconds;

        if (_runner == null || !_runner.IsRunning)
        {
            NetworkRunner found = FindFirstObjectByType<NetworkRunner>();
            if (found != null && found.IsRunning)
                _runner = found;
        }

        bool running = _runner != null && _runner.IsRunning;
        if (running)
        {
            Track();
            return;
        }

        if (!_watching)
            return;

        _watching = false;
        _runner = null;

        if (NetworkBootstrap.ExpectedShutdown || MatchResultHUD.ResultVisible)
            return;

        OnSessionLost();
    }

    /// <summary>Oturum calisirken: izlenmeye deger mi ve mac basladi mi.</summary>
    private void Track()
    {
        LobbyCountdownController lobby = FindFirstObjectByType<LobbyCountdownController>();
        bool lobbyValid = lobby != null && lobby.Object != null && lobby.Object.IsValid;

        // Bot maci tek basina oynanir (ag yok); takim odasini TeamRoomScreen izler.
        bool teamRoomWaiting = NetworkBootstrap.IsFriendRoom && lobbyValid && !lobby.RoomStarted;
        _watching = lobbyValid && !teamRoomWaiting && NetworkBootstrap.ActiveMode != BattleGameMode.Bots;
        if (!_watching)
            return;

        _wasServer = _runner.IsServer;
        _matchStarted = lobby.MatchStarted;
        _mode = NetworkBootstrap.ActiveMode;
    }

    private void OnSessionLost()
    {
        bool offline = Application.internetReachability == NetworkReachability.NotReachable;
        bool requeue = !offline && !_wasServer && !_matchStarted && !NetworkBootstrap.IsFriendRoom;

        string message;
        if (offline)
            message = "İNTERNET BAĞLANTIN KOPTU";
        else if (_wasServer)
            message = "SUNUCU BAĞLANTISI KOPTU";
        else if (requeue)
            message = "OYUN KURUCUSU AYRILDI\nYENİ MAÇ ARANIYOR...";
        else
            message = "OYUN KURUCUSUNUN BAĞLANTISI KOPTU\nMAÇ SONA ERDİ";

        NetworkBootstrap.ClearFriendRoom();
        NetworkBootstrap.PendingRejoin = null;
        NetworkBootstrap.PendingRequeue = requeue ? _mode : null;
        NetworkBootstrap.PendingMenuMessage = requeue ? null : message.Replace("\n", " · ");

        MainMenuScreen menu = FindFirstObjectByType<MainMenuScreen>(FindObjectsInactive.Include);
        LoadingScreen.ShowLeaving(menu != null ? menu.LoadingScreenPrefab : null, message, holdForRejoin: requeue);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
