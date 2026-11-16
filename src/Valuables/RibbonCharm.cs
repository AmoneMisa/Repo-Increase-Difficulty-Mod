using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Hud;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// The red ribbon of Fable Realm. It works only in the hands: if a player would die while holding it (a lethal hit,
/// a fall into a death pit, an instant kill), the ribbon takes the blow once. The player survives, is carried back
/// to the truck the way the game returns a player who fell out of the level, and the ribbon is used up.
///
/// Each client knows what its own player holds; the host sees every grab. Damage is applied on the player's own
/// client, so that client stops a lethal hit itself and asks the host for the rescue; the host performs it.
/// </summary>
internal sealed class RibbonCharm : MonoBehaviour
{
    private const float GraceSeconds = 3f, VanishSeconds = 1f, ClaimWindow = 1.5f;
    private const string HudKey = "ihm.ribbon";
    private static readonly List<RibbonCharm> Live = new();
    // Players just rescued: while they are carried to the truck, a second instant death in the same pit is ignored.
    private static readonly Dictionary<int, float> Grace = new();

    private PhotonView _view;
    private PhysGrabObject _grab;
    private bool _spent, _rescued, _heldLocal;
    // Host: who held the ribbon last and when, so a claim that arrives just after the grab broke still counts.
    private readonly Dictionary<int, float> _heldAt = new();

    private void Awake() => Live.Add(this);
    private void OnDestroy() => Live.Remove(this);

    private void Start()
    {
        _view = GetComponent<PhotonView>();
        _grab = GetComponent<PhysGrabObject>();
    }

    /// <summary>The network id of a player in multiplayer; in singleplayer the instance id (there is no network).</summary>
    private static int Key(PlayerAvatar avatar) =>
        avatar == null ? 0 : GameManager.Multiplayer() ? (avatar.photonView != null ? avatar.photonView.ViewID : 0) : avatar.GetInstanceID();

    private void Update()
    {
        if (_spent || _grab == null) return;
        // The local player is told once per pick-up that the ribbon is now protecting them.
        bool held = HeldBy(PlayerAvatar.instance);
        if (held && !_heldLocal)
            HudNotify.Toast(Localization.GameLanguage.Russian ? "Пока красная ленточка в руках, она убережёт вас один раз" : "While you hold the red ribbon, it will keep you from death once",
                HudStyle.Good, 4f, HudKey + ".held");
        _heldLocal = held;
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        foreach (var grabber in _grab.playerGrabbing)
            if (grabber != null && grabber.playerAvatar != null) _heldAt[Key(grabber.playerAvatar)] = Time.time;
    }

    /// <summary>
    /// True while <paramref name="avatar"/> holds this ribbon: the player's own grabber on their client,
    /// the grab list on the host (only the host receives every player's grabs).
    /// </summary>
    private bool HeldBy(PlayerAvatar avatar)
    {
        if (avatar == null || _grab == null) return false;
        if (avatar.isLocal && avatar.physGrabber != null && avatar.physGrabber.grabbed && avatar.physGrabber.grabbedPhysGrabObject == _grab) return true;
        foreach (var grabber in _grab.playerGrabbing)
            if (grabber != null && grabber.playerAvatar == avatar) return true;
        return false;
    }

    private static RibbonCharm Protecting(PlayerAvatar avatar)
    {
        foreach (var ribbon in Live)
            if (ribbon != null && !ribbon._spent && ribbon.HeldBy(avatar)) return ribbon;
        return null;
    }

    // On the client that stopped the death: the player's own client for a lethal hit, or the host for a kill it decided.
    private void Claim(PlayerAvatar avatar)
    {
        Protect(avatar);
        if (SemiFunc.IsMasterClientOrSingleplayer()) { Rescue(avatar); return; }
        _spent = true;
        _view.RPC(nameof(IHM_RibbonClaim), RpcTarget.MasterClient, Key(avatar));
    }

    [PunRPC]
    private void IHM_RibbonClaim(int player, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        var view = PhotonView.Find(player);
        var avatar = view != null ? view.GetComponent<PlayerAvatar>() : null;
        // Only the player's own client may claim, only once, and only for a ribbon the host saw in their hands.
        bool held = avatar != null && (HeldBy(avatar) || (_heldAt.TryGetValue(player, out float at) && Time.time - at <= ClaimWindow));
        if (avatar == null || info.Sender != view.Owner || _rescued || !held)
        {
            Plugin.Log.LogWarning($"[Fable] Ignored a ribbon claim for view {player} (held {held}, used {_rescued}).");
            return;
        }
        Rescue(avatar);
    }

