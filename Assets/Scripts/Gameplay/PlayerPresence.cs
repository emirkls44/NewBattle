using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Bir oyuncunun "dunyaya karsi durumu": cimende mi, hareket ediyor mu, catisma icinde mi.
    ///
    /// Burada sadece NESNEL gercekler aga yazilir. "Kim kimi goruyor" karari aga
    /// gonderilmez; her istemci kendi gozlemcisine gore o karari yerel olarak verir
    /// (bkz. PlayerVisibility). Bunun iki sebebi var:
    ///   - Ayni oyuncu takim arkadasi icin gorunur, dusman icin gorunmez olmali.
    ///     Tek bir networked "gorunur mu" bayragi bunu ifade edemez.
    ///   - Gorunurluk her karede degisebilen bir sey; her degisimi aga yazmak bant
    ///     genisligini bosa harcar.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerCombatStats))]
    public class PlayerPresence : NetworkBehaviour
    {
        [Header("Hareket Algilama")]
        [SerializeField, Min(0f)] private float movementSpeedThreshold = 0.35f;

        [Header("Catisma Hafizasi")]
        [Tooltip("Son hasar alis/verisinden sonra kac saniye 'catismada' sayilsin.")]
        [SerializeField, Min(0.5f)] private float combatMemorySeconds = 4f;

        [Networked] public NetworkBool InGrass { get; set; }
        [Networked] public NetworkBool IsMoving { get; set; }
        [Networked] private TickTimer CombatTimer { get; set; }

        /// <summary>Son birkac saniye icinde hasar aldi veya verdi mi.</summary>
        public bool InCombat => Runner != null && !CombatTimer.ExpiredOrNotRunning(Runner);

        /// <summary>
        /// PlayerRef -> Presence kaydi. Saldirganin catisma sayacini tazelemek icin
        /// her vurusta sahneyi taramak yerine (mevcut kodda oldugu gibi) O(1) bakiyoruz.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<PlayerRef, PlayerPresence> Registry = new();

        private PlayerCombatStats _stats;
        private Vector3 _lastPosition;

        /// <summary>
        /// Ic ice gecmis cali hacimlerinde dogru calismasi icin sayac tutuyoruz.
        /// Tek bir bool olsaydi, iki calinin kesistigi yerden cikarken oyuncu
        /// hala calidayken "cimenden ciktim" derdi.
        /// </summary>
        private int _grassVolumeCount;

        public override void Spawned()
        {
            _stats = GetComponent<PlayerCombatStats>();
            _lastPosition = transform.position;

            if (HasInputAuthority)
                LocalPlayerContext.Register(_stats);

            if (Object.InputAuthority != PlayerRef.None)
                Registry[Object.InputAuthority] = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            LocalPlayerContext.Unregister(_stats);

            if (Object != null && Object.InputAuthority != PlayerRef.None)
                Registry.Remove(Object.InputAuthority);
        }

        /// <summary>
        /// Sahnedeki tum gercek oyuncular (botlar haric - onlarin InputAuthority'si yok).
        /// Loot bolgelerini kimin actigini belirlemek icin kullanilir.
        /// </summary>
        public static System.Collections.Generic.Dictionary<PlayerRef, PlayerPresence>.ValueCollection All
            => Registry.Values;

        /// <summary>Verilen oyuncuya ait Presence. Yoksa null.</summary>
        public static PlayerPresence Find(PlayerRef player)
        {
            if (player == PlayerRef.None)
                return null;

            return Registry.TryGetValue(player, out PlayerPresence presence) ? presence : null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;

            float deltaTime = Runner.DeltaTime;
            if (deltaTime <= 0f)
                return;

            float speed = Vector3.Distance(transform.position, _lastPosition) / deltaTime;
            IsMoving = speed > movementSpeedThreshold;
            _lastPosition = transform.position;
        }

        /// <summary>Hasar alindiginda veya verildiginde cagrilir; catisma sayacini tazeler.</summary>
        public void MarkCombat()
        {
            if (!HasStateAuthority || Runner == null)
                return;

            CombatTimer = TickTimer.CreateFromSeconds(Runner, combatMemorySeconds);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!HasStateAuthority || !other.CompareTag("Bush"))
                return;

            _grassVolumeCount++;
            InGrass = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!HasStateAuthority || !other.CompareTag("Bush"))
                return;

            _grassVolumeCount = Mathf.Max(0, _grassVolumeCount - 1);
            InGrass = _grassVolumeCount > 0;
        }
    }
}
