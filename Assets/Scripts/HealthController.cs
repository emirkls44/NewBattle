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

        Rpc_ShowDamageNumber(baseDamage, shieldWasHit, attacker, currentHealth <= 0f);

        if (currentHealth <= 0f)
        {
            RecordDeath(attacker, attackerObject, weaponLabel);
            AwardKill(attacker);
            Rpc_AnnounceDeath(attackerObject != null ? attackerObject.Id : default, weaponLabel ?? string.Empty);
            Die();
        }
    }

    /// <summary>
    /// Olumu tum cihazlara duyurur: oldurme akisi, oldurme afisi ve "son 3
    /// oyuncu" uyarisi bunu dinler (bkz. KillEvents). Oldureni bos ise alan oldurdu.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_AnnounceDeath(NetworkId killerId, string weaponLabel)
    {
        PlayerCombatStats victimStats = GetComponent<PlayerCombatStats>();
        PlayerCombatStats killerStats = null;
        if (killerId.IsValid && Runner != null && Runner.TryFindObject(killerId, out NetworkObject killerObject))
            killerStats = killerObject.GetComponent<PlayerCombatStats>();

        PlayerCombatStats local = FindLocalStats();

        int alive = 0;
        foreach (HealthController player in FindObjectsByType<HealthController>(FindObjectsSortMode.None))
        {
            if (player != this && player.Object != null && player.Object.IsValid && player.currentHealth > 0f)
                alive++;
        }

        NewBattle.Gameplay.KillEvents.Raise(new NewBattle.Gameplay.KillInfo
        {
            KillerName = killerStats != null ? killerStats.NameOrDefault : string.Empty,
            VictimName = victimStats != null ? victimStats.NameOrDefault : "Oyuncu",
            Weapon = NewBattle.Gameplay.KillEvents.WeaponDisplayName(weaponLabel),
            ByZone = killerStats == null,
            KillerIsLocal = killerStats != null && killerStats.HasInputAuthority,
            VictimIsLocal = HasInputAuthority,
            KillerIsTeammate = killerStats != null && local != null && killerStats != local && local.IsTeammate(killerStats),
            VictimIsTeammate = victimStats != null && local != null && victimStats != local && local.IsTeammate(victimStats),
            AliveAfter = alive,
            VictimPosition = transform.position
        });
    }

    private static PlayerCombatStats FindLocalStats()
    {
        foreach (PlayerCombatStats stats in FindObjectsByType<PlayerCombatStats>(FindObjectsSortMode.None))
        {
            if (stats.Object != null && stats.Object.IsValid && stats.HasInputAuthority)
                return stats;
        }

        return null;
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
    private void Rpc_ShowDamageNumber(float damage, bool shieldHit, PlayerRef attacker, bool killed)
    {
        DamageNumberPopup.Show(transform.position, damage, shieldHit);
        PlayHitSounds(attacker, killed);

        if (HasInputAuthority)
            NewBattle.Gameplay.KillEvents.RaiseLocalDamaged(damage);

        // Govdenin kendisi de tepki versin: hasar sayisi karakterin ustunde
        // belirirken goz nisan alirken govdede oluyor.
        NewBattle.Gameplay.HitFlash.Play(gameObject);

        if (_animationDriver != null)
            _animationDriver.PlayHitFlinch();
    }

    /// <summary>
    /// Vuran bensem isabet "tik"i (oldurduysem zil), vurulan bensem darbe
    /// (oldusem olum sesi). Baskalarinin birbirini vurmasi sessiz; onu
    /// zaten atis sesinden duyuyoruz.
    /// </summary>
    private void PlayHitSounds(PlayerRef attacker, bool killed)
    {
        if (Runner == null)
            return;

        if (HasInputAuthority)
        {
            NewBattle.Audio.Sfx.PlayHud(killed ? "olum" : "hasar_al");
            return;
        }

        if (attacker != PlayerRef.None && attacker == Runner.LocalPlayer)
            NewBattle.Audio.Sfx.PlayHud(killed ? "oldurme" : "isabet");
    }

    private void Die()
    {
        OnDeath?.Invoke();
    }
}
