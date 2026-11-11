using System.Collections.Generic;
using IncreaseHardMod.Hud;
using UnityEngine;

namespace IncreaseHardMod.Gear;

internal sealed class ShopItemInfo : MonoBehaviour
{
    private HudListPanel _panel;
    private ItemAttributes _selected;
    private float _scan;
    private bool _open;
    private readonly List<HudListRow> _rows = new();

    private void Update()
    {
        if (!SemiFunc.RunIsShop() || Camera.main == null || !SemiFunc.NoTextInputsActive())
        { _open = false; _panel?.Hide(); return; }
        if (Time.time >= _scan)
        {
            _scan = Time.time + .5f;
            foreach (var item in Object.FindObjectsOfType<ItemAttributes>())
                if (item.item != null && item.GetComponent<HudWorldHint>() == null) item.gameObject.AddComponent<HudWorldHint>();
        }
        var camera = Camera.main.transform;
        var focused = Focused(camera);
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (focused != null && focused.item != null)
            { _open = !_open || focused != _selected; _selected = focused; if (_open) Populate(); }
            else _open = false;
        }
        if (_selected == null || Vector3.Distance(camera.position, _selected.transform.position) > 6f || Input.GetKeyDown(KeyCode.Escape)) _open = false;
        if (_open) _panel?.Show(); else _panel?.Hide();
    }

    // The item the native shop price tag follows (what the crosshair rests on, overlapping colliders resolved by
    // the game), else the item in the player's hands, else a ray that ignores the player's own colliders.
    private static ItemAttributes Focused(Transform camera)
    {
        var grabber = PhysGrabber.instance;
        if (grabber != null)
        {
            if (grabber.currentlyLookingAtItemAttributes != null) return grabber.currentlyLookingAtItemAttributes;
            if (grabber.grabbed && grabber.grabbedPhysGrabObject != null)
            {
                var held = grabber.grabbedPhysGrabObject.GetComponent<ItemAttributes>();
                if (held != null) return held;
            }
        }
        int mask = ~LayerMask.GetMask("Player", "PlayerOnlyCollision", "Ignore Raycast");
        return Physics.Raycast(camera.position, camera.forward, out var hit, 5f, mask, QueryTriggerInteraction.Ignore)
            ? hit.collider.GetComponentInParent<ItemAttributes>() : null;
    }

    private void Populate()
    {
        bool ru = Localization.GameLanguage.Russian;
        _panel ??= new HudListPanel("item-info", HudCorner.TopRight, 90, 500f) { RowsPerPage = 10, PageKeys = true };
        _panel.Title = Quests.QuestRewards.Label(_selected.item);
        _panel.Footer = ru ? "I — ЗАКРЫТЬ" : "I — CLOSE";
        _rows.Clear();
        void Add(string en, string russian, string value) => _rows.Add(new HudListRow(ru ? russian : en, value, HudStyle.Info));
        Add("PRICE", "ЦЕНА", "$" + _selected.value.ToString("N0"));
        var prefab = _selected.item.prefab?.Prefab;
        if (prefab == null) { _panel.SetRows(_rows); return; }
        var body = prefab.GetComponent<Rigidbody>();
        if (body != null) Add("MASS", "МАССА", body.mass.ToString("0.#") + (ru ? " кг" : " kg"));
        var gun = prefab.GetComponent<ItemGun>();
        var melee = prefab.GetComponent<ItemMelee>();
        var hurt = gun != null ? gun.bulletPrefab?.GetComponentInChildren<HurtCollider>(true) : prefab.GetComponentInChildren<HurtCollider>(true);
        string sec = ru ? " с" : " s";
        if (hurt != null && (gun != null || melee != null) && hurt.enemyDamage > 0) Add("BASE DAMAGE", "БАЗОВЫЙ УРОН", hurt.enemyDamage.ToString());
        if (gun != null && gun.numberOfBullets > 1) Add("PELLETS PER SHOT", "ДРОБИН ЗА ВЫСТРЕЛ", gun.numberOfBullets.ToString());
        var battery = prefab.GetComponent<ItemBattery>();
        if (battery != null) Add("BATTERY BARS", "ДЕЛЕНИЯ БАТАРЕИ", battery.batteryBars.ToString());
        // Grenades: the blast numbers the game passes to ParticleScriptExplosion.Spawn (monster / player damage).
        if (prefab.GetComponent<ItemGrenadeExplosive>() != null) { Add("BLAST DAMAGE (MONSTER)", "УРОН ВЗРЫВА (МОНСТР)", "160"); Add("BLAST DAMAGE (PLAYER)", "УРОН ВЗРЫВА (ИГРОК)", "75"); }
        else if (prefab.GetComponent<ItemGrenadeDuctTaped>() != null) { Add("BLAST DAMAGE (MONSTER)", "УРОН ВЗРЫВА (МОНСТР)", "100 + 3×100"); Add("CHILD GRENADES", "ДОЧЕРНИЕ ГРАНАТЫ", "3"); }
        else if (prefab.GetComponent<ItemGrenadeHuman>() != null) Add("BLAST DAMAGE (MONSTER)", "УРОН ВЗРЫВА (МОНСТР)", "100");
        var stun = prefab.GetComponent<ItemGrenadeStun>();
        var stunHurt = stun != null && stun.stunExplosion != null ? stun.stunExplosion.GetComponentInChildren<HurtCollider>(true) : null;
        if (stunHurt != null && stunHurt.enemyStunTime > 0f) Add("STUN", "ОГЛУШЕНИЕ", stunHurt.enemyStunTime.ToString("0.#") + sec);
        var shock = prefab.GetComponent<ItemGrenadeShockwave>();
        var shockHurt = shock != null && shock.shockwavePrefab != null ? shock.shockwavePrefab.GetComponentInChildren<HurtCollider>(true) : null;
        if (shockHurt != null && shockHurt.enemyStunTime > 0f) Add("STUN", "ОГЛУШЕНИЕ", shockHurt.enemyStunTime.ToString("0.#") + sec);
        var grenade = prefab.GetComponent<ItemGrenade>();
        if (grenade != null && grenade.tickTime > 0f) Add("FUSE", "ЗАПАЛ", grenade.tickTime.ToString("0.#") + sec);
        var drone = prefab.GetComponent<Drones.UtilityDrone>();
        if (drone != null && drone.kind is Drones.DroneKind.Ghost or Drones.DroneKind.Silencer)
        {
            Add("BASE ACTIVE TIME", "БАЗОВОЕ ВРЕМЯ РАБОТЫ", ru ? "3 мин" : "3 min");
            Add("TARGET", "ЦЕЛЬ", DroneTargets(prefab.GetComponent<ItemDrone>(), ru));
            if (drone.kind == Drones.DroneKind.Ghost) Add("VALUABLE IMPACT DAMAGE", "УРОН ЦЕННОСТИ ОТ УДАРОВ", "−50%");
        }
        var shield = prefab.GetComponent<ShieldItem>();
        if (shield != null)
        {
            if (shield.mode == ShieldMode.DefenceOrb)
                Add("SHARED VALUE PROTECTION", "ОБЩАЯ ЗАЩИТА ЦЕННОСТЕЙ", "$" + UtilityGearRules.LootCapacity(ModConfig.DefenceItemAbsorb.Value).ToString("N0"));
            else if (shield.mode == ShieldMode.DefenceDrone)
                Add("DIRECT DAMAGE ABSORPTION", "ПОГЛОЩЕНИЕ ПРЯМОГО УРОНА", UtilityGearRules.DefenceCapacity(ModConfig.DefenceAbsorb.Value).ToString());
            else Add("DIRECT DAMAGE REDUCTION", "СНИЖЕНИЕ ПРЯМОГО УРОНА", "−" + (UtilityGearRules.Protection(ModConfig.ProtectionReductionPercent.Value) * 100f).ToString("0") + "%");
        }
        if (prefab.GetComponent<FlameCart>() != null)
        {
            Add("BASE DAMAGE / SECOND", "БАЗОВЫЙ УРОН В СЕКУНДУ", ModConfig.FlameDamagePerSecond.Value.ToString("0.#"));
            string bonus = (Skills.SkillEffects.WeaponDamageMax(Skills.SkillCatalog.FlameCartDamage) * 100f).ToString("0") + "%";
            Add("DAMAGE PERK", "НАВЫК УРОНА", (ru ? "АРСЕНАЛ: +" : "ARSENAL: +") + bonus);
        }
        if (drone != null && drone.kind == Drones.DroneKind.Link)
        {
            Add("ACTIVE TIME PER DRONE", "РАБОТА КАЖДОГО ДРОНА", ru ? "5 мин" : "5 min");
            Add("TOWED LOOT DAMAGE", "УРОН БУКСИРУЕМОЙ ЦЕННОСТИ", "−50%");
        }
        else if (drone == null && prefab.GetComponent<ItemDrone>() != null)
            Add("TARGET", "ЦЕЛЬ", DroneTargets(prefab.GetComponent<ItemDrone>(), ru));
        if (prefab.GetComponent<PhysGrabCart>() != null && body != null)
            Add("TYPE", "ТИП", ru ? "ТЕЛЕЖКА" : "CART");
        if (_selected.item.itemType == SemiFunc.itemType.item_upgrade || _selected.item.itemType == SemiFunc.itemType.player_upgrade)
            Add("USE", "ПРИМЕНЕНИЕ", ru ? "ОДНОРАЗОВОЕ УЛУЧШЕНИЕ" : "ONE-TIME UPGRADE");
        var gear = prefab.GetComponent<GearGun>();
        if (gear?.kind == GearKind.Ice)
        {
            Add("FIRST FREEZE DAMAGE", "УРОН ПЕРВОЙ ЗАМОРОЗКИ", "20");
            Add("FREEZE", "ЗАМОРОЗКА", ModConfig.IceFreezeSeconds.Value.ToString("0.#") + sec);
            Add("MELEE DAMAGE", "МИЛИ УРОН", "+" + ModConfig.IceMeleeBonusPercent.Value + "%");
            Add("FIREARM DAMAGE TAKEN", "УРОН ОТ ОГНЕСТРЕЛА", ModConfig.IceGunDamagePercent.Value + "%");
            Add("GRENADE DAMAGE TAKEN", "УРОН ОТ ГРАНАТ", "+80%");
        }
        if (gear?.kind == GearKind.Sticky)
        {
            Add("ROOT", "ОБЕЗДВИЖИВАНИЕ", "2" + sec);
            Add("LOOT DAMAGE WHILE PULLING", "УРОН ЛУТУ ПРИ ПРИТЯГИВАНИИ", "−80%");
        }
        // The catalogue description is the item's source of truth, including third-party items.
        string description = _selected.item.description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            string line = "";
            foreach (string word in description.Split(' '))
            {
                if (line.Length + word.Length > 48 && line.Length > 0)
                { _rows.Add(new HudListRow(line, "", Color.white)); line = ""; }
                line += (line.Length == 0 ? "" : " ") + word;
            }
            if (line.Length > 0) _rows.Add(new HudListRow(line, "", Color.white));
        }
        _panel.SetRows(_rows);
    }

    private static string DroneTargets(ItemDrone drone, bool ru)
    {
        var parts = new List<string>();
        if (drone.targetValuables) parts.Add(ru ? "ЦЕННОСТИ" : "VALUABLES");
        if (drone.targetNonValuables) parts.Add(ru ? "ПРЕДМЕТЫ" : "ITEMS");
        if (drone.targetPlayers) parts.Add(ru ? "ИГРОКИ" : "PLAYERS");
        if (drone.targetEnemies) parts.Add(ru ? "МОНСТРЫ" : "MONSTERS");
        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }

    private void OnDestroy() => _panel?.Destroy();
}
