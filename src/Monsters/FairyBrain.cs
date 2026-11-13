using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Mischief Fairy (Tier I). Harmless to players and a pest for the haul: she flits about, and when she spots a
/// light, loose thing lying in a C.A.R.T. or an extraction point she hovers over it, chimes for 0.8 s, lifts it
/// with her wand and carries it off - for at most 7 s or 10 m - then sets it down somewhere else and giggles.
/// Taking the thing back, coming within 2.2 m, hitting or grabbing her makes her drop it and bolt. The thing
/// cannot break while she carries it or for a moment after she lets go. 14 s between thefts.
/// </summary>
internal sealed class FairyBrain : MonsterBrain
{
    private const byte Flit = 0, Approach = 1, Lift = 2, Carry = 3, Giggle = 4, Flee = 5;
    private PhysGrabObject _item;
    private Vector3 _tookAt, _dropAt;
    private float _nextTheft, _scan, _carried, _itemAwayFor;

    internal override MonsterPose Pose => State switch
    {
        Lift => MonsterPose.Prepare,
        Carry => MonsterPose.Hold,
        Giggle => MonsterPose.Special,
        Flee => MonsterPose.Flee,
        _ => MonsterPose.Move,
    };

    internal override MonsterAnim Anim => State switch
    {
        Lift => MonsterAnim.FairyLift,
        Carry => MonsterAnim.FairyCarry,
        Giggle => MonsterAnim.FairyGiggle,
        _ => MonsterAnim.None,
    };

    /// <summary>The carried thing: the host knows it; clients find it by the view id synced in Extra.</summary>
    internal override bool Tether(out Vector3 point)
    {
        point = default;
        if (State is not (Lift or Carry)) return false;
        if (_item != null) { point = _item.midPoint; return true; }
        if (IsHost || Extra < 1f) return false;
        var view = PhotonView.Find(Mathf.RoundToInt(Extra));
        if (view == null) return false;
        point = view.transform.position;
        return true;
    }

    protected override void OnSpawned() { Release(false); _nextTheft = Time.time + 6f; }
    protected override void OnDied() => Release(false);
    protected override void OnStunned() { if (_item != null) { Release(false); Announce(Flee); } }
    protected override void OnHurt() => Startle();
    protected override void OnGrabbed(PlayerAvatar player) => Startle();

    private void Startle()
    {
        Release(false);
        _nextTheft = Time.time + MonsterRules.FairyCooldown * .5f;
        Announce(Flee);
    }

    protected override void PlayStateCue(byte state)
    {
        // Warnings before a theft: a sly giggle and rising bells when she picks loot in the cart/extraction and
        // flies to it, and brighter bells as she starts to lift it - time for players to grab it back.
        if (state == Approach) { MonsterAudio.Play(MonsterAudio.Cue.Sparkle, BodyPosition, .85f); MonsterAudio.Play(MonsterAudio.Cue.Giggle, BodyPosition, .35f, 1.15f); }
        else if (state == Lift) MonsterAudio.Play(MonsterAudio.Cue.Sparkle, BodyPosition, 1f, 1.25f);
        else if (state == Giggle) MonsterAudio.Play(MonsterAudio.Cue.Giggle, BodyPosition, .5f, .95f);
        // Got away with it: a pleased giggle as she flies off with the loot.
        else if (state == Carry) MonsterAudio.Play(MonsterAudio.Cue.Giggle, BodyPosition, .45f, 1.1f);
        // Startled (hit, grabbed, stunned, or someone took the loot back): a sharp bell and fast wing beats.
        else if (state == Flee) { MonsterAudio.Play(MonsterAudio.Cue.Chime, BodyPosition, .8f, 1.3f); MonsterAudio.Play(MonsterAudio.Cue.FairyFlight, BodyPosition, .8f, 1.4f); }
    }

    private float _sparkleAt;

    // Every client: soft bells repeat while she flies to the loot and while she carries it off.
    protected override void Update()
    {
        base.Update();
        if (State is not (Approach or Carry) || Dead) { _sparkleAt = Time.time + 1.6f; return; }
        if (Time.time < _sparkleAt) return;
        _sparkleAt = Time.time + (State == Carry ? 2f : 1.6f);
        // Approach: soft bells; Carry: the bells keep trailing the stolen loot so players can chase it by ear.
        MonsterAudio.Play(MonsterAudio.Cue.Sparkle, BodyPosition, State == Carry ? .4f : .5f, State == Carry ? .95f : 1.1f);
    }

