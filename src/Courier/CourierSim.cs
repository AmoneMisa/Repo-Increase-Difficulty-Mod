using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Courier;

internal enum CourierJob : byte { None = 0, Charge = 1, Fetch = 2, Ally = 3, Cart = 4, Store = 5, Head = 6 }

internal enum CourierNotice : byte
{
    Refused = 1, Found = 2, Down = 3, Back = 4, Done = 5, Started = 6, CoinGot = 7, Stashed = 8,
}

internal enum CourierReason : byte
{
    NotHolding = 1, BatteryFull = 2, NoStation = 3, TooHeavy = 4, NotInTruck = 5, Valuable = 6, NoTeammate = 7,
    NoCart = 8, NeedPerk = 9, Cooldown = 10, KnockedOut = 11, LevelsOnly = 12, Gone = 13, NoBattery = 14, BackpackFull = 15,
    ItemBusy = 16, ChargeTimedOut = 17, NoHead = 18, HeadTooFar = 19,
}

/// <summary>
/// One courier, simulated by the host: walks the level's NavMesh after its owner, runs one job at a time and carries
/// items with the game's own grab overrides (no gravity, nobody else can grab it, it cannot break while carried).
/// It can only be hurt while it is out on a job (fetching, delivering, storing, charging, a head): then a monster
/// attacking it (a monster's hurt collider or a monster right next to it) can knock it out, and it comes back at the
/// truck with its backpack still full. Tagging along beside its owner it is safe and silent: no damage of any kind,
/// and it never draws monsters to them.
/// </summary>
internal sealed class CourierSim
{
    private enum Phase : byte { Follow, ToItem, ToCharger, AtCharger, Towing, Deliver }

    internal readonly int Actor;
    internal PlayerAvatar Owner;
    internal Vector3 Pos;
    internal float Yaw;
    internal float Hp = CourierRules.MaxHealth;
    internal bool Down, Flying;
    internal float DownUntil, DashUntil, DashReadyAt;
    internal CourierJob Job;

    private Phase _phase;
    private PhysGrabObject _item;
    private PlayerAvatar _ally;
    private PlayerDeathHead _head;
    private PhysGrabCart _cart;
    private float _chargeStarted;
    private CourierReason? _chargeFailure;
    private float _happyUntil, _surprisedUntil, _sadUntil, _jobStarted, _lastHit, _findAt = -1f;
    private readonly NavMeshPath _path = new();
    private readonly List<Vector3> _corners = new();
    private Vector3 _pathGoal = new(1e6f, 0, 0);
    private float _repathAt, _progressAt, _bestDistance = float.MaxValue, _itemAwayFor;
    private readonly Dictionary<HurtCollider, float> _hitCooldown = new();
    private float _nextHazardCheck;
    // Follow: where the owner stood last frame, since when they have stood still, and the spot in front of them the
    // pet keeps while they do (so it sits in view and can be petted, instead of trailing behind).
    private Vector3 _ownerLast;
    private float _ownerStillSince;
    private Vector3? _frontSpot;

    private static readonly AccessTools.FieldRef<ChargingStation, float> ChargeLeft = AccessTools.FieldRefAccess<ChargingStation, float>("chargeFloat");

    internal CourierSim(int actor, PlayerAvatar owner, Vector3 start, bool rollFind)
    {
        Actor = actor; Owner = owner; Pos = start;
        Hp = MaxHp;
        Yaw = owner != null ? owner.transform.eulerAngles.y : 0f;
        if (rollFind && Random.value < CourierRules.FindChance(Level(C.CourierLuck)))
            _findAt = Time.time + Random.Range(40f, 200f);
    }

    internal int Level(string node) => Skills.SkillEffects.Level(Owner, node);
    internal float MaxHp => CourierRules.Health(Level(C.CourierHealth));
    internal int Slots => CourierRules.Slots(Level(C.CourierPack));

    // ------------------------------------------------------------------ backpack (stashed items wait off-map, the game's own deactivation)

