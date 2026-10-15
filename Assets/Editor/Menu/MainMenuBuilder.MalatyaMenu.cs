#if UNITY_EDITOR
using NewBattle.Progression;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Tools > NewBattle > Menü > Malatya Ana Menü (deneme): ana menuyu Malatya / kayisi
    /// temasina ve yeni yerlesime gecirir. Var olan parcalar (kartlar, OYNA, para) tasinir,
    /// baglantilari bozulmaz; Battlelands'e benzeyen ust/alt bar gizlenir.
    ///   - Arka plan: kayisi bahcesi, ustte kilim seridi
    ///   - Sol: seviye karti, Iki Kat XP, Gorevler
    ///   - Orta: karakter (daha buyuk), altinda isim
    ///   - Sag ust: para; sag: ahsap tabela butonlar (Magaza, Karakterler, Ayarlar, Profil)
    ///   - Sag alt: kayisi renkli OYNA
    /// Beğenilmezse: git tag "safe-oncesi-yeni-ana-menu" ile geri donulur.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private static readonly Color Wood = Hex("#7A4A26");
        private static readonly Color WoodDark = Hex("#4E2D14");
        private static readonly Color Cream = Hex("#FFE9C7");
        private static readonly Color ApricotOrange = Hex("#F2994A");
        private static readonly Color ApricotRust = Hex("#B5452B");

        [MenuItem("Tools/NewBattle/Menü/Malatya Ana Menü (deneme)", false, 3)]
        public static void ApplyMalatyaMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Menü", "Önce Play modunu kapat.", "Tamam");
                return;
            }

            AssetDatabase.ImportAsset(ArtFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            if (!PrepareFont())
                return;
            PrepareMaterials();

            Canvas canvas = FindGameCanvas();
            var root = canvas != null ? FindMenuRoot(canvas, out _) as RectTransform : null;
            if (root == null)
            {
                Debug.LogError("Malatya menu: ana menu (AnaMenu) bulunamadi.");
                return;
            }

            Undo.SetCurrentGroupName("Malatya ana menu");
            int undoGroup = Undo.GetCurrentGroup();

            // Arka plan: kayisi bahcesi (magazadan secilen arka plan yine ustune gelir).
            Image background = root.GetComponent<Image>();
            Sprite orchard = Sprite("bg_kayisi_bahcesi");
            if (background != null && orchard != null)
            {
                Undo.RecordObject(background, "Arka plan");
                background.sprite = orchard;
                background.type = Image.Type.Simple;
                background.color = Color.white;
            }

            RectTransform layout = root.Find("MalatyaDuzeni") as RectTransform;
            if (layout == null)
            {
                layout = NewRect("MalatyaDuzeni", root);
                Undo.RegisterCreatedObjectUndo(layout.gameObject, "Malatya duzeni");
                Stretch(layout, 0f, 0f, 0f, 0f);
            }

            Transform status = root.Find("Durum");
            layout.SetSiblingIndex(status != null ? status.GetSiblingIndex() : root.childCount - 1);

            EnsureKilimStrip(layout);

            Vector2 topLeft = new(0f, 1f), topRight = new(1f, 1f), bottomRight = new(1f, 0f);
            MoveInto(root, "SezonKarti", layout, topLeft, new Vector2(30f, -44f), 0.55f);
            MoveInto(root, "CiftXpKarti", layout, topLeft, new Vector2(30f, -248f), 0.58f);
            MoveInto(root, "MucadelelerKarti", layout, topLeft, new Vector2(30f, -424f), 0.58f);
            MoveInto(root, "ParaPaneli", layout, topRight, new Vector2(-30f, -44f), 1f);
            RectTransform play = MoveInto(root, "OynaButonu", layout, bottomRight, new Vector2(-44f, 52f), 1f);
            if (play != null)
                PaintButton(play, ApricotOrange, ApricotRust);

            EnsureSignMenu(layout);

            // Karakter daha buyuk: orta alan neredeyse tum yuksekligi kullanir.
            if (FindDeep(root, "OrtaAlan") is RectTransform middle)
            {
                Undo.RecordObject(middle, "Orta alan");
                middle.offsetMin = new Vector2(middle.offsetMin.x, 30f);
                middle.offsetMax = new Vector2(middle.offsetMax.x, -50f);
            }

            foreach (string hide in new[] { "UstBar", "AltBar", "KostumKarti", "BattlePassKarti", "Oduller", "EtkinlikKarti" })
            {
                Transform target = FindDeep(root, hide);
                if (target != null && target.gameObject.activeSelf)
                {
                    Undo.RecordObject(target.gameObject, "Gizle");
                    target.gameObject.SetActive(false);
                }
            }

            UpdateDefaultBackgroundCard(orchard);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Selection.activeGameObject = layout.gameObject;
            Debug.Log("Malatya ana menu uygulandi. Geri donmek icin: git checkout safe-oncesi-yeni-ana-menu -- Assets/Scenes");
        }

        /// <summary>Bir parcayi yeni duzene tasir (baglantilari ayni kalir).</summary>
        private static RectTransform MoveInto(Transform root, string name, RectTransform layout, Vector2 anchor,
            Vector2 position, float scale)
        {
            if (FindDeep(root, name) is not RectTransform rect)
            {
                Debug.LogWarning("Malatya menu: bulunamadi: " + name);
                return null;
            }

            Undo.SetTransformParent(rect, layout, "Tasi " + name);
            Undo.RecordObject(rect, "Yerlestir " + name);
            Vector2 size = rect.rect.size;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * scale;
            if (!rect.gameObject.activeSelf)
                rect.gameObject.SetActive(true);
            return rect;
        }

        private static void PaintButton(Transform button, Color face, Color lip)
        {
            if (button.Find("Yuz")?.GetComponent<Image>() is Image faceImage)
            {
                Undo.RecordObject(faceImage, "Renk");
                faceImage.color = face;
            }

            if (button.Find("Alt")?.GetComponent<Image>() is Image lipImage)
            {
                Undo.RecordObject(lipImage, "Renk");
                lipImage.color = lip;
            }
        }

        private static void EnsureKilimStrip(RectTransform layout)
        {
            if (layout.Find("KilimSerit") != null)
                return;

            RectTransform strip = NewRect("KilimSerit", layout);
            Undo.RegisterCreatedObjectUndo(strip.gameObject, "Kilim");
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.anchoredPosition = Vector2.zero;
            strip.sizeDelta = new Vector2(0f, 26f);
            Image image = AddImage(strip, Sprite("kilim_serit"), Color.white);
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = 32f / 26f;
            strip.SetAsFirstSibling();
        }

        /// <summary>Sagda ahsap tabela butonlar; var olan islevlere (MenuButton) baglanir.</summary>
        private static void EnsureSignMenu(RectTransform layout)
        {
            if (layout.Find("SagMenu") != null)
                return;

            (string name, string label, string action)[] signs =
            {
                ("MagazaTabela", "Mağaza", "magaza"),
                ("KarakterlerTabela", "Karakterler", "karakter"),
                ("AyarlarTabela", "Ayarlar", "ayarlar"),
                ("ProfilTabela", "Profil", "liderlik")
            };

            RectTransform menu = NewRect("SagMenu", layout);
            Undo.RegisterCreatedObjectUndo(menu.gameObject, "Sag menu");
            Vector2 topRight = new(1f, 1f);
            Place(menu, topRight, topRight, new Vector2(-30f, -150f), new Vector2(330f, signs.Length * 108f));

            for (int i = 0; i < signs.Length; i++)
            {
                Button button = AddButton(menu, signs[i].name, topRight, topRight, new Vector2(0f, -i * 108f),
                    new Vector2(330f, 90f), Wood, WoodDark);
                TextMeshProUGUI label = AddText(button.transform.Find("Yuz"), "Yazi", signs[i].label, 46f, Cream,
                    TextAlignmentOptions.Left, shadow: true);
                Stretch(label.rectTransform, 34f, 12f, 0f, 6f);

                MenuButton action = button.gameObject.AddComponent<MenuButton>();
                var serialized = new SerializedObject(action);
                serialized.FindProperty("actionId").stringValue = signs[i].action;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Magazadaki "varsayilan arka plan" karti artik kayisi bahcesi.</summary>
        private static void UpdateDefaultBackgroundCard(Sprite orchard)
        {
            var library = AssetDatabase.LoadAssetAtPath<MenuBackgroundLibrary>(
                "Assets/Resources/" + MenuBackgroundLibrary.ResourcePath + ".asset");
            if (library == null || orchard == null)
                return;

            library.defaultName = "Kayısı Bahçesi (Varsayılan)";
            library.defaultImage = orchard;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
