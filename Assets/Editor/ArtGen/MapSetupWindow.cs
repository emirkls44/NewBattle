using System.Collections.Generic;
using NewBattle.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Disaridan getirilen bir haritayi (GLB/FBX) oyuna baglar.
    ///
    /// Elle yapilinca unutulmasi kolay dort is var ve biri eksik kalirsa hata
    /// sessiz olur (oyuncu zeminden duser, loot havada durur, guvenli alan
    /// haritanin disinda kapanir):
    ///   1. Olcek - Sketchfab modelleri genelde devasa veya minik gelir
    ///   2. Collider - GLB dosyasi collider bilgisi tasimaz
    ///   3. Merkezleme - oyun sistemlerinin hepsi (0,0) merkezli varsayiyor
    ///   4. Yaricap esitleme - guvenli alan, loot, inis, kamera, hareket siniri
    ///
    /// Bu pencere dordunu tek dugmede yapar. glTFast'e bagimliligi yoktur:
    /// sahnedeki hazir GameObject uzerinde calisir, nasil import edildigi onemsiz.
    /// </summary>
    public class MapSetupWindow : EditorWindow
    {
        private GameObject _mapRoot;
        private float _playableRadius = 26f;
        private bool _autoScale = true;
        private bool _centerAtOrigin = true;
        private bool _addColliders = true;
        private bool _markStatic = true;
        private bool _disableOldArena = true;
        private bool _syncGameSystems = true;
        private float _manualScale = 1f;
        private bool _gateWorld = true;
        private float _characterHeight = 1.8f;
        private float _groundFootprintRatio = 0.35f;

        /// <summary>Metrekare basina cali. 29 m'lik arenada 26 cali ~0.0098 idi.</summary>
        private float _grassPerSquareMeter = 0.0045f;

        /// <summary>
        /// Metrekare basina loot. Eski ayar ~0.0128 idi ve haritada dolasirken
        /// "hicbir sey bulamiyorum" hissi veriyordu. 0.03 kabaca her 33 m'de bir
        /// esya demek; 26 m yaricapli haritada ~64 esya eder.
        /// </summary>
        private float _lootPerSquareMeter = 0.03f;

        private Bounds _measuredBounds;
        private bool _hasMeasurement;
        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/Harita Kur (GLB/FBX)", false, 5)]
        public static void Open()
        {
            MapSetupWindow window = GetWindow<MapSetupWindow>("Harita Kur");
            window.minSize = new Vector2(380f, 460f);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Harita Kurulumu", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Once .glb dosyasini sahneye surukle, sonra o nesneyi asagiya birak.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _mapRoot = (GameObject)EditorGUILayout.ObjectField(
                "Harita Nesnesi", _mapRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
                Measure();

            if (_mapRoot == null)
            {
                EditorGUILayout.HelpBox("Sahnedeki harita nesnesini sec.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawMeasurement();
            EditorGUILayout.Space(8f);
            DrawOptions();
            EditorGUILayout.Space(12f);

            if (GUILayout.Button("HARITAYI KUR", GUILayout.Height(34f)))
                Apply();

            EditorGUILayout.EndScrollView();
        }

        #region Olcum

        private void Measure()
        {
            _hasMeasurement = false;

            if (_mapRoot == null)
                return;

            Renderer[] renderers = _mapRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            _measuredBounds = bounds;
            _hasMeasurement = true;
        }

        private void DrawMeasurement()
        {
            if (!_hasMeasurement)
            {
                EditorGUILayout.HelpBox("Nesnede Renderer bulunamadi.", MessageType.Error);
                return;
            }

            Vector3 size = _measuredBounds.size;
            EditorGUILayout.LabelField("Mevcut Olcu", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Genislik (X): {size.x:0.0} m");
            EditorGUILayout.LabelField($"Derinlik (Z): {size.z:0.0} m");
            EditorGUILayout.LabelField($"Yukseklik (Y): {size.y:0.0} m");

            float shortAxis = Mathf.Min(size.x, size.z);
            EditorGUILayout.LabelField($"Kisa kenar: {shortAxis:0.0} m", EditorStyles.miniLabel);

            EditorGUILayout.HelpBox(
                "Oynanabilir alan bir DAIRE oldugu icin kisa kenar belirleyicidir; " +
                "cember uzun kenara sigsa bile kisa kenardan tasarsa oyuncu haritanin " +
                "disina cikar.",
                MessageType.None);
        }

        private float ComputeScale()
        {
            if (!_autoScale)
                return Mathf.Max(0.0001f, _manualScale);

            Vector3 size = _measuredBounds.size;
            float shortAxis = Mathf.Min(size.x, size.z);

            if (shortAxis < 0.001f)
                return 1f;

            // Cemberin cevresinde biraz pay birak: kenarda duvar dibinde sikismasin.
            float targetShortAxis = _playableRadius * 2f * 1.12f;
            return targetShortAxis / shortAxis;
        }

        #endregion

        #region Secenekler

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Ayarlar", EditorStyles.boldLabel);

            _playableRadius = EditorGUILayout.Slider("Oynanabilir Yaricap", _playableRadius, 15f, 300f);

            _autoScale = EditorGUILayout.Toggle("Otomatik olcekle", _autoScale);
            if (!_autoScale)
                _manualScale = EditorGUILayout.FloatField("Elle olcek", _manualScale);

            float scale = ComputeScale();
            EditorGUILayout.LabelField($"Uygulanacak olcek: {scale:0.000}x", EditorStyles.miniLabel);

            if (_hasMeasurement)
            {
                Vector3 finalSize = _measuredBounds.size * scale;
                EditorGUILayout.LabelField(
                    $"Sonuc: {finalSize.x:0} x {finalSize.z:0} m", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(6f);
            _centerAtOrigin = EditorGUILayout.Toggle("Merkeze tasi", _centerAtOrigin);
            _addColliders = EditorGUILayout.Toggle("Collider ekle", _addColliders);

            if (_addColliders)
            {
                EditorGUI.indentLevel++;
                _characterHeight = EditorGUILayout.Slider(
                    "Karakter boyu (m)", _characterHeight, 0.5f, 5f);
                EditorGUILayout.LabelField(
                    "Bu boydan kisa nesnelerin icinden gecilir.", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            _markStatic = EditorGUILayout.Toggle("Static isaretle", _markStatic);
            _disableOldArena = EditorGUILayout.Toggle("Eski arenayi kapat", _disableOldArena);
            _syncGameSystems = EditorGUILayout.Toggle("Oyun sistemlerini esitle", _syncGameSystems);
            _gateWorld = EditorGUILayout.Toggle("Menude gizle", _gateWorld);

            if (_gateWorld)
            {
                EditorGUILayout.LabelField(
                    "Harita ancak mod secilip odaya baglanilinca acilir.",
                    EditorStyles.miniLabel);
            }

            if (_syncGameSystems)
            {
                EditorGUI.indentLevel++;
                float area = Mathf.PI * _playableRadius * _playableRadius;
                _lootPerSquareMeter = EditorGUILayout.Slider(
                    "Loot yogunlugu", _lootPerSquareMeter, 0.004f, 0.08f);
                EditorGUILayout.LabelField(
                    $"~{Mathf.RoundToInt(area * _lootPerSquareMeter)} esya",
                    EditorStyles.miniLabel);

                _grassPerSquareMeter = EditorGUILayout.Slider(
                    "Cimen yogunlugu", _grassPerSquareMeter, 0.001f, 0.02f);
                EditorGUILayout.LabelField(
                    $"~{Mathf.RoundToInt(area * _grassPerSquareMeter)} cali",
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
        }

        #endregion

        #region Uygulama

        private void Apply()
        {
            if (_mapRoot == null || !_hasMeasurement)
                return;

            Undo.RegisterFullObjectHierarchyUndo(_mapRoot, "Harita Kur");

            float scale = ComputeScale();
            ApplyTransform(scale);

            int colliderCount = _addColliders ? AddColliders() : 0;

            if (_markStatic)
                MarkStatic();

            int disabled = _disableOldArena ? DisableLegacyArena() : 0;

            if (_syncGameSystems)
                SyncSystems();

            int gated = _gateWorld ? RegisterWithWorldGate() : 0;

            Measure();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log(
                $"Harita kuruldu. Olcek {scale:0.000}x, {colliderCount} collider eklendi, " +
                $"{disabled} eski arena bileseni kapatildi, " +
                $"{gated} harita mac kapisina baglandi. " +
                $"Oynanabilir yaricap {_playableRadius:0} m.\n" +
                "Sahneyi kaydet (Ctrl+S) ve Play'e bas.");
        }

        private void ApplyTransform(float scale)
        {
            Transform root = _mapRoot.transform;
            root.localScale *= scale;

            if (!_centerAtOrigin)
                return;

            // Olcekten sonra yeniden olc: merkezleme yeni boyuta gore yapilmali.
            Measure();

            // Yatayda merkeze al, dikeyde zemini y=0'a otur.
            Vector3 offset = new(-_measuredBounds.center.x, -_measuredBounds.min.y, -_measuredBounds.center.z);
            root.position += offset;
        }

        /// <summary>
        /// Collider'lari SECEREK ekler.
        ///
        /// Her mesh'e collider koymak iki sey bozar: oyuncu otlara, taslara,
        /// kutuklere, mantarlara takilip kalir (harita "yapis yapis" hissedilir)
        /// ve fizik motoru gereksiz yere binlerce sekil tasir.
        ///
        /// Kural: bir nesne ancak KARAKTER BOYUNDAN buyukse engel olur. Agaclar,
        /// buyuk kayalar, buyuk calilar takilir; kucuk her sey icinden gecilir.
        /// Zemin bu kuralin disindadir - yassi oldugu icin boy testini gecemez
        /// ama ustunde yurunmesi sart.
        /// </summary>
        private int AddColliders()
        {
            int added = 0;
            int skipped = 0;

            // Zemin tespiti icin: haritanin yatay boyutunun buyuk bir kismini
            // kaplayan her sey zemin sayilir.
            float mapShortAxis = Mathf.Min(_measuredBounds.size.x, _measuredBounds.size.z);
            float groundFootprint = mapShortAxis * _groundFootprintRatio;

            foreach (MeshFilter filter in _mapRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                // Onceki kurulumun biraktigi collider'lari temizle ki kurallar
                // degistiginde tekrar calistirmak ise yarasin.
                MeshCollider existing = filter.GetComponent<MeshCollider>();
                if (existing != null)
                    Undo.DestroyObjectImmediate(existing);

                if (filter.GetComponent<Collider>() != null)
                    continue;

                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                Vector3 size = renderer.bounds.size;
                float footprint = Mathf.Max(size.x, size.z);
                bool isGround = footprint >= groundFootprint;
                bool isTallEnough = size.y >= _characterHeight;

                if (!isGround && !isTallEnough)
                {
                    skipped++;
                    continue;
                }

                MeshCollider collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                added++;
            }

            Debug.Log(
                $"Collider: {added} engel kuruldu, {skipped} kucuk nesne " +
                $"gecilebilir birakildi (esik {_characterHeight:0.0} m).");

            return added;
        }

        private void MarkStatic()
        {
            foreach (Transform child in _mapRoot.GetComponentsInChildren<Transform>(true))
                child.gameObject.isStatic = true;
        }

        /// <summary>
        /// Eski prosedurel arenayi kapatir. Silmiyoruz: yeni harita beklenmedik bir
        /// sekilde bozulursa geri donebilmek icin sahnede kapali dursun.
        /// </summary>
        private int DisableLegacyArena()
        {
            int disabled = 0;

            PrototypeIslandBuilder builder =
                Object.FindFirstObjectByType<PrototypeIslandBuilder>(FindObjectsInactive.Include);

            if (builder != null && builder.gameObject.activeSelf)
            {
                Undo.RecordObject(builder.gameObject, "Harita Kur");
                builder.gameObject.SetActive(false);
                disabled++;
            }

            // Onceki calistirmalardan kalan uretilmis ada varsa o da kapansin.
            GameObject generated = GameObject.Find("GeneratedIsland") ?? GameObject.Find("Island");
            if (generated != null && generated != _mapRoot && !generated.transform.IsChildOf(_mapRoot.transform))
            {
                Undo.RecordObject(generated, "Harita Kur");
                generated.SetActive(false);
                disabled++;
            }

            return disabled;
        }

        /// <summary>
        /// Yaricapa bagli butun sistemleri yeni haritaya gore ayarlar.
        /// Faz 1'deki WorldScaleSync'in yaptigi isin aynisi, artik harita
        /// yaricapi bu pencereden geliyor.
        /// </summary>
        private void SyncSystems()
        {
            WorldScaleSync.Apply(_playableRadius);

            SyncMoveStateLimits();
            SyncGrassArea();
            SyncLootZones();
        }

        /// <summary>
        /// PlayerMoveState kendi arenaRadius'unu tutuyor; WorldScaleSync bunu
        /// bilmiyordu. Prefab uzerinde guncellenmezse oyuncu haritanin kenarina
        /// gorunmez bir duvara carpiyor.
        /// </summary>
        private void SyncMoveStateLimits()
        {
            const string playerPrefabPath = "Assets/Prefabs/PlayerOnline.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath) == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(playerPrefabPath);

            try
            {
                PlayerMoveState moveState = root.GetComponent<PlayerMoveState>();

                if (moveState != null)
                {
                    moveState.arenaRadius = _playableRadius * 1.02f;
                    moveState.arenaCenter = Vector2.zero;
                    PrefabUtility.SaveAsPrefabAsset(root, playerPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Haritayi MatchWorldGate'e kaydeder: menude gizli kalsin, oyuncu mod
        /// secip odaya baglanince acilsin.
        /// </summary>
        private int RegisterWithWorldGate()
        {
            MatchWorldGate gate = Object.FindFirstObjectByType<MatchWorldGate>(FindObjectsInactive.Include);

            if (gate == null)
            {
                // Kapiyi, oyunun zaten var olan ag nesnesinin yanina koyuyoruz;
                // basibos yeni bir nesne yaratmak sahneyi kirletir.
                DropPhaseController dropPhase =
                    Object.FindFirstObjectByType<DropPhaseController>(FindObjectsInactive.Include);

                GameObject host = dropPhase != null
                    ? dropPhase.gameObject
                    : new GameObject("MatchWorldGate");

                gate = Undo.AddComponent<MatchWorldGate>(host);
            }

            Undo.RecordObject(gate, "Harita kapisina kaydet");
            gate.RegisterWorldRoot(_mapRoot);

            // Cimen uretici de mac baslayana kadar beklesin.
            GrassPatchSpawner spawner =
                Object.FindFirstObjectByType<GrassPatchSpawner>(FindObjectsInactive.Include);

            if (spawner != null)
            {
                SerializedObject serialized = new(spawner);
                SerializedProperty wait = serialized.FindProperty("waitForMatch");
                if (wait != null)
                    wait.boolValue = true;
                serialized.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(gate);
            return 1;
        }

        private void SyncGrassArea()
        {
            GrassPatchSpawner spawner =
                Object.FindFirstObjectByType<GrassPatchSpawner>(FindObjectsInactive.Include);

            if (spawner == null)
                return;

            SerializedObject serialized = new(spawner);
            serialized.FindProperty("areaRadius").floatValue = _playableRadius * 0.92f;

            // Cali sayisi alanla orantili, AMA yogunluk sabit kalmali.
            // Onceki formul 29 m'lik arenanin 26 caliyi alan basina koruyordu;
            // gercek ormanda bu "her adimda cali" demek oluyordu. Gizlenme ozel
            // bir firsat olmali, varsayilan durum degil - yogunlugu dusurduk.
            float area = Mathf.PI * _playableRadius * _playableRadius;
            int patches = Mathf.RoundToInt(area * _grassPerSquareMeter);
            SerializedProperty count = serialized.FindProperty("patchCount");
            count.intValue = Mathf.Clamp(patches, 10, 400);

            // Calilar arasi bosluk da olcekle buyusun; yoksa buyuk haritada
            // hepsi birbirine yapisik tek bir yesil leke olur.
            SerializedProperty spacing = serialized.FindProperty("minSpacing");
            if (spacing != null)
                spacing.floatValue = Mathf.Clamp(_playableRadius * 0.16f, 4.5f, 14f);

            serialized.ApplyModifiedProperties();
        }

        private void SyncLootZones()
        {
            LootZoneManager manager =
                Object.FindFirstObjectByType<LootZoneManager>(FindObjectsInactive.Include);

            if (manager == null)
                return;

            SerializedObject serialized = new(manager);
            serialized.FindProperty("mapRadiusOverride").floatValue = _playableRadius;

            // Bolge boyutu haritayla birlikte buyumeli, yoksa bolge sayisi patlar.
            float zoneSize = Mathf.Clamp(_playableRadius / 2.4f, 10f, 30f);
            serialized.FindProperty("zoneSize").floatValue = zoneSize;
            // Aktivasyon yaricapi EKRANDAN buyuk olmali: loot oyuncunun goruntusune
            // girmeden once spawn olmali, yoksa esyalar gozunun onunde beliriyor.
            // Ortografik kamera ~23 m yukseklik gosteriyor, yani ~12 m yaricap.
            serialized.FindProperty("activationRadius").floatValue =
                Mathf.Max(zoneSize * 1.8f, 20f);

            // KRITIK: bolge basina loot sayisi da bolge ALANIYLA buyumeli.
            // Onceki surum zoneSize'i buyutup lootPerZone'u 1-2'de birakiyordu;
            // sonuc, 2.5 kat buyuyen haritada ayni sayida loot - yani oyuncunun
            // "loot cok az" diye hissettigi sey tam olarak buydu.
            float zoneArea = zoneSize * zoneSize;
            float expected = zoneArea * _lootPerSquareMeter;
            int minLoot = Mathf.Max(1, Mathf.FloorToInt(expected * 0.75f));
            int maxLoot = Mathf.Max(minLoot + 1, Mathf.CeilToInt(expected * 1.3f));

            SerializedProperty perZone = serialized.FindProperty("lootPerZone");
            if (perZone != null)
                perZone.vector2IntValue = new Vector2Int(minLoot, maxLoot);

            serialized.ApplyModifiedProperties();

            Debug.Log(
                $"Loot: bolge {zoneSize:0} m, bolge basina {minLoot}-{maxLoot} esya " +
                $"(haritanin tamaminda kabaca {Mathf.RoundToInt(Mathf.PI * _playableRadius * _playableRadius * _lootPerSquareMeter)} esya).");
        }

        #endregion
    }
}
