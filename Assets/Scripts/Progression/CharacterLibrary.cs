using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Progression
{
    public enum CharacterUnlockType
    {
        Free,
        Level,
        Coins,
        [Tooltip("Satilmaz, magazada gorunmez. Sadece hediye koduyla acilir; sahibi olmayan listede bile gormez.")]
        Gift
    }

    [Serializable]
    public class CharacterDefinition
    {
        [Tooltip("Degistirme: kayitlar ve hediye kodlari bu kimlige bagli.")]
        public string id = "yeni_karakter";
        public string displayName = "Yeni Karakter";

        [Header("Model")]
        [Tooltip("Humanoid olarak ice aktarilmis karakter modeli (FBX).")]
        public GameObject model;
        [Tooltip("Modelin Humanoid avatari (FBX'in icinde).")]
        public Avatar avatar;
        [Tooltip("Bossa modelin kendi materyali kalir.")]
        public Material material;
        [Tooltip("Boy carpani: karakter digerlerinden buyuk/kucuk gorunuyorsa ayarla.")]
        [Range(0.5f, 2f)] public float scale = 1f;
        [Tooltip("Menude (ve magaza onizlemesinde) karakter ne kadar yakindan gosterilsin. " +
                 "Kanat, hale gibi parcalar kadraji buyutup karakteri kucuk gosteriyorsa artir. " +
                 "Ayaklar sabit kalir, fazlasi tepeden kirpilir.")]
        [Range(0.6f, 1.8f)] public float menuZoom = 1f;
        [Tooltip("Model dosyasinda karaktere ait olmayan, kaldirilacak parcalarin adlari " +
                 "(ornek: Meshy'nin biraktigi 'Icosphere').")]
        public string[] removeObjects = { };

        [Header("Kilit")]
        public CharacterUnlockType unlock = CharacterUnlockType.Free;
        [Tooltip("Level: gereken seviye. Coins: fiyat.")]
        [Min(0)] public int requirement;

        public Sprite icon;
    }

    /// <summary>
    /// Oyuncunun secebilecegi karakterler. Dosya: Assets/Resources/KarakterKutuphanesi
    /// (Menü > Kur olusturur). Hediye karakterleri sadece hediye koduyla acilir.
    /// </summary>
    [CreateAssetMenu(menuName = "NewBattle/Karakter Kutuphanesi", fileName = "KarakterKutuphanesi")]
    public class CharacterLibrary : ScriptableObject
    {
        public const string ResourceName = "KarakterKutuphanesi";
        private const string SelectedKey = "nb_selected_character";

        [SerializeField] private string defaultCharacterId = "takim_elbiseli";
        [SerializeField] private List<CharacterDefinition> characters = new();

        private static CharacterLibrary _instance;

        public static CharacterLibrary Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<CharacterLibrary>(ResourceName);
                return _instance;
            }
        }

        public IReadOnlyList<CharacterDefinition> Characters => characters;

        public CharacterDefinition Default => Find(defaultCharacterId) ?? (characters.Count > 0 ? characters[0] : null);

        /// <summary>Oyuncunun secili (ve acik) karakteri; yoksa varsayilan.</summary>
        public CharacterDefinition Current
        {
            get
            {
                CharacterDefinition selected = Find(PlayerPrefs.GetString(SelectedKey, string.Empty));
                return selected != null && IsUnlocked(selected) ? selected : Default;
            }
        }

        public CharacterDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (CharacterDefinition character in characters)
            {
                if (character != null && character.id == id)
                    return character;
            }

            return null;
        }

        public bool IsUnlocked(CharacterDefinition character)
        {
            if (character == null)
                return false;

            if (character == Default || PlayerProgress.Owns(character.id))
                return true;

            return character.unlock switch
            {
                CharacterUnlockType.Free => true,
                CharacterUnlockType.Level => PlayerProgress.Level >= character.requirement,
                _ => false
            };
        }

        /// <summary>Listede gosterilsin mi: hediye karakterini sadece sahibi gorur.</summary>
        public bool IsVisible(CharacterDefinition character) =>
            character != null && (character.unlock != CharacterUnlockType.Gift || IsUnlocked(character));

        public string RequirementText(CharacterDefinition character) => character.unlock switch
        {
            CharacterUnlockType.Level => $"Seviye {character.requirement}",
            CharacterUnlockType.Coins => $"{character.requirement} Para",
            CharacterUnlockType.Gift => "Hediye",
            _ => "Ücretsiz"
        };

        public bool TryPurchase(CharacterDefinition character)
        {
            if (character == null || character.unlock != CharacterUnlockType.Coins || IsUnlocked(character))
                return false;

            if (!PlayerProgress.TrySpendCoins(character.requirement))
                return false;

            PlayerProgress.GrantOwnership(character.id);
            return true;
        }

        public bool Select(CharacterDefinition character)
        {
            if (!IsUnlocked(character))
                return false;

            PlayerPrefs.SetString(SelectedKey, character.id);
            PlayerPrefs.Save();
            CharacterSelectionChanged?.Invoke();
            return true;
        }

        /// <summary>Secili karakter degisince (menudeki model yenilensin).</summary>
        public static event Action CharacterSelectionChanged;

#if UNITY_EDITOR
        /// <summary>Kurulum araci icin: karakter ekler ya da gunceller.</summary>
        public void EditorSet(CharacterDefinition definition, bool makeDefault)
        {
            int index = characters.FindIndex(c => c != null && c.id == definition.id);
            if (index >= 0)
                characters[index] = definition;
            else
                characters.Add(definition);

            if (makeDefault)
                defaultCharacterId = definition.id;
        }
#endif
    }
}
