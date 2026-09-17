using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Yerel oyuncuya (bu cihazi kullanan kisiye) tek noktadan erisim.
    ///
    /// Cember rengi, can bari gorunurlugu ve gizlenme kurallarinin hepsi
    /// "gozlemciye gore" hesaplanir; her biri ayri ayri sahnede yerel oyuncuyu
    /// aramak yerine burada tutulan onbellegi kullanir.
    /// </summary>
    public static class LocalPlayerContext
    {
        public static PlayerCombatStats Stats { get; private set; }
        public static Transform Transform { get; private set; }

        public static bool IsReady => Stats != null && Transform != null;

        public static void Register(PlayerCombatStats stats)
        {
            if (stats == null)
                return;

            Stats = stats;
            Transform = stats.transform;
        }

        public static void Unregister(PlayerCombatStats stats)
        {
            if (Stats != stats)
                return;

            Stats = null;
            Transform = null;
        }

        /// <summary>
        /// Gozlemcinin verilen oyuncuyla iliskisi. Yerel oyuncu henuz spawn olmadiysa
        /// (inis fazi, seyirci modu) herkesi dusman saymak yerine Unknown doneriz -
        /// cagiran taraf bu durumda gizleme yapmaz, yoksa harita bos gorunur.
        /// </summary>
        public static PlayerRelation GetRelation(PlayerCombatStats other)
        {
            if (other == null)
                return PlayerRelation.Unknown;

            if (!IsReady)
                return PlayerRelation.Unknown;

            if (other == Stats)
                return PlayerRelation.Self;

            return Stats.IsTeammate(other) ? PlayerRelation.Teammate : PlayerRelation.Enemy;
        }

        public static float DistanceTo(Vector3 worldPosition)
        {
            return IsReady ? Vector3.Distance(Transform.position, worldPosition) : float.MaxValue;
        }
    }

    public enum PlayerRelation
    {
        Unknown,
        Self,
        Teammate,
        Enemy
    }
}
