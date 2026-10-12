using System;
using UnityEngine;

/// <summary>
/// Oyuncunun AYARLAR ekranindan degistirdigi cihaz ayarlari (ses, kare hizi,
/// kamera yakinligi). Cihazda saklanir ve oyun acilirken uygulanir.
/// </summary>
public static class GameSettings
{
    private const string SoundKey = "nb_set_sound";
    private const string FrameRateKey = "nb_set_fps";
    private const string CameraKey = "nb_set_camera";

    public const float CameraMin = 6f;
    public const float CameraMax = 12f;
    public const float CameraStep = 0.75f;

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

    /// <summary>Oyuncu kamera yakinligini degistirdiyse true (yoksa CameraFollow'daki deger gecerli).</summary>
    public static bool HasCameraViewSize => PlayerPrefs.HasKey(CameraKey);

    /// <summary>Kamera boyutu: KUCUK = YAKIN.</summary>
    public static float CameraViewSize
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(CameraKey, 8.5f), CameraMin, CameraMax);
        set
        {
            PlayerPrefs.SetFloat(CameraKey, Mathf.Clamp(value, CameraMin, CameraMax));
            Save();
        }
    }

    /// <summary>Ekranda gosterilen yakinlik seviyesi: 1 = en uzak, 9 = en yakin.</summary>
    public static int CameraLevel(float viewSize) =>
        Mathf.RoundToInt((CameraMax - viewSize) / CameraStep) + 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyOnStart() => Apply();

    public static void Apply()
    {
        AudioListener.volume = SoundOn ? 1f : 0f;

#if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = FrameRate;
#endif

        if (HasCameraViewSize && CameraFollow.Active != null)
            CameraFollow.Active.ViewSize = CameraViewSize;
    }

    private static void Save()
    {
        PlayerPrefs.Save();
        Apply();
        Changed?.Invoke();
    }
}
