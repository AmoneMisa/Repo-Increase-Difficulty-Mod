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
/// Defence: 100% battery = <c>DefenceAbsorb</c> damage. Protection: -25% damage, and the orb is made to
/// drain its battery 3x faster than a normal orb.
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
        if (mode == ShieldMode.ProtectionOrb)
            StartCoroutine(FasterDrain());
    }

    // ItemOrb.Start sets the drain from its preset; scale it once that has run.
    private System.Collections.IEnumerator FasterDrain()
    {
        yield return null;
        yield return null;
        if (Battery != null)
            Battery.batteryDrainRate *= 3f;
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
    [HarmonyPatch(typeof(PlayerHealth), "Hurt")]
    private static class Hurt
    {
        // After the difficulty scaling (which runs First) so shields work on the final number.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(PlayerHealth __instance, ref int damage, bool hurtByHeal)
        {
            if (hurtByHeal || damage <= 0 || ShieldItem.All.Count == 0)
                return;
            var avatar = __instance.playerAvatar;
            if (avatar == null)
                return;

            float reduction = ModConfig.ProtectionReductionPercent.Value / 100f;
            bool protectedByOrb = false;
            foreach (var s in ShieldItem.All)
                if (s.mode == ShieldMode.ProtectionOrb && s.Battery != null && s.Battery.batteryLife > 0f && s.Covers(avatar))
                {
                    protectedByOrb = true;
                    break;
                }
            if (protectedByOrb)
                damage = Mathf.Max(1, Mathf.CeilToInt(damage * (1f - reduction)));

            float perPercent = ModConfig.DefenceAbsorb.Value / 100f; // damage absorbed per 1% of battery
            foreach (var s in ShieldItem.All)
            {
                if (damage <= 0)
                    break;
                if (s.mode == ShieldMode.ProtectionOrb || s.Battery == null || s.Battery.batteryLife <= 0f || !s.Covers(avatar))
                    continue;
                int absorbed = Mathf.Min(damage, Mathf.FloorToInt(s.Battery.batteryLife * perPercent));
                if (absorbed <= 0)
                    continue;
                damage -= absorbed;
                float percent = absorbed / perPercent;
                if (SemiFunc.IsMasterClientOrSingleplayer())
                    s.Battery.batteryLife = Mathf.Max(0f, s.Battery.batteryLife - percent);
                else
                    ModNetwork.Instance?.BroadcastBatteryDrain(s.Battery.GetComponent<Photon.Pun.PhotonView>().ViewID, percent);
            }
        }
    }
}
