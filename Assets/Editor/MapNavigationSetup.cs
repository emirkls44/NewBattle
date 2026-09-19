using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Botlarin uzerinde yuruyecegi NavMesh'i kurar ve uretir.
    ///
    /// Neden gerekli: EnemyController bir NavMeshAgent istiyor, ama sahnede
    /// hicbir NavMesh verisi yoktu. Duz zeminde bu fark edilmiyor; ev ve
    /// kaya konur konmaz botlar icinden gecmeye baslar.
    ///
    /// Onemli ayar useGeometry = PhysicsColliders. Iki sebebi var:
    ///   - Prop'larin basit kutu/kapsul collider'i engeli tanimlar; yuksek
    ///     poligonlu gorsel mesh hesaba katilmaz, bake suresi patlamaz.
    ///   - Trigger collider'lar hesaba katilmaz. Calilar trigger oldugu icin
    ///     botlarin yolunu kesmez; zaten saklanma alani olmalari gerekiyor,
    ///     duvar degil.
    /// </summary>
    public class MapNavigationSetup : EditorWindow
    {
        /// <summary>
        /// NavMesh verisinin yazilacagi klasor. Sahne dosyasinin yaninda
        /// degil ayri bir klasorde: sahne YAML'ini elle duzenlemek zorunda
        /// kaldigimiz durumlarda binary bir varligin araya girmesi isi
        /// zorlastiriyor.
        /// </summary>
        private const string NavMeshFolder = "Assets/GeneratedIslandAssets/NavMesh";

        // Oyuncu kapsulu: yaricap 0.35, yukseklik 2 (PlayerOnline.prefab).
        // Bot ayni govdeyi kullandigi icin ajan olculeri buna yakin olmali.
        private const float PlayerRadius = 0.35f;
        private const float PlayerHeight = 2f;

        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/Harita Navigasyonu")]
        private static void Open()
        {
            GetWindow<MapNavigationSetup>("Harita Navigasyonu").minSize =
                new Vector2(430f, 300f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Sirasiyla: once Kur, sonra Uret. Haritaya yeni ev/kaya " +
                "ekledikten sonra tekrar Uret demen yeterli.",
                MessageType.Info);

            EditorGUILayout.Space(6f);

            if (GUILayout.Button("1) NavMeshSurface Kur", GUILayout.Height(32f)))
                SetupSurface();

            if (GUILayout.Button("2) NavMesh Uret (Bake)", GUILayout.Height(32f)))
                Bake();

            EditorGUILayout.Space(10f);

            if (GUILayout.Button("Mavi NavMesh gorunumunu ac/kapat", GUILayout.Height(26f)))
                ToggleGizmos();

            EditorGUILayout.LabelField(
                "Sadece NavMesh'i gizlemek icin: Scene penceresi ustundeki " +
                "Gizmos menusu > NavMeshSurface.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(6f);

            if (GUILayout.Button("Durumu Yaz", GUILayout.Height(26f)))
                ReportStatus();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Prop kurallari", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Kati engel (ev, kaya, duvar): normal collider. Bot da mermi de " +
                "takilir.\n\n" +
                "Cali (saklanma): trigger collider + 'Bush' etiketi. Bot da mermi " +
                "de gecer, oyuncu gizlenir.\n\n" +
                "Dekor (cicek, kucuk tas): collider yok.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        // Kurulum
        // ------------------------------------------------------------------

        /// <summary>
        /// NavMeshSurface'i bulur ya da olusturur ve haritaya gore ayarlar.
        ///
        /// Surface'i BattleMap'e degil kendi nesnesine koyuyoruz: BattleMap
        /// sadece arazi parcalarini tutuyor, oysa yurunebilir zemin (Ground)
        /// onun disinda. Ayri bir kok ile collectObjects = All kullanip
        /// ikisini de topluyoruz.
        /// </summary>
        private static void SetupSurface()
        {
            NavMeshSurface surface = FindSurface();

            if (surface == null)
            {
                GameObject host = new("Navigation");
                Undo.RegisterCreatedObjectUndo(host, "Navigasyon kur");
                surface = Undo.AddComponent<NavMeshSurface>(host);
            }
            else
            {
                Undo.RecordObject(surface, "Navigasyon kur");
            }

            surface.collectObjects = CollectObjects.All;

            // Gorsel mesh yerine collider: hem hizli hem de trigger calilari
            // otomatik disarida birakiyor.
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            surface.layerMask = ~0;
            surface.defaultArea = 0;

            // Kucuk kopuk adaciklar bot yapay zekasini kilitliyor: ajan
            // ulasilamaz bir parcaya hedef koyup oldugu yerde titriyor.
            surface.minRegionArea = 2f;

            EditorUtility.SetDirty(surface);
            MarkSceneDirty();

            Selection.activeObject = surface.gameObject;

            Debug.Log(
                "NavMeshSurface kuruldu: " + Path(surface.transform) + "\n" +
                "  Toplama : Butun sahne\n" +
                "  Geometri: Physics Colliders (trigger'lar haric)\n" +
                "  Sonraki adim: '2) NavMesh Uret'.");

            WarnAboutAgentSize(surface);
        }

        /// <summary>
        /// Ajan olculeri oyuncu kapsuluyle uyusmuyorsa uyarir.
        ///
        /// Bu degerleri koddan yazamiyoruz: Unity'nin ajan tipleri proje
        /// ayarlarinda tutuluyor ve yazma API'si yok. O yuzden sadece olcup
        /// soyluyoruz - sessizce yanlis kalmasi, "bot neden duvarin
        /// dibinden gecemiyor" diye aranmaya yol acardi.
        /// </summary>
        private static void WarnAboutAgentSize(NavMeshSurface surface)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(surface.agentTypeID);

            bool radiusOff = Mathf.Abs(settings.agentRadius - PlayerRadius) > 0.15f;
            bool heightOff = Mathf.Abs(settings.agentHeight - PlayerHeight) > 0.4f;

            if (!radiusOff && !heightOff)
                return;

            Debug.LogWarning(
                "Ajan olculeri oyuncu govdesiyle uyusmuyor.\n" +
                $"  Ajan   : yaricap {settings.agentRadius}, yukseklik {settings.agentHeight}\n" +
                $"  Oyuncu : yaricap {PlayerRadius}, yukseklik {PlayerHeight}\n" +
                "Window > AI > Navigation > Agents bolumunden duzeltebilirsin. " +
                "Ajan oyuncudan genisse botlar dar gecitlere giremez, darsa " +
                "duvarlara surtunur.");
        }

        // ------------------------------------------------------------------
        // Uretim
        // ------------------------------------------------------------------

        private static void Bake()
        {
            NavMeshSurface surface = FindSurface();

            if (surface == null)
            {
                Debug.LogError("NavMeshSurface yok. Once '1) NavMeshSurface Kur'.");
                return;
            }

            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError(
                    "NavMesh uretilemedi. Muhtemel sebep: yurunebilir collider yok. " +
                    "Zemin nesnelerinde Mesh/Box Collider bulundugundan emin ol.");
                return;
            }

            PersistNavMeshData(surface);

            EditorUtility.SetDirty(surface);
            MarkSceneDirty();

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();

            Debug.Log(
                "NavMesh uretildi.\n" +
                $"  Varlik  : {AssetDatabase.GetAssetPath(surface.navMeshData)}\n" +
                $"  Ucgen   : {tri.indices.Length / 3}\n" +
                $"  Kose    : {tri.vertices.Length}\n" +
                "Haritaya yeni ev/kaya ekledikce tekrar uret.");
        }

        /// <summary>
        /// Uretilen veriyi diske yazar.
        ///
        /// BuildNavMesh her cagrida YENI bir NavMeshData nesnesi uretiyor;
        /// eskisi varlik olarak kalirsa sahne eski veriye isaret etmeye
        /// devam eder ve degisiklik oyunda gorunmez. O yuzden ayni yola
        /// yaziyor, oncekini siliyoruz.
        /// </summary>
        private static void PersistNavMeshData(NavMeshSurface surface)
        {
            if (AssetDatabase.Contains(surface.navMeshData))
                return;

            EnsureFolder(NavMeshFolder);

            string sceneName = SceneManager.GetActiveScene().name;
            string path = $"{NavMeshFolder}/{sceneName}_NavMesh.asset";

            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) != null)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(surface.navMeshData, path);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        // ------------------------------------------------------------------
        // Durum
        // ------------------------------------------------------------------

        private static void ReportStatus()
        {
            StringBuilder sb = new();
            sb.AppendLine("=== HARITA NAVIGASYONU ===");

            NavMeshSurface surface = FindSurface();

            if (surface == null)
            {
                sb.AppendLine("NavMeshSurface: YOK");
            }
            else
            {
                sb.AppendLine($"NavMeshSurface: {Path(surface.transform)}");
                sb.AppendLine($"  Toplama : {surface.collectObjects}");
                sb.AppendLine($"  Geometri: {surface.useGeometry}");

                string dataPath = surface.navMeshData == null
                    ? "YOK - bake edilmemis"
                    : AssetDatabase.GetAssetPath(surface.navMeshData);

                sb.AppendLine($"  Veri    : {dataPath}");

                NavMeshBuildSettings s = NavMesh.GetSettingsByID(surface.agentTypeID);
                sb.AppendLine($"  Ajan    : yaricap {s.agentRadius}, yukseklik " +
                              $"{s.agentHeight}, tirmanis {s.agentClimb}, " +
                              $"egim {s.agentSlope}");
            }

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            sb.AppendLine($"Yuklu NavMesh ucgeni: {tri.indices.Length / 3}");

            CountProps(sb);

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Sahnedeki collider'lari kati / trigger diye ayirip sayar.
        ///
        /// Amac tek bakista "calilarim gercekten trigger mi" sorusunu
        /// cevaplamak; yanlis kurulmus bir cali, botlarin onunde gorunmez
        /// bir duvara donusur.
        /// </summary>
        private static void CountProps(StringBuilder sb)
        {
            Collider[] colliders =
                Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);

            int solid = 0;
            int triggers = 0;
            int bushes = 0;
            int bushWithoutTrigger = 0;

            foreach (Collider c in colliders)
            {
                bool isBush = c.CompareTag("Bush");

                if (isBush)
                    bushes++;

                if (c.isTrigger)
                    triggers++;
                else
                    solid++;

                if (isBush && !c.isTrigger)
                    bushWithoutTrigger++;
            }

            sb.AppendLine($"Collider: {solid} kati, {triggers} trigger, " +
                          $"{bushes} tanesi 'Bush' etiketli");

            if (bushWithoutTrigger > 0)
            {
                sb.AppendLine($"  UYARI: {bushWithoutTrigger} 'Bush' collider'i " +
                              "trigger DEGIL. Bunlar botlari ve mermileri " +
                              "durdurur, saklanma da calismaz.");
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Sahne goruntusundeki gizmolari topluca kapatir.
        ///
        /// NavMesh'in mavi ortusu prop yerlestirirken zemini kapatiyor ve
        /// agaci nereye koydugunu goremiyorsun. Sadece NavMesh'i gizlemenin
        /// yolu Gizmos menusundeki tek tik, ama o menu her Unity surumunde
        /// biraz farkli yerde; buradaki dugme her yerde calisir.
        ///
        /// Kapali gizmo hicbir seyi bozmaz, sadece gorunumdur; NavMesh
        /// verisi yerinde durur.
        /// </summary>
        private static void ToggleGizmos()
        {
            SceneView view = SceneView.lastActiveSceneView;

            if (view == null)
            {
                Debug.LogWarning("Acik bir Scene penceresi yok.");
                return;
            }

            view.drawGizmos = !view.drawGizmos;
            view.Repaint();

            Debug.Log("Sahne gizmolari: " + (view.drawGizmos ? "ACIK" : "KAPALI"));
        }

        private static NavMeshSurface FindSurface()
        {
            return Object.FindFirstObjectByType<NavMeshSurface>(
                FindObjectsInactive.Include);
        }

        private static string Path(Transform t)
        {
            string path = t.name;

            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        private static void MarkSceneDirty()
        {
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
