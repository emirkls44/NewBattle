#if UNITY_EDITOR
using System;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NewBattle.EditorTools
{
    /// <summary>ASAMA 4-5: "AKSIYONA HAZIRLAN" harita secim ekraninin kurulumu.</summary>
    public static partial class MainMenuBuilder
    {
        private const string DropScreenName = "HaritaSecimEkrani";
        private const string DropMapFolder = "Assets/GameArt/DropMap";
        private const string DropMapDataPath = DropMapFolder + "/HaritaVerisi.asset";
        private const string DropMapTexturePath = DropMapFolder + "/harita_gecici.png";
        private const string DropMapRegionsPath = DropMapFolder + "/harita_bolgeleri.json";

        private const int SlotColumns = 16;
        private const int SlotRows = 2;
        private const float SlotSize = 42f;
        private const float SlotSpacing = 5f;

        private static readonly Color WaitingGreen = Hex("#B8E06A");

        [Serializable]
        private class RegionJson
        {
            public string id;
            public string name;
            public float x, y, w, h;
        }

        [Serializable]
        private class RegionListJson
        {
            public RegionJson[] regions;
        }

        /// <summary>Ilk surumde olan Geri butonunu kaldirir (oyuncu bu ekrandan cikamaz).</summary>
        private static bool RemoveDropBackButton(Canvas canvas)
        {
            DropSelectionScreen screen = canvas.GetComponentInChildren<DropSelectionScreen>(true);
            Transform back = screen != null ? screen.transform.Find("GeriButonu") : null;
            if (back == null)
                return false;

            Undo.DestroyObjectImmediate(back.gameObject);
            return true;
        }

        private static bool EnsureDropScreen(Canvas canvas, bool rebuild)
        {
            DropSelectionScreen existing = canvas.GetComponentInChildren<DropSelectionScreen>(true);
            if (existing != null && !rebuild)
                return false;

            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            DisableLegacyDropSelection(canvas);
            BuildDropScreen(canvas.transform, EnsureDropMapData());
            return true;
        }

        /// <summary>Son geri sayimi (oda dolduktan sonra) ayarlar. Degistiyse true.</summary>
        private static bool SetFinalCountdownSeconds(float seconds)
        {
            LobbyCountdownController lobby = Object.FindFirstObjectByType<LobbyCountdownController>(FindObjectsInactive.Include);
            if (lobby == null)
                return false;

            var serialized = new SerializedObject(lobby);
            SerializedProperty property = serialized.FindProperty("finalCountdownSeconds");
            if (Mathf.Approximately(property.floatValue, seconds))
                return false;

            property.floatValue = seconds;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private static void DisableLegacyDropSelection(Canvas canvas)
        {
            foreach (DropSelectionHUD legacy in Object.FindObjectsByType<DropSelectionHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(legacy, "Eski harita secimini kapat");
                legacy.enabled = false;
            }

            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "DropSelection")
                    continue;

                Undo.RecordObject(child.gameObject, "Eski harita secimini kapat");
                child.gameObject.name = "DropSelection_Eski";
                child.gameObject.SetActive(false);
            }
        }

        private static DropMapData EnsureDropMapData()
        {
            var data = AssetDatabase.LoadAssetAtPath<DropMapData>(DropMapDataPath);
            if (data != null)
                return data;

            AssetDatabase.ImportAsset(DropMapFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            data = ScriptableObject.CreateInstance<DropMapData>();
            AssetDatabase.CreateAsset(data, DropMapDataPath);

            var serialized = new SerializedObject(data);
            serialized.FindProperty("mapTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(DropMapTexturePath);

            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(DropMapRegionsPath);
            RegionListJson list = json != null ? JsonUtility.FromJson<RegionListJson>(json.text) : null;
            SerializedProperty regions = serialized.FindProperty("regions");
            regions.arraySize = 0;

            if (list?.regions != null)
            {
                foreach (RegionJson region in list.regions)
                {
                    regions.arraySize++;
                    SerializedProperty item = regions.GetArrayElementAtIndex(regions.arraySize - 1);
                    item.FindPropertyRelative("id").stringValue = region.id;
                    item.FindPropertyRelative("name").stringValue = region.name;
                    item.FindPropertyRelative("area").rectValue = new Rect(region.x, region.y, region.w, region.h);
                }
            }
            else
            {
                Debug.LogWarning($"Harita: {DropMapRegionsPath} okunamadi; bolge listesi bos.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void BuildDropScreen(Transform canvas, DropMapData data)
        {
            RectTransform root = NewRect(DropScreenName, canvas);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Harita secim ekrani");
            Stretch(root, 0f, 0f, 0f, 0f);

            // Oyun arayuzunun (HUD) ustunde, yukleme ekraninin altinda.
            Canvas screenCanvas = root.gameObject.AddComponent<Canvas>();
            screenCanvas.overrideSorting = true;
            screenCanvas.sortingOrder = 900;
            root.gameObject.AddComponent<GraphicRaycaster>();

            RectTransform background = NewRect("Arkaplan", root);
            Stretch(background, 0f, 0f, 0f, 0f);
            AddImage(background, Sprite("bg_ocean"), Color.white, raycast: true).type = Image.Type.Simple;

            for (int i = 0; i < 2; i++)
            {
                Image cloud = AddIcon(background, $"Bulut{i + 1}", "cloud", new Color(1f, 1f, 1f, 0.35f),
                    new Vector2(0.5f, 0.5f), new Vector2(-500f + i * 700f, i == 0 ? -330f : 360f), 260f);
                cloud.gameObject.AddComponent<UIDrift>();
            }

            var refs = new DropRefs();
            BuildDropMap(root, data, refs);
            BuildDropLeftColumn(root, refs);

            DropSelectionScreen screen = root.gameObject.AddComponent<DropSelectionScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("screenCanvas").objectReferenceValue = screenCanvas;
            serialized.FindProperty("mapData").objectReferenceValue = data;
            serialized.FindProperty("mapImage").objectReferenceValue = refs.Map;
            serialized.FindProperty("marker").objectReferenceValue = refs.Marker;
            serialized.FindProperty("markerPin").objectReferenceValue = refs.Pin;
            serialized.FindProperty("regionLabel").objectReferenceValue = refs.Label;
            serialized.FindProperty("regionLabelText").objectReferenceValue = refs.LabelText;
            serialized.FindProperty("modeIcon").objectReferenceValue = refs.ModeIcon;
            serialized.FindProperty("modeText").objectReferenceValue = refs.ModeText;
            serialized.FindProperty("soloIcon").objectReferenceValue = Sprite("icon_person");
            serialized.FindProperty("duoIcon").objectReferenceValue = Sprite("icon_friends");
            serialized.FindProperty("slotsRoot").objectReferenceValue = refs.Slots;
            serialized.FindProperty("waitingText").objectReferenceValue = refs.Waiting;
            serialized.FindProperty("countdownGroup").objectReferenceValue = refs.Countdown;
            serialized.FindProperty("countdownText").objectReferenceValue = refs.CountdownText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var tapSerialized = new SerializedObject(refs.Map.GetComponent<DropMapTapArea>());
            tapSerialized.FindProperty("screen").objectReferenceValue = screen;
            tapSerialized.ApplyModifiedPropertiesWithoutUndo();

            screenCanvas.enabled = false;
        }

        private sealed class DropRefs
        {
            public RawImage Map;
            public RectTransform Marker, Pin, Slots, Countdown;
            public GameObject Label;
            public TextMeshProUGUI LabelText, ModeText, Waiting, CountdownText;
            public Image ModeIcon;
        }

        private static void BuildDropMap(RectTransform root, DropMapData data, DropRefs refs)
        {
            // Sag tarafta, ekran yuksekliginin %86'si kadar bir kare.
            RectTransform holder = NewRect("Harita", root);
            holder.anchorMin = new Vector2(1f, 0.07f);
            holder.anchorMax = new Vector2(1f, 0.93f);
            holder.pivot = new Vector2(1f, 0.5f);
            holder.anchoredPosition = new Vector2(-110f, 0f);
            holder.sizeDelta = new Vector2(900f, 0f);
            AspectRatioFitter fitter = holder.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = 1f;

            RectTransform frame = NewRect("Cerceve", holder);
            Stretch(frame, -18f, -18f, -18f, -18f);
            AddImage(frame, Sprite("ui_round_outline"), new Color(1f, 1f, 1f, 0.3f)).pixelsPerUnitMultiplier = 1.6f;

            RectTransform mapRect = NewRect("Resim", holder);
            Stretch(mapRect, 0f, 0f, 0f, 0f);
            refs.Map = mapRect.gameObject.AddComponent<RawImage>();
            refs.Map.texture = data != null ? data.MapTexture : null;
            refs.Map.raycastTarget = true;
            mapRect.gameObject.AddComponent<DropMapTapArea>();
            mapRect.gameObject.AddComponent<RectMask2D>();

            Image mapCloud = AddIcon(mapRect, "Bulut", "cloud", new Color(1f, 1f, 1f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(-120f, 220f), 220f);
            mapCloud.gameObject.AddComponent<UIDrift>();
            Image mapCloud2 = AddIcon(mapRect, "Bulut2", "cloud", new Color(1f, 1f, 1f, 0.45f), new Vector2(0.5f, 0.5f), new Vector2(260f, -260f), 180f);
            mapCloud2.gameObject.AddComponent<UIDrift>();

            // Isaretci: halka + sari pin + bolge adi etiketi.
            refs.Marker = NewRect("Isaretci", mapRect);
            Place(refs.Marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            AddIcon(refs.Marker, "Halka", "ring", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 64f);

            refs.Pin = NewRect("Pin", refs.Marker);
            Place(refs.Pin, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(74f, 74f));
            AddImage(refs.Pin, Sprite("drop_pin"), Color.white).type = Image.Type.Simple;

            RectTransform label = NewRect("BolgeEtiketi", refs.Marker);
            Place(label, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), new Vector2(-14f, -22f), new Vector2(200f, 56f));
            AddImage(label, Sprite("ui_round"), Color.white).pixelsPerUnitMultiplier = 2.2f;
            HorizontalLayoutGroup labelLayout = label.gameObject.AddComponent<HorizontalLayoutGroup>();
            labelLayout.padding = new RectOffset(20, 20, 6, 8);
            labelLayout.childAlignment = TextAnchor.MiddleCenter;
            labelLayout.childControlWidth = true;
            labelLayout.childControlHeight = true;
            labelLayout.childForceExpandWidth = false;
            ContentSizeFitter labelFitter = label.gameObject.AddComponent<ContentSizeFitter>();
            labelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            labelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            refs.LabelText = AddText(label, "Yazi", "Mezarlık", 34f, Hex("#2A2A2E"), TextAlignmentOptions.Center);
            refs.LabelText.enableAutoSizing = false;
            refs.Label = label.gameObject;

            refs.Marker.gameObject.SetActive(false);

            // Pusula (sag ust).
            RectTransform compass = NewRect("Pusula", root);
            Place(compass, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-70f, -76f), new Vector2(96f, 96f));
            AddImage(compass, Sprite("compass_star"), new Color(1f, 1f, 1f, 0.75f)).type = Image.Type.Simple;
            (string letter, Vector2 offset)[] letters = { ("N", new Vector2(0f, 62f)), ("S", new Vector2(0f, -62f)), ("W", new Vector2(-62f, 0f)), ("E", new Vector2(62f, 0f)) };
            foreach ((string letter, Vector2 offset) in letters)
            {
                TextMeshProUGUI text = AddText(compass, letter, letter, 28f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
                Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), offset, new Vector2(40f, 36f));
            }
        }

        private static void BuildDropLeftColumn(RectTransform root, DropRefs refs)
        {
            Vector2 topLeft = new(0f, 1f);

            refs.ModeIcon = AddIcon(root, "ModIkonu", "icon_person", Color.white, topLeft, new Vector2(96f, -100f), 64f);
            refs.ModeText = AddText(root, "ModYazisi", "Solo", 34f, Color.white, TextAlignmentOptions.Center, shadow: true);
            Place(refs.ModeText.rectTransform, topLeft, new Vector2(0.5f, 0.5f), new Vector2(96f, -154f), new Vector2(150f, 44f));

            TextMeshProUGUI title = AddText(root, "Baslik", "AKSİYONA HAZIRLAN", 104f, Color.white, TextAlignmentOptions.Left, shadow: true);
            Place(title.rectTransform, topLeft, topLeft, new Vector2(160f, -40f), new Vector2(900f, 120f));

            TextMeshProUGUI subtitle = AddText(root, "AltBaslik", "Tedarik noktanı seç", 50f, Yellow, TextAlignmentOptions.Left, shadow: true);
            Place(subtitle.rectTransform, topLeft, topLeft, new Vector2(164f, -156f), new Vector2(800f, 64f));

            RectTransform doubleXp = NewRect("CiftXpDurumu", root);
            Place(doubleXp, topLeft, topLeft, new Vector2(160f, -232f), new Vector2(470f, 74f));
            AddImage(doubleXp, Sprite("ui_round"), new Color(0.08f, 0.22f, 0.36f, 0.6f)).pixelsPerUnitMultiplier = 1.8f;
            Color muted = Hex("#8FA4B6");
            AddIcon(doubleXp, "Ikon", "icon_skull", muted, new Vector2(0f, 0.5f), new Vector2(42f, 0f), 46f);
            TextMeshProUGUI doubleXpText = AddText(doubleXp, "Yazi", "Çift XP etkin değil", 34f, muted, TextAlignmentOptions.Left);
            Stretch(doubleXpText.rectTransform, 80f, 16f, 0f, 2f);

            // Buyuk geri sayim (oda dolunca).
            refs.Countdown = NewRect("GeriSayim", root);
            Place(refs.Countdown, topLeft, new Vector2(0.5f, 0.5f), new Vector2(470f, -470f), new Vector2(440f, 300f));
            Image glow = AddImage(refs.Countdown, Sprite("glow"), new Color(1f, 1f, 1f, 0.35f));
            glow.type = Image.Type.Simple;
            refs.CountdownText = AddText(refs.Countdown, "Sayi", "5", 230f, Color.white, TextAlignmentOptions.Center, shadow: true);
            refs.CountdownText.enableAutoSizing = false;
            refs.Countdown.gameObject.SetActive(false);

            // Oyuncu yuvalari: 2 x 16.
            refs.Slots = NewRect("Oyuncular", root);
            float width = SlotColumns * (SlotSize + SlotSpacing) - SlotSpacing;
            float height = SlotRows * (SlotSize + SlotSpacing) - SlotSpacing;
            Place(refs.Slots, topLeft, topLeft, new Vector2(130f, -650f), new Vector2(width, height));
            GridLayoutGroup grid = refs.Slots.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(SlotSize, SlotSize);
            grid.spacing = new Vector2(SlotSpacing, SlotSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = SlotColumns;

            for (int i = 0; i < SlotColumns * SlotRows; i++)
            {
                RectTransform slot = NewRect($"Yuva{i + 1:00}", refs.Slots);
                AddImage(slot, Sprite("ui_round"), new Color(0.10f, 0.22f, 0.32f, 0.55f)).pixelsPerUnitMultiplier = 3.2f;
                AddIcon(slot, "Ikon", "icon_person", new Color(0.45f, 0.55f, 0.65f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, 30f);
            }

            refs.Waiting = AddText(root, "Bekleme", "Oyuncular bekleniyor...\n5", 40f, WaitingGreen, TextAlignmentOptions.Top, shadow: true);
            refs.Waiting.textWrappingMode = TextWrappingModes.Normal;
            Place(refs.Waiting.rectTransform, topLeft, new Vector2(0.5f, 1f), new Vector2(130f + width * 0.5f, -760f), new Vector2(720f, 120f));
        }
    }
}
#endif