    // Host: carry the player to the truck exactly as the game returns a player who fell out of the level.
    private void Rescue(PlayerAvatar avatar)
    {
        if (_rescued) return;
        _rescued = _spent = true;
        var point = TruckSafetySpawnPoint.instance;
        var body = avatar.tumble != null ? avatar.tumble.physGrabObject : null;
        if (point != null && body != null)
        {
            var position = point.transform.position;
            avatar.OutOfBoundsCameraTransition(position);
            avatar.tumble.TumbleSet(true, false, true);
            body.Teleport(position, point.transform.rotation);
            if (body.rb != null) { body.rb.linearVelocity = Vector3.zero; body.rb.angularVelocity = Vector3.zero; }
            avatar.FallDamageResetSet(2f);
        }
        if (GameManager.Multiplayer()) _view.RPC(nameof(IHM_RibbonSaved), RpcTarget.All, Key(avatar));
        else Saved(avatar);
        Plugin.Log.LogInfo($"[Fable] The red ribbon saved {avatar.playerName}{(point == null ? " (no truck point: not moved)" : "")}.");
        // Leave the rescue message a moment to reach everyone, then the ribbon is gone.
        Invoke(nameof(Vanish), VanishSeconds);
    }

    [PunRPC]
    private void IHM_RibbonSaved(int player, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient) return;
        _spent = true;
        var view = PhotonView.Find(player);
        var avatar = view != null ? view.GetComponent<PlayerAvatar>() : null;
        if (avatar != null) Saved(avatar);
    }

    private static void Saved(PlayerAvatar avatar)
    {
        Protect(avatar);
        if (avatar.isLocal)
            HudNotify.Toast(Localization.GameLanguage.Russian ? "Красная ленточка спасла вас и вернула к машине" : "The red ribbon saved you and took you back to the truck",
                HudStyle.Good, 5f, HudKey + ".saved");
    }

    // A short grace while the player is moved: the game's own invulnerability timer against damage on the
    // player's client, and Grace against a second instant death (the pit can fire again before the teleport lands).
    private static void Protect(PlayerAvatar avatar)
    {
        Grace[Key(avatar)] = Time.time + GraceSeconds;
        if (avatar.isLocal && avatar.playerHealth != null) avatar.playerHealth.InvincibleSet(GraceSeconds);
    }

    private void Vanish()
    {
        if (_grab != null && SemiFunc.IsMasterClientOrSingleplayer()) _grab.DestroyPhysGrabObject();
    }

    private static bool Playing => GameDirector.instance != null && GameDirector.instance.currentState == GameDirector.gameState.Main;

    // Runs after every other damage change (shields, monster scaling, perks) and just before Death Protection,
    // so it sees the final blow; once it leaves 1 HP, Death Protection does not spend a charge.
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static class StopLethalHit
    {
        [HarmonyPrefix, HarmonyPriority(Priority.Last - 75)]
        private static void Prefix(PlayerHealth __instance, ref int damage, bool savingGrace)
        {
            if (Live.Count == 0) return;
            var avatar = __instance.playerAvatar;
            if (damage <= 0 || __instance.health <= 0 || avatar == null || avatar.deadSet || __instance.godMode ||
                __instance.invincibleTimer > 0f || !Playing ||
                (GameManager.Multiplayer() && (__instance.photonView == null || !__instance.photonView.IsMine))) return;
            // The game's own small-hit saving grace already keeps the player alive in this band.
            if (savingGrace && damage <= 25 && __instance.health > 5 && __instance.health <= 20) return;
            if (__instance.health - damage > 0) return;
            var ribbon = Protecting(avatar);
            if (ribbon == null) return;
            damage = __instance.health - 1;
            ribbon.Claim(avatar);
        }
    }

    // Death pits and instant kills skip damage and call PlayerDeath directly (from the host or the player's client).
    // Skipping it skips the death RPC too, so no death is counted anywhere.
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeath))]
    private static class StopInstantDeath
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Prefix(PlayerAvatar __instance)
        {
            if (__instance == null || __instance.deadSet || !Playing) return true;
            if (Grace.TryGetValue(Key(__instance), out float until) && Time.time < until) return false;
            var ribbon = Live.Count > 0 ? Protecting(__instance) : null;
            if (ribbon == null) return true;
            ribbon.Claim(__instance);
            return false;
        }
    }
}
