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
        Gift,
        [Tooltip("Satin alinir: seviye (Requirement) yetmeli VE fiyat (Price) kadar altin odenir.")]
        LevelAndCoins
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
        [Tooltip("Menude (ve onizlemelerde) karakteri saga (+) / sola (-) kaydirir (goruntu genisliginin orani).")]
        [Range(-0.5f, 0.5f)] public float menuOffsetX;
        [Tooltip("Menude (ve onizlemelerde) karakteri yukari (+) / asagi (-) kaydirir (goruntu yuksekliginin orani).")]
        [Range(-0.5f, 0.5f)] public float menuOffsetY;

        [Header("Durus")]
        [Tooltip("Boyun animasyondan sonra bu kadar derece geriye egilir (eksi = one). " +
                 "Kafasi one cikik duran karakterler icin. Play'de degistirince aninda gorunur.")]
        [Range(-45f, 45f)] public float neckBackTilt;
        [Tooltip("Bas, boyundan ayrica bu kadar derece geriye egilir (eksi = one).")]
        [Range(-45f, 45f)] public float headBackTilt;
        [Tooltip("Ayaklari yere paralel tutar (0 = animasyon nasilsa, 1 = tamamen duz). " +
                 "Ayak burnu asagi/yukari egik duruyorsa artir.")]
        [Range(0f, 1f)] public float footFlatten = 1f;
        [Tooltip("Ayak parmagi kemigini (ayakkabi burnu) dinlenme (T) pozundaki gibi sabit tutar " +
                 "(0 = animasyon, 1 = sabit). Ayakkabi ezik/bukuk gorunuyorsa artir.")]
        [Range(0f, 1f)] public float toeLock = 1f;
        [Tooltip("Model dosyasinda karaktere ait olmayan, kaldirilacak parcalarin adlari " +
                 "(ornek: Meshy'nin biraktigi 'Icosphere').")]
        public string[] removeObjects = { };

        [Header("Kilit")]
        [Tooltip("Baslangic karakteri: ilk acilista secilebilir (Ahmet / Ayse). Secilen oyuncunun " +
                 "olur; digeri 'Level' kilidiyle (Requirement seviyesinde) acilir.")]
        public bool starter;
        [Tooltip("Isaretliyse botlar (dusmanlar) da bu karakterle gelir. Baslangic karakterleri zaten gelir.")]
        public bool botSkin;
        public CharacterUnlockType unlock = CharacterUnlockType.Free;
        [Tooltip("Level ve LevelAndCoins: gereken seviye. Coins: fiyat.")]
        [Min(0)] public int requirement;
        [Tooltip("LevelAndCoins: altin fiyati.")]
        [Min(0)] public int price;

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
        public const string StarterChosenKey = "nb_starter_chosen";

        [SerializeField] private string defaultCharacterId = "ahmet";
        [SerializeField] private List<CharacterDefinition> characters = new();

        [Header("Karakter Ekrani Gorunumu")]
        [Tooltip("Karakter ekranindaki (Karakterler / Animasyonlar) karakter resmini kaydirir " +
                 "(piksel, 1920x1080 olcusunde): x saga +, y yukari +. Play'de degistirince aninda gorunur, kalici.")]
        public Vector2 characterScreenOffset;
        [Tooltip("Karakter ekranindaki karakter resminin boyutu.")]
        [Range(0.5f, 2f)] public float characterScreenScale = 1f;

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

        /// <summary>Oyuncunun secili (ve acik) karakteri; yoksa acik bir baslangic karakteri; yoksa varsayilan.</summary>
        public CharacterDefinition Current
        {
            get
            {
                CharacterDefinition selected = Find(PlayerPrefs.GetString(SelectedKey, string.Empty));
                if (selected != null && IsUnlocked(selected))
                    return selected;

                foreach (CharacterDefinition starter in Starters)
                {
                    if (IsUnlocked(starter))
                        return starter;
                }

                return Default;
            }
        }

        /// <summary>Ilk acilista secilebilen karakterler (Ahmet, Ayse).</summary>
        public IEnumerable<CharacterDefinition> Starters
        {
            get
            {
                foreach (CharacterDefinition character in characters)
                {
                    if (character != null && character.starter)
                        yield return character;
                }
            }
        }

        /// <summary>Oyuncu ilk acilista karakterini secti mi.</summary>
        public static bool HasChosenStarter => PlayerPrefs.GetInt(StarterChosenKey, 0) == 1;

        /// <summary>Ilk acilis secimi: karakter oyuncunun olur ve secilir.</summary>
        public void ChooseStarter(CharacterDefinition character)
        {
            if (character == null)
                return;

            PlayerProgress.GrantOwnership(character.id);
            PlayerPrefs.SetInt(StarterChosenKey, 1);
            PlayerPrefs.SetString(SelectedKey, character.id);
            PlayerPrefs.Save();
            CharacterSelectionChanged?.Invoke();
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

            if (PlayerProgress.Owns(character.id))
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
            CharacterUnlockType.Coins => $"{character.requirement} Altın",
            CharacterUnlockType.LevelAndCoins => $"Seviye {character.requirement} + {character.price} Altın",
            CharacterUnlockType.Gift => "Hediye",
            _ => "Ücretsiz"
        };

        /// <summary>Satin alinabilir mi (Coins ya da LevelAndCoins; seviyesi yetiyor mu).</summary>
        public bool CanPurchase(CharacterDefinition character) =>
            character != null && !IsUnlocked(character) &&
            (character.unlock == CharacterUnlockType.Coins ||
             (character.unlock == CharacterUnlockType.LevelAndCoins && PlayerProgress.Level >= character.requirement));

        public bool TryPurchase(CharacterDefinition character)
        {
            if (!CanPurchase(character))
                return false;

            int cost = character.unlock == CharacterUnlockType.LevelAndCoins ? character.price : character.requirement;
            if (!PlayerProgress.TrySpendCoins(cost))
                return false;

            PlayerProgress.GrantOwnership(character.id);
            return true;
        }

        /// <summary>Botlara ve varsayilana: rastgele bir baslangic karakteri (yoksa varsayilan).</summary>
        /// <summary>Botlarin gorunumu: baslangic karakterleri + "botSkin" isaretli olanlar.</summary>
        public CharacterDefinition RandomBotCharacter()
        {
            var list = new List<CharacterDefinition>(Starters);
            foreach (CharacterDefinition character in characters)
            {
                if (character != null && character.botSkin && character.model != null && !list.Contains(character))
                    list.Add(character);
            }

            return list.Count > 0 ? list[UnityEngine.Random.Range(0, list.Count)] : Default;
        }

        public CharacterDefinition RandomStarter()
        {
            var list = new List<CharacterDefinition>(Starters);
            return list.Count > 0 ? list[UnityEngine.Random.Range(0, list.Count)] : Default;
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
        /// <summary>Kurulum araci icin: karakteri kaldirir.</summary>
        public bool EditorRemove(string id) => characters.RemoveAll(c => c != null && c.id == id) > 0;

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
