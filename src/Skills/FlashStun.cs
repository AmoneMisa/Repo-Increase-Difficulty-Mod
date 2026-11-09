using System.Collections.Generic;
using ExitGames.Client.Photon;
using IncreaseHardMod.Hud;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Flash Stun perk (Recon → Flashlight, needs maxed Brightness). Pressing <c>FlashStunKey</c> (F) flares the
/// flashlight: every monster inside the beam (the flashlight's own cone and range, line of sight) whose tier the
/// perk covers is stunned for <c>FlashStunSeconds</c> through the game's own EnemyStateStunned.Set. Level 1 covers
/// tier 1 monsters, level 2 tiers 1-2, level 3 every tier. <c>FlashStunCooldown</c> between uses.
///
/// The beam is measured on the presser's client (it owns the camera and flashlight); monsters are the host's, so a
/// guest sends the beam to the host (<see cref="EventCode"/>), which checks the sender's perk level, Brightness and
/// cooldown itself, clamps the beam, and applies the stuns. The flare is a short burst of the presser's own beam.
/// </summary>
internal sealed class FlashStun : MonoBehaviour
{
    internal const byte EventCode = 160; // guest -> host; see the event-code list in ModNetwork
    private const float MaxRange = 30f, MaxHalfAngle = 60f, FlareSeconds = .35f;

    private static float _readyAt, _flareUntil;
    private static readonly Dictionary<int, float> HostReadyAt = new();
    private static bool _subscribed;
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    /// <summary>Extra brightness for the local beam while it flares (read by the flashlight beam patch).</summary>
    internal static float Flare(PlayerAvatar owner) =>
        owner != null && owner.isLocal && Time.time < _flareUntil ? Mathf.Lerp(1f, 7f, (_flareUntil - Time.time) / FlareSeconds) : 1f;

    private static bool Usable(PlayerAvatar avatar, out int level)
    {
        level = SkillStore.Level(avatar, C.FlashStun);
        var bright = C.Find(C.Brightness);
        return level > 0 && bright != null && SkillStore.Level(avatar, C.Brightness) >= bright.MaxLevel;
    }

    private void Update()
    {
        if (!_subscribed && PhotonNetwork.NetworkingClient != null) { PhotonNetwork.NetworkingClient.EventReceived += OnEvent; _subscribed = true; }
        if (!Input.GetKeyDown(ModConfig.FlashStunKey.Value) || !SemiFunc.NoTextInputsActive() || SkillTreeScreen.IsOpen) return;
        if (!SemiFunc.RunIsLevel() && !SemiFunc.RunIsShop() && !SemiFunc.RunIsLobby()) return;
        var me = PlayerAvatar.instance;
        if (me == null || me.isDisabled || !Usable(me, out int level)) return;
        bool ru = SkillText.Russian;
        if (Time.time < _readyAt)
        {
            HudNotify.Toast(ru ? $"Вспышка будет готова через {Mathf.CeilToInt(_readyAt - Time.time)} с" : $"Flash Stun ready in {Mathf.CeilToInt(_readyAt - Time.time)} s",
                HudStyle.Bad, 1.5f, "flashstun");
            return;
        }
        var light = me.flashlightController != null ? me.flashlightController.spotlight : null;
        var view = me.localCamera != null ? me.localCamera.transform : Camera.main != null ? Camera.main.transform : null;
        if (view == null) return;
        float range = light != null ? light.range : 12f, half = light != null ? light.spotAngle * .5f : 25f;
        _readyAt = Time.time + Mathf.Max(5f, ModConfig.FlashStunCooldown.Value);
        _flareUntil = Time.time + FlareSeconds;
        HudNotify.Toast(ru ? "Вспышка!" : "Flash!", HudStyle.Good, 1.2f, "flashstun");
        if (SemiFunc.IsMasterClientOrSingleplayer()) Apply(me, view.position, view.forward, range, half);
        else PhotonNetwork.RaiseEvent(EventCode, new object[] { view.position, view.forward, range, half },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    private static void OnEvent(EventData ev)
    {
        if (ev.Code != EventCode || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        try
        {
            if (ev.CustomData is not object[] d || d.Length < 4 || d[0] is not Vector3 origin || d[1] is not Vector3 dir ||
                d[2] is not float range || d[3] is not float half) return;
            PlayerAvatar sender = null;
            foreach (var p in SemiFunc.PlayerGetList())
                if (p != null && p.photonView != null && p.photonView.Owner != null && p.photonView.Owner.ActorNumber == ev.Sender) { sender = p; break; }
            if (sender == null || sender.isDisabled) return;
            // The presser's eye must be where their avatar is (no flashing from across the map).
            if ((origin - sender.transform.position).sqrMagnitude > 9f) return;
            Apply(sender, origin, dir, range, half);
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[FlashStun] Request failed harmlessly: {e.Message}"); }
    }

    // Host: stun the monsters in the beam that this player's level covers.
    private static void Apply(PlayerAvatar who, Vector3 origin, Vector3 dir, float range, float half)
    {
        if (!Usable(who, out int level)) return;
        int key = who.photonView != null ? who.photonView.ViewID : 0;
        if (HostReadyAt.TryGetValue(key, out float ready) && Time.time < ready - .5f) return; // host-side cooldown
        HostReadyAt[key] = Time.time + Mathf.Max(5f, ModConfig.FlashStunCooldown.Value);
        range = Mathf.Clamp(range, 1f, MaxRange);
        half = Mathf.Clamp(half, 5f, MaxHalfAngle);
        dir = dir.sqrMagnitude > .0001f ? dir.normalized : Vector3.forward;
        var spawned = EnemyDirector.instance != null ? EnemyDirector.instance.enemiesSpawned : null;
        if (spawned == null) return;
        float seconds = Mathf.Max(.5f, ModConfig.FlashStunSeconds.Value);
        int stunned = 0;
        foreach (var ep in spawned)
        {
            var enemy = ep != null && ep.Spawned ? ep.Enemy : null;
            if (enemy == null || !enemy.HasStateStunned || enemy.StateStunned == null) continue;
            if ((int)ep.difficulty + 1 > level) continue;                // level 1: tier 1; level 2: tiers 1-2; level 3: all
            var at = enemy.CenterTransform != null ? enemy.CenterTransform.position : enemy.transform.position;
            var to = at - origin;
            float distance = to.magnitude;
            if (distance > range || distance < .01f || Vector3.Angle(dir, to) > half) continue;
            if (Blocked(origin, to / distance, distance, ep)) continue;
            enemy.StateStunned.Set(seconds);
            stunned++;
        }
        Plugin.Log.LogInfo($"[FlashStun] {SemiFunc.PlayerGetName(who)} flashed (level {level}): {stunned} monster(s) stunned for {seconds:0.#} s.");
    }

    // Walls block the flash; the monster's own colliders, players and loose objects don't.
    private static bool Blocked(Vector3 origin, Vector3 dir, float distance, EnemyParent target)
    {
        int count = Physics.RaycastNonAlloc(origin, dir, Hits, distance, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var c = Hits[i].collider;
            if (c == null || c.GetComponentInParent<EnemyParent>() == target) continue;
            if (c.GetComponentInParent<PlayerAvatar>() != null || c.GetComponentInParent<PhysGrabObject>() != null) continue;
            if (Hits[i].distance < distance - .4f) return true;
        }
        return false;
    }
}
