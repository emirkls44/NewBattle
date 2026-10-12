using Fusion;
using NewBattle.Progression;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Botlarin zorlugu, oyuncunun seviyesine gore (IlerlemeAyarlari >
    /// Bot Zorlugu). Seviye 1'de botlar cabuk olur ve az vurur; seviye
    /// yukseldikce hem dayaniklilasir hem daha cok vurur.
    ///
    /// Hasar host'ta hesaplandigi icin host'un (bot macinda tek oyuncunun)
    /// seviyesi kullanilir.
    /// </summary>
    public static class BotDifficulty
    {
        public static BotDifficultyStep Current =>
            ProgressionSettings.Instance.BotDifficultyFor(PlayerProgress.Level);

        public static bool IsBot(NetworkObject networkObject) =>
            networkObject != null && networkObject.InputAuthority == PlayerRef.None;

        /// <summary>
        /// Hasar carpani: bot gercek oyuncuyu vuruyorsa botun gucu, gercek
        /// oyuncu botu vuruyorsa botun dayanikliligi uygulanir. Bot-bot ve
        /// alan hasari degismez.
        /// </summary>
        public static float DamageScale(NetworkObject attacker, NetworkObject victim)
        {
            if (attacker == null || victim == null || attacker == victim)
                return 1f;

            bool attackerIsBot = IsBot(attacker);
            bool victimIsBot = IsBot(victim);

            if (attackerIsBot && !victimIsBot)
                return Current.damageMultiplier;

            if (!attackerIsBot && victimIsBot)
                return 1f / Current.toughness;

            return 1f;
        }
    }
}
