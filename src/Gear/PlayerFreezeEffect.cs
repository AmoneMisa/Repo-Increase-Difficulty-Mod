using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using IncreaseHardMod.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IncreaseHardMod.Gear;

/// <summary>Host-confirmed player freeze; movement pauses on its owner, visuals appear on every client:
/// frost on the body and, for everyone except the frozen player themselves, the same refracting ice block
/// monsters get (seeded from the player's network ID, so all clients see the same block).</summary>
internal sealed class PlayerFreezeEffect : MonoBehaviour
{
    internal static readonly List<PlayerFreezeEffect> Active = new();
    internal PlayerAvatar Avatar;
    private bool IsLocalPlayer() => PlayerController.instance != null && PlayerController.instance.playerAvatarScript == Avatar;
    private float _until;
    private Scene _scene;
    private readonly FrostCoating _frost = new();
    private bool _coated;
    private IceBlock _block;

    internal float Remaining => Avatar != null && Avatar.isActiveAndEnabled && !Avatar.isDisabled && !Avatar.deadSet &&
        _scene == SceneManager.GetActiveScene() ? Mathf.Max(0f, _until - Time.time) : 0f;

    internal static void Apply(PlayerAvatar avatar, float seconds)
    {
        if (avatar == null || !avatar.isActiveAndEnabled || avatar.isDisabled || avatar.deadSet ||
            float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return;
        var effect = avatar.GetComponent<PlayerFreezeEffect>();
        if (effect == null) effect = avatar.gameObject.AddComponent<PlayerFreezeEffect>();
        effect.Avatar = avatar;
        // Scene.handle changed from int to SceneHandle in Unity 6. Compare scenes
        // through the stable equality operator instead of binding to the old getter.
        Scene scene = SceneManager.GetActiveScene();
        if (effect._scene != scene) effect._until = 0f;
        effect._scene = scene;
        effect._until = Mathf.Max(effect._until, Time.time + Mathf.Min(seconds, 30f));
        effect.enabled = true;
    }

    internal static bool ReadSnapshot(object payload, double now, string levelKey, out int viewId, out float remaining)
    {
        viewId = 0; remaining = 0f;
        if (payload is not object[] packet || packet.Length != 4 ||
            packet[0] is not int id || id <= 0 || packet[1] is not float seconds ||
            packet[2] is not double sent || packet[3] is not string level || level != levelKey ||
            float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f || seconds > 30f ||
            double.IsNaN(sent) || double.IsInfinity(sent) || double.IsNaN(now) || double.IsInfinity(now)) return false;
        double age = System.Math.Max(0d, now - sent);
        if (age >= seconds) return false;
        viewId = id;
        remaining = seconds - (float)age;
        return true;
    }

    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }

    private void FixedUpdate()
    {
        if (Remaining <= 0f) { enabled = false; return; }
        // Native timed overrides expire on their own; no persistent constraints or movement settings.
        var controller = PlayerController.instance;
        if (controller != null && controller.playerAvatarScript == Avatar)
        {
            controller.InputDisable(.12f);
            controller.JumpImpulse = false;
            controller.JumpInputBuffer = 0f;
            controller.VelocityImpulse = Vector3.zero;
            if (!Avatar.isTumbling) controller.Kinematic(.12f);
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
        if (!_coated && Avatar.playerAvatarVisuals != null)
        {
            _frost.Apply(Avatar.playerAvatarVisuals.GetComponentsInChildren<Renderer>(true));
            _coated = true;
            // The block would surround the frozen player's own camera and fill their screen: they get the
            // frost and the HUD note, everyone else sees them encased.
            bool own = PlayerController.instance != null && PlayerController.instance.playerAvatarScript == Avatar;
            if (ModConfig.IceBlockEnabled.Value && _block == null && !own)
            {
                var body = BodyRenderers(out var bounds);
                var view = Avatar.photonView;
                _block = IceBlock.Create(Avatar.playerAvatarVisuals.transform, bounds, view != null ? view.ViewID : GetInstanceID(), body);
            }
        }
        _frost.Tick();
        var controller = PlayerController.instance;
        if (controller == null || controller.playerAvatarScript != Avatar) return;
        // HUD toolkit status line, refreshed every frame while it lasts.
        Hud.HudConditions.Refresh(Hud.PlayerCondition.Frozen, remaining);
    }

    private List<Renderer> BodyRenderers(out Bounds bounds) => PlayerBody.Renderers(Avatar, out bounds);

    private void OnDisable()
    {
        Active.Remove(this);
        _until = 0f;
        _frost.Clear();
        _coated = false;
        if (_block != null) { _block.Thaw(); _block = null; }
        if (IsLocalPlayer()) Hud.HudConditions.End(Hud.PlayerCondition.Frozen);
    }

    private void OnDestroy() => OnDisable();
}
