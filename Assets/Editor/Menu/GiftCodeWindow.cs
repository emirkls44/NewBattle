#if UNITY_EDITOR
using NewBattle.Progression;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Tools > NewBattle > Hediye Kodlari: hediye kodlarini belirle.
    /// Bir kod ekle, odulunu sec (karakter / animasyon / para / XP); oyunda
    /// Ayarlar > Hediye Kodu'na bu kodu yazan herkes odulu alir.
    /// </summary>
    public class GiftCodeWindow : EditorWindow
    {
        private GiftCodeCatalog _catalog;
        private Editor _editor;
        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/Hediye Kodları", false, 45)]
        public static void Open()
        {
            GetWindow<GiftCodeWindow>("Hediye Kodları").minSize = new Vector2(440f, 420f);
        }

        private void OnEnable() => _catalog = AssetDatabase.LoadAssetAtPath<GiftCodeCatalog>("Assets/Resources/HediyeKodlari.asset");

        private void OnDisable()
        {
            if (_editor != null)
                DestroyImmediate(_editor);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Kod ekle (+), ödülünü seç ve kaydet. Oyunda Ayarlar > Hediye Kodu'na bu kodu yazan herkes ödülü alır.\n" +
                "Büyük/küçük harf, boşluk ve tire fark etmez. Her kod bir telefonda bir kez kullanılır.\n" +
                "Karakter ödülü için 'Item Id': karakterin kimliği (ör. melek). Kodu iptal etmek için 'Active' işaretini kaldır.\n" +
                "Not: Kod listesi oyunun içinde durur; yeni kod eklemek için yeni bir sürüm çıkman gerekir.",
                MessageType.Info);

            if (_catalog == null)
            {
                if (GUILayout.Button("Hediye kodları dosyasını oluştur"))
                {
                    MainMenuBuilder.EnsureGiftCodeCatalog(out _catalog);
                    Repaint();
                }
                return;
            }

            if (_editor == null || _editor.target != _catalog)
            {
                if (_editor != null)
                    DestroyImmediate(_editor);
                _editor = Editor.CreateEditor(_catalog);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _editor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Test: bu bilgisayarda kullanılmış kodları sıfırla"))
            {
                GiftCodes.ForgetRedeemed();
                ShowNotification(new GUIContent("Kodlar tekrar kullanılabilir"));
            }
        }
    }
}
#endif
