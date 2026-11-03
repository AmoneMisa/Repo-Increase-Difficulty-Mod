using System;
using System.Collections;
using HarmonyLib;
using IncreaseHardMod.Drones;
using IncreaseHardMod.Visuals;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Gear;

/// <summary>One close-range dose, using the game's native tranq bullet/stun and replicated firing path.</summary>
internal static class SpritzItems
{
    internal const string AssetName = "Item Spritz";
    internal const int PriceThousands = 5;
    private static bool _built;

    [HarmonyPatch(typeof(StatsManager), "RunStartStats")]
    private static class Register
    {
        [HarmonyPostfix, HarmonyPriority(Priority.High)]
        private static void Postfix()
        {
            if (_built || !ModConfig.GearEnabled.Value) return;
            GameObject go = null;
            try
            {
                Item donor = null;
                var names = new System.Collections.Generic.List<string>(StatsManager.instance.itemDictionary.Keys);
                names.Sort(StringComparer.Ordinal);
                foreach (string name in names)
                {
                    var candidate = StatsManager.instance.itemDictionary[name];
                    if (candidate?.prefab?.Prefab != null && candidate.prefab.Prefab.GetComponent<ItemGunTranq>() != null)
                    { donor = candidate; break; }
                }
                if (donor == null)
                {
                    Plugin.Log.LogWarning("[Spritz] Native tranquilizer prefab unavailable; registration will retry on the next run.");
                    return;
                }

                // Fail before registration if the new visual resources are missing.
                ItemArt.LoadModel("spritz");
                var item = Object.Instantiate(donor);
                item.name = AssetName;
                item.itemName = "Spritz";
                item.itemNameLocalized = null;
                item.description = "Single-use tranquilizer injector. Stuns a monster at close range. Consumed on use, even if you miss.";
                item.value = Object.Instantiate(donor.value);
                item.value.valueMin = item.value.valueMax = 5000f;
                item.maxAmount = 3;
                item.maxAmountInShop = 1;
                item.maxPurchase = false;
                item.minPlayerCount = 1;
                go = Object.Instantiate(donor.prefab.Prefab, DroneItems.EnsureHolder().transform);
                go.name = AssetName;
                var attributes = go.GetComponent<ItemAttributes>();
                attributes.item = item;
                var gun = go.GetComponent<ItemGun>();
                if (gun == null || gun.bulletPrefab == null) throw new InvalidOperationException("Incomplete native tranq donor.");
                gun.hasOneShot = true;
                gun.hasBuildUp = false;
                gun.numberOfBullets = 1;
                gun.gunRange = 1.8f;
                gun.gunRandomSpread = 0f;
                gun.gunRecoilForce = 0f;
                gun.cameraShakeMultiplier = .08f;
                gun.cameraImpactMultiplier = .08f;
                gun.investigateRadius = 2f;
                gun.batteryDrainFullBar = false;
                gun.batteryDrain = 100f;

                var bullet = Object.Instantiate(gun.bulletPrefab, DroneItems.EnsureHolder().transform);
                bullet.name = "Spritz Native Tranq Hit";
                // Keep the native tranq stun duration/type (including enemy-specific immunity).
                foreach (var hit in bullet.GetComponentsInChildren<HurtCollider>(true))
                {
                    hit.enemyLogic = true;
                    hit.enemyKill = false;
                    hit.deathPit = false;
                    hit.enemyDamage = 0;
                    hit.enemyStun = true;
                    hit.enemyImpact = HurtCollider.BreakImpact.None;
                    hit.enemyHitForce = hit.enemyHitTorque = 0f;
                    hit.playerDamage = 0;
                    hit.playerLogic = false;
                    hit.physLogic = false;
                    hit.playerStunTime = 0f;
                    hit.playerHitForce = hit.playerTumbleForce = hit.playerTumbleTorque = hit.playerTumbleTime = 0f;
                    hit.physHitForce = hit.physHitTorque = 0f;
                }
                gun.bulletPrefab = bullet;
                go.AddComponent<SpritzDose>();
                ItemArt.Weapon(go, "spritz");
                if (go.transform.Find("IHM Blender Visual") == null)
                    throw new InvalidOperationException("Spritz visual could not be attached.");
                FitCollision(go);
                // A short original mechanical hiss replaces both native gunshot paths.
                gun.soundShoot = ItemAudio.Cue("spritz_shot", .35f);
                gun.soundShootGlobal = new Sound { Sounds = Array.Empty<AudioClip>() };
                gun.soundHit = ItemAudio.Cue("spritz_shot", .12f);
                if (Items.RegisterItem(attributes) == null) throw new InvalidOperationException("REPOLib rejected Spritz.");
                _built = true;
                Plugin.Log.LogInfo("[Spritz] Registered: fixed $5,000, one close-range tranquilizer use.");
            }
            catch (Exception e)
            {
                if (go != null) Object.DestroyImmediate(go);
                Plugin.Log.LogError($"[Spritz] Registration failed: {e}");
            }
        }
    }

