using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Progression
{
    public enum LobbyUnlockType
    {
        [InspectorName("Ücretsiz")] Free = 0,
        [InspectorName("Seviye")] Level = 1,
        [InspectorName("Galibiyet sayısı")] Wins = 2,
        [InspectorName("Parayla satın al")] Coins = 3
    }

    [Serializable]
    public class LobbyAnimation
    {
        [Tooltip("Kayitlarda kullanilan kimlik. Sonradan DEGISTIRME; oyuncunun secimi bu isimle saklanir.")]
        public string id = "yeni_animasyon";

        [Tooltip("Oyuncunun gordugu isim.")]
        public string displayName = "Yeni Animasyon";

        [Tooltip("Humanoid bir animasyon klibi (Mixamo vb.).")]
        public AnimationClip clip;

        public LobbyUnlockType unlock = LobbyUnlockType.Free;

        [Tooltip("Seviye: gereken seviye. Galibiyet: gereken galibiyet. Para: fiyat.")]
        [Min(0)] public int requirement;

        [Range(0.25f, 2f)] public float speed = 1f;

        [Tooltip("Secim ekraninda gosterilecek ikon (istege bagli).")]
        public Sprite icon;
    }

    /// <summary>
    /// Lobide karakterin oynatabilecegi animasyonlar ve acilma sartlari.
    ///
    /// Yeni animasyon eklemek: klibi projeye koy, bu dosyada listeye bir satir
    /// ekle, sartini sec. Herkesin ilk gordugu animasyon "Default Animasyon"
    /// alanindan secilir; o animasyon her zaman aciktir.
    /// </summary>
    [CreateAssetMenu(fileName = "LobiAnimasyonlari", menuName = "NewBattle/Lobi Animasyon Kutuphanesi")]
    public class LobbyAnimationLibrary : ScriptableObject
    {
        [SerializeField] private string defaultAnimationId = "bekleme";
        [SerializeField] private List<LobbyAnimation> animations = new();

        public IReadOnlyList<LobbyAnimation> Animations => animations;

        public string DefaultAnimationId => defaultAnimationId;

        public LobbyAnimation Default => Find(defaultAnimationId) ?? (animations.Count > 0 ? animations[0] : null);

        /// <summary>Oyuncunun secili animasyonu; secim yoksa ya da kilitliyse default.</summary>
        public LobbyAnimation Current
        {
            get
            {
                LobbyAnimation selected = Find(PlayerProgress.SelectedLobbyAnimation);
                return selected != null && IsUnlocked(selected) ? selected : Default;
            }
        }

        public LobbyAnimation Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (LobbyAnimation animation in animations)
            {
                if (animation != null && animation.id == id)
                    return animation;
            }

            return null;
        }

        public bool IsUnlocked(LobbyAnimation animation)
        {
            if (animation == null)
                return false;

            if (animation == Default)
                return true;

            // Hediye ya da seviye odulu olarak verilmis olabilir.
            if (PlayerProgress.Owns(animation.id))
                return true;

            return animation.unlock switch
            {
                LobbyUnlockType.Free => true,
                LobbyUnlockType.Level => PlayerProgress.Level >= animation.requirement,
                LobbyUnlockType.Wins => PlayerProgress.Wins >= animation.requirement,
                LobbyUnlockType.Coins => PlayerProgress.Owns(animation.id),
                _ => false
            };
        }

        /// <summary>Kilitli animasyonun sartini oyuncuya gosterilecek sekilde yazar.</summary>
        public string RequirementText(LobbyAnimation animation)
        {
            if (animation == null)
                return string.Empty;

            return animation.unlock switch
            {
                LobbyUnlockType.Level => $"Seviye {animation.requirement}",
                LobbyUnlockType.Wins => $"{animation.requirement} Galibiyet",
                LobbyUnlockType.Coins => $"{animation.requirement} Para",
                _ => "Ücretsiz"
            };
        }

        /// <summary>Parayla acilan animasyonu satin alir. Basariliysa true.</summary>
        public bool TryPurchase(LobbyAnimation animation)
        {
            if (animation == null || animation.unlock != LobbyUnlockType.Coins || IsUnlocked(animation))
                return false;

            if (!PlayerProgress.TrySpendCoins(animation.requirement))
                return false;

            PlayerProgress.GrantOwnership(animation.id);
            return true;
        }

        /// <summary>Acik bir animasyonu secer. Kilitliyse false.</summary>
        public bool Select(LobbyAnimation animation)
        {
            if (!IsUnlocked(animation))
                return false;

            PlayerProgress.SelectedLobbyAnimation = animation.id;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var seen = new HashSet<string>();
            foreach (LobbyAnimation animation in animations)
            {
                if (animation == null)
                    continue;

                if (!seen.Add(animation.id))
                    Debug.LogWarning($"Lobi animasyonlari: '{animation.id}' kimligi birden fazla kez kullanilmis.", this);
            }
        }
#endif
    }
}