    private readonly List<PhysGrabObject> _stash = new();
    internal bool HasItem(PhysGrabObject item) => _item == item || _stash.Contains(item);

    internal string StashLabels()
    {
        _stash.RemoveAll(x => x == null);
        var names = new List<string>();
        foreach (var item in _stash) names.Add(ItemLabel(item));
        return string.Join("|", names);
    }

    internal CourierReason? TryStash(PhysGrabObject item)
    {
        if (!HeldByOwner(item)) return CourierReason.NotHolding;
        if (item.GetComponent<ValuableObject>() != null) return CourierReason.Valuable;
        _stash.RemoveAll(x => x == null);
        if (_stash.Count >= Slots) return CourierReason.BackpackFull;
        if (TooHeavy(item)) return CourierReason.TooHeavy;
        TakeFromHands(item);
        _stash.Add(item);
        item.OverrideDeactivate(.5f);
        _happyUntil = Time.time + 1f;
        return null;
    }

    internal bool TryUnstash(int index)
    {
        _stash.RemoveAll(x => x == null);
        if (index < 0 || index >= _stash.Count) return false;
        var item = _stash[index];
        _stash.RemoveAt(index);
        Release(item, Pos + Vector3.up * .8f + Fwd() * .45f);
        return true;
    }

    internal void UnstashAll()
    {
        _stash.RemoveAll(x => x == null);
        for (int i = 0; i < _stash.Count; i++) Release(_stash[i], Pos + Vector3.up * (.8f + .35f * i));
        _stash.Clear();
    }

    private static void Release(PhysGrabObject item, Vector3 at)
    {
        item.OverrideDeactivateReset();
        item.Teleport(at, item.transform.rotation);
        if (item.rb != null) item.rb.velocity = Vector3.up * .5f;
    }

    /// <summary>Out on a job (not just tagging along): the only time it can be hurt or draw monsters.</summary>
    internal bool OnDelivery => Job != CourierJob.None;

    internal bool Carrying => _item != null && _phase is Phase.ToCharger or Phase.AtCharger or Phase.Deliver;

    internal CourierMood Mood
    {
        get
        {
            if (Time.time < _surprisedUntil) return CourierMood.Surprised;
            if (Time.time < _sadUntil || (Owner != null && Owner.isDisabled)) return CourierMood.Sad;
            if (Job != CourierJob.None) return CourierMood.Focus;
            return CourierMood.Neutral;
        }
    }

    internal CourierFlags Flags =>
        (Time.time < _happyUntil ? CourierFlags.Happy : 0) | (Carrying || _phase == Phase.Towing ? CourierFlags.Carrying : 0) |
        (Flying ? CourierFlags.Flying : 0) | (Down ? CourierFlags.Down : 0) | (Time.time < DashUntil ? CourierFlags.Dashing : 0) |
        (Time.time < HitUntil ? CourierFlags.Hit : 0);

    internal void Petted() { _happyUntil = Time.time + 1.8f; _sadUntil = 0f; }

    // ------------------------------------------------------------------ jobs (host validated)

    internal static bool CanPickUp(PhysGrabObject item) =>
        item != null && !item.dead && !item.grabbed && item.playerGrabbing.Count == 0 && item.grabDisableTimer <= 0f &&
        item.rb != null && !item.rb.isKinematic && item.gameObject.activeInHierarchy &&
        (item.GetComponent<ItemEquippable>() is not { } equipment ||
            (!equipment.isEquipped && !equipment.isEquipping && !equipment.isUnequipping));

