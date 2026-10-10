#if UNITY_EDITOR
using NewBattle.Progression;
using NewBattle.UI;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Test penceresi: oyuncunun seviye/para/galibiyet degerlerini degistirir,
    /// lobi animasyonlarinin kilitlerini dener. Play modunda secim aninda
    /// menudeki karaktere yansir.
    ///
    /// Oyuncunun kendi secim ekrani (menudeki bir butondan acilacak) sonraki
    /// asamalarda yapilacak; o zamana kadar bu pencereyle test ediyoruz.
    /// </summary>
    public class PlayerProfileWindow : EditorWindow
    {
        private LobbyAnimationLibrary _library;
        private int _level, _xp, _coins, _wins;
        private Vector2 _scroll;

        [MenuItem("Tools/NewBattle/Oyuncu Profili (Test)", false, 40)]
        public static void Open()
        {
            GetWindow<PlayerProfileWindow>("Oyuncu Profili").minSize = new Vector2(420f, 460f);
        }

        private void OnEnable()
        {
            PlayerProgress.Changed += Reload;
            Reload();
        }

        private void OnDisable() => PlayerProgress.Changed -= Reload;

        private void OnFocus() => Reload();

        private void Reload()
        {
            _level = PlayerProgress.Level;
            _xp = PlayerProgress.Xp;
            _coins = PlayerProgress.Coins;
            _wins = PlayerProgress.Wins;
            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("İlerleme", EditorStyles.boldLabel);
            _level = EditorGUILayout.IntField("Seviye", _level);
            _xp = EditorGUILayout.IntField($"XP (sonraki: {PlayerProgress.XpForNextLevel(_level)})", _xp);
            _coins = EditorGUILayout.IntField("Para", _coins);
            _wins = EditorGUILayout.IntField("Galibiyet", _wins);
            EditorGUILayout.LabelField("Rütbe", PlayerProgress.RankName(Mathf.Max(1, _level)));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Kaydet"))
                    PlayerProgress.SetValues(_level, _xp, _coins, _wins);
                if (GUILayout.Button("+50 XP"))
                    PlayerProgress.AddXp(50);
                if (GUILayout.Button("+100 Para"))
                    PlayerProgress.AddCoins(100);
                if (GUILayout.Button("+1 Galibiyet"))
                    PlayerProgress.AddWin();
            }

            if (GUILayout.Button("Her Şeyi Sıfırla (ilk açılış gibi)"))
            {
                if (EditorUtility.DisplayDialog("Oyuncu Profili", "Seviye, para, galibiyet ve animasyonlar sıfırlansın mı?", "Sıfırla", "Vazgeç"))
                    PlayerProgress.ResetAll();
            }

            EditorGUILayout.Space(14f);
            EditorGUILayout.LabelField("Lobi Animasyonları", EditorStyles.boldLabel);

            if (_library == null)
                _library = AssetDatabase.LoadAssetAtPath<LobbyAnimationLibrary>(LobbyAnimationSetup.LibraryPath);

            _library = (LobbyAnimationLibrary)EditorGUILayout.ObjectField("Kütüphane", _library, typeof(LobbyAnimationLibrary), false);

            if (_library == null)
            {
                EditorGUILayout.HelpBox("Kütüphane yok. 'Tools > NewBattle > Menü > 2 - Lobi Animasyonlarını Kur' çalıştır.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Seçimi karakter üzerinde görmek için Play modunda kullan.", MessageType.None);

            LobbyAnimation current = _library.Current;
            LobbyCharacterAnimator player = Application.isPlaying ? FindFirstObjectByType<LobbyCharacterAnimator>() : null;

            foreach (LobbyAnimation animation in _library.Animations)
            {
                if (animation == null)
                    continue;

                bool unlocked = _library.IsUnlocked(animation);
                string state = animation == current ? "● SEÇİLİ" : unlocked ? "Açık" : $"Kilitli – {_library.RequirementText(animation)}";
                if (animation == _library.Default)
                    state += "  [default]";

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(animation.displayName, GUILayout.Width(140f));
                    EditorGUILayout.LabelField(state);

                    using (new EditorGUI.DisabledScope(!unlocked || animation == current))
                    {
                        if (GUILayout.Button("Seç", GUILayout.Width(50f)))
                            _library.Select(animation);
                    }

                    bool canBuy = animation.unlock == LobbyUnlockType.Coins && !unlocked;
                    using (new EditorGUI.DisabledScope(!canBuy))
                    {
                        if (GUILayout.Button("Satın Al", GUILayout.Width(70f)) && !_library.TryPurchase(animation))
                            ShowNotification(new GUIContent("Para yetmiyor"));
                    }

                    using (new EditorGUI.DisabledScope(player == null || !player.isActiveAndEnabled))
                    {
                        if (GUILayout.Button("Önizle", GUILayout.Width(60f)))
                            player.Play(animation);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }
    }
}
#endif