    private static void FitCollision(GameObject go)
    {
        // Keep native tranq/equipping transforms, replacing the hidden rifle's collision silhouette.
        foreach(var collider in go.GetComponentsInChildren<Collider>(true))if(!collider.isTrigger)collider.enabled=false;
        var visual=go.transform.Find("IHM Blender Visual");int layer=LayerMask.NameToLayer("PhysGrabObject");
        GameObject Part(string name,Vector3 center)
        {
            var child=new GameObject(name);child.transform.SetParent(visual,false);child.transform.localPosition=center;child.layer=layer>=0?layer:go.layer;
            child.AddComponent<PhysGrabObjectCollider>();return child;
        }
        var body=Part("Spritz barrel collision",new Vector3(0,0,-.295f)).AddComponent<CapsuleCollider>();body.direction=2;body.radius=.031f;body.height=.20f;
        var needle=Part("Spritz needle collision",new Vector3(0,0,-.095f)).AddComponent<CapsuleCollider>();needle.direction=2;needle.radius=.004f;needle.height=.19f;
        var grip=Part("Spritz grip collision",new Vector3(0,0,-.44f)).AddComponent<UnityEngine.BoxCollider>();grip.size=new Vector3(.147f,.035f,.18f);
        var com=go.transform.Find("Center of Mass");if(com!=null)com.position=visual.TransformPoint(new Vector3(0,0,-.34f));
    }

    // GetValue stores prices in thousands. Run after the mod's shop-economy scaling;
    // the requested $5,000 remains fixed even when a sale/tax or player-count multiplier is active.
    [HarmonyPatch(typeof(ItemAttributes), "GetValue")]
    private static class FixedPrice
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(ItemAttributes __instance)
        {
            if (__instance.item == null || __instance.item.name != AssetName || !__instance.shopItem ||
                !SemiFunc.IsMasterClientOrSingleplayer()) return;
            __instance.value = PriceThousands;
            var view = __instance.GetComponent<PhotonView>();
            if (GameManager.Multiplayer() && view != null && view.ViewID != 0)
                view.RPC("GetValueRPC", RpcTarget.Others, PriceThousands);
        }
    }
}

internal sealed class SpritzDose : MonoBehaviour
{
    internal bool Used { get; private set; }
    private bool _removing;
    private bool _removedFromStats;

    internal bool BeginUse()
    {
        if (Used) return false;
        Used = true; // Set before native firing, preventing re-entry and rapid/replayed activation.
        return true;
    }

    internal void FinishUse()
    {
        if (!Used || _removing || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        _removing = true;
        var battery = GetComponent<ItemBattery>();
        if (battery != null) battery.batteryLife = 0f;
        var toggle = GetComponent<ItemToggle>();
        if (toggle != null) toggle.ToggleDisable(true);
        RemoveFromStatsWhenReady();
        StartCoroutine(RemoveAfterNativeHit());
    }

    private bool RemoveFromStatsWhenReady()
    {
        if (_removedFromStats) return true;
        var attributes = GetComponent<ItemAttributes>();
        if (attributes == null || string.IsNullOrEmpty(attributes.instanceName)) return false;
        StatsManager.instance.ItemRemove(attributes.instanceName);
        _removedFromStats = true;
        return true;
    }

    private IEnumerator RemoveAfterNativeHit()
    {
        // The native hit collider lives independently for 0.25 s; also show the plunger motion.
        yield return new WaitForSeconds(.65f);
        // ItemAttributes assigns its persistent instance name asynchronously at spawn.
        // A very early activation must not leave an unremoved item in the truck/save list.
        while (SemiFunc.IsMasterClientOrSingleplayer() && !RemoveFromStatsWhenReady()) yield return null;
        if (!SemiFunc.IsMasterClientOrSingleplayer()) { _removing = false; yield break; }
        var pgo = GetComponent<PhysGrabObject>();
        if (pgo != null) pgo.DestroyPhysGrabObject();
    }

    // A new host can complete removal if host migration occurs during the short use animation.
    private void Update()
    {
        if (Used && !_removing && SemiFunc.IsMasterClientOrSingleplayer()) FinishUse();
    }

    [HarmonyPatch(typeof(ItemGun), "Shoot")]
    private static class BlockReactivation
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemGun __instance, ref bool __result)
        {
            var dose = __instance.GetComponent<SpritzDose>();
            if (dose == null || (!dose.Used && !SemiFunc.RunIsShop())) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemGun), "MuzzleFlash")]
    private static class NoGunFlash
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemGun __instance) => __instance.GetComponent<SpritzDose>() == null;
    }

    [HarmonyPatch(typeof(ItemGun), "ShootRPC")]
    private static class UseOnce
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemGun __instance, PhotonMessageInfo _info, out bool __state)
        {
            __state = false;
            var dose = __instance.GetComponent<SpritzDose>();
            if (dose == null || !SemiFunc.MasterOnlyRPC(_info)) return true;
            if (SemiFunc.RunIsShop() || !dose.BeginUse()) return false;
            __state = true;
            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(ItemGun __instance, bool __state)
        {
            if (__state) __instance.GetComponent<SpritzDose>()?.FinishUse();
        }
    }
}
