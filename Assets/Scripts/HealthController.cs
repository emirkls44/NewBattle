using System;
using Fusion;
using UnityEngine;

public class HealthController : NetworkBehaviour
{
    public enum DeathCause : byte
    {
        None = 0,
        Player = 1,
        Zone = 2
    }

    [Header("Health Settings")]
    public float maxHealth = 100f;
    [Networked] public float currentHealth { get; set; }

    [Header("Shield Settings")]
    public float maxShield = 100f;
    [Networked] public float currentShield { get; set; }

    [Tooltip("Kalkanin hasarin ne kadarini emdigi. 1 = hasar once tamamen kalkandan " +
             "gider, kalkan bitince tasan kisim cana isler.")]
    [Range(0f, 1f)] public float shieldAbsorbRatio = 1f;

    [Networked] public DeathCause LastDeathCause { get; private set; }
    /// <summary>Oldureni (oyuncu ya da bot) ag nesnesi.</summary>
    [Networked] public NetworkId KillerObject { get; private set; }
    [Networked] public NetworkString<_16> KillerWeapon { get; private set; }

    public event Action OnDeath;
    public event Action OnHealthChanged;

    private ChangeDetector _changeDetector;
    private NewBattle.Gameplay.PlayerPresence _presence;
    private PlayerCombatStats _ownStats;
    private CharacterAnimationDriver _animationDriver;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _animationDriver = GetComponent<CharacterAnimationDriver>();

        if (HasStateAuthority)
        {
            currentHealth = maxHealth;
            currentShield = 0f;
        }
    }

    public void TakeDamage(float baseDamage)
    {
        TakeDamage(baseDamage, PlayerRef.None);
    }

    public void TakeDamage(float baseDamage, PlayerRef attacker)
    {
        TakeDamage(baseDamage, attacker, null, null);
    }

    /// <summary>
    /// attackerObject / weaponLabel: kimin hangi silahla vurdugu. Olum
    /// mesajinda ("Dorian seni Pompali ile vurdu!") kullanilir. Ikisi de
    /// bossa hasar oyuncudan degil, guvenli alandan gelmistir.
    /// </summary>
    public void TakeDamage(float baseDamage, PlayerRef attacker, NetworkObject attackerObject, string weaponLabel)
    {
        if (!HasStateAuthority || currentHealth <= 0f || baseDamage <= 0f)
            return;

        if (IsFriendlyFire(attacker))
            return;

        // Bot zorlugu (oyuncu seviyesine gore): bkz. BotDifficulty.
        baseDamage *= NewBattle.Gameplay.BotDifficulty.DamageScale(attackerObject, Object);
        if (baseDamage <= 0f)
            return;

        bool shieldWasHit = currentShield > 0f;

        // Kalkan EMER, bagisiklik vermez. Hasarin shieldAbsorbRatio kadari once
        // kalkandan dusulur; kalkan yetmezse arta kalan dogrudan cana isler.
        //
        // Onceki model doluluk oranini hasar azaltma yuzdesine ceviriyordu ve tam
        // kalkanda cana SIFIR hasar gidiyordu - yani 100 kalkanli bir oyuncu
        // kalkani bitene kadar oldurulemiyordu. Bir battle royale'de bu, ilk
        // kalkani bulan oyuncuyu dokunulmaz yapar.
        float absorbable = baseDamage * shieldAbsorbRatio;
        float absorbed = Mathf.Min(currentShield, absorbable);

        currentShield = Mathf.Max(0f, currentShield - absorbed);
        float healthDamage = baseDamage - absorbed;

        if (healthDamage > 0f)
            currentHealth = Mathf.Max(0f, currentHealth - healthDamage);

        // Hem vurulan hem vuran "catismada" sayilir. Kafa ustu can barlari
        // sadece bu pencere boyunca gorunur (bkz. WorldHealthBar).
        MarkCombatants(attacker);

        Rpc_ShowDamageNumber(baseDamage, shieldWasHit);

        if (currentHealth <= 0f)
        {
            RecordDeath(attacker, attackerObject, weaponLabel);
            AwardKill(attacker);
            Die();
        }
    }

    private void RecordDeath(PlayerRef attacker, NetworkObject attackerObject, string weaponLabel)
    {
        bool byPlayer = attackerObject != null || attacker != PlayerRef.None;
        LastDeathCause = byPlayer ? DeathCause.Player : DeathCause.Zone;
        KillerObject = attackerObject != null ? attackerObject.Id : default;

        string weapon = weaponLabel ?? string.Empty;
        KillerWeapon = weapon.Length > 16 ? weapon.Substring(0, 16) : weapon;
    }

    private void MarkCombatants(PlayerRef attacker)
    {
        if (_presence == null)
            _presence = GetComponent<NewBattle.Gameplay.PlayerPresence>();

        if (_presence != null)
            _presence.MarkCombat();

        NewBattle.Gameplay.PlayerPresence attackerPresence =
            NewBattle.Gameplay.PlayerPresence.Find(attacker);

        if (attackerPresence != null)
            attackerPresence.MarkCombat();
    }

    private bool IsFriendlyFire(PlayerRef attacker)
    {
        if (attacker == PlayerRef.None)
            return false;

        if (_ownStats == null)
            _ownStats = GetComponent<PlayerCombatStats>();

        if (_ownStats == null)
            return false;

        // Sahne taramasi yerine O(1) kayit defteri: her mermi icin butun
        // sahneyi taramak mobilde olculebilir bir maliyetti.
        NewBattle.Gameplay.PlayerRegistry attackerEntry =
            NewBattle.Gameplay.PlayerRegistry.Find(attacker);

        return attackerEntry != null && _ownStats.IsTeammate(attackerEntry.Stats);
    }

    private void AwardKill(PlayerRef attacker)
    {
        if (attacker == PlayerRef.None || (Object != null && Object.InputAuthority == attacker))
            return;

        NewBattle.Gameplay.PlayerRegistry attackerEntry =
            NewBattle.Gameplay.PlayerRegistry.Find(attacker);

        if (attackerEntry != null && attackerEntry.Stats != null)
            attackerEntry.Stats.AddKill();
    }

    public void Heal(float healAmount)
    {
        if (!HasStateAuthority || healAmount <= 0f)
            return;

        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
    }

    public void AddShield(float shieldAmount)
    {
        if (!HasStateAuthority || shieldAmount <= 0f)
            return;

        currentShield = Mathf.Min(currentShield + shieldAmount, maxShield);
    }

    public override void Render()
    {
        if (_changeDetector == null)
            return;

        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(currentHealth) || change == nameof(currentShield))
                OnHealthChanged?.Invoke();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ShowDamageNumber(float damage, bool shieldHit)
    {
        DamageNumberPopup.Show(transform.position, damage, shieldHit);

        // Govdenin kendisi de tepki versin: hasar sayisi karakterin ustunde
        // belirirken goz nisan alirken govdede oluyor.
        NewBattle.Gameplay.HitFlash.Play(gameObject);

        if (_animationDriver != null)
            _animationDriver.PlayHitFlinch();
    }

    private void Die()
    {
        OnDeath?.Invoke();
    }
}
