using System;
using UnityEngine;

namespace NewBattle.Progression
{
    public enum RewardType
    {
        Coins,
        Xp,
        LobbyAnimation
    }

    /// <summary>Tek bir odul: para, XP ya da lobi animasyonu.</summary>
    [Serializable]
    public class RewardEntry
    {
        public RewardType type = RewardType.Coins;
        [Min(0)] public int amount = 100;
        [Tooltip("Tip 'LobbyAnimation' ise: animasyonun kimligi (ornek: havali).")]
        public string animationId;
        [Tooltip("Tip 'LobbyAnimation' ise: pencerede gorunecek ad (ornek: Havalı Yürüyüş).")]
        public string label;
        [Tooltip("Rastgele secimde agirlik. 2, 1'in iki kati sik cikar.")]
        [Min(0f)] public float weight = 1f;

        public RewardEntry() { }

        public RewardEntry(RewardType type, int amount, float weight, string animationId = null, string label = null)
        {
            this.type = type;
            this.amount = amount;
            this.weight = weight;
            this.animationId = animationId;
            this.label = label;
        }
    }

    [Serializable]
    public class LevelReward
    {
        [Min(2)] public int level = 2;
        [Min(0)] public int coins = 100;
        [Tooltip("Istege bagli: bu seviyede hediye edilen lobi animasyonu (kimlik).")]
        public string animationId;
        public string animationLabel;
    }

    public enum ChallengeGoal
    {
        [Tooltip("Mac oyna (her mac 1).")]
        PlayMatches,
        [Tooltip("Rakip yen (her oldurme 1).")]
        Kills,
        [Tooltip("Mac kazan.")]
        Wins,
        [Tooltip("Ilk 3'e gir.")]
        TopThree,
        [Tooltip("Ilk 10'a gir.")]
        TopTen
    }

    /// <summary>Bir gunluk ya da haftalik gorev: "3 maç oyna" → +80 XP.</summary>
    [Serializable]
    public class ChallengeDef
    {
        public string id = "gorev";
        public string title = "3 maç oyna";
        public ChallengeGoal goal = ChallengeGoal.PlayMatches;
        [Min(1)] public int target = 3;
        [Min(0)] public int xp = 80;

        public ChallengeDef() { }

        public ChallengeDef(string id, string title, ChallengeGoal goal, int target, int xp)
        {
            this.id = id;
            this.title = title;
            this.goal = goal;
            this.target = target;
            this.xp = xp;
        }
    }

    /// <summary>Oyuncu seviyesine gore botlarin ne kadar zor oldugu.</summary>
    [Serializable]
    public class BotDifficultyStep
    {
        [Tooltip("Oyuncu bu seviyeye ulasinca bu satir gecerli olur.")]
        [Min(1)] public int fromLevel = 1;
        [Tooltip("Botun SANA verdigi hasarin carpani. 0.5 = yarisi.")]
        [Min(0f)] public float damageMultiplier = 1f;
        [Tooltip("Botun dayanikliligi. 2 = olmesi iki kat hasar ister, 0.5 = yarisi yeter.")]
        [Min(0.1f)] public float toughness = 1f;
        [Tooltip("Nisan hatasi carpani. 2 = iki kat iskalar.")]
        [Min(0f)] public float aimErrorMultiplier = 1f;
        [Tooltip("Tepki suresi carpani. 2 = seni gorunce iki kat gec ates eder.")]
        [Min(0.1f)] public float reactionMultiplier = 1f;

        public BotDifficultyStep() { }

        public BotDifficultyStep(int fromLevel, float damage, float toughness, float aimError, float reaction)
        {
            this.fromLevel = fromLevel;
            damageMultiplier = damage;
            this.toughness = toughness;
            aimErrorMultiplier = aimError;
            reactionMultiplier = reaction;
        }
    }

    /// <summary>
    /// Ilerlemenin tum rakamlari tek yerde: seviye egrisi, mac XP'si, gunluk
    /// giris odulu, ilk oyun hediyesi, seviye odulleri ve bot zorlugu.
    ///
    /// Dosya: Assets/Resources/IlerlemeAyarlari.asset (Menü > Kur olusturur).
    /// Inspector'dan degistirdigin her deger oyuna dogrudan yansir.
    /// </summary>
    [CreateAssetMenu(menuName = "NewBattle/Ilerleme Ayarlari", fileName = "IlerlemeAyarlari")]
    public class ProgressionSettings : ScriptableObject
    {
        public const string ResourceName = "IlerlemeAyarlari";

