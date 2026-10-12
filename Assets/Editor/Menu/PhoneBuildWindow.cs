#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Tools > NewBattle > Telefon: oyunu Android telefona yukler.
    ///   1. Ayarlari hazirla (yatay ekran, paket adi, Photon bolgesi).
    ///   2. APK olustur ve USB ile bagli TUM telefonlara yukleyip ac.
    /// Iki telefonla ikili denemek icin ikisini de USB ile bagla.
    /// </summary>
    public class PhoneBuildWindow : EditorWindow
    {
        private const string PackageName = "com.emirkls.newbattle";
        private const string PhotonSettingsPath = "Assets/Photon/Fusion/Resources/PhotonAppSettings.asset";
        private const string PhotonRegion = "eu";
        private const string BuildFolder = @"D:\emir\Game\_claude_work\Builds";
        private const string GradleHome = @"D:\emir\Game\_claude_work\gradle";

        private string _devices = "(Yenile'ye bas)";
        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/Telefon (APK yükle)", false, 50)]
        public static void Open()
        {
            GetWindow<PhoneBuildWindow>("Telefon").minSize = new Vector2(460f, 460f);
        }

        private void OnEnable() => RefreshDevices();

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Telefonlarda bir kez: Ayarlar > Telefon hakkında > Yapım numarasına 7 kez dokun (Geliştirici seçenekleri açılır) > " +
                "Geliştirici seçenekleri > USB hata ayıklama: AÇIK.\n" +
                "Sonra iki telefonu da USB ile bağla; telefonda çıkan 'USB hata ayıklamaya izin ver' sorusuna İzin ver de.",
                MessageType.Info);

            EditorGUILayout.LabelField("Bağlı telefonlar", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_devices, MessageType.None);
            if (GUILayout.Button("Yenile"))
                RefreshDevices();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("1. Ayarları hazırla (bir kez)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Yatay ekran, paket adı " + PackageName + ", Photon bölgesi '" + PhotonRegion +
                                       "' (iki telefon aynı sunucuda buluşsun).", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Ayarları Hazırla", GUILayout.Height(28f)))
                PrepareSettings();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("2. APK oluştur ve telefonlara yükle", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "İlk seferde Unity Android'e geçer ve her şeyi yeniden derler: 10-30 dakika sürebilir. Sonrakiler çok daha hızlı.",
                EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("APK Oluştur + Telefonlara Yükle", GUILayout.Height(36f)))
                    RunAfterGui(() => BuildAndInstall(install: true));

                if (GUILayout.Button("Sadece APK Oluştur"))
                    RunAfterGui(() => BuildAndInstall(install: false));
            }

            if (GUILayout.Button("Mevcut APK'yı tekrar yükle (derlemeden)"))
                RunAfterGui(() => InstallToAll(Path.Combine(BuildFolder, "NewBattle.apk")));

            if (GUILayout.Button("APK klasörünü aç"))
            {
                Directory.CreateDirectory(BuildFolder);
                EditorUtility.RevealInFinder(BuildFolder);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "İkili deneme: iki telefonda da OYNA > RASTGELE İKİLİ > ÇEVRİMİÇİ OYNA. 20 saniye içinde ikisi de basarsa " +
                "aynı maça ve AYNI TAKIMA düşersiniz; kalan yerler botla dolar.",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------

        private static void PrepareSettings()
        {
            PlayerSettings.companyName = "emirkls";
            PlayerSettings.productName = "New Battle";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, PackageName);

            // Oyun yatay: telefon dik tutulunca donmesin.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Iki telefon farkli bolgeye (biri Wi-Fi, biri mobil veri) baglanirsa
            // birbirini bulamaz; bolgeyi sabitliyoruz.
            var photon = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PhotonSettingsPath);
            if (photon != null)
            {
                var serialized = new SerializedObject(photon);
                SerializedProperty region = serialized.FindProperty("AppSettings.FixedRegion");
                if (region != null)
                {
                    region.stringValue = PhotonRegion;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(photon);
                }
                else
                {
                    Debug.LogWarning("Photon ayarlarinda FixedRegion bulunamadi; bolgeyi Tools > Fusion > Realtime Settings'ten 'eu' yap.");
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Telefon", "Ayarlar hazır.\n\nYatay ekran, paket adı " + PackageName +
                                                   ", Photon bölgesi '" + PhotonRegion + "'.", "Tamam");
        }

        /// <summary>
        /// Uzun islemi (derleme) pencere cizimi bittikten sonra calistirir; OnGUI icinde
        /// calisinca "EndLayoutGroup: BeginLayoutGroup must be called first" hatasi verir.
        /// </summary>
        private static void RunAfterGui(System.Action action)
        {
            EditorApplication.delayCall += () => action();
            GUIUtility.ExitGUI();
        }

        private void BuildAndInstall(bool install)
        {
            PrepareSettingsQuiet();

            // C: dolu: Gradle onbellegini D:'ye al (Unity'nin baslattigi Gradle bunu miras alir).
            Directory.CreateDirectory(GradleHome);
            Environment.SetEnvironmentVariable("GRADLE_USER_HOME", GradleHome);

            Directory.CreateDirectory(BuildFolder);
            string apk = Path.Combine(BuildFolder, "NewBattle.apk");

            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    scenes.Add(scene.path);
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUtility.DisplayDialog("Telefon",
                    "Unity Android'e geçecek ve her şeyi yeniden derleyecek (ilk sefer 10-30 dk). Devam?",
                    "Devam", "Vazgeç"))
                return;

            EditorUserBuildSettings.buildAppBundle = false; // .aab degil, dogrudan yuklenebilir .apk

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Telefon", "APK oluşturulamadı: " + report.summary.result +
                                                       "\nHata ayrıntıları Console'da.", "Tamam");
                return;
            }

            float sizeMb = new FileInfo(apk).Length / (1024f * 1024f);
            Debug.Log($"APK hazir: {apk} ({sizeMb:0.0} MB)");

            if (install)
            {
                InstallToAll(apk);
                return;
            }

            EditorUtility.RevealInFinder(apk);
            EditorUtility.DisplayDialog("APK hazır",
                $"NewBattle.apk ({sizeMb:0} MB) açılan klasörde.\n\n" +
                "Telefona at: WhatsApp / Telegram / Google Drive / e-posta / USB ile kopyala.\n" +
                "Telefonda dosyaya dokun → 'Bilinmeyen uygulamaları yükle' iznini ver → Yükle.\n\n" +
                "Yeni sürümü aynı şekilde üstüne kurabilirsin; ilerleme silinmez.",
                "Tamam");
        }

        private static void PrepareSettingsQuiet()
        {
            if (PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android) != PackageName)
                PrepareSettings();

            EnsureAlwaysIncludedShaders();
        }

        /// <summary>
        /// Kodda Shader.Find ile yuklenen shader'lar hicbir materyalde kullanilmadigi icin
        /// derlemeye girmez; telefonda yedek (opak) shader'a duserler. Ornek: loot parlamasi
        /// duz renkli disk gibi gorunuyordu. Bunlari "her zaman dahil" listesine ekler.
        /// </summary>
        private static void EnsureAlwaysIncludedShaders()
        {
            string[] names =
            {
                "NewBattle/ToonOverlay",
                "NewBattle/ZoneBand",
                "Universal Render Pipeline/Unlit"
            };

            var settings = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            SerializedProperty list = settings.FindProperty("m_AlwaysIncludedShaders");
            bool changed = false;

            foreach (string shaderName in names)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogWarning("Telefon: shader bulunamadi: " + shaderName);
                    continue;
                }

                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        present = true;
                        break;
                    }
                }

                if (present)
                    continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                changed = true;
                Debug.Log("Telefon: derlemeye her zaman dahil edilen shader eklendi: " + shaderName);
            }

            if (changed)
            {
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }

        private void InstallToAll(string apk)
        {
            if (!File.Exists(apk))
            {
                EditorUtility.DisplayDialog("Telefon", "APK yok: önce 'APK Oluştur'.", "Tamam");
                return;
            }

            List<string> devices = ListDevices(out _);
            if (devices.Count == 0)
            {
                EditorUtility.DisplayDialog("Telefon",
                    "Bağlı telefon bulunamadı.\nUSB hata ayıklama açık mı, telefonda 'izin ver' dedin mi?\n\nAPK burada: " + apk,
                    "Tamam");
                return;
            }

            var results = new List<string>();
            for (int i = 0; i < devices.Count; i++)
            {
                string serial = devices[i];
                EditorUtility.DisplayProgressBar("Telefon", $"Yükleniyor: {serial}", i / (float)devices.Count);
                string output = Adb($"-s {serial} install -r \"{apk}\"", 300000);
                bool ok = output.Contains("Success");
                if (ok)
                    Adb($"-s {serial} shell monkey -p {PackageName} -c android.intent.category.LAUNCHER 1", 30000);
                results.Add($"{serial}: {(ok ? "YÜKLENDİ ve açıldı" : "HATA\n" + output.Trim())}");
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Telefon", string.Join("\n\n", results), "Tamam");
            RefreshDevices();
        }

        private void RefreshDevices()
        {
            List<string> devices = ListDevices(out string raw);
            _devices = devices.Count == 0
                ? "Bağlı telefon yok.\n" + raw.Trim()
                : $"{devices.Count} telefon bağlı:\n" + string.Join("\n", devices);
            Repaint();
        }

        private static List<string> ListDevices(out string raw)
        {
            raw = Adb("devices", 15000);
            var devices = new List<string>();
            foreach (string line in raw.Split('\n'))
            {
                string[] parts = line.Trim().Split('\t');
                if (parts.Length == 2 && parts[1].Trim() == "device")
                    devices.Add(parts[0].Trim());
            }

            return devices;
        }

        /// <summary>Unity'nin Android modulu ile gelen adb.</summary>
        private static string AdbPath()
        {
            return Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines", "AndroidPlayer", "SDK", "platform-tools", "adb.exe");
        }

        private static string Adb(string arguments, int timeoutMs)
        {
            try
            {
                var start = new ProcessStartInfo(AdbPath(), arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using Process process = Process.Start(start);
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit(timeoutMs);
                return output;
            }
            catch (Exception exception)
            {
                return "adb çalıştırılamadı: " + exception.Message;
            }
        }
    }
}
#endif
