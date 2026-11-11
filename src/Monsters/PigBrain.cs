using IncreaseHardMod.Hud;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Golden Pig (Tier I, neutral): Fly -> SeekFood -> Eat (grow) -> Land -> Flee, never attacks.
///
/// Players feed it by putting a small valuable down near it (at most $3,500 and 4.5 kg, not a soul). Only a
/// valuable a player let go of within its reach counts, and it stays on offer for 30 s; loot still lying where the
/// level put it, and anything in a cart, an extraction zone or the truck, is never eaten. The host checks the item
/// again when the pig bites, then destroys it
/// with the native call, so one item can never be eaten twice. Each meal grows the pig (HP 60-180, mass 18-75 kg,
/// size, its soul value). From the third meal the fattening can be finished by grabbing the pig (or it ends by
/// itself after 25 s without food, or at ten meals): it drops its wings, lands and runs away.
/// Rewards are paid once per level on death: the soul (MonsterRules.PigSoul, capped by the value fed), from three
/// meals a small golden souvenir, at ten meals a 10% chance of an upgrade. A dead pig does not come back that level.
/// <see cref="MonsterBrain.Extra"/> carries the meal count, +100 once landed, for every client's hint and visuals.
/// </summary>
internal sealed class PigBrain : MonsterBrain
{
    private const byte Fly = 0, Seek = 1, Eat = 2, Land = 3, Flee = 4;
    private int _feeds;
    private float _consumed, _lastMeal, _scan;
    private bool _landed;
    private PhysGrabObject _food;
    // Valuables a player put down near the pig, and until when each stays on offer.
    private readonly System.Collections.Generic.Dictionary<PhysGrabObject, float> _offered = new();
    private const float OfferRange = 6f;
    private Vector3 _bodyScale = Vector3.zero;
    private static bool _diedThisLevel, _rewarded;

    internal static void ResetLevel() { _diedThisLevel = false; _rewarded = false; }

    internal override MonsterPose Pose => State switch
    {
        Eat => MonsterPose.Attack,
        Land => MonsterPose.Prepare,
        Flee => MonsterPose.Flee,
        _ => MonsterPose.Move,
    };

    internal override MonsterAnim Anim => State switch
    {
        Eat => MonsterAnim.Eat,
        Land => MonsterAnim.Land,
        _ => MonsterAnim.None,
    };

    protected override float HoverHeight() => _landed || (!IsHost && Extra >= 100f) ? 0f : Hover;

    protected override void OnSpawned()
    {
        if (_diedThisLevel)
        {
            // Once per level: a killed pig stays gone.
            Parent.Despawn();
            Parent.DespawnedTimerSet(1e6f);
            return;
        }
        _lastMeal = Time.time;
        _offered.Clear();
    }

    protected override int SoulValue() => MonsterRules.PigSoul(_feeds, _consumed, MonsterRegistry.TeamSoulHunter());

    protected override void OnDied()
    {
        _diedThisLevel = true;
        if (_rewarded) return;
        _rewarded = true;
        MonsterRules.PigRewards(_feeds, Random.value, out bool souvenir, out bool upgrade);
        var at = BodyPosition + Vector3.up * .3f;
        if (souvenir) MonsterRegistry.SpawnSouvenir(at + Vector3.right * .4f);
        if (upgrade) MonsterRegistry.SpawnUpgrade(at + Vector3.left * .4f);
        Plugin.Log.LogInfo($"[Monsters] Golden Pig died after {_feeds} meal(s) (${_consumed:N0} fed): souvenir={souvenir}, upgrade={upgrade}.");
    }

    protected override void OnGrabbed(PlayerAvatar player)
    {
        if (_landed) return;
        if (MonsterRules.PigCanFinish(_feeds)) Finish();
        else MonsterAudio.Play(MonsterAudio.Cue.Oink, BodyPosition, 1.2f);
    }

    protected override void OnHurt()
    {
        if (!_landed) MonsterAudio.Play(MonsterAudio.Cue.Oink, BodyPosition, 1.3f);
    }

    private void Finish()
    {
        _landed = true;
        _food = null;
        Announce(Land);
    }

    protected override void PlayStateCue(byte state)
    {
        if (state == Eat) MonsterAudio.Play(MonsterAudio.Cue.Chomp, BodyPosition);
        else if (state == Land) MonsterAudio.Play(MonsterAudio.Cue.Oink, BodyPosition, 1.4f);
    }

    protected override void HostTick(float dt)
    {
        Extra = _feeds + (_landed ? 100 : 0);
        switch (State)
        {
            case Fly:
                Roam(1.8f);
                if (Random.value < dt * .15f) MonsterAudio.Play(MonsterAudio.Cue.Oink, BodyPosition, .6f);
                if (MonsterRules.PigCanFinish(_feeds) && Time.time - _lastMeal > MonsterRules.PigSatedAfter) { Finish(); break; }
                if (Time.time >= _scan) { _scan = Time.time + .5f; _food = FindFood(6f); if (_food != null) SetState(Seek); }
                break;
            case Seek:
                if (!IsFood(_food) || Vector3.Distance(_food.transform.position, BodyPosition) > 8f) { _food = null; SetState(Fly); break; }
                Move(_food.transform.position, 2.6f);
                if (Vector3.Distance(Flat(_food.transform.position), Flat(BodyPosition)) <= MonsterRules.PigEatRange) Announce(Eat);
                break;
            case Eat:
                Halt();
                if (_food != null) Face(_food.transform.position);
                if (StateTime < 1f) break;
                if (IsFood(_food) && Vector3.Distance(Flat(_food.transform.position), Flat(BodyPosition)) <= MonsterRules.PigEatRange + .5f)
                    Consume(_food);
                _food = null;
                if (_feeds >= MonsterRules.PigMaxFeeds) Finish(); else SetState(Fly);
                break;
            case Land:
                Halt();
                if (StateTime > 1.2f) SetState(Flee);
                break;
            case Flee:
                var threat = NearestVisible(12f);
                float speed = Mathf.Lerp(3f, 1.2f, _feeds / (float)MonsterRules.PigMaxFeeds);
                if (threat != null) Move(AwayFrom(threat.transform.position, 8f), speed); else Roam(speed * .7f);
                break;
        }
    }