        [Header("Seviye Egrisi")]
        [Tooltip("Ilk seviyeler icin gereken XP. [0] = 1'den 2'ye, [1] = 2'den 3'e ...")]
        public int[] levelXp = { 200, 500, 1000 };
        [Tooltip("Listeden sonraki her seviye, bir oncekinin bu kati XP ister (2 = iki kati).")]
        [Min(1f)] public float growthAfterList = 2f;
        [Tooltip("Bir seviye icin istenebilecek en fazla XP. 0 = sinir yok.")]
        [Min(0)] public int maxXpPerLevel;

        [Header("Mac XP")]
        [Min(0)] public int participationXp = 40;
        [Min(0)] public int xpPerKill = 15;
        [Tooltip("Siralama XP'si: 1. tamamini alir, sonuncu hic almaz. 0 = kapali.")]
        [Min(0)] public int maxPlacementXp;
        [Min(0)] public int victoryXp = 100;

        [Header("Mac Parasi")]
        [Min(0)] public int coinsPerKill = 5;
        [Min(0)] public int topThreeCoins = 10;
        [Min(0)] public int victoryCoins = 25;

        [Header("Iki Kat XP (reklam)")]
        [Tooltip("Bir reklam kac maclik cift XP verir.")]
        [Min(1)] public int doubleXpMatchesPerAd = 1;
        [Tooltip("En fazla kac maclik cift XP biriktirilebilir.")]
        [Min(1)] public int maxStoredDoubleXp = 3;

        [Header("Gunluk Giris Odulu")]
        [Tooltip("Her gun oyuna girene verilen XP. Sirayla 1. gun, 2. gun ... Liste bitince odul biter.")]
        public int[] dailyLoginXp = { 50, 100, 150, 200, 300, 400, 800 };

        [Header("Ilk Oyun Hediyesi")]
        [Tooltip("Ilk mactan sonra bunlardan biri agirligina gore rastgele verilir.")]
        public RewardEntry[] firstGameGifts =
        {
            new(RewardType.Coins, 150, 3f),
            new(RewardType.Coins, 300, 2f),
            new(RewardType.Coins, 500, 1f),
            new(RewardType.LobbyAnimation, 0, 1f, "nisan", "Nişan Al")
        };

        [Header("Seviye Odulleri")]
        [Tooltip("Seviye atlayinca verilenler. Listede olmayan seviyeler asagidaki parayi alir.")]
        public LevelReward[] levelRewards =
        {
            new() { level = 2, coins = 100 },
            new() { level = 3, coins = 250 },
            new() { level = 4, coins = 500 },
            new() { level = 5, coins = 1000 }
        };
        [Min(0)] public int coinsPerLevelAfterList = 1000;

        [Header("Mucadeleler (gunluk / haftalik gorevler)")]
        [Tooltip("Her gun havuzdan kac gorev secilsin.")]
        [Min(1)] public int dailyChallengeCount = 3;
        [Tooltip("Gunluk gorev havuzu. Her gun bunlardan rastgele secilir.")]
        public ChallengeDef[] dailyChallenges =
        {
            new("g_mac1", "1 maç oyna", ChallengeGoal.PlayMatches, 1, 30),
            new("g_mac3", "3 maç oyna", ChallengeGoal.PlayMatches, 3, 80),
            new("g_oldur5", "5 rakip yen", ChallengeGoal.Kills, 5, 80),
            new("g_oldur10", "10 rakip yen", ChallengeGoal.Kills, 10, 120),
            new("g_ilk10", "Bir maçta ilk 10'a gir", ChallengeGoal.TopTen, 1, 50),
            new("g_ilk3", "Bir maçta ilk 3'e gir", ChallengeGoal.TopThree, 1, 100),
            new("g_kazan", "Bir maç kazan", ChallengeGoal.Wins, 1, 150)
        };

