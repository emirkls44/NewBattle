using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Sahnedeki tum oyuncularin (bot dahil) tek merkezi listesi.
    ///
    /// NEDEN VAR: Proje bir yerde her tick, bir yerde her hasar olayinda
    /// FindObjectsByType cagiriyordu. Bu cagri sahnedeki BUTUN nesneleri tarar ve
    /// her seferinde yeni bir dizi tahsis eder. Loot bolgesi sistemiyle ayni anda
    /// ~30 loot canli olabiliyor; her biri her tick tum oyunculari tariyordu.
    /// 16 oyuncu x 30 loot x 60 tick = saniyede 1800 tam sahne taramasi ve 1800
    /// dizi tahsisi. Mobilde bunun adi kasma.
    ///
    /// Burada kayit Spawned/Despawned'da bir kez yapilir, okuma bedavaya gelir.
    ///
    /// KULLANIM KURALI: Donen liste CANLI listedir. Icinde dolasirken oyuncu
    /// despawn olursa liste degisir. Tick icinde despawn tetikleyebilecek
    /// dongulerde (ornegin loot toplama) once indeksle gezin, sonra islem yap.
    /// </summary>
    public class PlayerRegistry : NetworkBehaviour
    {
        private static readonly List<PlayerRegistry> Entries = new();
        private static readonly Dictionary<PlayerRef, PlayerRegistry> ByPlayer = new();

        /// <summary>Sahnedeki tum oyuncular ve botlar.</summary>
        public static IReadOnlyList<PlayerRegistry> All => Entries;

        public HealthController Health { get; private set; }
        public PlayerLoadout Loadout { get; private set; }
        public PlayerCombatStats Stats { get; private set; }
        public PlayerVisibility Visibility { get; private set; }
        public PlayerPresence Presence { get; private set; }

        /// <summary>Gercek oyuncu mu (bot degil).</summary>
        public bool IsHuman => Object != null && Object.InputAuthority != PlayerRef.None;

        /// <summary>Hayatta mi. Olu veya gecersiz nesneler icin false.</summary>
        public bool IsAlive => Object != null && Object.IsValid &&
                               Health != null && Health.currentHealth > 0f;

        private void Awake()
        {
            Health = GetComponent<HealthController>();
            Loadout = GetComponent<PlayerLoadout>();
            Stats = GetComponent<PlayerCombatStats>();
            Visibility = GetComponent<PlayerVisibility>();
            Presence = GetComponent<PlayerPresence>();
        }

        public override void Spawned()
        {
            if (!Entries.Contains(this))
                Entries.Add(this);

            if (Object.InputAuthority != PlayerRef.None)
                ByPlayer[Object.InputAuthority] = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void Unregister()
        {
            Entries.Remove(this);

            if (Object != null && Object.InputAuthority != PlayerRef.None)
                ByPlayer.Remove(Object.InputAuthority);
        }

        /// <summary>Verilen oyuncuya ait kayit. Yoksa null.</summary>
        public static PlayerRegistry Find(PlayerRef player)
        {
            if (player == PlayerRef.None)
                return null;

            return ByPlayer.TryGetValue(player, out PlayerRegistry entry) ? entry : null;
        }

        /// <summary>
        /// Verilen noktaya en yakin, hayatta ve dusman olan oyuncu.
        /// requireVisible aciksa cimen gizlenmesine uyar - bot da oyuncuyla ayni
        /// kurala tabidir, yoksa cimende saklanmak ise yaramaz.
        /// </summary>
        public static PlayerRegistry FindNearestEnemy(
            Vector3 position,
            PlayerCombatStats askerStats,
            NetworkRunner runner,
            GameObject exclude = null,
            bool humansOnly = false,
            bool requireVisible = false)
        {
            PlayerRegistry nearest = null;
            float nearestSquared = float.MaxValue;

            for (int i = 0; i < Entries.Count; i++)
            {
                PlayerRegistry entry = Entries[i];

                if (entry == null || entry.gameObject == exclude || !entry.IsAlive)
                    continue;

                if (runner != null && entry.Runner != runner)
                    continue;

                if (humansOnly && !entry.IsHuman)
                    continue;

                if (askerStats != null && askerStats.IsTeammate(entry.Stats))
                    continue;

                if (requireVisible && entry.Visibility != null &&
                    !entry.Visibility.IsBodyVisibleFrom(position))
                    continue;

                float squared = (entry.transform.position - position).sqrMagnitude;
                if (squared >= nearestSquared)
                    continue;

                nearestSquared = squared;
                nearest = entry;
            }

            return nearest;
        }
    }
}
