using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace IncreaseHardMod.Skills;

/// <summary>Personal firing cadence; does not change beam duration, bullet speed or battery cost.</summary>
[HarmonyPatch]
internal static class WeaponFireRate
{
    private sealed class Scope
    {
        internal bool Active;
        internal ItemGun Gun;
        internal ItemGunRevolver Revolver;
        internal float Cooldown, Recover, Index;
    }
    private static readonly ConditionalWeakTable<ItemGun, Scope> Scopes = new();

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(ItemGun), "Update");
        foreach (string name in new[] { "Update", "TriggerPull", "TriggerRPC", "ChamberRPC" })
            yield return AccessTools.Method(typeof(ItemGunRevolver), name);
    }

    [HarmonyPrefix]
    private static void Prefix(object __instance, out Scope __state)
    {
        __state = null;
        var gun = __instance as ItemGun ?? (__instance as ItemGunRevolver)?.GetComponent<ItemGun>();
        if (gun == null) return;
        var scope = Scopes.GetOrCreateValue(gun);
        if (scope.Active) return; // TriggerPull/TriggerRPC may run inside ItemGun.Update.
        var equipped = gun.GetComponent<ItemEquippable>();
        var grab = gun.GetComponent<PhysGrabObject>();
        PlayerAvatar user = equipped != null && equipped.isEquipped ? equipped.GetOwnerPlayerAvatar() :
            grab != null && grab.playerGrabbing.Count > 0 ? grab.playerGrabbing[0]?.playerAvatar : grab?.lastPlayerGrabbing;
        float rate = PerkRules.FireRate(SkillEffects.Level(user, SkillCatalog.RapidFire));
        if (rate <= 1f) return;
        scope.Gun = gun;
        scope.Revolver = gun.GetComponent<ItemGunRevolver>();
        scope.Cooldown = gun.shootCooldown;
        scope.Active = true;
        __state = scope;
        gun.shootCooldown /= rate;
        if (scope.Revolver != null)
        {
            scope.Recover = scope.Revolver.recoverTime;
            scope.Index = scope.Revolver.indexTime;
            scope.Revolver.recoverTime /= rate;
            scope.Revolver.indexTime /= rate;
        }
    }

    [HarmonyFinalizer]
    private static void Restore(Scope __state)
    {
        if (__state == null) return;
        if (__state.Gun != null) __state.Gun.shootCooldown = __state.Cooldown;
        if (__state.Revolver != null)
        {
            __state.Revolver.recoverTime = __state.Recover;
            __state.Revolver.indexTime = __state.Index;
        }
        __state.Active = false;
    }
}