    private void Consume(PhysGrabObject food)
    {
        var valuable = food.GetComponent<ValuableObject>();
        float value = valuable != null ? valuable.dollarValueCurrent : 0f;
        food.DestroyPhysGrabObject(); // native host destroy: gone for everyone, cannot be eaten again
        _feeds++;
        _consumed += value;
        _lastMeal = Time.time;
        if (E.HasHealth)
        {
            int previousMax = E.Health.health;
            E.Health.health = MonsterRules.PigHealth(_feeds);
            E.Health.healthCurrent = Mathf.Min(E.Health.health, E.Health.healthCurrent + (E.Health.health - previousMax));
        }
        if (E.HasRigidbody) E.Rigidbody.rb.mass = MonsterRules.PigMass(_feeds);
        Plugin.Log.LogInfo($"[Monsters] Golden Pig ate a ${value:N0} valuable ({_feeds}/{MonsterRules.PigMaxFeeds}).");
    }

    private PhysGrabObject FindFood(float range)
    {
        NoteOffers();
        PhysGrabObject best = null; float bestD = range;
        foreach (var pair in _offered)
        {
            var pgo = pair.Key;
            if (!IsFood(pgo)) continue;
            float d = Vector3.Distance(pgo.transform.position, BodyPosition);
            if (d < bestD) { best = pgo; bestD = d; }
        }
        return best;
    }

    // A valuable becomes an offer when a player lets go of it within reach of the pig (the game remembers the last
    // holder for 3 s after the release; this runs twice a second).
    private void NoteOffers()
    {
        float now = Time.time;
        _stale.Clear();
        foreach (var pair in _offered) if (pair.Key == null || pair.Key.dead || pair.Value <= now) _stale.Add(pair.Key);
        foreach (var gone in _stale) _offered.Remove(gone);
        foreach (var hit in Physics.OverlapSphere(BodyPosition, OfferRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var pgo = hit.GetComponentInParent<PhysGrabObject>();
            if (pgo == null || pgo.dead || pgo.lastPlayerGrabbing == null || pgo.GetComponent<ValuableObject>() == null) continue;
            _offered[pgo] = now + MonsterRules.PigOfferSeconds;
        }
    }

    private readonly System.Collections.Generic.List<PhysGrabObject> _stale = new();

    private bool IsFood(PhysGrabObject pgo)
    {
        if (pgo == null || pgo.dead || pgo.rb == null) return false;
        var valuable = pgo.GetComponent<ValuableObject>();
        if (valuable == null || Patches.Effects.BiggerSoulDropsEffect.IsEnemyValuable(valuable)) return false;
        var room = pgo.roomVolumeCheck;
        return MonsterRules.PigAccepts(valuable.dollarValueCurrent, pgo.rb.mass, pgo.grabbed || pgo.playerGrabbing.Count > 0,
            _offered.TryGetValue(pgo, out float until) && until > Time.time, pgo.impactDetector != null && pgo.impactDetector.inCart,
            room != null && room.inExtractionPoint, room != null && room.inTruck);
    }

    protected override void Update()
    {
        base.Update();
        if (E == null || !E.HasRigidbody) return;
        // Size follows the meals on every client (Extra is synced); the collider body grows with the model.
        if (_bodyScale == Vector3.zero) _bodyScale = E.Rigidbody.transform.localScale;
        int feeds = Mathf.RoundToInt(Extra % 100f);
        var wanted = _bodyScale * MonsterRules.PigScale(feeds);
        if (E.Rigidbody.transform.localScale != wanted)
            E.Rigidbody.transform.localScale = Vector3.MoveTowards(E.Rigidbody.transform.localScale, wanted, Time.deltaTime);
        Hint(feeds, Extra >= 100f);
    }

    // Every client: a short instruction while the local player is near a live pig.
    private void Hint(int feeds, bool landed)
    {
        var me = PlayerController.instance != null ? PlayerController.instance.playerAvatarScript : null;
        if (me == null || Dead || Parent == null || !Parent.Spawned || Vector3.Distance(me.transform.position, BodyPosition) > 9f) return;
        bool ru = Localization.GameLanguage.Russian;
        string text = landed
            ? (ru ? $"ЗОЛОТАЯ СВИНКА СЫТА ({feeds}/10) — ДОГОНИТЕ ЕЁ" : $"GOLDEN PIG IS FULL ({feeds}/10) — CATCH IT")
            : MonsterRules.PigCanFinish(feeds)
                ? (ru ? $"КОРМЛЕНИЙ {feeds}/10 — СХВАТИТЕ СВИНКУ, ЧТОБЫ ЗАКОНЧИТЬ" : $"FED {feeds}/10 — GRAB THE PIG TO FINISH")
                : (ru ? $"ЗОЛОТАЯ СВИНКА {feeds}/10 — ПОЛОЖИТЕ РЯДОМ НЕБОЛЬШУЮ ЦЕННОСТЬ" : $"GOLDEN PIG {feeds}/10 — DROP A SMALL VALUABLE NEAR IT");
        HudNotify.Status("ihm.pig", text, HudStyle.Info);
    }
}
