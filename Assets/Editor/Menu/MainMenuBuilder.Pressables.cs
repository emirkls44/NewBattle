#if UNITY_EDITOR
using System.Collections.Generic;
using NewBattle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Ekranlardaki kartlari ve panelleri basilabilir yapar: her biri bir
    /// MenuButton eylem kimligi alir, islevi sonradan kodla baglanir.
    /// Sadece eksik olani ekler; senin ayarladigin konum ve gorunume dokunmaz.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        /// <summary>Ekran kokune gore yol -> eylem kimligi.</summary>
        private static readonly (string path, string id)[] MenuPressables =
        {
            ("OrtaAlan/SezonKarti", "sezon"),
            ("OrtaAlan/MucadelelerKarti", "mucadeleler"),
            ("OrtaAlan/Oduller", "oduller"),
            ("OrtaAlan/CiftXpKarti", "cift_xp_kart"),
            ("UstBar/ParaPaneli", "para_paneli"),
            ("OrtaAlan/IsimEtiketi/RutbeIkon", "seviye_yolu"),
        };

        private static readonly (string path, string id)[] DropPressables =
        {
            ("CiftXpDurumu", "cift_xp_durum"),
        };

        private static readonly (string path, string id)[] MatchEndPressables =
        {
            ("OldurmeKarti", "mac_sonu_oldurme"),
            ("XpKarti", "mac_sonu_xp"),
            ("ParaKarti", "mac_sonu_para"),
            ("XpBari", "mac_sonu_seviye"),
        };

        /// <summary>Eksik basilabilirleri ekler. Kac tane eklendigini dondurur.</summary>
        private static int EnsurePressables(Canvas canvas)
        {
            int added = 0;
            added += MakePressables(canvas.GetComponentInChildren<MainMenuScreen>(true), MenuPressables);
            added += MakePressables(canvas.GetComponentInChildren<DropSelectionScreen>(true), DropPressables);
            added += MakePressables(canvas.GetComponentInChildren<MatchEndScreen>(true), MatchEndPressables);
            return added;
        }

        private static int MakePressables(Component screen, (string path, string id)[] targets)
        {
            if (screen == null)
                return 0;

            int added = 0;
            foreach ((string path, string id) in targets)
            {
                Transform target = screen.transform.Find(path);
                if (target == null || target.GetComponent<MenuButton>() != null)
                    continue;

                Image image = target.GetComponent<Image>();
                if (image == null)
                {
                    image = Undo.AddComponent<Image>(target.gameObject);
                    image.color = new Color(1f, 1f, 1f, 0f);
                }

                Undo.RecordObject(image, "Basilabilir");
                image.raycastTarget = true;

                Button button = target.GetComponent<Button>();
                if (button == null)
                {
                    button = Undo.AddComponent<Button>(target.gameObject);
                    button.targetGraphic = image;
                }

                if (target.GetComponent<UIPressScale>() == null)
                    Undo.AddComponent<UIPressScale>(target.gameObject);

                MenuButton action = Undo.AddComponent<MenuButton>(target.gameObject);
                var serialized = new SerializedObject(action);
                serialized.FindProperty("actionId").stringValue = id;
                serialized.ApplyModifiedProperties();
                added++;
            }

            return added;
        }

        /// <summary>Harita secimi ve mac sonu ekranina "YAKINDA!" bildirimi ekler.</summary>
        private static bool EnsureScreenToasts(Canvas canvas)
        {
            bool added = false;
            foreach (Component screen in new Component[]
                     {
                         canvas.GetComponentInChildren<DropSelectionScreen>(true),
                         canvas.GetComponentInChildren<MatchEndScreen>(true)
                     })
            {
                if (screen == null)
                    continue;

                var serialized = new SerializedObject(screen);
                SerializedProperty property = serialized.FindProperty("toast");
                if (property.objectReferenceValue != null)
                    continue;

                property.objectReferenceValue = BuildToastView((RectTransform)screen.transform);
                serialized.ApplyModifiedProperties();
                added = true;
            }

            return added;
        }

        private static ToastView BuildToastView(RectTransform parent)
        {
            RectTransform toast = NewRect("Bildirim", parent);
            Undo.RegisterCreatedObjectUndo(toast.gameObject, "Bildirim");
            Place(toast, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(520f, 90f));
            AddImage(toast, Sprite("ui_round"), new Color(0f, 0f, 0f, 0.75f));
            CanvasGroup group = toast.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            TextMeshProUGUI text = AddText(toast, "Yazi", "YAKINDA!", 44f, Color.white, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 16f, 16f, 0f, 4f);

            ToastView view = toast.gameObject.AddComponent<ToastView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("text").objectReferenceValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }
    }
}
#endif
