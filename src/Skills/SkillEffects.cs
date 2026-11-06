using System.Runtime.CompilerServices;
using HarmonyLib;
using IncreaseHardMod.Gear;
using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>
/// What the tree's perks do. Existing upgrade effects ask <see cref="Equivalent"/> for the level they used to read,
/// so a compressed ladder keeps its old strength at max (Melee 5/5 = the old 10/10) and the per-level config values
/// keep meaning what they did. New perks (roots, Arsenal, Flashlight, Second Wind) live here.
/// Levels are synced to every client, so effects that others can see (a teammate's flashlight) work everywhere;
/// damage and statuses are decided by the host.
/// </summary>
internal static class SkillEffects
{
    internal static bool On => ModConfig.PlayerUpgradesEnabled.Value;

    internal static int Level(PlayerAvatar avatar, string node) =>
        On && avatar != null && !string.IsNullOrEmpty(avatar.steamID) ? SkillStore.Level(avatar.steamID, node) : 0;
    internal static bool Has(PlayerAvatar avatar, string node) => Level(avatar, node) > 0;
    internal static int LocalLevel(string node) => Level(SemiFunc.PlayerAvatarLocal(), node);
    internal static bool LocalHas(string node) => LocalLevel(node) > 0;

    /// <summary>The tree level expressed on an old upgrade's ladder of <paramref name="oldMax"/> levels.</summary>
    internal static float Equivalent(PlayerAvatar avatar, string node, int oldMax)
    {
        var definition = C.Find(node);
        int level = Level(avatar, node);
        return definition == null || level <= 0 ? 0f : level * (float)Mathf.Max(1, oldMax) / definition.MaxLevel;
    }

    internal static float RootBonus => Mathf.Clamp(ModConfig.SkillRootBonus.Value, 0f, .5f);

    // ------------------------------------------------------------------ weapons

    internal enum Weapon : byte { None, Handgun, Revolver, Shotgun, CartCannon, CartLaser, WizardStaff, PoisonCrossbow, OtherGun }

    private static string NodeFor(Weapon weapon) => weapon switch
    {
        Weapon.Handgun => C.HandgunDamage,
        Weapon.Revolver => C.RevolverDamage,
        Weapon.Shotgun => C.ShotgunDamage,
        Weapon.CartCannon => C.CartCannonDamage,
        Weapon.CartLaser => C.CartLaserDamage,
        Weapon.WizardStaff => C.StaffDamage,
        Weapon.PoisonCrossbow => C.CrossbowDamage,
        _ => null,
    };

    /// <summary>Damage multiplier for this player's shot: Marksman's small bonus on every gun plus the weapon's own perk.</summary>
    internal static float WeaponMultiplier(PlayerAvatar shooter, Weapon weapon)
    {
        if (!On || shooter == null || weapon == Weapon.None) return 1f;
        float multiplier = 1f;
        if (Has(shooter, C.Marksman)) multiplier += RootBonus;
        var node = C.Find(NodeFor(weapon));
        if (node != null) multiplier += Mathf.Max(0f, ModConfig.SkillWeaponDamageMax.Value) * Level(shooter, node.Id) / node.MaxLevel;
        return multiplier;
    }

    internal static Weapon WeaponOf(GameObject source)
    {
        if (source == null) return Weapon.None;
        if (source.GetComponentInParent<ItemCartCannon>() != null) return Weapon.CartCannon;
        if (source.GetComponentInParent<ItemCartLaser>() != null) return Weapon.CartLaser;
        var gear = source.GetComponentInParent<GearGun>();
        if (gear != null) return gear.kind == GearKind.Lightning ? Weapon.WizardStaff : gear.kind == GearKind.Poison ? Weapon.PoisonCrossbow : Weapon.OtherGun;
        string name = source.GetComponentInParent<ItemAttributes>()?.item?.name ?? source.name;
        if (name.Contains("Gun Handgun")) return Weapon.Handgun;
        if (name.Contains("Gun Shotgun")) return Weapon.Shotgun;
        if (name.Contains("Gun Revolver") || source.GetComponentInParent<ItemGunRevolver>() != null) return Weapon.Revolver;
        return source.GetComponentInParent<ItemGun>() != null ? Weapon.OtherGun : Weapon.None;
    }

