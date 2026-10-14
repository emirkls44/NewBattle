#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Tools > NewBattle > Play Store (AAB): Google Play'e yuklenecek .aab dosyasini olusturur.
    ///   - Yukleme anahtariyla (upload key) imzalar. Anahtar bilgileri
    ///     UserSettings/AndroidKeystore.json dosyasindan okunur; o dosya git'e gitmez.
    ///   - Her derlemede surum kodu (versionCode) 1 artar: Play ayni kodu ikinci kez kabul etmez.
    ///   - ARM64 + ARMv7 (eski 32 bit telefonlar da), IL2CPP, en guncel hedef Android surumu.
    /// Test icin telefona APK yuklemek hala Tools > NewBattle > Telefon penceresinden.
    /// </summary>
    public class PlayStoreBuildWindow : EditorWindow
    {
        private const string KeystoreConfigPath = "UserSettings/AndroidKeystore.json";
        private const string OutputFolder = @"D:\emir\Game\_claude_work\Builds\PlayStore";
        private const string GradleHome = @"D:\emir\Game\_claude_work\gradle";

        [Serializable]
        private class KeystoreConfig
        {
            public string keystorePath;
            public string keystorePass;
            public string alias;
            public string aliasPass;
        }

        private string _versionName;
        private string _lastResult = string.Empty;

        [MenuItem("Tools/NewBattle/Play Store (AAB derle)", false, 51)]
        public static void Open()
        {
            GetWindow<PlayStoreBuildWindow>("Play Store").minSize = new Vector2(440f, 380f);
        }

        private void OnEnable()
        {
            _versionName = PlayerSettings.bundleVersion;
        }

        private void OnGUI()
        {
            KeystoreConfig keystore = LoadKeystore(out string keystoreProblem);

            EditorGUILayout.LabelField("İmzalama anahtarı", EditorStyles.boldLabel);
            if (keystore != null)
                EditorGUILayout.HelpBox($"Hazır: {keystore.keystorePath}  (alias: {keystore.alias})", MessageType.Info);
            else
                EditorGUILayout.HelpBox(keystoreProblem, MessageType.Error);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Sürüm", EditorStyles.boldLabel);
            _versionName = EditorGUILayout.TextField(new GUIContent("Sürüm adı",
                "Oyuncunun mağazada gördüğü sürüm (ör. 1.0.0, 1.0.1)."), _versionName);
            int nextCode = PlayerSettings.Android.bundleVersionCode + 1;
            EditorGUILayout.LabelField("Sürüm kodu", $"{PlayerSettings.Android.bundleVersionCode}  →  bu derleme {nextCode}");
            EditorGUILayout.LabelField("Paket adı", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(keystore == null || EditorApplication.isPlaying))
            {
                if (GUILayout.Button("AAB Derle (Play Store)", GUILayout.Height(36f)))
                {
                    string versionName = _versionName;
                    EditorApplication.delayCall += () => Build(versionName);
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("AAB klasörünü aç"))
            {
                Directory.CreateDirectory(OutputFolder);
                EditorUtility.RevealInFinder(OutputFolder);
            }

            if (!string.IsNullOrEmpty(_lastResult))
                EditorGUILayout.HelpBox(_lastResult, MessageType.None);

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Play Console > Uygulamanız > Test > Kapalı test > Yeni sürüm oluştur > bu .aab dosyasını yükle.\n" +
                "İlk yüklemede 'Play Uygulama İmzalama'yı kabul et: Google asıl anahtarı saklar, bizimki yükleme anahtarı olur.",
                MessageType.Info);
        }

        private static KeystoreConfig LoadKeystore(out string problem)
        {
            problem = null;
            if (!File.Exists(KeystoreConfigPath))
            {
                problem = $"{KeystoreConfigPath} yok. Anahtar bilgileri bu dosyada olmalı (D:\\emir\\Game\\Keystore\\OKU_BENI... dosyasına bak).";
                return null;
            }

            KeystoreConfig config;
            try
            {
                config = JsonUtility.FromJson<KeystoreConfig>(File.ReadAllText(KeystoreConfigPath));
            }
            catch (Exception exception)
            {
                problem = $"{KeystoreConfigPath} okunamadı: {exception.Message}";
                return null;
            }

            if (config == null || string.IsNullOrEmpty(config.keystorePath) || !File.Exists(config.keystorePath))
            {
                problem = $"Anahtar dosyası bulunamadı: {config?.keystorePath}";
                return null;
            }

            return config;
        }

        private void Build(string versionName)
        {
            KeystoreConfig keystore = LoadKeystore(out string problem);
            if (keystore == null)
            {
                EditorUtility.DisplayDialog("Play Store", problem, "Tamam");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUtility.DisplayDialog("Play Store",
                    "Unity Android'e geçecek ve her şeyi yeniden derleyecek. Devam?", "Devam", "Vazgeç"))
                return;

            PhoneBuildWindow.PrepareSettingsQuiet();

            if (!string.IsNullOrWhiteSpace(versionName))
                PlayerSettings.bundleVersion = versionName.Trim();

            int previousCode = PlayerSettings.Android.bundleVersionCode;
            PlayerSettings.Android.bundleVersionCode = previousCode + 1;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto; // kurulu en yeni (36)
            PlayerSettings.Android.forceInternetPermission = true;

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore.keystorePath;
            PlayerSettings.Android.keystorePass = keystore.keystorePass;
            PlayerSettings.Android.keyaliasName = keystore.alias;
            PlayerSettings.Android.keyaliasPass = keystore.aliasPass;

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(GradleHome);
            Environment.SetEnvironmentVariable("GRADLE_USER_HOME", GradleHome);
            Directory.CreateDirectory(OutputFolder);
            string output = Path.Combine(OutputFolder,
                $"NewBattle-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.aab");

            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    scenes.Add(scene.path);

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                });
            }
            finally
            {
                // Test APK'lari yine hata ayiklama anahtariyla imzalansin.
                EditorUserBuildSettings.buildAppBundle = false;
                PlayerSettings.Android.useCustomKeystore = false;
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                // Basarisiz derleme surum kodunu harcamasin.
                PlayerSettings.Android.bundleVersionCode = previousCode;
                AssetDatabase.SaveAssets();
                _lastResult = "Derleme başarısız: " + report.summary.result;
                EditorUtility.DisplayDialog("Play Store", "AAB oluşturulamadı: " + report.summary.result +
                                                          "\nHata ayrıntıları Console'da.", "Tamam");
                Repaint();
                return;
            }

            AssetDatabase.SaveAssets();
            float sizeMb = new FileInfo(output).Length / (1024f * 1024f);
            _lastResult = $"Hazır: {Path.GetFileName(output)} ({sizeMb:0.0} MB)\n" +
                          $"Sürüm {PlayerSettings.bundleVersion}, kod {PlayerSettings.Android.bundleVersionCode}. " +
                          "ProjectSettings değişikliğini commitle.";
            Debug.Log("Play Store AAB hazir: " + output);
            EditorUtility.RevealInFinder(output);
            Repaint();
        }
    }
}
#endif
