using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Drones;
using IncreaseHardMod.Networking;
using UnityEngine;

namespace IncreaseHardMod.Gear;

/// <summary>
/// Defence / Protection shield logic for the cloned orbs and the Defence drone.
///
/// Damage to a player is handled on that player's own client (PlayerHealth.Hurt is owner-only), so the
/// shield is applied there from replicated item state: an orb covers a player standing inside its
/// radius while Active; the Defence drone covers the player it is attached to. The battery is owned by the
/// host, so a client that absorbed damage asks the host to drain it (<see cref="ModNetwork.BroadcastBatteryDrain"/>).
///
/// Defence: a full charge absorbs at least 450 direct damage (drone) or $10,000 lost value (orb).
/// Protection: at least 30% less direct damage, at 1.5 times the native orb drain.
/// </summary>
internal sealed class ShieldItem : MonoBehaviour
{
    public ShieldMode mode;

    public static readonly List<ShieldItem> All = new();

    private ItemOrb _orb;
    private UtilityDrone _drone;
    internal ItemBattery Battery;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        _orb = GetComponent<ItemOrb>();
        _drone = GetComponent<UtilityDrone>();
        Battery = GetComponent<ItemBattery>();
        StartCoroutine(AdjustDrain());
    }

    // ItemOrb / ItemDrone set the drain from their preset in Start; override it once that has run.
    // (ItemBattery drains batteryDrainRate * 2 percent per second while the item is in use.)
    private System.Collections.IEnumerator AdjustDrain()
    {
        yield return null;
        yield return null;
        if (Battery == null)
            yield break;
        if (mode == ShieldMode.ProtectionOrb)
            Battery.batteryDrainRate *= UtilityGearRules.ProtectionDrain;
        else if (mode == ShieldMode.DefenceDrone)
            Battery.batteryDrainRate = 100f / UtilityGearRules.DefenceSeconds(ModConfig.DefenceDroneSeconds.Value) / 2f;
        else
            Battery.batteryDrainRate = Mathf.Min(10f, ModConfig.DefenceIdleDrainPerMinute.Value) / 60f / 2f;
        if (_drone?.Drone != null) _drone.Drone.batteryDrainRate = Battery.batteryDrainRate;
        if (_orb != null)
        {
            _orb.batteryDrainRate = Battery.batteryDrainRate;
            _orb.orbRadiusOriginal *= UtilityGearRules.OrbRadius;
        }
    }

    /// <summary>Is this valuable inside an active Defence Orb field?</summary>
    public bool CoversObject(Vector3 position)
    {
        if (mode != ShieldMode.DefenceOrb || Battery == null || _orb == null || _orb.currentState != ItemOrb.State.Active)
            return false;
        return Vector3.Distance(transform.position, position) <= _orb.orbRadius + 0.5f;
    }

    public bool Covers(PlayerAvatar p)
    {
        if (p == null || Battery == null)
            return false;
        if (mode == ShieldMode.DefenceDrone)
            return _drone != null && _drone.TargetPlayer == p;
        if (_orb == null || _orb.currentState != ItemOrb.State.Active)
            return false;
        return Vector3.Distance(transform.position, p.transform.position) <= _orb.orbRadius + 0.5f;
    }
}

internal static class ShieldPatches
{
    // Defence Orb: protects LOOT. Value lost to breakage inside its field is absorbed from the battery
    // (100% = DefenceItemAbsorb dollars). Break only runs on the host, which owns the batteries.
    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.Break))]
    private static class BreakValuable
    {
        [HarmonyPrefix]
        private static bool Prefix(PhysGrabObjectImpactDetector __instance, ref float valueLost)
        {
            if (valueLost <= 0f || !__instance.isValuable || ShieldItem.All.Count == 0 ||
                !SemiFunc.IsMasterClientOrSingleplayer())
                return true;

            float perPercent = UtilityGearRules.LootCapacity(ModConfig.DefenceItemAbsorb.Value) / 100f;
            Vector3 pos = __instance.transform.position;
            foreach (var s in ShieldItem.All)
            {
                if (valueLost <= 0f)
                    break;
                if (s.Battery == null || s.Battery.batteryLife <= 0f || !s.CoversObject(pos))
                    continue;
                float absorbed = Mathf.Min(valueLost, s.Battery.batteryLife * perPercent);
                valueLost -= absorbed;
                s.Battery.batteryLife = Mathf.Max(0f, s.Battery.batteryLife - absorbed / perPercent);
            }
            return valueLost > 0.5f; // fully absorbed: no break at all
        }
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static class Hurt
    {
        // After the difficulty scaling (which runs First) so shields work on the final number.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(PlayerHealth __instance, ref int damage, bool hurtByHeal, int enemyIndex)
        {
            if (Monsters.MonsterAfflictions.DotDamage > 0) return;
            var avatar = __instance.playerAvatar;
            // Match the native Hurt rejection gates before spending a shield's battery.
            // Prefixes run even when the original would ignore a remote or invincible player.
            if (hurtByHeal || damage <= 0 || avatar == null || __instance.invincibleTimer > 0f ||
                (GameManager.Multiplayer() && !__instance.photonView.IsMine) || avatar.deadSet || __instance.godMode ||
                GameDirector.instance.currentState != GameDirector.gameState.Main ||
                (enemyIndex >= 0 && avatar.tumble != null && avatar.tumble.CocoonHeld() &&
                 enemyIndex == SemiFunc.EnemyGetIndex(avatar.tumble.cocoonOwner.enemy)))
                return;

            // Level modifiers (Padded Suits / Thin Skin ...) first, so shields work on the final number.
            float taken = Modifiers.StatModifiers.Mult(Modifiers.StatAxis.PlayerDamage);
            if (!Mathf.Approximately(taken, 1f))
                damage = Mathf.Max(1, Mathf.CeilToInt(damage * taken));

            if (ShieldItem.All.Count == 0)
                return;
            float reduction = UtilityGearRules.Protection(ModConfig.ProtectionReductionPercent.Value);
            bool protectedByOrb = false;
            foreach (var s in ShieldItem.All)
                if (s.mode == ShieldMode.ProtectionOrb && s.Battery != null && s.Battery.batteryLife > 0f && s.Covers(avatar))
                {
                    protectedByOrb = true;
                    break;
                }
            if (protectedByOrb)
                damage = Mathf.Max(1, Mathf.CeilToInt(damage * (1f - reduction)));

            float perPercent = UtilityGearRules.DefenceCapacity(ModConfig.DefenceAbsorb.Value) / 100f;
            foreach (var s in ShieldItem.All)
            {
                if (damage <= 0)
                    break;
                if (s.mode != ShieldMode.DefenceDrone || s.Battery == null || s.Battery.batteryLife <= 0f || !s.Covers(avatar))
                    continue;
                int absorbed = Mathf.Min(Mathf.Max(0, damage - (enemyIndex >= 0 ? 1 : 0)), Mathf.FloorToInt(s.Battery.batteryLife * perPercent));
                if (absorbed <= 0)
                    continue;
                damage -= absorbed;
                float percent = absorbed / perPercent;
                // Predict the spend locally too, so another hit before the host's next battery
                // update cannot absorb the same remaining charge for a second time.
                s.Battery.batteryLife = Mathf.Max(0f, s.Battery.batteryLife - percent);
                if (!SemiFunc.IsMasterClientOrSingleplayer())
                    ModNetwork.Instance?.BroadcastBatteryDrain(s.Battery.GetComponent<Photon.Pun.PhotonView>().ViewID, percent);
            }
        }
    }
}
