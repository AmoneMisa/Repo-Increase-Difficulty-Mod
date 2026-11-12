using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using IncreaseHardMod.Hud;
using IncreaseHardMod.Networking;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// What the new monsters do to players.
///
/// HOST: owns every timer (<see cref="AfflictionTimers"/>), the 3 s immunity after hard control, the damage
/// over time, and every damage number. Damage is delivered to the victim's own client by
/// <see cref="EventCode"/> (the native PlayerHealth.Hurt only works on the owner), with the monster's enemy
/// index so death attribution, the room attack limit and the mod's damage perks all see it.
///
/// VICTIM CLIENT: applies its own movement lock / levitation / drag / slow, the HUD condition cards, and sends
/// a break-free request (<see cref="StruggleCode"/>) after enough struggling while constricted.
///
/// OTHER CLIENTS: a small effect on the victim's body (burning glow, electric arc, coils, stone shell, blood).
///
/// Snapshots are re-sent every second while an effect lasts (late joiners) and carry the level key and send
/// time, so stale or other-level packets are ignored.
/// </summary>
internal static class MonsterAfflictions
{
    internal const byte EventCode = 155, StruggleCode = 156;
    private const byte DamageKind = 255;

    // ------------------------------------------------------------------ host

    private sealed class HostState
    {
        public readonly AfflictionTimers Timers = new();
        public Enemy Source;
        public float BurnCarry, BleedCarry, VenomCarry, HoldCarry;
        public int BurnLeft, BleedLeft, VenomLeft;
        public float BurnDotUntil, BurnRate, StartedHold;
        public float NextSync;
    }

    private static readonly Dictionary<PlayerAvatar, HostState> Host = new();
    private static readonly List<PlayerAvatar> Scratch = new();
    private static string _level;

    private static HostState For(PlayerAvatar p)
    {
        if (!Host.TryGetValue(p, out var s)) Host[p] = s = new HostState();
        return s;
    }

    private static int Perk(PlayerAvatar p, string node) => Skills.SkillEffects.Level(p, node);

    internal static bool IsHardControlled(PlayerAvatar p) =>
        p != null && Host.TryGetValue(p, out var s) && s.Timers.HardControlUntil > Time.time;

    internal static bool CanControl(PlayerAvatar p) =>
        p != null && (!Host.TryGetValue(p, out var s) ||
                      MonsterRules.CanStartHardControl(Time.time, s.Timers.HardControlUntil, s.Timers.ImmuneUntil));

