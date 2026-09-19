using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Modelleri haritaya konmaya HAZIR prefab'lara cevirir.
    ///
    /// Neden modeli dogrudan sahneye surukleyip birakmiyoruz: bir agaci
    /// 300 kez koyduktan sonra collider'inin yanlis oldugunu fark edersen
    /// 300 kopyayi tek tek duzeltmen gerekir. Prefab'da tek yerde duzeltip
    /// hepsine yansitiyorsun.
    ///
    /// Uretilen yapi iki katli:
    ///
    ///   Agac            <- prefab koku: collider, etiket, static bayraklari
    ///     ModelAdi      <- modelin kendisi, bagli (nested) olarak duruyor
    ///
    /// Modeli ayri bir cocukta tutmanin sebebi: modeli yeniden import
    /// edince ya da degistirince prefab kokundeki ayarlar bozulmuyor.
    /// Ayrica pivot yanlissa cocugu kaydirarak duzeltebiliyorsun, kok
    /// hep zeminde kaliyor.
    ///
    /// MATERYAL VE RENKLERE DOKUNMUYOR. Onlar senin isin.
    /// </summary>
    public class PropPrefabBuilder : EditorWindow
    {
        private const string OutputFolder = "Assets/Prefabs/Props";

        /// <summary>
        /// Disaridan gelen modellerin durdugu klasor. Tek dugmeyle tarama
        /// buradan yapiliyor, boylece Project penceresinde dosya aramaya
        /// gerek kalmiyor.
        /// </summary>
        private const string ImportFolder = "Assets/GameArt/Imported/Props";

        /// <summary>Prop'un oyundaki rolu. Collider ve etiketi bu belirliyor.</summary>
        private enum PropKind
        {
            /// <summary>Ev, kaya, duvar. Botu da mermiyi de durdurur.</summary>
            Solid = 0,

            /// <summary>Cali, uzun ot. Trigger; gecilir, icinde gizlenilir.</summary>
            Bush = 1,

            /// <summary>Cicek, kucuk tas. Collider yok, sadece gorsel.</summary>
            Decor = 2,
        }

        private enum ColliderShape
        {
            /// <summary>Olculere bakip kutu mu kapsul mu oldugunu kendi secer.</summary>
            Auto = 0,
            Box = 1,
            Capsule = 2,
        }

        /// <summary>Aracin ne yapacagi.</summary>
        private enum Mode
        {
            /// <summary>Modelden sifirdan yeni bir prefab uretir.</summary>
            NewPrefab = 0,

            /// <summary>
            /// Hazir bir prefab'i YERINDE duzenler: collider, etiket ve
            /// golge ayarlarini ekler, dosyayi ayni yere yazar.
            ///
            /// Haritaya zaten yuzlerce kopya konduktan sonra tek care bu:
            /// kaynak prefab degisince butun kopyalar otomatik aliyor,
            /// yeniden dagitmak gerekmiyor.
            /// </summary>
            InPlace = 1,
        }

        private Mode _mode = Mode.NewPrefab;
        private bool _overwriteCollider;
        private PropKind _kind = PropKind.Solid;
        private ColliderShape _shape = ColliderShape.Auto;
        private bool _castShadows = true;
        private bool _batchingStatic = true;

        /// <summary>
        /// Collider'i gorsele gore biraz daraltir.
        ///
        /// Agacin dallari genis ama govdesi ince; collider'i dis olculere
        /// esitlersen oyuncu agaca 2 metre uzaktan carpar ve bu cok kotu
        /// hissettirir. Battlelands'te de carpisma govdeye yakindir.
        /// </summary>
        private float _colliderShrink = 0.7f;

        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/Prop Kutuphanesi")]
        private static void Open()
        {
            GetWindow<PropPrefabBuilder>("Prop Kutuphanesi").minSize =
                new Vector2(430f, 380f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            _mode = (Mode)EditorGUILayout.EnumPopup("Calisma sekli", _mode);

            if (_mode == Mode.NewPrefab)
            {
                EditorGUILayout.HelpBox(
                    "Project penceresinden bir ya da birden fazla model sec " +
                    "(FBX / OBJ / prefab), ayarlari sec, Kur'a bas.\n\n" +
                    "Uretilenler: " + OutputFolder,
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Hazir prefab'lari secip collider ekler ve DOSYAYI YERINDE " +
                    "degistirir.\n\n" +
                    "Haritada o prefab'in kac kopyasi varsa hepsi otomatik " +
                    "collider kazanir; yeniden dagitmana gerek kalmaz.\n\n" +
                    "FBX/OBJ dosyalari yerinde degistirilemez, onlar icin " +
                    "'NewPrefab' kullan.",
                    MessageType.Warning);

                _overwriteCollider = EditorGUILayout.Toggle(
                    new GUIContent("Mevcut collider'i degistir",
                        "Kapaliyken collider'i olan prefab atlanir."),
                    _overwriteCollider);
            }

            EditorGUILayout.Space(6f);

            _kind = (PropKind)EditorGUILayout.EnumPopup("Tur", _kind);

            switch (_kind)
            {
                case PropKind.Solid:
                    EditorGUILayout.HelpBox(
                        "Kati engel: normal collider. Bot etrafindan dolasir, " +
                        "mermi takilir.", MessageType.None);
                    break;

                case PropKind.Bush:
                    EditorGUILayout.HelpBox(
                        "Cali: trigger collider + 'Bush' etiketi. Bot da mermi " +
                        "de gecer; icine giren oyuncu gizlenir.", MessageType.None);
                    break;

                case PropKind.Decor:
                    EditorGUILayout.HelpBox(
                        "Dekor: collider yok. Hicbir seyi engellemez, " +
                        "fizik maliyeti de yok.", MessageType.None);
                    break;
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(_kind == PropKind.Decor))
            {
                _shape = (ColliderShape)EditorGUILayout.EnumPopup("Collider sekli", _shape);

                _colliderShrink = EditorGUILayout.Slider(
                    new GUIContent("Collider daraltma",
                        "1 = gorselin tam olcusu. 0.7 = govdeye yakin."),
                    _colliderShrink, 0.2f, 1f);
            }

            EditorGUILayout.Space(6f);

            _castShadows = EditorGUILayout.Toggle(
                new GUIContent("Golge dussun",
                    "Kucuk propta kapat. Mobilde yuzlerce golge kare hizini yariya boler."),
                _castShadows);

            _batchingStatic = EditorGUILayout.Toggle(
                new GUIContent("Static (batching)",
                    "Hareket etmeyen her prop icin acik olmali; cizim cagrisini dusurur."),
                _batchingStatic);

            EditorGUILayout.Space(10f);

            GameObject[] models = SelectedModels();

            GameObject[] pending = PendingImports();

            using (new EditorGUI.DisabledScope(pending.Length == 0))
            {
                if (GUILayout.Button(
                        $"Yeni gelen modelleri prefab yap ({pending.Length})",
                        GUILayout.Height(34f)))
                {
                    BuildAll(pending, forceNew: true);
                }
            }

            EditorGUILayout.LabelField(
                ImportFolder + " icinde prefab'i olmayan modeller.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(10f);

            string verb = _mode == Mode.NewPrefab ? "Kur" : "Collider Ekle";

            using (new EditorGUI.DisabledScope(models.Length == 0))
            {
                if (GUILayout.Button($"{verb} ({models.Length} secili)", GUILayout.Height(34f)))
                    BuildAll(models, forceNew: false);
            }

            if (models.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Project penceresinden model sec.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("Secili:", EditorStyles.boldLabel);

                foreach (GameObject m in models)
                    EditorGUILayout.LabelField("  " + m.name);
            }

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Secimden yalniz DISK UZERINDEKI model/prefab varliklarini ayiklar.
        ///
        /// Sahnedeki nesneleri disarida birakiyoruz: sahneden prefab uretmek
        /// istenirse o ayri bir is akisi ve kazara calismasi karisiklik
        /// yaratir.
        /// </summary>
        private static GameObject[] SelectedModels()
        {
            List<GameObject> found = new();

            foreach (Object o in Selection.objects)
            {
                if (o is not GameObject go)
                    continue;

                if (!AssetDatabase.Contains(go))
                    continue;

                found.Add(go);
            }

            return found.ToArray();
        }

        /// <summary>
        /// Prefab'i henuz uretilmemis modelleri bulur.
        ///
        /// Zaten prefab'i olanlari listeye almiyoruz: dugmenin yanindaki
        /// sayi "kac is kaldi" demek olmali, yoksa her basista ayni
        /// modeller icin "zaten var, atlandi" satirlari dolar.
        /// </summary>
        private static GameObject[] PendingImports()
        {
            if (!AssetDatabase.IsValidFolder(ImportFolder))
                return System.Array.Empty<GameObject>();

            string[] guids = AssetDatabase.FindAssets(
                "t:Model", new[] { ImportFolder });

            List<GameObject> pending = new();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (model == null)
                    continue;

                string target = $"{OutputFolder}/{CleanName(model.name)}.prefab";

                if (AssetDatabase.LoadAssetAtPath<GameObject>(target) != null)
                    continue;

                pending.Add(model);
            }

            return pending.ToArray();
        }

        private void BuildAll(GameObject[] models, bool forceNew)
        {
            EnsureFolder(OutputFolder);

            StringBuilder log = new();
            log.AppendLine("=== PROP KURULUMU ===");

            bool newPrefab = forceNew || _mode == Mode.NewPrefab;
            int made = 0;

            foreach (GameObject model in models)
            {
                string path = newPrefab
                    ? Build(model, log)
                    : PatchInPlace(model, log);

                if (path != null)
                    made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine($"\n{made}/{models.Length} prefab uretildi.");
            log.AppendLine("Simdi 'Harita Navigasyonu > NavMesh Uret' demeyi unutma; " +
                           "yoksa botlar yeni engelleri gormez.");

            Debug.Log(log.ToString());
        }

        private string Build(GameObject model, StringBuilder log)
        {
            string name = CleanName(model.name);
            string path = $"{OutputFolder}/{name}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                log.AppendLine($"  {name}: zaten var, atlandi.");
                return null;
            }

            GameObject root = new(name);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);

            if (visual == null)
            {
                log.AppendLine($"  {name}: model ornegi olusturulamadi.");
                Object.DestroyImmediate(root);
                return null;
            }

            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            Bounds bounds = LocalBounds(root);

            if (bounds.size == Vector3.zero)
            {
                log.AppendLine($"  {name}: renderer bulunamadi, olcu alinamadi.");
                Object.DestroyImmediate(root);
                return null;
            }

            // Koku modelin TABANINA tasiyoruz. Yerlestirme araci prop'u
            // zemine oturturken kokun ayak hizasinda olmasina guveniyor;
            // pivot govdenin ortasindaysa agac yariya kadar gomulu cikar.
            float bottom = bounds.min.y;
            visual.transform.localPosition = new Vector3(0f, -bottom, 0f);
            bounds.center -= new Vector3(0f, bottom, 0f);

            ApplyCollider(root, bounds);
            ApplyRendererSettings(root);
            ApplyTagAndStatic(root);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            log.AppendLine($"  {name}: {_kind}, olcu " +
                           $"{bounds.size.x:0.00} x {bounds.size.y:0.00} x " +
                           $"{bounds.size.z:0.00}");

            return path;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Hazir bir prefab dosyasini yerinde duzenler.
        ///
        /// LoadPrefabContents prefab'i gorunmez bir sahnede aciyor; orada
        /// degistirip ayni yola geri yaziyoruz. Sahnedeki kopyalara hic
        /// dokunmuyoruz - onlar prefab'i takip ettigi icin degisikligi
        /// kendiliginden aliyorlar.
        ///
        /// Konum, donus ve olcu HIC degismiyor: yuzlerce agaci yerinden
        /// oynatmak butun haritayi bozardi.
        /// </summary>
        private string PatchInPlace(GameObject asset, StringBuilder log)
        {
            string path = AssetDatabase.GetAssetPath(asset);

            if (string.IsNullOrEmpty(path))
            {
                log.AppendLine($"  {asset.name}: disk uzerinde degil, atlandi.");
                return null;
            }

            if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Model)
            {
                log.AppendLine($"  {asset.name}: FBX/OBJ yerinde degistirilemez. " +
                               "'NewPrefab' modunu kullan.");
                return null;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                Collider existing = root.GetComponent<Collider>();

                if (existing != null && !_overwriteCollider)
                {
                    log.AppendLine($"  {asset.name}: collider'i zaten var, atlandi.");
                    return null;
                }

                if (existing != null)
                {
                    foreach (Collider c in root.GetComponents<Collider>())
                        Object.DestroyImmediate(c, true);
                }

                Bounds bounds = LocalBounds(root);

                if (bounds.size == Vector3.zero)
                {
                    log.AppendLine($"  {asset.name}: renderer yok, olcu alinamadi.");
                    return null;
                }

                ApplyCollider(root, bounds);
                ApplyRendererSettings(root);
                ApplyTagAndStatic(root);

                PrefabUtility.SaveAsPrefabAsset(root, path);

                log.AppendLine($"  {asset.name}: {_kind}, olcu " +
                               $"{bounds.size.x:0.00} x {bounds.size.y:0.00} x " +
                               $"{bounds.size.z:0.00}");

                return path;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------

        /// <summary>Butun renderer'lari kapsayan, koke gore olculmus kutu.</summary>
        private static Bounds LocalBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Kok orijinde ve donmemis oldugu icin dunya olculeri dogrudan
            // yerel olculere esit.
            bounds.center -= root.transform.position;
            return bounds;
        }

        private void ApplyCollider(GameObject root, Bounds bounds)
        {
            if (_kind == PropKind.Decor)
                return;

            float width = Mathf.Max(bounds.size.x, bounds.size.z) * _colliderShrink;
            float height = bounds.size.y;

            ColliderShape shape = _shape;

            if (shape == ColliderShape.Auto)
            {
                // Ince ve uzun olan her sey kapsul: agac govdesi, varil,
                // direk. Kapsul hem daha ucuz hem de kose takilmasi yapmaz.
                shape = height > width * 1.6f ? ColliderShape.Capsule : ColliderShape.Box;
            }

            // Merkezi bounds'tan aliyoruz, "taban sifirda" varsayimindan
            // degil: yerinde duzenleme modunda prefab'in pivotu nerede
            // olursa olsun collider gorselin uzerine oturmali.
            if (shape == ColliderShape.Capsule)
            {
                CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = Mathf.Max(0.05f, width * 0.5f);
                capsule.height = Mathf.Max(capsule.radius * 2f, height);
                capsule.center = bounds.center;
                capsule.isTrigger = _kind == PropKind.Bush;
            }
            else
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(
                    Mathf.Max(0.05f, bounds.size.x * _colliderShrink),
                    Mathf.Max(0.05f, height),
                    Mathf.Max(0.05f, bounds.size.z * _colliderShrink));
                box.center = bounds.center;
                box.isTrigger = _kind == PropKind.Bush;
            }
        }

        private void ApplyRendererSettings(GameObject root)
        {
            UnityEngine.Rendering.ShadowCastingMode mode = _castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = mode;
        }

        private void ApplyTagAndStatic(GameObject root)
        {
            if (_kind == PropKind.Bush)
                root.tag = "Bush";

            if (!_batchingStatic)
                return;

            // ContributeGI bilerek yok: isik haritasi pisirmiyoruz, acik
            // olsaydi her bake'te gereksiz is cikarirdi.
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic;

            GameObjectUtility.SetStaticEditorFlags(root, flags);

            foreach (Transform child in root.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Meshy AI gibi uretecilerin uzun dosya adlarini kisaltir.
        ///
        /// "Meshy_AI_tree_bush_3d_0917235249_image-to-3d-texture" gibi bir
        /// adla hiyerarside calismak mumkun degil.
        /// </summary>
        private static string CleanName(string raw)
        {
            string name = raw;

            foreach (string junk in new[]
                     {
                         "Meshy_AI_", "_image-to-3d-texture", "_texture",
                         "_3d", "-to-3d",
                     })
            {
                name = name.Replace(junk, string.Empty);
            }

            // Sondaki uretim numarasini at: "_0917235249"
            string[] parts = name.Split('_');

            if (parts.Length > 1 && parts[^1].Length >= 8 &&
                long.TryParse(parts[^1], out _))
            {
                name = string.Join("_", parts, 0, parts.Length - 1);
            }

            name = name.Trim('_', '-', ' ');
            return string.IsNullOrEmpty(name) ? raw : name;
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
    }
}
