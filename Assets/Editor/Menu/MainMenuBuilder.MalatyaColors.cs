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
    /// Malatya ana menu renk temasi: ust/alt bantlar ahsap kahvesi, soldaki kartlar ve sag
    /// ustteki para ahsap / kayisi / yaprak renklerinde. Malatya menu kuruluysa Unity
    /// derleyince bir kez kendiliginden uygulanir (sonra rengi elle degistirebilirsin,
    /// tekrar ezilmez). Elle tekrar uygulamak: Tools > NewBattle > Menü > Malatya Renkleri.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const int MalatyaThemeVersion = 1;

        private static readonly Color TanLight = Hex("#E3B07A");
        private static readonly Color Leaf = Hex("#4F7A34");
        private static readonly Color LeafDark = Hex("#2F4A22");
        private static readonly Color LeafLight = Hex("#6E9A45");

        [InitializeOnLoadMethod]
        private static void AutoApplyMalatyaColorsLater()
        {
            EditorApplication.delayCall += AutoApplyMalatyaColors;
        }

        private static void AutoApplyMalatyaColors()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoApplyMalatyaColors;
                return;
            }

            Canvas canvas = FindGameCanvas();
            MainMenuScreen screen = canvas != null ? canvas.GetComponentInChildren<MainMenuScreen>(true) : null;
            MenuBands bands = screen != null ? screen.GetComponent<MenuBands>() : null;
            if (bands == null || bands.ThemeVersion >= MalatyaThemeVersion)
                return;

            ApplyMalatyaColorsTo(screen.transform, bands);
            Debug.Log("Malatya ana menu renkleri uygulandi (bantlar, soldaki kartlar, para).");
        }

        [MenuItem("Tools/NewBattle/Menü/Malatya Renkleri (yeniden uygula)", false, 4)]
        private static void ApplyMalatyaColorsMenu()
        {
            Canvas canvas = FindGameCanvas();
            MainMenuScreen screen = canvas != null ? canvas.GetComponentInChildren<MainMenuScreen>(true) : null;
            if (screen == null)
            {
                Debug.LogError("Malatya renkleri: ana menu bulunamadi.");
                return;
            }

            ApplyMalatyaColorsTo(screen.transform, screen.GetComponent<MenuBands>());
        }

        private static void ApplyMalatyaColorsTo(Transform root, MenuBands bands)
        {
            Undo.SetCurrentGroupName("Malatya renkleri");

            if (bands != null)
            {
                Undo.RecordObject(bands, "Bant renkleri");
                bands.fillColor = Wood;
                bands.lineColor = WoodDark;
                bands.ThemeVersion = MalatyaThemeVersion;
                bands.Apply();
                EditorUtility.SetDirty(bands);
            }

            // Seviye karti: koyu ahsap cerceve, acik tarcin ic, ahsap baslik, kayisi XP dolumu.
            Transform season = FindDeep(root, "SezonKarti");
            if (season != null)
            {
                Paint(season, WoodDark);
                Paint(season.Find("Ic"), TanLight);
                Paint(season.Find("Baslik"), Wood);
                Tint(season.Find("Baslik/Yazi"), Cream);
                Paint(season.Find("XpBari"), WoodDark);
                Paint(season.Find("XpBari/Dolum"), ApricotOrange);
            }

            // Iki Kat XP: yaprak yesili, kayisi renkli Etkinlestir.
            Transform doubleXp = FindDeep(root, "CiftXpKarti");
            if (doubleXp != null)
            {
                Paint(doubleXp, Leaf);
                Paint(doubleXp.Find("Baslik"), LeafDark);
                Tint(doubleXp.Find("Baslik/Yazi"), Cream);
                Tint(doubleXp.Find("Yazi"), Cream);
                Paint(doubleXp.Find("EtkinlestirButonu/Yuz"), ApricotOrange);
                Paint(doubleXp.Find("EtkinlestirButonu/Alt"), ApricotRust);
            }

            // Mucadeleler: seviye karti gibi ahsap.
            Transform challenges = FindDeep(root, "MucadelelerKarti");
            if (challenges != null)
            {
                Paint(challenges, WoodDark);
                Paint(challenges.Find("Ic"), TanLight);
                Paint(challenges.Find("Baslik"), Wood);
                Tint(challenges.Find("Baslik/Yazi"), Cream);
                Paint(challenges.Find("Yakinda"), Wood);
                Tint(challenges.Find("Yakinda/Yazi"), Cream);
            }

            // Para: ahsap zemin, krem yazi, yaprak yesili arti.
            Transform wallet = FindDeep(root, "ParaPaneli");
            if (wallet != null)
            {
                Paint(wallet, Wood);
                Tint(wallet.Find("ParaMiktari"), Cream);
                Paint(wallet.Find("ParaEkleButonu/Yuz"), LeafLight);
                Paint(wallet.Find("ParaEkleButonu/Alt"), Leaf);
            }

            // Isim plakasi ahsap zeminin ustunde: koyu yazi okunmaz, krem olsun.
            Transform nameTag = FindDeep(root, "IsimEtiketi");
            if (nameTag != null)
            {
                foreach (TextMeshProUGUI text in nameTag.GetComponentsInChildren<TextMeshProUGUI>(true))
                    Tint(text.transform, Cream);
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
        }

        private static void Paint(Transform target, Color color)
        {
            if (target == null || !target.TryGetComponent(out Image image))
                return;

            Undo.RecordObject(image, "Renk");
            image.color = color;
            EditorUtility.SetDirty(image);
        }

        private static void Tint(Transform target, Color color)
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