    /// <summary>Host: starts an affliction (perks applied); false when hard control is refused.</summary>
    internal static bool Apply(PlayerAvatar player, Affliction kind, float seconds, Enemy source)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || player == null || player.isDisabled || player.deadSet) return false;
        switch (kind)
        {
            case Affliction.Electrified: seconds = MonsterRules.GroundedSeconds(seconds, Perk(player, C.Grounding)); break;
            case Affliction.Venom: seconds = MonsterRules.AntidoteSeconds(seconds, Perk(player, C.Antidote)); break;
            case Affliction.Bleeding: seconds *= 1f - Skills.PerkRules.DurationResistance(Perk(player, C.Hemostasis)); break;
        }
        var s = For(player);
        if (!s.Timers.TryApply(kind, Time.time, seconds)) return false;
        s.Source = source != null ? source : s.Source;
        switch (kind)
        {
            case Affliction.Burning:
                // Burning is two separate effects: the 4 HP/s DoT for 3 s, and +50% damage taken for 5 s.
                s.BurnRate = MonsterRules.BurnDotPerSecond;
                float burnSeconds = MonsterRules.BurnDotSeconds * (1f - Skills.PerkRules.DurationResistance(Perk(player, C.ThermalProtection)));
                s.BurnLeft = MonsterRules.DotTotal(s.BurnRate, burnSeconds);
                s.BurnDotUntil = Time.time + burnSeconds;
                s.BurnCarry = 0f;
                break;
            case Affliction.Bleeding:
                s.BleedLeft = MonsterRules.DotTotal(MonsterRules.BleedPerSecond, MonsterRules.BleedSeconds); s.BleedCarry = 0f; break;
            case Affliction.Venom:
                s.VenomLeft = MonsterRules.DotTotal(MonsterRules.VenomPerSecond, seconds); s.VenomCarry = 0f; break;
        }
        if (MonsterRules.IsHardControl(kind)) { s.StartedHold = Time.time; s.HoldCarry = 0f; }
        Send(player, kind, s.Timers.Until(kind) - Time.time, source);
        return true;
    }

    /// <summary>Host: ends an affliction early (the source died, was stunned, the victim broke free).</summary>
    internal static void End(PlayerAvatar player, Affliction kind)
    {
        if (player == null || !Host.TryGetValue(player, out var s) || !s.Timers.Active(kind, Time.time)) return;
        s.Timers.End(kind, Time.time);
        Send(player, kind, 0f, null);
    }

    /// <summary>Host: ends every hard control a monster holds (its death, stun or despawn).</summary>
    internal static void ReleaseFrom(Enemy source)
    {
        if (source == null) return;
        Scratch.Clear();
        foreach (var kv in Host) if (kv.Value.Source == source) Scratch.Add(kv.Key);
        foreach (var p in Scratch)
            foreach (var a in new[] { Affliction.Electrified, Affliction.Constricted, Affliction.Petrified, Affliction.Dragged, Affliction.Seized })
                End(p, a);
    }

    internal static bool Has(PlayerAvatar player, Affliction kind) =>
        player != null && Host.TryGetValue(player, out var s) && s.Timers.Active(kind, Time.time);

    /// <summary>Host: monster damage to a player, delivered to the player's own client.</summary>
    internal static void Damage(PlayerAvatar player, int damage, Enemy source, bool bypassBurning = false)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || player == null || damage <= 0 || player.isDisabled || player.deadSet) return;
        int index = source != null ? SemiFunc.EnemyGetIndex(source) : -1;
        if (!SemiFunc.IsMultiplayer() || player.photonView == null || player.photonView.IsMine)
        { HurtLocal(player, damage, index, bypassBurning); return; }
        PhotonNetwork.RaiseEvent(EventCode, new object[] { player.photonView.ViewID, DamageKind, (float)damage, PhotonNetwork.Time, LevelKey(), index, bypassBurning },
            new RaiseEventOptions { TargetActors = new[] { player.photonView.Owner.ActorNumber } }, SendOptions.SendReliable);
    }

    internal static void TickHost(float dt)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        string level = LevelKey();
        if (level != _level) { _level = level; Host.Clear(); }
        if (Host.Count == 0) return;
        float now = Time.time;
        Scratch.Clear();
        foreach (var kv in Host)
        {
            var p = kv.Key; var s = kv.Value;
            if (p == null || p.isDisabled || p.deadSet) { Scratch.Add(p); continue; }
            s.Timers.Expire(now, a => Send(p, a, 0f, null));
            var src = s.Source;
            if (src != null && (!src.isActiveAndEnabled || (src.HasHealth && src.Health.dead)))
            { ReleaseFrom(src); s.Source = null; }

            // Damage over time, whole points.
            if (s.BurnLeft > 0 && now < s.BurnDotUntil + .05f)
            { int d = MonsterRules.DotTick(ref s.BurnCarry, s.BurnRate, dt, ref s.BurnLeft); if (d > 0) Damage(p, d, s.Source, bypassBurning: true); }
            if (s.BleedLeft > 0)
            { int d = MonsterRules.DotTick(ref s.BleedCarry, MonsterRules.BleedPerSecond, dt, ref s.BleedLeft); if (d > 0) Damage(p, d, s.Source, bypassBurning: true); }
            if (s.VenomLeft > 0 && s.Timers.Active(Affliction.Venom, now))
            { int d = MonsterRules.DotTick(ref s.VenomCarry, MonsterRules.VenomPerSecond, dt, ref s.VenomLeft); if (d > 0) Damage(p, d, s.Source, bypassBurning: true); }
            else s.VenomLeft = 0;
            bool hold = s.Timers.Active(Affliction.Electrified, now) || s.Timers.Active(Affliction.Constricted, now);
            if (hold)
            {
                float rate = s.Timers.Active(Affliction.Electrified, now) ? MonsterRules.LevitatePerSecond : MonsterRules.ConstrictPerSecond;
                int big = 1000;
                int d = MonsterRules.DotTick(ref s.HoldCarry, rate, dt, ref big);
                if (d > 0) Damage(p, d, s.Source, bypassBurning: true);
            }

            if (now >= s.NextSync)
            {
                s.NextSync = now + 1f;
                for (int i = 0; i < MonsterRules.AfflictionCount; i++)
                {
                    var a = (Affliction)i;
                    if (s.Timers.Active(a, now)) Send(p, a, s.Timers.Until(a) - now, s.Source, cacheOnly: true);
                }
            }
        }
        foreach (var p in Scratch) Host.Remove(p);
    }

    private static void Send(PlayerAvatar player, Affliction kind, float seconds, Enemy source, bool cacheOnly = false)
    {
        if (player == null) return;
        int sourceId = source != null && source.PhotonView != null ? source.PhotonView.ViewID : 0;
        if (!cacheOnly) Receive(player, kind, seconds, sourceId, source);
        if (!SemiFunc.IsMultiplayer() || player.photonView == null || !PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(EventCode, new object[] { player.photonView.ViewID, (byte)kind, seconds, PhotonNetwork.Time, LevelKey(), sourceId, false },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    internal static string LevelKey() => ModNetwork.FreezeLevelKey();

    /// <summary>Validates the host's packet (sender, level, age, ranges) and applies it.</summary>
    internal static void OnEvent(EventData message)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null || message.Sender != PhotonNetwork.MasterClient.ActorNumber ||
            !MonsterPackets.ReadAffliction(message.CustomData, PhotonNetwork.Time, LevelKey(), out int viewId, out byte kind,
                out float value, out int extra, out bool flag)) return;
        var view = PhotonView.Find(viewId);
        var player = view != null ? view.GetComponent<PlayerAvatar>() : null;
        if (player == null) return;
        if (kind == DamageKind)
        {
            if (view.IsMine) HurtLocal(player, Mathf.RoundToInt(value), extra, flag);
            return;
        }
        Receive(player, (Affliction)kind, value, extra);
    }

    /// <summary>Host: a victim's client says it struggled free of a constriction.</summary>
    internal static void OnStruggle(EventData message)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || message.Sender <= 0 || message.CustomData is not object[] data ||
            data.Length != 2 || data[0] is not int viewId || data[1] is not string level || level != LevelKey()) return;
        var view = PhotonView.Find(viewId);
        var player = view != null ? view.GetComponent<PlayerAvatar>() : null;
        if (player == null || view.Owner == null || view.Owner.ActorNumber != message.Sender) return; // only for yourself
        HostStruggle(player);
    }

    private static void HostStruggle(PlayerAvatar player)
    {
        if (!Host.TryGetValue(player, out var s) || !s.Timers.Active(Affliction.Constricted, Time.time)) return;
        // The client's meter already needs EscapeSeconds of struggling; the host only rejects impossible timing.
        float needed = MonsterRules.EscapeSeconds(Perk(player, C.Evasion)) * .5f;
        if (Time.time - s.StartedHold < needed) return;
        End(player, Affliction.Constricted);
    }

    // ------------------------------------------------------------------ every client

    private sealed class LocalState
    {
        public readonly float[] Until = new float[MonsterRules.AfflictionCount];
        public int SourceId;
        public Enemy Source; // host and singleplayer: known directly (no view id offline)
    }

    private static readonly Dictionary<PlayerAvatar, LocalState> Shown = new();
    private static float _struggle;
    private static Vector3 _lastMove;
    private static bool _struggleSent;

    private static void Receive(PlayerAvatar player, Affliction kind, float seconds, int sourceId, Enemy sourceEnemy = null)
    {
        if (player == null) return;
        if (!Shown.TryGetValue(player, out var s)) Shown[player] = s = new LocalState();
        s.Until[(int)kind] = seconds > 0f ? Time.time + seconds : 0f;
        if (sourceId != 0) s.SourceId = sourceId;
        if (sourceEnemy != null) s.Source = sourceEnemy;
        var visual = player.GetComponent<AfflictionVisual>() ?? player.gameObject.AddComponent<AfflictionVisual>();
        visual.Set(kind, seconds, sourceId);
        if (!IsLocal(player)) return;
        if (kind == Affliction.Constricted && seconds > 0f) { _struggle = 0f; _struggleSent = false; }
        var condition = Condition(kind);
        if (condition == null) return;
        if (seconds > 0f) HudConditions.Start(condition.Value, seconds);
        else HudConditions.End(condition.Value);
    }

    private static PlayerCondition? Condition(Affliction a) => a switch
    {
        Affliction.Burning => PlayerCondition.Burning,
        Affliction.Electrified => PlayerCondition.Electrified,
        Affliction.Constricted => PlayerCondition.Constricted,
        Affliction.Petrified => PlayerCondition.Petrified,
        Affliction.Bleeding => PlayerCondition.Bleeding,
        Affliction.Venom => PlayerCondition.Venom,
        Affliction.Slowed => PlayerCondition.Slowed,
        Affliction.Dragged => PlayerCondition.Dragged,
        Affliction.Seized => PlayerCondition.Seized,
        _ => null,
    };

    private static bool IsLocal(PlayerAvatar p) => p != null && (!SemiFunc.IsMultiplayer() || (p.photonView != null && p.photonView.IsMine));

    internal static bool LocalActive(Affliction a)
    {
        var me = PlayerController.instance != null ? PlayerController.instance.playerAvatarScript : null;
        return me != null && Shown.TryGetValue(me, out var s) && s.Until[(int)a] > Time.time;
    }

    private static bool _bypass;
    internal static int DotDamage { get; private set; }
    private static void HurtLocal(PlayerAvatar player, int damage, int enemyIndex, bool bypassBurning)
    {
        if (player.playerHealth == null) return;
        bool previous = _bypass;
        int previousDot = DotDamage;
        _bypass = bypassBurning;
        DotDamage = bypassBurning ? damage : 0;
        try { player.playerHealth.Hurt(damage, false, enemyIndex); }
        finally { _bypass = previous; DotDamage = previousDot; }
    }

    // Damage-over-time ticks (burn, bleed, venom, holds) are silent: no hurt glitch or hurt sound every second on the
    // victim's screen, and no hurt sound for teammates. The health bar still drops; direct hits keep their effects.
    [HarmonyPatch(typeof(CameraGlitch), nameof(CameraGlitch.PlayShortHurt))]
    private static class QuietDotShort
    {
        [HarmonyPrefix] private static bool Prefix() => DotDamage <= 0;
    }

    [HarmonyPatch(typeof(CameraGlitch), nameof(CameraGlitch.PlayLongHurt))]
    private static class QuietDotLong
    {
        [HarmonyPrefix] private static bool Prefix() => DotDamage <= 0;
    }

    [HarmonyPatch(typeof(PhotonView), nameof(PhotonView.RPC), new[] { typeof(string), typeof(RpcTarget), typeof(object[]) })]
    private static class QuietDotForOthers
    {
        [HarmonyPrefix]
        private static void Prefix(string methodName, object[] parameters)
        {
            if (DotDamage > 0 && methodName == "UpdateHealthRPC" && parameters != null && parameters.Length >= 3 && parameters[2] is bool)
                parameters[2] = false; // effect: no hurt sound on the other clients
        }
    }

    /// <summary>+50% damage taken while burning, once (not per fireball); the burn DoT itself is exempt.</summary>
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static class BurningVulnerability
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(PlayerHealth __instance, ref int damage, bool hurtByHeal)
        {
            if (_bypass || hurtByHeal || damage <= 0 || __instance.playerAvatar == null || !IsLocal(__instance.playerAvatar)) return;
            damage = MonsterRules.BurningDamage(damage, LocalActive(Affliction.Burning));
        }
    }

    /// <summary>Victim client, every physics step: movement lock, levitation, drag, slow, struggle meter.</summary>
    internal static void TickLocal()
    {
        var controller = PlayerController.instance;
        var me = controller != null ? controller.playerAvatarScript : null;
        if (me == null || !Shown.TryGetValue(me, out var s)) return;
        float now = Time.time;
        bool electric = s.Until[(int)Affliction.Electrified] > now, coil = s.Until[(int)Affliction.Constricted] > now,
            stone = s.Until[(int)Affliction.Petrified] > now, drag = s.Until[(int)Affliction.Dragged] > now,
            slow = s.Until[(int)Affliction.Slowed] > now, seized = s.Until[(int)Affliction.Seized] > now;
        if (me.isDisabled || me.deadSet) { System.Array.Clear(s.Until, 0, s.Until.Length); return; }
        if (electric || coil || stone || drag || seized)
        {
            controller.OverrideSpeed(0f, .12f);
            controller.OverrideJumpCooldown(.2f);
            controller.JumpImpulse = false;
            controller.JumpInputBuffer = 0f;
        }
        else if (slow) controller.OverrideSpeed(MonsterRules.StareSlowFactor, .12f);
        if (stone && PhysGrabber.instance != null)
        {
            PhysGrabber.instance.OverrideGrabDisable(.2f);
            if (PhysGrabber.instance.grabbed) PhysGrabber.instance.ReleaseObject(-1, .3f);
        }
        var rb = controller.rb;
        var source = s.Source != null ? s.Source.transform : s.SourceId != 0 ? PhotonView.Find(s.SourceId)?.transform : null;
        if (rb != null && !rb.isKinematic)
        {
            if (electric)
            {
                // Lifted and held: rise to about 1.4 m above the floor, gentle bob.
                float floor = Physics.Raycast(rb.position + Vector3.up * .2f, Vector3.down, out var hit, 4f, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore)
                    ? hit.point.y : rb.position.y - MonsterRules.LevitateHeight;
                float wanted = floor + MonsterRules.LevitateHeight + .1f * Mathf.Sin(now * 6f);
                rb.linearVelocity = new Vector3(rb.linearVelocity.x * .5f, Mathf.Clamp((wanted - rb.position.y) * 6f, -3f, 3f), rb.linearVelocity.z * .5f);
            }
            else if (drag && source != null)
            {
                // Pinned on the gorgon's horns: kept just in front of it.
                var bull = source;
                var target = bull.position + bull.forward * 1.6f;
                var delta = target - rb.position; delta.y = 0f;
                rb.linearVelocity = new Vector3(Mathf.Clamp(delta.x * 10f, -9f, 9f), rb.linearVelocity.y, Mathf.Clamp(delta.z * 10f, -9f, 9f));
            }
            else if (seized && source != null)
            {
                // Held between the twins and swung from side to side before the throw.
                var target = source.position + source.forward * .9f + Vector3.up * .45f + source.right * (Mathf.Sin(now * 15.7f) * .4f);
                var delta = target - rb.position;
                rb.linearVelocity = new Vector3(Mathf.Clamp(delta.x * 10f, -8f, 8f), Mathf.Clamp(delta.y * 8f, -4f, 4f), Mathf.Clamp(delta.z * 10f, -8f, 8f));
            }
            else if (coil || stone || seized)
                rb.linearVelocity = new Vector3(0f, Mathf.Min(rb.linearVelocity.y, 0f), 0f);
        }
        if (coil && !_struggleSent)
        {
            // Struggle: changing direction keys or jump presses fill the meter; holding still drains it.
            var move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            bool active = Input.GetKeyDown(KeyCode.Space) || (move.sqrMagnitude > .1f && (move - _lastMove).sqrMagnitude > .1f) ||
                          (move.sqrMagnitude > .1f && Input.anyKeyDown);
            _lastMove = move;
            _struggle = Mathf.Max(0f, _struggle + (active ? .35f : move.sqrMagnitude > .1f ? Time.fixedDeltaTime * .6f : -Time.fixedDeltaTime * .3f));
            float need = MonsterRules.EscapeSeconds(Skills.SkillEffects.Level(me, C.Evasion));
            HudNotify.Status("ihm.struggle", (Localization.GameLanguage.Russian ? "ВЫРЫВАЙТЕСЬ: " : "STRUGGLE: ") +
                Mathf.RoundToInt(Mathf.Clamp01(_struggle / need) * 100f) + "%", HudStyle.Caution);
            if (_struggle >= need)
            {
                _struggleSent = true;
                HudNotify.Clear("ihm.struggle");
                if (!SemiFunc.IsMultiplayer()) HostStruggle(me);
                else PhotonNetwork.RaiseEvent(StruggleCode, new object[] { me.photonView.ViewID, LevelKey() },
                    new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
            }
        }
        else if (!coil) HudNotify.Clear("ihm.struggle");
    }

    internal static void ClearAll()
    {
        Host.Clear();
        Shown.Clear();
    }
}
