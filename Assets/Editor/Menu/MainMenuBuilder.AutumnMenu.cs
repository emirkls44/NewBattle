#if UNITY_EDITOR
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Sonbahar temali ana menu: yerlesim ayni, arka plan sonbahar agaclari, ust / alt bar
    /// krem, kartlar krem-tarcin, Savas Etkinlikleri karti ahsap, magaza ve liderlik
    /// butonlari ahsap kahvesi. Unity derleyince bir kez kendiliginden uygulanir (sonra elle
    /// yaptigin degisiklikleri ezmez). Tekrar: Tools > NewBattle > Menü > Sonbahar Ana Menü.
    /// Geri donmek: git tag "safe-oncesi-sonbahar-menu".
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string AutumnBackground = "bg_sonbahar";

        [InitializeOnLoadMethod]
        private static void AutoApplyAutumnMenuLater()
        {
            EditorApplication.delayCall += AutoApplyAutumnMenu;
        }

        private static void AutoApplyAutumnMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoApplyAutumnMenu;
                return;
            }

            Canvas canvas = FindGameCanvas();
            MainMenuScreen screen = canvas != null ? canvas.GetComponentInChildren<MainMenuScreen>(true) : null;
            Image background = screen != null ? screen.GetComponent<Image>() : null;
            if (background == null || (background.sprite != null && background.sprite.name == AutumnBackground))
                return;

            ApplyAutumnMenu(screen.transform);
        }

        [MenuItem("Tools/NewBattle/Menü/Sonbahar Ana Menü (yeniden uygula)", false, 6)]
        private static void ApplyAutumnMenuItem()
        {
            Canvas canvas = FindGameCanvas();
            MainMenuScreen screen = canvas != null ? canvas.GetComponentInChildren<MainMenuScreen>(true) : null;
            if (screen != null)
                ApplyAutumnMenu(screen.transform);
        }

        private static void ApplyAutumnMenu(Transform root)
        {
            AssetDatabase.ImportAsset($"{ArtFolder}/{AutumnBackground}.png");
            Sprite autumn = Sprite(AutumnBackground);
            if (autumn == null)
                return;

            Undo.SetCurrentGroupName("Sonbahar ana menu");

            Color cream = Hex("#FBF3E6");
            Color creamDeep = Hex("#F4E4CC");
            Color woodFace = Hex("#9E6B47");
            Color woodLip = Hex("#6E4528");
            Color darkWood = Hex("#6E4528");
            Color midWood = Hex("#8C5A36");
            Color cardEdge = Hex("#C9A078");
            Color cardFill = Hex("#F7E8D3");
            Color softInk = Hex("#7A5638");

            if (root.GetComponent<Image>() is Image background)
            {
                Undo.RecordObject(background, "Arka plan");
                background.sprite = autumn;
                background.type = Image.Type.Simple;
                background.color = Color.white;
                EditorUtility.SetDirty(background);
            }

            // Ust ve alt bar krem.
            PaintImage(FindDeep(root, "UstBar"), cream);
            PaintImage(FindDeep(root, "AltBar"), creamDeep);
            TintText(FindDeep(root, "Logo"), Hex("#E8A23A"));
            TintText(FindDeep(root, "OnlineSayac"), softInk);
            PaintImage(FindDeep(root, "OnlineIkon"), softInk);

            // Magaza ve liderlik ahsap kahvesi (digerleri zaten turuncu / yesil).
            foreach (string name in new[] { "MagazaButonu", "LiderlikButonu" })
            {
                Transform button = FindDeep(root, name);
                PaintImage(button != null ? button.Find("Yuz") : null, woodFace);
                PaintImage(button != null ? button.Find("Alt") : null, woodLip);
            }

            // Sezon ve Mucadeleler kartlari: tarcin cerceve, krem ic.
            foreach (string name in new[] { "SezonKarti", "MucadelelerKarti" })
            {
                Transform card = FindDeep(root, name);
                PaintImage(card, cardEdge);
                PaintImage(card != null ? card.Find("Ic") : null, cardFill);
            }

            // Savas Etkinlikleri karti ahsap.
            Transform events = FindDeep(root, "EtkinlikKarti");
            if (events != null)
            {
                PaintImage(events, darkWood);
                PaintImage(events.Find("Ic"), midWood);
                PaintImage(events.Find("Baslik"), Hex("#4E2D14"));
            }

            // Magazadaki varsayilan arka plan karti da sonbahar.
            var library = AssetDatabase.LoadAssetAtPath<NewBattle.Progression.MenuBackgroundLibrary>(
                "Assets/Resources/" + NewBattle.Progression.MenuBackgroundLibrary.ResourcePath + ".asset");
            if (library != null)
            {
                library.defaultName = "Sonbahar (Varsayılan)";
                library.defaultImage = autumn;
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("Sonbahar ana menu uygulandi (arka plan, krem barlar, ahsap kartlar).");
        }

        private static void PaintImage(Transform target, Color color)
        {
            if (target == null || !target.TryGetComponent(out Image image))
                return;

            Undo.RecordObject(image, "Renk");
            image.color = color;
            EditorUtility.SetDirty(image);
        }

        private static void TintText(Transform target, Color color)
        {
            if (target == null || !target.TryGetComponent(out TextMeshProUGUI text))
                return;

            Undo.RecordObject(text, "Yazi rengi");
            text.color = color;
            EditorUtility.SetDirty(text);
        }
    }
}
#endif