    internal CourierReason? TryCharge(PhysGrabObject item, bool pickUp)
    {
        if (item == null || item.dead) return CourierReason.Gone;
        if (pickUp)
        {
            if (!CanPickUp(item)) return CourierReason.ItemBusy;
            float reach = CourierRules.ChargeTargetRange + 2f; // camera height and network position updates
            if ((item.midPoint - Owner.transform.position).sqrMagnitude > reach * reach) return CourierReason.Gone;
        }
        else if (HandoffProblem(item) is CourierReason problem) return problem;
        var battery = item.GetComponent<ItemBattery>();
        if (battery == null) return CourierReason.NoBattery;
        if (battery.batteryLife >= 100f) return CourierReason.BatteryFull;
        if (PoweredChargeArea() == null) return CourierReason.NoStation;
        if (TooHeavy(item)) return CourierReason.TooHeavy;
        Start(CourierJob.Charge, item);
        _phase = pickUp ? Phase.ToItem : Phase.ToCharger;
        if (!pickUp) TakeFromHands(item);
        return null;
    }

    internal CourierReason? TryStore(PhysGrabObject item)
    {
        if (item == null || item.dead) return CourierReason.NotHolding;
        if (HandoffProblem(item) is CourierReason problem) return problem;
        if (item.GetComponent<ValuableObject>() != null || item.isCart || item.GetComponent<Gear.FlameCartHandle>() != null ||
            item.GetComponent<PlayerDeathHead>() != null) return CourierReason.Valuable;
        if (TooHeavy(item)) return CourierReason.TooHeavy;
        Start(CourierJob.Store, item);
        _phase = Phase.Deliver;
        TakeFromHands(item);
        return null;
    }

    internal void FetchHead(PlayerDeathHead head)
    {
        Start(CourierJob.Head, head.physGrabObject);
        _head = head;
        _phase = Phase.ToItem;
    }

    private CourierReason? HandoffProblem(PhysGrabObject item)
    {
        var hands = Owner != null ? Owner.physGrabber : null;
        if (hands == null || !item.playerGrabbing.Contains(hands)) return CourierReason.NotHolding;
        return item.playerGrabbing.Exists(grabber => grabber != null && grabber != hands) ? CourierReason.ItemBusy : null;
    }

    internal CourierReason? TryFetch(PhysGrabObject item)
    {
        if (item == null) return CourierReason.Gone;
        var rooms = item.GetComponent<RoomVolumeCheck>();
        if (rooms == null || !rooms.inTruck || item.grabbed) return CourierReason.NotInTruck;
        if (item.GetComponent<ValuableObject>() != null) return CourierReason.Valuable;
        if (TooHeavy(item)) return CourierReason.TooHeavy;
        Start(CourierJob.Fetch, item);
        _phase = Phase.ToItem;
        return null;
    }

    internal CourierReason? TryAlly(PhysGrabObject item, PlayerAvatar ally)
    {
        if (Level(C.CourierAlly) <= 0) return CourierReason.NeedPerk;
        if (!HeldByOwner(item)) return CourierReason.NotHolding;
        if (item.GetComponent<ValuableObject>() != null) return CourierReason.Valuable;
        if (ally == null || ally == Owner || ally.isDisabled) return CourierReason.NoTeammate;
        if (TooHeavy(item)) return CourierReason.TooHeavy;
        Start(CourierJob.Ally, item);
        _ally = ally;
        _phase = Phase.Deliver;
        TakeFromHands(item);
        return null;
    }

    internal CourierReason? TryCart()
    {
        if (Level(C.CourierCart) <= 0) return CourierReason.NeedPerk;
        PhysGrabCart best = null; float bestD = 80f;
        foreach (var cart in Object.FindObjectsOfType<PhysGrabCart>())
        {
            if (cart == null || cart.rb == null) continue;
            var grab = cart.GetComponent<PhysGrabObject>();
            if (grab != null && grab.grabbed) continue;
            float d = Vector3.Distance(cart.transform.position, Owner.transform.position);
            if (d < 3f) continue; // already here
            if (d < bestD) { bestD = d; best = cart; }
        }
        if (best == null) return CourierReason.NoCart;
        Start(CourierJob.Cart, null);
        _cart = best;
        _phase = Phase.ToItem;
        return null;
    }

    internal CourierReason? TryDash()
    {
        if (Level(C.CourierDash) <= 0) return CourierReason.NeedPerk;
        if (Time.time < DashReadyAt) return CourierReason.Cooldown;
        DashUntil = Time.time + CourierRules.DashSeconds;
        DashReadyAt = Time.time + CourierRules.DashCooldown;
        return null;
    }

