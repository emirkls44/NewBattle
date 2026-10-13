using UnityEngine;

/// <summary>
/// Telefonda kare hizini duruma gore ayarlar: maç oynanirken oyuncunun ayari (60 ya da 30),
/// menude / takim odasinda / harita seciminde / mac sonu ekraninda 30.
///
/// Bu ekranlarda hizli hareket yok; 60 kare cizmek telefonu bosuna isitip pili bitiriyordu.
/// Sahneye eklenmesi gerekmez: oyun acilirken kendini kurar.
/// </summary>
public class FrameRateGovernor : MonoBehaviour
{
    private const int IdleFrameRate = 30;
    private const float CheckSeconds = 0.5f;

    private DropPhaseController _dropPhase;
    private float _nextCheck;

#if UNITY_ANDROID || UNITY_IOS
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var host = new GameObject("KareHiziAyari") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(host);
        host.AddComponent<FrameRateGovernor>();
    }
#endif

    /// <summary>Su an mac oynaniyor mu (inis + savas, izlerken de).</summary>
    public static bool InGameplay(DropPhaseController dropPhase)
    {
        return dropPhase != null && dropPhase.Object != null && dropPhase.Object.IsValid &&
               dropPhase.Runner != null && dropPhase.Runner.IsRunning &&
               dropPhase.GameplayStarted && !MatchResultHUD.ResultVisible;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextCheck)
            return;

        _nextCheck = Time.unscaledTime + CheckSeconds;

        if (_dropPhase == null)
            _dropPhase = FindFirstObjectByType<DropPhaseController>();

        int target = InGameplay(_dropPhase) ? GameSettings.FrameRate : Mathf.Min(IdleFrameRate, GameSettings.FrameRate);
        if (Application.targetFrameRate != target)
            Application.targetFrameRate = target;
    }
}
