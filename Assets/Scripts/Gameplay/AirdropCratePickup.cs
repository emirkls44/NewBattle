using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Airdrop sandigi: gokten parasutle iner, yere degince 3 saniye uzerinde
    /// beklenerek acilir ve icindeki gucli silahi verir.
    ///
    /// Toplama mekanigi TimedLootPickup'tan miras aliniyor - normal loot ile ayni
    /// "uzerinde bekle, halka dolsun" hissi. Fark sadece surenin uzun olmasi ve
    /// verdigi silahin kademesi.
    ///
    /// Inis, sandiktaki ayri bir bilesen degil burada: sandik NetworkTransform ile
    /// senkronize oldugu icin sunucunun konumu hareket ettirmesi yeterli.
    /// </summary>
    public class AirdropCratePickup : TimedLootPickup
    {
        [Header("Icerik")]
        [SerializeField, Min(1)] private int ammoAmount = 60;
        [SerializeField, Min(1)] private int weaponTier = 1;

        [Header("Inis")]
        [SerializeField, Min(0f)] private float fallSpeed = 7f;
        [SerializeField] private float groundOffset = 0.05f;

        [Header("Gorsel")]
        [SerializeField] private Transform parachuteVisual;
        [SerializeField] private float swaySpeed = 1.3f;
        [SerializeField] private float swayAmount = 5f;

        [Networked] private NetworkBool Landed { get; set; }
        [Networked] private float TargetGroundY { get; set; }

        protected override bool CanCollect(PlayerLoadout player)
        {
            // Havada asili sandik toplanamaz; once yere inmeli.
            return Landed && player != null;
        }

        protected override void GiveTo(PlayerLoadout player)
        {
            player.GrantRifle(ammoAmount, weaponTier);
        }

        /// <summary>Sunucu spawn'dan hemen sonra cagirir.</summary>
        public void ConfigureDrop(float groundY)
        {
            if (!HasStateAuthority)
                return;

            TargetGroundY = groundY;
            Landed = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority && !Landed)
            {
                Descend();

                // Inmeden toplama mantigini hic calistirma.
                return;
            }

            base.FixedUpdateNetwork();
        }

        private void Descend()
        {
            Vector3 position = transform.position;
            float nextY = position.y - fallSpeed * Runner.DeltaTime;

            if (nextY <= TargetGroundY + groundOffset)
            {
                transform.position = new Vector3(position.x, TargetGroundY + groundOffset, position.z);
                Landed = true;
                return;
            }

            transform.position = new Vector3(position.x, nextY, position.z);
        }

        public override void Render()
        {
            if (parachuteVisual != null)
            {
                bool showParachute = !Landed;

                if (parachuteVisual.gameObject.activeSelf != showParachute)
                    parachuteVisual.gameObject.SetActive(showParachute);

                if (showParachute)
                {
                    float sway = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
                    parachuteVisual.localRotation = Quaternion.Euler(sway * 0.4f, 0f, sway);
                }
            }

            // Halkalar sadece yere indikten sonra anlamli.
            if (Landed)
                base.Render();
        }
    }
}