    internal CourierReason? TryFly(bool on)
    {
        if (on && Level(C.CourierFlight) <= 0) return CourierReason.NeedPerk;
        Flying = on;
        return null;
    }

    internal void Cancel()
    {
        if (Carrying) Drop(Owner != null ? Owner.transform.forward : Vector3.forward);
        _item = null; _chargeFailure = null; _itemAwayFor = 0f;
        Job = CourierJob.None; _phase = Phase.Follow; _ally = null; _cart = null; _head = null;
    }

    private void Start(CourierJob job, PhysGrabObject item)
    {
        Cancel();
        Job = job; _item = item; _jobStarted = Time.time; _surprisedUntil = Time.time + .6f;
        _bestDistance = float.MaxValue; _progressAt = Time.time;
    }

    private bool HeldByOwner(PhysGrabObject item)
    {
        if (item == null || Owner == null || Owner.physGrabber == null) return false;
        return item.playerGrabbing.Contains(Owner.physGrabber) || (item.lastPlayerGrabbing == Owner && Vector3.Distance(item.transform.position, Owner.transform.position) < 3f);
    }

    private bool TooHeavy(PhysGrabObject item) =>
        item.rb != null && item.rb.mass > CourierRules.MaxLoad(Level(C.CourierLoad)) + .01f;

    private static void TakeFromHands(PhysGrabObject item)
    {
        foreach (var grabber in item.playerGrabbing.ToArray())
        {
            if (grabber == null) continue;
            if (GameManager.Multiplayer()) grabber.photonView.RPC("ReleaseObjectRPC", RpcTarget.All, false, .5f, item.photonView.ViewID);
            else grabber.ReleaseObject(item.photonView.ViewID, .5f);
        }
    }

    private static Transform PoweredChargeArea()
    {
        var station = ChargingStation.instance;
        return station != null && station.isActiveAndEnabled && ChargeLeft(station) > 0f
            ? station.transform.Find("Charge Area") : null;
    }

    // ------------------------------------------------------------------ tick