        [Tooltip("Her hafta (pazartesi) havuzdan kac gorev secilsin.")]
        [Min(1)] public int weeklyChallengeCount = 3;
        [Tooltip("Haftalik gorev havuzu.")]
        public ChallengeDef[] weeklyChallenges =
        {
            new("h_mac15", "15 maç oyna", ChallengeGoal.PlayMatches, 15, 400),
            new("h_oldur30", "30 rakip yen", ChallengeGoal.Kills, 30, 400),
            new("h_oldur50", "50 rakip yen", ChallengeGoal.Kills, 50, 700),
            new("h_ilk3", "5 kez ilk 3'e gir", ChallengeGoal.TopThree, 5, 500),
            new("h_kazan3", "3 maç kazan", ChallengeGoal.Wins, 3, 600)
        };

        [Header("Bot Zorlugu (oyuncu seviyesine gore)")]
        [Tooltip("Oyuncunun seviyesine en uygun (fromLevel'i en buyuk) satir kullanilir.")]
        public BotDifficultyStep[] botDifficulty =
        {
            new(1, 0.35f, 0.5f, 2f, 1.8f),
            new(2, 0.6f, 0.7f, 1.5f, 1.4f),
            new(3, 0.8f, 0.85f, 1.2f, 1.15f),
            new(4, 1f, 1f, 1f, 1f),
            new(6, 1.3f, 1.3f, 0.75f, 0.85f),
            new(8, 1.6f, 1.7f, 0.55f, 0.7f)
        };

        private const int HardXpLimit = 1_000_000_000;

        private static ProgressionSettings _instance;
        private static bool _isFallback;

        public static ProgressionSettings Instance
        {
            get
            {
                if (_instance != null && !_isFallback)
                    return _instance;

                // Dosya henuz yoksa varsayilan degerlerle calisir; dosya
                // olusturulunca (Menü > Kur) bir sonraki cagrida onu kullanir.
                ProgressionSettings loaded = Resources.Load<ProgressionSettings>(ResourceName);
                if (loaded != null)
                {
                    _instance = loaded;
                    _isFallback = false;
                }
                else if (_instance == null)
                {
                    _instance = CreateInstance<ProgressionSettings>();
                    _isFallback = true;
                }

                return _instance;
            }
        }

        /// <summary>Verilen seviyeden bir sonrakine gecmek icin gereken XP.</summary>
        public int XpForLevel(int level)
        {
            int index = Mathf.Max(1, level) - 1;
            double xp;

            if (levelXp == null || levelXp.Length == 0)
                xp = 200d * Math.Pow(growthAfterList, index);
            else if (index < levelXp.Length)
                xp = levelXp[index];
            else
                xp = levelXp[^1] * Math.Pow(growthAfterList, index - (levelXp.Length - 1));

            if (maxXpPerLevel > 0)
                xp = Math.Min(xp, maxXpPerLevel);

            return (int)Math.Clamp(Math.Round(xp), 1d, HardXpLimit);
        }

        public BotDifficultyStep BotDifficultyFor(int level)
        {
            BotDifficultyStep best = null;
            if (botDifficulty == null)
                return new BotDifficultyStep();

            foreach (BotDifficultyStep step in botDifficulty)
            {
                if (step == null || step.fromLevel > level)
                    continue;

                if (best == null || step.fromLevel > best.fromLevel)
                    best = step;
            }

            return best ?? new BotDifficultyStep();
        }

        public LevelReward LevelRewardFor(int level)
        {
            if (levelRewards != null)
            {
                foreach (LevelReward reward in levelRewards)
                {
                    if (reward != null && reward.level == level)
                        return reward;
                }
            }

            return new LevelReward { level = level, coins = coinsPerLevelAfterList };
        }

        public RewardEntry PickFirstGameGift()
        {
            if (firstGameGifts == null || firstGameGifts.Length == 0)
                return null;

            float total = 0f;
            foreach (RewardEntry entry in firstGameGifts)
                total += entry != null ? entry.weight : 0f;

            if (total <= 0f)
                return firstGameGifts[0];

            float roll = UnityEngine.Random.value * total;
            foreach (RewardEntry entry in firstGameGifts)
            {
                if (entry == null)
                    continue;

                roll -= entry.weight;
                if (roll <= 0f)
                    return entry;
            }

            return firstGameGifts[^1];
        }
    }
}
