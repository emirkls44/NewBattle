using System;
using UnityEngine;

/// <summary>
/// Oyuncunun AYARLAR ekranindan degistirdigi cihaz ayarlari (ses, kare hizi).
/// Cihazda saklanir ve oyun acilirken uygulanir. Kamera yakinligini oyuncu
/// degistiremez; onu CameraFollow > View Size'dan sen ayarlarsin.
/// </summary>
public static class GameSettings
{
    private const string SoundKey = "nb_set_sound";
    private const string FrameRateKey = "nb_set_fps";
    private const string LegacyCameraKey = "nb_set_camera";

    /// <summary>Herhangi bir ayar degisince tetiklenir.</summary>
    public static event Action Changed;

    public static bool SoundOn
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
            Save();
        }
    }

    /// <summary>30 ya da 60.</summary>
    public static int FrameRate
    {
        get => PlayerPrefs.GetInt(FrameRateKey, 60) <= 30 ? 30 : 60;
        set
        {
            PlayerPrefs.SetInt(FrameRateKey, value <= 30 ? 30 : 60);
            Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyOnStart() => Apply();

    public static void Apply()
    {
        AudioListener.volume = SoundOn ? 1f : 0f;

#if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = FrameRate;
#endif

        // Eski surumde oyuncu kamera yakinligini kaydedebiliyordu; artik yok.
        if (PlayerPrefs.HasKey(LegacyCameraKey))
            PlayerPrefs.DeleteKey(LegacyCameraKey);
    }

    private static void Save()
    {
        PlayerPrefs.Save();
        Apply();
        Changed?.Invoke();
    }
}