    /// <summary>Host: one frame. Returns a notice for the owner when something happened.</summary>
    internal (CourierNotice, string)? Tick(float dt, Vector3 truck)
    {
        if (Owner == null) return null;
        // The backpack stays hidden on the pet the whole time, knocked out or not (it keeps its loot).
        foreach (var stashed in _stash) if (stashed != null) stashed.OverrideDeactivate(.5f);
        if (Down)
        {
            if (Time.time < DownUntil) return null;
            Down = false; Hp = MaxHp; Pos = Ground(truck); _happyUntil = Time.time + 1.5f;
            return (CourierNotice.Back, null);
        }
        var notice = OnDelivery ? Hazards() ?? Monsters() : null;
        if (notice != null) return notice;
        if (Time.time - _lastHit > 8f) Hp = Mathf.Min(MaxHp, Hp + 2f * dt);

        // Rare find: once per level, while it is just tagging along.
        if (_findAt > 0f && Time.time >= _findAt && Job == CourierJob.None)
        {
            _findAt = -1f;
            var upgrade = Quests.QuestRewards.PickAvailableUpgrade();
            if (upgrade != null && Quests.QuestRewards.SpawnItemAt(upgrade, Pos + Vector3.up * .7f + Fwd() * .4f, Quaternion.identity))
            {
                _happyUntil = Time.time + 3f;
                return (CourierNotice.Found, Quests.QuestRewards.Label(upgrade));
            }
        }

        float speed = CourierRules.Speed(Level(C.CourierSpeed), Time.time < DashUntil, Flying);
        var ownerPos = Owner.transform.position;
        bool ownerGone = Owner.isDisabled;

        if (Job is CourierJob.Charge or CourierJob.Fetch or CourierJob.Ally or CourierJob.Store or CourierJob.Head && (_item == null || _item.dead))
            return Refuse(CourierReason.Gone);
        if (Job == CourierJob.Head && !CourierHeadRoute.Active(_head)) return Refuse(CourierReason.NoHead);
        if (Job != CourierJob.None && Time.time - _jobStarted > 120f) { Cancel(); _sadUntil = Time.time + 2f; return null; }

        switch (_phase)
        {
            case Phase.Follow:
            {
                var goal = ownerGone ? truck : FollowSpot(ownerPos, dt);
                float d = Flat(goal - Pos).magnitude;
                if (d > CourierRules.FollowDistance * .6f) MoveTo(goal, speed * (d > 6f ? 1.35f : 1f), .3f);
                else FaceTowards(ownerGone ? truck : ownerPos, dt);
                if (!ownerGone && Vector3.Distance(Pos, ownerPos) > CourierRules.CatchUpDistance && !Visible(Pos)) Pos = Ground(goal);
                return null;
            }
            case Phase.ToItem:
            {
                if (Job == CourierJob.Cart)
                {
                    if (_cart == null) { Cancel(); return null; }
                    if (MoveTo(_cart.transform.position - Flat(_cart.transform.forward) * 1.0f, speed, .8f)) _phase = Phase.Towing;
                    return null;
                }
                if (Job is CourierJob.Charge or CourierJob.Head && !CanPickUp(_item)) return Refuse(CourierReason.ItemBusy);
                var target = Job is CourierJob.Charge or CourierJob.Head ? _item.midPoint : _item.transform.position;
                if (MoveTo(target, speed, .7f))
                {
                    if (_item.grabbed) return Refuse(CourierReason.ItemBusy);
                    if (Job == CourierJob.Head && (ownerGone || !CourierHeadRoute.WithinRoomLimit(CourierHeadRoute.Path(ownerPos, target))))
                        return Refuse(CourierReason.HeadTooFar);
                    _phase = Job == CourierJob.Charge ? Phase.ToCharger : Phase.Deliver;
                    Hold(_item, HoldPoint(), dt);
                }
                return null;
            }
            case Phase.ToCharger:
            {
                var area = PoweredChargeArea();
                bool full = Battery() >= 100f;
                if (full || area == null)
                {
                    BeginChargeReturn(full ? null : CourierReason.NoStation);
                    return null;
                }
                Hold(_item, HoldPoint(), dt);
                if (MoveTo(area.position, speed * .85f, .9f))
                {
                    _phase = Phase.AtCharger;
                    _chargeStarted = Time.time;
                }
                return null;
            }
            case Phase.Deliver:
            {
                Hold(_item, HoldPoint(), dt);
                if (Job == CourierJob.Ally && (_ally == null || _ally.isDisabled)) { Cancel(); return (CourierNotice.Refused, ((byte)CourierReason.NoTeammate).ToString()); }
                bool toTruck = Job == CourierJob.Store || (ownerGone && Job is CourierJob.Charge or CourierJob.Head);
                var dest = toTruck ? truck : Job == CourierJob.Ally ? _ally.transform.position : ownerPos;
                if (toTruck && TruckSafetySpawnPoint.instance != null) dest = TruckSafetySpawnPoint.instance.transform.position;
                if (MoveTo(dest, speed * .85f, toTruck ? .5f : 1.2f))
                {
                    string label = ItemLabel(_item);
                    Drop(toTruck ? Vector3.zero : Flat(dest - Pos));
                    if (_chargeFailure is CourierReason failure) return Refuse(failure);
                    var done = Job;
                    Cancel(); _happyUntil = Time.time + 1.5f;
                    return (CourierNotice.Done, ((byte)done) + "|" + label);
                }
                return null;
            }
            case Phase.AtCharger:
            {
                var area = PoweredChargeArea();
                if (Battery() >= 100f) BeginChargeReturn(null);
                else if (area == null) BeginChargeReturn(CourierReason.NoStation);
                else if (Time.time - _chargeStarted > CourierRules.ChargeTimeout) BeginChargeReturn(CourierReason.ChargeTimedOut);
                else
                {
                    Hold(_item, area.position, dt);
                    FaceTowards(area.position, dt);
                }
                return null;
            }
            case Phase.Towing:
            {
                if (_cart == null || _cart.rb == null) { Cancel(); return null; }
                var grab = _cart.GetComponent<PhysGrabObject>();
                if (grab != null && grab.grabbed) { Cancel(); return null; } // someone took it over
                bool there = MoveTo(ownerPos, Mathf.Min(speed, 3.2f), 2.6f);
                var anchor = Pos - Fwd() * 1.1f;
                var pull = Flat(anchor - _cart.transform.position);
                var v = Vector3.ClampMagnitude(pull * 4f, 4f);
                _cart.rb.velocity = new Vector3(v.x, _cart.rb.velocity.y, v.z);
                if (grab != null) grab.OverrideGrabDisable(.2f);
                if (there) { Job = CourierJob.None; _phase = Phase.Follow; _cart = null; _happyUntil = Time.time + 1.5f; return (CourierNotice.Done, ((byte)CourierJob.Cart) + "|"); }
                return null;
            }
        }
        return null;
    }

