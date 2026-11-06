using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using IncreaseHardMod.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IncreaseHardMod.Gear;

/// <summary>
/// A player caught by the Net Launcher. Host-confirmed (the host decides the hit, the duration and the
/// break-free rolls); on the netted player's own client walking and jumping stop but looking, shooting and
/// using items still work; every client draws the rope net over the player (hidden from their own
/// first-person view, shadow kept).
/// </summary>
internal sealed class PlayerNetEffect : MonoBehaviour
{
    internal static readonly List<PlayerNetEffect> Active = new();
    internal PlayerAvatar Avatar;
    private float _until;
    private Scene _scene;
    private NetDrape _net;

    internal float Remaining => Avatar != null && Avatar.isActiveAndEnabled && !Avatar.isDisabled && !Avatar.deadSet &&
        _scene == SceneManager.GetActiveScene() ? Mathf.Max(0f, _until - Time.time) : 0f;

    private bool IsLocal => PlayerController.instance != null && PlayerController.instance.playerAvatarScript == Avatar;

    internal static void Apply(PlayerAvatar avatar, float seconds)
    {
        if (avatar == null || !avatar.isActiveAndEnabled || avatar.isDisabled || avatar.deadSet ||
            float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return;
        var effect = avatar.GetComponent<PlayerNetEffect>() ?? avatar.gameObject.AddComponent<PlayerNetEffect>();
        effect.Avatar = avatar;
        Scene scene = SceneManager.GetActiveScene();
        if (effect._scene != scene) effect._until = 0f;
        effect._scene = scene;
        effect._until = Mathf.Max(effect._until, Time.time + Mathf.Min(seconds, 30f));
        effect.enabled = true;
    }

    /// <summary>The net ended early: torn = burst apart, otherwise sag away.</summary>
    internal static void End(PlayerAvatar avatar, bool torn)
    {
        var effect = avatar != null ? avatar.GetComponent<PlayerNetEffect>() : null;
        if (effect == null) return;
        effect._until = 0f;
        if (effect._net != null) { effect._net.Release(torn); effect._net = null; }
        effect.enabled = false;
    }

    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }

    private void FixedUpdate()
    {
        if (Remaining <= 0f) { enabled = false; return; }
        var controller = PlayerController.instance;
        if (controller != null && controller.playerAvatarScript == Avatar)
        {
            // Rooted: no walking or jumping. Looking, shooting and items are untouched.
            controller.OverrideSpeed(0f, .12f);
            controller.OverrideJumpCooldown(.2f);
            controller.JumpImpulse = false;
            controller.JumpInputBuffer = 0f;
            controller.VelocityImpulse = Vector3.zero;
            if (controller.rb != null && !controller.rb.isKinematic)
            {
                var v = controller.rb.velocity;
                controller.rb.velocity = new Vector3(0f, Mathf.Min(v.y, 0f), 0f);
            }
        }
        // Tumble bodies are simulated by the host, including remote players.
        if (SemiFunc.IsMasterClientOrSingleplayer() && Avatar.isTumbling && Avatar.tumble != null &&
            Avatar.tumble.physGrabObject != null)
            Avatar.tumble.physGrabObject.FreezeForces(.12f, Vector3.zero, Vector3.zero);
    }

    private void LateUpdate()
    {
        float remaining = Remaining;
        if (remaining <= 0f) { enabled = false; return; }
        if (_net == null && Avatar.playerAvatarVisuals != null)
        {
            var bounds = VisualBounds();
            _net = NetDrape.Create(Avatar.playerAvatarVisuals.transform, bounds, localOwner: IsLocal);
            Plugin.Log.LogInfo($"[Net] Net on player {Avatar.playerName}: body {bounds.center} size {bounds.size}, " +
                $"{(bounds.center - Avatar.transform.position).magnitude:0.00} m from the avatar, local={IsLocal}.");
        }
        if (!IsLocal) return;
        // HUD toolkit status line, refreshed every frame while it lasts.
        Hud.HudConditions.Refresh(Hud.PlayerCondition.Netted, remaining);
    }

    private Bounds VisualBounds()
    {
        PlayerBody.Renderers(Avatar, out var bounds);
        return bounds;
    }

    private void OnDisable()
    {
        Active.Remove(this);
        if (_net != null) { _net.Release(false); _net = null; }
        if (IsLocal) Hud.HudConditions.End(Hud.PlayerCondition.Netted);
    }

    private void OnDestroy() => OnDisable();
}