    protected override void HostTick(float dt)
    {
        Extra = _item != null && _item.photonView != null && State is Lift or Carry ? _item.photonView.ViewID : 0f;
        switch (State)
        {
            case Flit:
                Roam(2.6f);
                if (Time.time < _nextTheft || Time.time < _scan) break;
                _scan = Time.time + 1f;
                _item = FindLoot();
                if (_item != null) Announce(Approach);
                break;
            case Approach:
                if (!IsLoot(_item) || StateTime > 8f) { _item = null; SetState(Flit); break; }
                Move(_item.midPoint, 3.4f);
                if (Vector3.Distance(Flat(_item.midPoint), Flat(BodyPosition)) <= 1.1f) Announce(Lift);
                break;
            case Lift:
                Halt();
                if (!IsLoot(_item)) { _item = null; SetState(Flit); break; }
                Face(_item.midPoint);
                if (StateTime < MonsterRules.FairyLiftSeconds) break;
                _tookAt = _item.midPoint;
                _dropAt = PickDropPoint();
                _carried = 0f; _itemAwayFor = 0f;
                Announce(Carry);
                break;
            case Carry:
            {
                if (_item == null || _item.dead || _item.rb == null) { _item = null; _nextTheft = Time.time + MonsterRules.FairyCooldown; SetState(Flit); break; }
                _carried += dt;
                bool held = _item.playerGrabbing.Count > 0;
                var near = NearestVisible(MonsterRules.FairyStartleRange);
                float distance = Vector3.Distance(Flat(_item.midPoint), Flat(_tookAt));
                bool arrived = Vector3.Distance(Flat(BodyPosition), Flat(_dropAt)) < 1.2f && _carried > 1.5f;
                if (MonsterRules.FairyDrops(_carried, distance, held, near != null) || arrived)
                {
                    bool startled = held || near != null;
                    Release(!held);
                    _nextTheft = Time.time + MonsterRules.FairyCooldown;
                    if (startled) Announce(Flee); else Announce(Giggle);
                    break;
                }
                Move(_dropAt, 3.2f);
                Hold(_item, BodyPosition + Vector3.down * .55f, dt);
                break;
            }
            case Giggle:
                Halt();
                if (StateTime > 1.2f) SetState(Flit);
                break;
            case Flee:
            {
                var threat = NearestVisible(10f);
                if (threat != null) Move(AwayFrom(threat.transform.position, 8f), 4.6f); else Roam(3.4f);
                if (StateTime > MonsterRules.FairyFleeSeconds) SetState(Flit);
                break;
            }
        }
    }

    // Same carry as the courier: weightless, unbreakable, pulled to the hold point, teleported if it got stuck
    // behind something. Players can still grab it, which ends the theft.
    private void Hold(PhysGrabObject item, Vector3 point, float dt)
    {
        item.OverrideZeroGravity(.3f);
        item.OverrideIndestructible(.3f);
        var delta = point - item.midPoint;
        item.rb.linearVelocity = Vector3.ClampMagnitude(delta * 12f, 10f);
        item.rb.angularVelocity *= .85f;
        _itemAwayFor = delta.magnitude > 2.5f ? _itemAwayFor + dt : 0f;
        if (_itemAwayFor > 1.5f) { item.Teleport(point, item.transform.rotation); _itemAwayFor = 0f; }
    }

    // Let go; a thing she set down herself is lowered gently and cannot break on landing.
    private void Release(bool gently)
    {
        if (_item != null && _item.rb != null && !_item.dead)
        {
            _item.OverrideIndestructible(2f);
            if (gently) _item.rb.linearVelocity = Vector3.down * .5f;
        }
        _item = null;
    }

    private PhysGrabObject FindLoot()
    {
        PhysGrabObject best = null; float bestD = MonsterRules.FairySearchRange;
        foreach (var hit in Physics.OverlapSphere(BodyPosition, MonsterRules.FairySearchRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var pgo = hit.GetComponentInParent<PhysGrabObject>();
            if (pgo == null || !IsLoot(pgo)) continue;
            float d = Vector3.Distance(pgo.midPoint, BodyPosition);
            if (d < bestD) { best = pgo; bestD = d; }
        }
        return best;
    }

    private static bool IsLoot(PhysGrabObject pgo)
    {
        if (pgo == null || pgo.dead || pgo.rb == null || pgo.isEnemy || pgo.isCart) return false;
        var valuable = pgo.GetComponent<ValuableObject>();
        if (valuable == null || Patches.Effects.BiggerSoulDropsEffect.IsEnemyValuable(valuable)) return false;
        var room = pgo.roomVolumeCheck;
        bool inCart = pgo.impactDetector != null && pgo.impactDetector.inCart;
        if (Gear.CustomCarts.InArmoredCart(pgo.impactDetector)) return false;
        return MonsterRules.FairyMayLift(pgo.rb.mass, pgo.playerGrabbing.Count > 0, inCart, room != null && room.inExtractionPoint, room != null && room.inTruck);
    }

    // Somewhere else on the level: a roam point, else simply away from where the thing lay.
    private Vector3 PickDropPoint()
    {
        Vector3 best = AwayFrom(_tookAt, MonsterRules.FairyCarryDistance);
        float bestScore = float.MaxValue;
        var points = SemiFunc.LevelPointsGetAll();
        if (points == null) return best;
        foreach (var point in points)
        {
            if (point == null) continue;
            float d = Vector3.Distance(point.transform.position, _tookAt);
            if (d < MonsterRules.FairyCarryDistance * .8f || d > MonsterRules.FairyCarryDistance * 2.2f) continue;
            float score = Mathf.Abs(d - MonsterRules.FairyCarryDistance * 1.3f) + Random.value * 4f;
            if (score < bestScore) { bestScore = score; best = point.transform.position; }
        }
        return best;
    }
}
