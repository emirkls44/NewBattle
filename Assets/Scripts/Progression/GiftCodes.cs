using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NewBattle.Progression
{
    public static class GiftCodes
    {
        private const string RedeemedKey = "nb_redeemed_codes";

        public enum Result
        {
            Success,
            Invalid,
            AlreadyUsed,
            AlreadyOwned
        }

        /// <summary>Kodu dener; basariliysa odulu verir ve pencerede yazacak bildirimi doldurur.</summary>
        public static Result Redeem(string typed, out RewardNotice notice)
        {
            notice = null;
            GiftCodeCatalog catalog = GiftCodeCatalog.Instance;
            GiftCode code = catalog != null ? catalog.Find(typed) : null;

            if (code == null || !code.active)
                return Result.Invalid;

            string key = Normalize(code.code);
            HashSet<string> redeemed = LoadRedeemed();
            if (redeemed.Contains(key))
                return Result.AlreadyUsed;

            switch (code.reward)
            {
                case GiftRewardType.Character:
                {
                    CharacterLibrary library = CharacterLibrary.Instance;
                    CharacterDefinition character = library != null ? library.Find(code.itemId) : null;
                    if (character == null)
                        return Result.Invalid;
                    if (PlayerProgress.Owns(character.id))
                        return Result.AlreadyOwned;

                    PlayerProgress.GrantOwnership(character.id);
                    library.Select(character);
                    notice = new RewardNotice("HEDİYE!", $"{character.displayName}\nartık senin!", RewardNoticeIcon.Gift);
                    break;
                }

                case GiftRewardType.LobbyAnimation:
                    if (string.IsNullOrEmpty(code.itemId))
                        return Result.Invalid;
                    if (PlayerProgress.Owns(code.itemId))
                        return Result.AlreadyOwned;

                    PlayerProgress.GrantOwnership(code.itemId);
                    notice = new RewardNotice("HEDİYE!", "Yeni lobi animasyonu!", RewardNoticeIcon.Animation);
                    break;

                case GiftRewardType.Coins:
                    PlayerProgress.AddCoins(code.amount);
                    notice = new RewardNotice("HEDİYE!", $"+{code.amount} Para", RewardNoticeIcon.Coins);
                    break;

                case GiftRewardType.Xp:
                    PlayerProgress.AddXp(code.amount);
                    notice = new RewardNotice("HEDİYE!", $"+{code.amount} XP", RewardNoticeIcon.Xp);
                    break;
            }

            redeemed.Add(key);
            PlayerPrefs.SetString(RedeemedKey, string.Join(",", redeemed));
            PlayerPrefs.Save();
            return Result.Success;
        }

        /// <summary>Test icin: bu cihazda kullanilmis kodlari unutur.</summary>
        public static void ForgetRedeemed()
        {
            PlayerPrefs.DeleteKey(RedeemedKey);
            PlayerPrefs.Save();
        }

        /// <summary>Buyuk harf, sadece harf ve rakam: "hale-melek 1" = "HALEMELEK1". Turkce i/ı farki yok sayilir.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder();
            foreach (char c in text.Replace('ı', 'i').Replace('İ', 'I').ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(c);
            }

            return builder.ToString();
        }

        private static HashSet<string> LoadRedeemed()
        {
            string raw = PlayerPrefs.GetString(RedeemedKey, string.Empty);
            return new HashSet<string>(raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