    private void BeginChargeReturn(CourierReason? failure)
    {
        _chargeFailure = failure;
        _phase = Phase.Deliver;
        _jobStarted = Time.time;
    }

    private (CourierNotice, string) Refuse(CourierReason reason)
    {
        Cancel();
        _sadUntil = Time.time + 2f;
        return (CourierNotice.Refused, ((byte)reason).ToString());
    }

    private float Battery()
    {
        var b = _item != null ? _item.GetComponent<ItemBattery>() : null;
        return b != null ? b.batteryLife : 100f;
    }

    private static string ItemLabel(PhysGrabObject item)
    {
        var attributes = item != null ? item.GetComponent<ItemAttributes>() : null;
        return attributes != null ? Quests.QuestRewards.Label(attributes.item) : "";
    }

    private Vector3 Fwd() => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
    private Vector3 HoldPoint() => Pos + Vector3.up * (Flying ? 1.25f : .8f) + Fwd() * .05f;

    // ------------------------------------------------------------------ carrying

    private void Hold(PhysGrabObject item, Vector3 point, float dt)
    {
        if (item == null || item.rb == null) return;
        item.OverrideZeroGravity(.3f);
        item.OverrideGrabDisable(.4f);
        item.OverrideIndestructible(.3f);
        var delta = point - item.midPoint;
        item.rb.velocity = Vector3.ClampMagnitude(delta * 14f, 12f);
        item.rb.angularVelocity *= .85f;
        _itemAwayFor = delta.magnitude > 2.5f ? _itemAwayFor + dt : 0f;
        if (_itemAwayFor > 1.5f) { item.Teleport(point, item.transform.rotation); _itemAwayFor = 0f; }
    }

    private void Drop(Vector3 towards)
    {
        if (_item != null && _item.rb != null)
        {
            var push = towards.sqrMagnitude > .01f ? towards.normalized * 1.2f : Vector3.zero;
            _item.rb.velocity = push + Vector3.up * .5f;
        }
        _item = null;
    }

    // ------------------------------------------------------------------ hazards

    private (CourierNotice, string)? Hazards()
    {
        if (Time.time < _nextHazardCheck) return null;
        _nextHazardCheck = Time.time + .2f;
        foreach (var c in Physics.OverlapSphere(Pos + Vector3.up * .3f, .32f, ~0, QueryTriggerInteraction.Collide))
        {
            var hc = c != null ? c.GetComponent<HurtCollider>() : null;
            if (hc == null || !hc.isActiveAndEnabled || !hc.playerLogic || (!hc.playerKill && hc.playerDamage <= 0)) continue;
            if (hc.GetComponentInParent<ItemMelee>() != null || hc.GetComponentInParent<PlayerTumble>() != null || hc.GetComponentInParent<PlayerAvatar>() != null) continue;
            // Only a monster's own attack: traps, explosions, pits and other hazards leave it alone.
            if (hc.GetComponentInParent<EnemyParent>() == null && hc.GetComponentInParent<Enemy>() == null) continue;
            if (_hitCooldown.TryGetValue(hc, out float until) && Time.time < until) continue;
            _hitCooldown[hc] = Time.time + .75f;
            float damage = hc.playerKill ? 999f : hc.playerDamage;
            var down = Hurt(damage, hc.transform.position);
            if (down != null) return down;
        }
        return null;
    }

