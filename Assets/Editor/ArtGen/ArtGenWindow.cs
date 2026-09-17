using UnityEditor;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Tum prosedurel sanat uretiminin tek giris noktasi.
    /// Tools > NewBattle > Art Generator menusunden acilir.
    ///
    /// Buradaki her buton idempotenttir: tekrar basmak asset'leri yerinde gunceller,
    /// GUID'leri korur ve sahnedeki referanslari bozmaz. Bu sayede "agaclar seyrek kalmis"
    /// gibi bir geri bildirim, slider'i oynatip tekrar basmakla cozulur.
    /// </summary>
    public class ArtGenWindow : EditorWindow
    {
        private IslandFactory.IslandSettings _island = new();
        private bool _replaceSceneIsland = true;
        private bool _syncWorldScale = true;
        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/Art Generator", false, 0)]
        public static void Open()
        {
            ArtGenWindow window = GetWindow<ArtGenWindow>("Art Generator");
            window.minSize = new Vector2(360f, 480f);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("NewBattle - Prosedurel Sanat Ureteci", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Uretilen her sey Assets/GameArt altina yazilir.\n" +
                "Butun mesh'ler tek bir toon materyalini paylasir (vertex color), " +
                "bu yuzden mobilde draw call sayisi dusuk kalir.",
                MessageType.Info);

            EditorGUILayout.Space(8f);
            DrawCharacterSection();

            EditorGUILayout.Space(8f);
            DrawLootSection();

            EditorGUILayout.Space(8f);
            DrawIslandSection();

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("HEPSINI URET", GUILayout.Height(34f)))
                GenerateAll();

            EditorGUILayout.EndScrollView();
        }

        #region Bolumler

        private void DrawCharacterSection()
        {
            EditorGUILayout.LabelField("1. Karakterler", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Ranger ve Commando - humanoid rig + Avatar", EditorStyles.miniLabel);

            if (GUILayout.Button("Karakterleri Uret"))
                GenerateCharacters();
        }

        private void DrawLootSection()
        {
            EditorGUILayout.LabelField("2. Loot ve Silahlar", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mermi, kucuk/buyuk kalkan, medkit, 6 silah, airdrop kutusu",
                EditorStyles.miniLabel);

            if (GUILayout.Button("Loot Modellerini Uret"))
                GenerateLoot();
        }

        private void DrawIslandSection()
        {
            EditorGUILayout.LabelField("3. Ada", EditorStyles.boldLabel);

            _island.Seed = EditorGUILayout.IntField("Seed", _island.Seed);
            _island.PlayableRadius = EditorGUILayout.Slider("Oynanabilir Yaricap", _island.PlayableRadius, 60f, 400f);
            _island.BeachWidth = EditorGUILayout.Slider("Kum Seridi", _island.BeachWidth, 5f, 60f);
            _island.MaxHeight = EditorGUILayout.Slider("Tepe Yuksekligi", _island.MaxHeight, 2f, 25f);
            _island.ChunkCount = EditorGUILayout.IntSlider("Arazi Parcasi", _island.ChunkCount, 2, 10);
            _island.CellsPerChunk = EditorGUILayout.IntSlider("Parca Cozunurlugu", _island.CellsPerChunk, 8, 48);

            EditorGUILayout.Space(4f);
            _island.TreeDensity = EditorGUILayout.Slider("Agac Yogunlugu", _island.TreeDensity, 0f, 3f);
            _island.BushDensity = EditorGUILayout.Slider("Cali / Ot Yogunlugu", _island.BushDensity, 0f, 3f);
            _island.RockDensity = EditorGUILayout.Slider("Kaya Yogunlugu", _island.RockDensity, 0f, 3f);

            EditorGUILayout.Space(4f);
            _replaceSceneIsland = EditorGUILayout.Toggle("Sahnedeki adayi degistir", _replaceSceneIsland);

            EditorGUILayout.HelpBox(
                $"Yaricap {_island.PlayableRadius:0} m. Ada uretildikten sonra guvenli alan, loot " +
                "dagitimi, inis fazi ve kamera siniri bu yaricapa gore otomatik guncellenir.",
                MessageType.None);

            _syncWorldScale = EditorGUILayout.Toggle("Oyun sistemlerini esitle", _syncWorldScale);

            if (GUILayout.Button("Adayi Uret"))
                GenerateIsland();

            if (GUILayout.Button("Sadece Yaricaplari Esitle"))
                WorldScaleSync.Apply(_island.PlayableRadius);
        }

        #endregion

        #region Uretim

        private void GenerateAll()
        {
            GenerateCharacters();
            GenerateLoot();
            GenerateIsland();
        }

        private static Material PrepareCommonAssets()
        {
            ArtGenIO.EnsureFolders();
            ArtGenTags.EnsureTags();
            return ArtGenIO.GetOrCreateToonMaterial("ToonWorld");
        }

        private void GenerateCharacters()
        {
            Material material = PrepareCommonAssets();

            // Not: burada AssetDatabase.StartAssetEditing kullanmiyoruz. Prefab ve Avatar
            // kaydederken "olustur, sonra geri yukle" adimi var; toplu duzenleme modunda
            // yeni olusturulan asset geri yuklenemeyip sessizce kopya uretebiliyor.
            foreach (CharacterFactory.CharacterStyle style in CharacterFactory.DefaultRoster())
            {
                GameObject instance = CharacterFactory.Generate(style, material);
                ArtGenIO.SavePrefab(instance, $"Character_{style.Name}", "Characters");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("ArtGen: Karakterler uretildi -> " + ArtGenIO.PrefabFolder + "/Characters");
        }

        private void GenerateLoot()
        {
            Material material = PrepareCommonAssets();

            {
                foreach (LootFactory.LootKind kind in System.Enum.GetValues(typeof(LootFactory.LootKind)))
                {
                    Mesh mesh = ArtGenIO.SaveMesh(LootFactory.Build(kind), $"Loot_{kind}", "Loot");
                    GameObject instance = ArtGenIO.CreateMeshObject($"Loot_{kind}", mesh, material);

                    // Parasut ve airdrop kutusu disindaki her sey yerde duran, hafifce
                    // salinan bir pickup gorselidir.
                    bool isPickup = kind != LootFactory.LootKind.Parachute
                                    && kind != LootFactory.LootKind.AirdropCrate;

                    if (isPickup)
                    {
                        instance.AddComponent<LootBob>();
                        instance.tag = ArtGenTags.LootTag;
                    }

                    ArtGenIO.SavePrefab(instance, $"Loot_{kind}", "Loot");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("ArtGen: Loot modelleri uretildi -> " + ArtGenIO.PrefabFolder + "/Loot");
        }

        private void GenerateIsland()
        {
            Material material = PrepareCommonAssets();
            GameObject island;

            try
            {
                AssetDatabase.StartAssetEditing();
                island = IslandFactory.Generate(_island, material);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (_replaceSceneIsland)
            {
                // Eski prosedurel adayi sahneden kaldir; ayni anda iki ada olmasin.
                GameObject previous = GameObject.Find("Island");
                if (previous != null && previous != island)
                    DestroyImmediate(previous);

                Undo.RegisterCreatedObjectUndo(island, "Ada Uretildi");
                Selection.activeGameObject = island;
                EditorGUIUtility.PingObject(island);
            }
            else
            {
                ArtGenIO.SavePrefab(island, "Island", "World");
            }

            if (_syncWorldScale)
                WorldScaleSync.Apply(_island.PlayableRadius);

            Debug.Log($"ArtGen: Ada uretildi. Oynanabilir yaricap {_island.PlayableRadius:0} m.");
        }

        #endregion
    }
}
