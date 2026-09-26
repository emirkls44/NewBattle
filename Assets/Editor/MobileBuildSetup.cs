using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Telefonda oynamadan once proje ayarlarindaki eksikleri kapatir:
///
///   - Ekran yonu: sadece YATAY. Otomatik donme acikti ve dikey yonlere de
///     izin veriyordu; telefonu dik tutunca HUD ve joystick'ler bozulurdu.
///   - Paket adi: Unity sablonundan kalma "com.UnityTechnologies...urpblank"
///     yerine oyuna ait bir ad. (Sadece sablon adi duruyorsa degisir.)
///   - Internet izni: online modlar Photon'a baglanir; "Auto" algilamasina
///     guvenmek yerine izni zorunlu yap.
///
/// Mimari (ARM64) ve derleyici (IL2CPP) zaten dogru; onlara dokunulmaz.
/// </summary>
public static class MobileBuildSetup
{
    private const string TemplateIdentifier = "com.unity.template";
    private const string GameIdentifier = "com.emir.newbattle";

    [MenuItem("Tools/NewBattle/Telefon Icin Hazirla", false, 80)]
    public static void Run()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string identifierLog = $"Paket adi: {identifier} (degismedi)";

        if (string.IsNullOrEmpty(identifier) || identifier.Contains(TemplateIdentifier))
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, GameIdentifier);
            identifierLog = $"Paket adi: {identifier} -> {GameIdentifier}";
        }

        PlayerSettings.Android.forceInternetPermission = true;

        AssetDatabase.SaveAssets();

        Debug.Log(
            "Telefon ayarlari hazir:\n" +
            "Ekran yonu: sadece yatay (sola ve saga donebilir)\n" +
            identifierLog + "\n" +
            "Internet izni: zorunlu\n" +
            $"Mimari: {PlayerSettings.Android.targetArchitectures}, " +
            $"derleyici: {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}");
    }
}