    private sealed class Tag { internal Weapon Weapon; internal bool Tranq; }
    private static readonly ConditionalWeakTable<HurtCollider, Tag> Tags = new();
    private static Weapon _firing;
    private static bool _firingTranq;

    // Bullets are spawned inside the gun's (or cart cannon's) ShootBulletRPC and activated right there, so the
    // weapon firing is known while the bullet activates; its hit colliders remember it.
    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.ShootBulletRPC))]
    private static class GunFiring
    {
        [HarmonyPrefix] private static void Prefix(ItemGun __instance) { _firing = WeaponOf(__instance.gameObject); _firingTranq = __instance.GetComponent<ItemGunTranq>() != null; }
        [HarmonyPostfix] private static void Postfix() { _firing = Weapon.None; _firingTranq = false; }
    }

    [HarmonyPatch(typeof(ItemCartCannon), nameof(ItemCartCannon.ShootBulletRPC))]
    private static class CannonFiring
    {
        [HarmonyPrefix] private static void Prefix() { _firing = Weapon.CartCannon; _firingTranq = false; }
        [HarmonyPostfix] private static void Postfix() => _firing = Weapon.None;
    }

    [HarmonyPatch(typeof(ItemGunBullet), nameof(ItemGunBullet.ActivateAll))]
    private static class BulletActivate
    {
        [HarmonyPrefix]
        private static void Prefix(ItemGunBullet __instance)
        {
            if (_firing == Weapon.None) return;
            foreach (var collider in __instance.GetComponentsInChildren<HurtCollider>(true))
            {
                Tags.Remove(collider);
                Tags.Add(collider, new Tag { Weapon = _firing, Tranq = _firingTranq });
            }
        }
    }

    /// <summary>Which weapon a hit collider belongs to, and who fired it (for the shared monster-hit hook).</summary>
    internal static Weapon WeaponHit(HurtCollider collider, out PlayerAvatar shooter, out bool tranq)
    {
        shooter = null;
        tranq = false;
        if (collider == null) return Weapon.None;
        if (Tags.TryGetValue(collider, out var tag))
        {
            shooter = collider.playerCausingHurt;
            tranq = tag.Tranq;
            return tag.Weapon;
        }
        var laser = collider.GetComponentInParent<ItemCartLaser>();
        if (laser != null)
        {
            var grab = laser.GetComponent<PhysGrabObject>();
            shooter = collider.playerCausingHurt != null ? collider.playerCausingHurt : grab != null ? grab.lastPlayerGrabbing : null;
            return Weapon.CartLaser;
        }
        return Weapon.None;
    }

    // ------------------------------------------------------------------ statuses

    internal enum Status : byte { Tranq, Net, Freeze, Poison }

    /// <summary>How long a status from this player's weapon lasts. Never past 8 s; poison shrinks toward 8 s instead.</summary>
    internal static float StatusSeconds(PlayerAvatar shooter, Status status, float baseSeconds)
    {
        if (!On || shooter == null) return baseSeconds;
        string id = status switch { Status.Tranq => C.TranqTime, Status.Net => C.NetTime, Status.Freeze => C.FreezeTime, _ => C.PoisonRush };
        var node = C.Find(id);
        bool poison = status == Status.Poison;
        float seconds = C.StatusSeconds(baseSeconds, Level(shooter, id), node.MaxLevel, poison);
        if (Has(shooter, C.StatusLab))
        {
            float step = Mathf.Max(0f, ModConfig.SkillStatusRootSeconds.Value);
            if (poison && seconds > C.StatusCapSeconds) seconds = Mathf.Max(C.StatusCapSeconds, seconds - step);
            else if (!poison && seconds < C.StatusCapSeconds) seconds = Mathf.Min(C.StatusCapSeconds, seconds + step);
        }
        return seconds;
    }

    // ------------------------------------------------------------------ flashlight (every client draws every flashlight)

    internal static readonly Color[] BeamColours =
    {
        Color.white,                       // 0: the game's own beam (no tint)
        new(1f, .93f, .78f),               // warm
        new(.82f, .9f, 1f),                // cool
        new(1f, .7f, .3f),                 // amber
        new(1f, .35f, .3f),                // red
        new(.45f, 1f, .45f),               // green
        new(.4f, .95f, 1f),                // cyan
        new(.45f, .55f, 1f),               // blue
        new(.85f, .45f, 1f),               // violet
    };
    internal static readonly string[] BeamColourNames = { "Default", "Warm", "Cool", "Amber", "Red", "Green", "Cyan", "Blue", "Violet" };

    /// <summary>Hidden per-player setting: chosen beam colour + 1 (0 = never chosen). Saved and synced like a level.</summary>
    internal const string BeamColourChoiceId = SkillStore.Prefix + "BeamColourChoice";

    internal static int BeamColourIndex(PlayerAvatar avatar)
    {
        if (!Has(avatar, C.BeamColour) || !RepoUpgrades.TryGetUpgrade(BeamColourChoiceId, out var choice)) return 0;
        return Mathf.Clamp(choice.GetLevel(avatar.steamID), 0, BeamColours.Length - 1);
    }

    /// <summary>The local player picks a beam colour. It is cosmetic, so the owner writes it directly; REPOLib syncs it.</summary>
    internal static void ChooseBeamColour(int index)
    {
        var local = SemiFunc.PlayerAvatarLocal();
        if (local == null || !Has(local, C.BeamColour) || !RepoUpgrades.TryGetUpgrade(BeamColourChoiceId, out var choice)) return;
        choice.SetLevel(local.steamID, Mathf.Clamp(index, 0, BeamColours.Length - 1));
    }

    private sealed class Beam { internal float Range, Angle; internal Color Colour; }
    private static readonly ConditionalWeakTable<FlashlightController, Beam> Beams = new();

    [HarmonyPatch(typeof(FlashlightController), "Update")]
    private static class FlashlightBeam
    {
        [HarmonyPostfix]
        private static void Postfix(FlashlightController __instance)
        {
            var light = __instance.spotlight;
            if (light == null) return;
            var beam = Beams.GetValue(__instance, _ => new Beam { Range = light.range, Angle = light.spotAngle, Colour = light.color });
            var owner = __instance.PlayerAvatar;
            float range = 1f, angle = 1f, bright = 1f;
            int colour = 0;
            if (On && owner != null && Has(owner, C.FlashTuning))
            {
                int width = Level(owner, C.BeamWidth), brightness = Level(owner, C.Brightness);
                range += Mathf.Max(0f, ModConfig.SkillFlashRange.Value) + .5f * brightness * Mathf.Max(0f, ModConfig.SkillFlashBrightnessPerLevel.Value);
                angle += width * Mathf.Max(0f, ModConfig.SkillFlashWidthPerLevel.Value);
                bright += brightness * Mathf.Max(0f, ModConfig.SkillFlashBrightnessPerLevel.Value);
                colour = BeamColourIndex(owner);
            }
            light.range = beam.Range * range;
            light.spotAngle = Mathf.Min(170f, beam.Angle * angle);
            light.intensity *= bright; // the game rewrites intensity every frame, so this never compounds
            light.color = colour > 0 ? BeamColours[colour] : beam.Colour;
        }
    }

    // ------------------------------------------------------------------ stamina: Survivor root + Second Wind (local player)

    internal sealed class StaminaController : MonoBehaviour
    {
        private PlayerController _controller;
        private float _applied, _last;

        private void LateUpdate()
        {
            var controller = PlayerController.instance;
            if (controller != _controller) { _controller = controller; _applied = 0f; _last = controller != null ? controller.EnergyCurrent : 0f; }
            if (controller == null || controller.playerAvatarScript == null) return;
            var avatar = controller.playerAvatarScript;

            // Survivor: a flat max-stamina bonus. The game only ever adds to EnergyStart, so track what we added.
            float bonus = Has(avatar, C.Survivor) ? Mathf.Max(0f, ModConfig.SkillSurvivorStamina.Value) : 0f;
            if (!Mathf.Approximately(bonus, _applied))
            {
                controller.EnergyStart += bonus - _applied;
                if (controller.EnergyCurrent > controller.EnergyStart) controller.EnergyCurrent = controller.EnergyStart;
                _applied = bonus;
            }

            // Second Wind: whatever the game recharged since last frame, recharge a share more.
            float now = controller.EnergyCurrent;
            float gained = now - _last;
            int wind = Level(avatar, C.SecondWind);
            if (wind > 0 && gained > 0f && gained < 20f && now < controller.EnergyStart)
            {
                now = Mathf.Min(controller.EnergyStart, now + gained * wind * Mathf.Max(0f, ModConfig.SkillSecondWindPerLevel.Value));
                controller.EnergyCurrent = now;
            }
            _last = now;
        }
    }
}