    internal float HitUntil;

    private (CourierNotice, string)? Hurt(float damage, Vector3 from)
    {
        Hp -= CourierRules.DamageTaken(damage, Level(C.CourierArmor));
        _lastHit = Time.time; _surprisedUntil = Time.time + 1f; HitUntil = Time.time + .6f;
        var away = Flat(Pos - from);
        if (away.sqrMagnitude < .01f) away = -Fwd();
        Pos = Ground(Pos + away.normalized * 1.2f); // knocked back
        if (Hp > 0f) return null;
        Cancel(); // the thing it was carrying drops where it fell; the backpack stays with it
        Down = true; Hp = 0f;
        float wait = CourierRules.Respawn(Level(C.CourierRecovery));
        DownUntil = Time.time + wait;
        return (CourierNotice.Down, Mathf.RoundToInt(wait).ToString());
    }

    // ------------------------------------------------------------------ monsters

    private readonly Dictionary<EnemyParent, float> _monsterCooldown = new();
    private float _nextMonsterCheck, _nextNotice;

    /// <summary>
    /// Monsters treat a courier as fair game: now and then one nearby notices it and comes to look (the game's own
    /// investigate call), and a monster right next to it knocks it back and hurts it. Low Profile makes both rarer.
    /// </summary>
    private (CourierNotice, string)? Monsters()
    {
        if (Time.time < _nextMonsterCheck) return null;
        _nextMonsterCheck = Time.time + .3f;
        var director = EnemyDirector.instance;
        if (director == null || director.enemiesSpawned == null || !SemiFunc.RunIsLevel()) return null;
        float aggression = CourierRules.Aggression(Level(C.CourierStealth));
        bool anyNear = false;
        foreach (var parent in director.enemiesSpawned)
        {
            if (parent == null || !parent.Spawned || parent.Enemy == null) continue;
            var enemy = parent.Enemy;
            var at = enemy.CenterTransform != null ? enemy.CenterTransform.position : enemy.transform.position;
            float d = Flat(at - Pos).magnitude;
            if (d < CourierRules.NoticeRadius) anyNear = true;
            if (d > CourierRules.HitRange || Mathf.Abs(at.y - Pos.y) > 1.8f || enemy.IsStunned()) continue;
            if (_monsterCooldown.TryGetValue(parent, out float ready) && Time.time < ready) continue;
            _monsterCooldown[parent] = Time.time + CourierRules.HitCooldown;
            if (Random.value > .55f * aggression) continue;
            var down = Hurt(CourierRules.HitDamage, at);
            if (down != null) return down;
        }
        if (anyNear && Time.time >= _nextNotice)
        {
            _nextNotice = Time.time + CourierRules.NoticeEvery;
            if (Random.value < CourierRules.NoticeChance * aggression) director.SetInvestigate(Pos + Vector3.up * .3f, 6f);
        }
        return null;
    }

    // ------------------------------------------------------------------ movement

    /// <summary>
    /// Where the pet heads while following. On the move it trails behind and to the right, out of the owner's way.
    /// Once the owner has stood still for a moment it walks round to a spot in front of them, a little to the right,
    /// and stays there (facing them) until it drifts out of their view or they walk off, so it can be petted. Turning
    /// to look around within that view doesn't make it circle the owner.
    /// </summary>
    private Vector3 FollowSpot(Vector3 ownerPos, float dt)
    {
        var forward = Flat(Owner.transform.forward);
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward);

