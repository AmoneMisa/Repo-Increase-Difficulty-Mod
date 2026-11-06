using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

internal static class InventorySlotsUpgrade
{
    internal const string Id = "IHM_InventorySlots";
    internal const int BaseSlots = 3, MaxSlots = 7, MaxLevel = MaxSlots - BaseSlots;
    private static readonly HashSet<string> Restored = new();

    internal static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !ModConfig.InventorySlotsEnabled.Value) return;
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = Id, DisplayName = "Inventory Slot",
            MaxLevel = MaxLevel,
        });
    }

    internal static int SlotCount(PlayerAvatar avatar)
    {
        if (avatar == null || string.IsNullOrEmpty(avatar.steamID) || !ModConfig.PlayerUpgradesEnabled.Value || !ModConfig.InventorySlotsEnabled.Value) return BaseSlots;
        int level = Skills.SkillEffects.Level(avatar, Skills.SkillCatalog.InventorySlot); // skill tree
        return BaseSlots + Mathf.Clamp(level, 0, MaxLevel);
    }

    internal static KeyCode SlotKey(int index) => index switch
    {
        3 => ModConfig.InventorySlot4Key.Value, 4 => ModConfig.InventorySlot5Key.Value,
        5 => ModConfig.InventorySlot6Key.Value, 6 => ModConfig.InventorySlot7Key.Value, _ => KeyCode.None,
    };

    internal static bool InventoryInputActive()
    {
        if (SemiFunc.InputDown(InputKey.Inventory1) || SemiFunc.InputDown(InputKey.Inventory2) || SemiFunc.InputDown(InputKey.Inventory3)) return true;
        if (Skills.SkillTreeScreen.IsOpen) return false; // number keys turn tree pages
        for (int i=BaseSlots;i<MaxSlots;i++) if (SlotKey(i) != KeyCode.None && Input.GetKeyDown(SlotKey(i))) return true;
        return false;
    }

    // Extra slots are real native InventorySpot instances, with the native icon, battery and input path.
    internal static bool EnsureLocalSlots()
    {
        var inventory = Inventory.instance;
        var ui = InventoryUI.instance;
        if (inventory == null || ui == null || GameDirector.instance == null || RunManager.instance == null || RunManager.instance.levelCurrent == null) return false;
        var avatar = SemiFunc.PlayerAvatarLocal();
        if (inventory == null || ui == null || avatar == null || SemiFunc.RunIsArena()) return false;
        var spots = inventory.inventorySpots;
        if (spots.Count < BaseSlots) return false;
        for (int i=0;i<BaseSlots;i++) if (spots[i] == null) return false;
        // A live donor may have hidden its children. Wait until its native Start has completed.
        if (spots[0].inventoryIcon == null || spots[0].batteryVisualLogic == null || AssetManager.instance == null) return false;
        int wanted = SlotCount(avatar);
        bool changed = false;
        while (spots.Count < wanted)
        {
            int index = spots.Count;
            var holder = new GameObject("IHM inventory cloning");
            holder.SetActive(false); // Strip scene PhotonViews before their Awake can register duplicate IDs.
            var clone = Object.Instantiate(spots[0].gameObject, holder.transform);
            foreach (var view in clone.GetComponentsInChildren<PhotonView>(true)) Object.DestroyImmediate(view);
            var spot = clone.GetComponent<InventorySpot>();
            // Native Start searches ACTIVE children, then initializes SemiUI's animation curves.
            // Cloning a hidden live slot leaves its icon/label/battery (or their parents) inactive;
            // UpdateUI would throw before base.Start, leaving a permanently broken widget.
            var icon = CloneChild(spots[0].inventoryIcon.transform, spots[0].transform, clone.transform)?.GetComponent<Image>();
            var battery = CloneChild(spots[0].batteryVisualLogic.transform, spots[0].transform, clone.transform)?.GetComponent<BatteryVisualLogic>();
            if (spot == null || icon == null || spot.noItem == null || battery == null)
            {
                DestroyTemporary(holder);
                return false;
            }
            ActivatePath(icon.transform, clone.transform);
            ActivatePath(spot.noItem.transform, clone.transform);
            ActivatePath(battery.transform, clone.transform);
            ResetBatteryWidget(battery, spots[0].batteryVisualLogic);
            spot.doNotDisable ??= new List<GameObject>();
            spot.inventorySpotIndex = index;
            spot.currentState = InventorySpot.SpotState.Empty;
            spot.CurrentItem = null;
            // After a level change the slots are rebuilt while slot 1 already holds its restored item.
            // Instantiate copies that live state: the battery widget would stay bound to slot 1's item and
            // show its bars on this empty slot. Unbind it and re-enter the empty state so the native
            // StateEmpty resets the bars and plays the battery outro.
            battery.itemBattery = null;
            battery.batteryPercent = 100f;
            battery.currentBars = battery.batteryBars;
            spot.stateStart = true;
            spot.layoutOffsetX = spot.appliedLayoutOffsetX = 0f;
            foreach (var glyph in clone.GetComponentsInChildren<InputGlyphText>(true))
            {
                glyph.enabled = false;
                var label = glyph.GetComponent<TMP_Text>();
                if (label != null) label.text = BindingName(index);
            }
            if (spot.noItem != null) spot.noItem.text = (index+1).ToString();
            spots.Add(spot); // Never leave null entries for native inventory loops.
            clone.name = "Inventory Slot " + (index+1);
            clone.SetActive(false);
            clone.transform.SetParent(ui.transform, false);
            var position = spots[0].transform.localPosition;
            position.x = ui.slotCenterX;
            // SemiUI.Start adds showPosition to hidePosition, and its Update restores these
            // serialized animation anchors each frame. Do not inherit the donor slot's X.
            spot.hidePosition = spots[0].hidePosition - spots[0].showPosition;
            spot.showPosition = new Vector2(position.x,spots[0].showPosition.y);
            clone.transform.localPosition = new Vector3(position.x,spot.showPosition.y,position.z);
            if(spots[0].originalScale.sqrMagnitude>.001f) clone.transform.localScale=spots[0].originalScale;
            clone.SetActive(true);
            DestroyTemporary(holder);
            changed = true;
        }
        while (spots.Count > wanted)
        {
            var last = spots[spots.Count-1];
            if (last != null && last.IsOccupied()) break; // Never delete an item to shrink the HUD.
            spots.RemoveAt(spots.Count-1);
            if (last != null) { last.transform.SetParent(null); DestroyTemporary(last.gameObject); }
            changed = true;
        }
        if (changed)
        {
            foreach (var spot in spots) spot.LayoutOffsetSet(0f);
            ui.slotCenterX = ui.itemWidth = 0f;
            ui.LayoutSetup();
            ui.LayoutUpdate();
        }
        for (int index=BaseSlots;index<spots.Count;index++)
            foreach(var glyph in spots[index].GetComponentsInChildren<InputGlyphText>(true))
            {
                glyph.enabled=false;
                var label=glyph.GetComponent<TMP_Text>();
                if(label!=null) label.text=BindingName(index);
            }
        return spots.Count >= wanted;
    }

    // BatteryVisualLogic.Awake records its CURRENT transform as the "original" it always returns to. The
    // donor slot is usually empty, so its battery sits shrunk to scale 0 by BatteryOutro; a copy would wake
    // up believing 0 is its normal size and could never be shown. Put the copy back to the donor's true
    // rest pose before it wakes, and drop the donor's spawned bar segments (the copy does not track them,
    // so BatteryBarsSet would never clear them).
    private static void ResetBatteryWidget(BatteryVisualLogic copy, BatteryVisualLogic donor)
    {
        float scale = donor.targetScaleOriginal > .001f ? donor.targetScaleOriginal : 1f;
        copy.transform.localScale = Vector3.one * scale;
        copy.transform.localPosition = donor.targetPositionOriginal;
        if (copy.batteryBarContainer != null)
            for (int i = copy.batteryBarContainer.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(copy.batteryBarContainer.GetChild(i).gameObject);
    }

    private static void ActivatePath(Transform child, Transform root)
    {
        for (var current = child; current != null; current = current.parent)
        {
            current.gameObject.SetActive(true);
            if (current == root) break;
        }
    }

    private static Transform CloneChild(Transform original, Transform donor, Transform clone)
    {
        if (original == donor) return clone;
        if (original == null || original.parent == null) return null;
        var parent = CloneChild(original.parent, donor, clone);
        return parent != null && original.GetSiblingIndex() < parent.childCount
            ? parent.GetChild(original.GetSiblingIndex()) : null;
    }

    private static string BindingName(int index)
    {
        var key = SlotKey(index);
        string name = key.ToString();
        return name.StartsWith("Alpha") ? name.Substring(5) : key == KeyCode.None ? "-" : name;
    }

    private static void DestroyTemporary(Object value)
    {
        if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
    }

    internal sealed class Controller : MonoBehaviour
    {
        private float _next;
        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + .2f;
            EnsureLocalSlots();
            for (int i=Pending.Count-1;i>=0;i--)
            {
                var p = Pending[i];
                if (p.Item == null || Time.unscaledTime > p.Until) { Pending.RemoveAt(i); continue; }
                if (Inventory.instance == null || p.Slot >= Inventory.instance.inventorySpots.Count ||
                    Inventory.instance.inventorySpots[p.Slot] == null || Inventory.instance.inventorySpots[p.Slot].inventoryIcon == null) continue;
                Pending.RemoveAt(i);
                p.Item.RPC_UpdateItemState(p.State,p.Slot,p.Owner,p.Info);
            }
        }
    }

    [HarmonyPatch(typeof(InventorySpot), "Update")]
    private static class ExtraInput
    {
        [HarmonyPostfix]
        private static void Postfix(InventorySpot __instance)
        {
            int index = __instance.inventorySpotIndex;
            if (index < BaseSlots || index >= MaxSlots || SlotKey(index) == KeyCode.None ||
                !Input.GetKeyDown(SlotKey(index)) || !SemiFunc.NoTextInputsActive() || Skills.SkillTreeScreen.IsOpen) return;
            // A retained occupied slot can always be emptied after an administrative downgrade.
            if (index < SlotCount(SemiFunc.PlayerAvatarLocal()) || __instance.IsOccupied()) __instance.HandleInput();
        }
    }

    // Native validation uses the host's own local slot count, which rejects upgraded remote players.
    [HarmonyPatch(typeof(ItemEquippable), "RPC_RequestEquip")]
    private static class ValidateExtraEquip
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemEquippable __instance, int spotIndex, int physGrabberPhotonViewID, PhotonMessageInfo _info)
        {
            if (spotIndex < BaseSlots) return true;
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return false;
            bool multiplayer = SemiFunc.IsMultiplayer();
            var avatar = multiplayer ? SemiFunc.PlayerAvatarGetFromPhotonID(physGrabberPhotonViewID) : PlayerAvatar.instance;
            if (avatar == null || (multiplayer && !SemiFunc.MasterAndOwnerOnlyRPC(_info,avatar.photonView)) || spotIndex >= SlotCount(avatar)) return false;
            if (__instance.currentState == ItemEquippable.ItemState.Idle)
            {
                if (multiplayer) __instance.photonView.RPC("RPC_UpdateItemState",RpcTarget.All,2,spotIndex,physGrabberPhotonViewID);
                else __instance.RPC_UpdateItemState(2,spotIndex,physGrabberPhotonViewID);
            }
            return false;
        }
    }

    private sealed class PendingEquip
    {
        internal ItemEquippable Item; internal int State,Slot,Owner; internal PhotonMessageInfo Info; internal float Until;
    }
    private static readonly List<PendingEquip> Pending = new();

    [HarmonyPatch(typeof(ItemEquippable), "RPC_UpdateItemState")]
    private static class ReadyBeforeRestore
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemEquippable __instance, int state, int spotIndex, int ownerId, PhotonMessageInfo _info)
        {
            if (spotIndex < BaseSlots || state != 2) return true; // Always allow existing contents to be taken out.
            var avatar = SemiFunc.IsMultiplayer() ? SemiFunc.PlayerAvatarGetFromPhotonID(ownerId) : PlayerAvatar.instance;
            if (avatar == null || !avatar.isLocal) return true;
            if (spotIndex >= SlotCount(avatar)) return false;
            EnsureLocalSlots();
            if (Inventory.instance != null && spotIndex < Inventory.instance.inventorySpots.Count &&
                Inventory.instance.inventorySpots[spotIndex] != null && Inventory.instance.inventorySpots[spotIndex].inventoryIcon != null) return true;
            if (!SemiFunc.MasterAndOwnerOnlyRPC(_info,avatar.photonView)) return false;
            Pending.RemoveAll(p=>p.Item==__instance);
            Pending.Add(new PendingEquip{Item=__instance,State=state,Slot=spotIndex,Owner=ownerId,Info=_info,Until=Time.unscaledTime+10f});
            return false;
        }
    }

    private static string Storage(int index) => "IHM_InventorySpot" + (index+1);
    private static Dictionary<string,int> SlotData(int index)
    {
        var all = StatsManager.instance.dictionaryOfDictionaries;
        string key = Storage(index);
        if (!all.TryGetValue(key,out var data)) all[key] = data = new Dictionary<string,int>();
        return data;
    }

    [HarmonyPatch(typeof(StatsManager), "Awake")]
    private static class RegisterStorage
    {
        [HarmonyPostfix]
        private static void Postfix() { for(int i=BaseSlots;i<MaxSlots;i++) SlotData(i); }
    }

    [HarmonyPatch(typeof(StatsManager), "PlayerInventoryUpdate")]
    private static class SaveExtraItem
    {
        [HarmonyPrefix]
        private static bool Prefix(string _steamID, string itemName, int spot, bool sync)
        {
            if (spot < BaseSlots) return true;
            if (!SemiFunc.IsMasterClientOrSingleplayer() || spot >= MaxSlots || StatsManager.instance == null) return false;
            int value = string.IsNullOrEmpty(itemName) ? -1 : itemName.GetHashCode();
            var data = SlotData(spot);
            if (value == -1) { data.Remove(_steamID); Restored.Remove(_steamID+":"+spot); }
            else { data[_steamID] = value; Restored.Add(_steamID+":"+spot); }
            if (sync && PunManager.instance != null) PunManager.instance.UpdateStat(Storage(spot),_steamID,value);
            return false;
        }
    }

    [HarmonyPatch(typeof(PunManager), "TruckPopulateItemVolumes", new[] { typeof(List<PunManager.ItemSpawn>) })]
    private static class ResetRestoreClaims
    {
        [HarmonyPrefix]
        private static void Prefix() { Restored.Clear(); Pending.Clear(); }
    }

    [HarmonyPatch(typeof(PunManager), "SetItemNameLOGIC")]
    private static class RestoreExtraItem
    {
        [HarmonyPostfix]
        private static void Postfix(string _name, int photonViewID, ItemAttributes _itemAttributes)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || StatsManager.instance == null) return;
            var attr = _itemAttributes;
            if (SemiFunc.IsMultiplayer()) attr = PhotonView.Find(photonViewID)?.GetComponent<ItemAttributes>();
            var item = attr != null ? attr.GetComponent<ItemEquippable>() : null;
            if (item == null || item.IsEquipped()) return;
            foreach(var avatar in SemiFunc.PlayerGetList())
            {
                if (avatar == null || string.IsNullOrEmpty(avatar.steamID)) continue;
                for(int index=BaseSlots;index<SlotCount(avatar);index++)
                {
                    string claim = avatar.steamID+":"+index;
                    if (Restored.Contains(claim) || !SlotData(index).TryGetValue(avatar.steamID,out int hash) || hash != _name.GetHashCode()) continue;
                    Restored.Add(claim);
                    item.RequestEquip(index,SemiFunc.IsMultiplayer()?avatar.photonView.ViewID:-1);
                    return;
                }
            }
        }
    }
}