        bool moving = Flat(ownerPos - _ownerLast).magnitude > Mathf.Max(.02f, dt * .6f); // faster than a slow shuffle
        _ownerLast = ownerPos;
        if (moving) { _ownerStillSince = Time.time; _frontSpot = null; }
        if (moving || Time.time - _ownerStillSince < .8f)
            return ownerPos - forward * 1.1f + right * .7f;

        // Re-pick the spot when there is none yet, or the current one is out of the owner's view or too far.
        if (_frontSpot is Vector3 spot)
        {
            var toSpot = Flat(spot - ownerPos);
            bool inView = toSpot.sqrMagnitude > .01f && Vector3.Angle(forward, toSpot) < 50f;
            if (inView && toSpot.magnitude < 2.5f) return spot;
        }
        _frontSpot = ownerPos + forward * 1.3f + right * .45f;
        return _frontSpot.Value;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private static bool Visible(Vector3 p)
    {
        var cam = Camera.main;
        if (cam == null) return false;
        var v = cam.WorldToViewportPoint(p + Vector3.up * .3f);
        return v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
    }

    internal static Vector3 Ground(Vector3 p) =>
        NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas) ? hit.position : p;

    private void FaceTowards(Vector3 p, float dt)
    {
        var d = Flat(p - Pos);
        if (d.sqrMagnitude > .01f) Yaw = Mathf.MoveTowardsAngle(Yaw, Quaternion.LookRotation(d).eulerAngles.y, 360f * dt);
    }

    /// <summary>Walks (or flies) toward <paramref name="goal"/> along the NavMesh. True once within <paramref name="stop"/>.</summary>
    private bool MoveTo(Vector3 goal, float speed, float stop)
    {
        float dt = Time.deltaTime;
        if (Flat(goal - Pos).magnitude <= stop && Mathf.Abs(goal.y - Pos.y) < 2f) return true;

        if (Time.time >= _repathAt || (goal - _pathGoal).sqrMagnitude > 1f)
        {
            if ((goal - _pathGoal).sqrMagnitude > 1f) { _bestDistance = float.MaxValue; _progressAt = Time.time; }
            _repathAt = Time.time + .5f; _pathGoal = goal; _corners.Clear();
            var from = Ground(Pos); var to = Ground(goal);
            if (NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _path) && _path.status != NavMeshPathStatus.PathInvalid)
            {
                _corners.AddRange(_path.corners);
                if (_path.status == NavMeshPathStatus.PathPartial && Flying) _corners.Add(goal); // fly the rest
            }
            else if (Flying) _corners.Add(goal);
        }
        Vector3 next = goal;
        while (_corners.Count > 0 && Flat(_corners[0] - Pos).magnitude < .15f) _corners.RemoveAt(0);
        if (_corners.Count > 0) next = _corners[0];

        var step = Flat(next - Pos);
        float move = Mathf.Min(step.magnitude, speed * dt);
        if (step.sqrMagnitude > .0001f)
        {
            Pos += step.normalized * move;
            Yaw = Mathf.MoveTowardsAngle(Yaw, Quaternion.LookRotation(step).eulerAngles.y, 540f * dt);
        }
        // Stay on the floor (the visual adds the hover height when flying).
        float y = NavMesh.SamplePosition(Pos, out var hit, 1.5f, NavMesh.AllAreas) ? hit.position.y : Mathf.Lerp(Pos.y, next.y, .2f);
        Pos.y = Mathf.Lerp(Pos.y, y, 1f - Mathf.Exp(-15f * dt));

        // Stuck with no progress for a while: hop there (players rarely see it; the visual poofs).
        float left = (goal - Pos).magnitude;
        if (left < _bestDistance - .25f) { _bestDistance = left; _progressAt = Time.time; }
        if (Time.time - _progressAt > 4f)
        {
            Pos = Ground(goal - Flat(goal - Pos).normalized * Mathf.Min(stop + .3f, 1f));
            _progressAt = Time.time; _bestDistance = float.MaxValue;
        }
        return false;
    }
}
