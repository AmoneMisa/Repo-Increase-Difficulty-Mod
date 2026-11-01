using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using IncreaseHardMod.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IncreaseHardMod.Gear;

/// <summary>Host-confirmed player freeze; movement pauses on its owner, visuals appear on every client.</summary>
internal sealed class PlayerFreezeEffect : MonoBehaviour
{
    internal static readonly List<PlayerFreezeEffect> Active = new();
    internal PlayerAvatar Avatar;
    private float _until;
    private int _scene;
    private readonly FrostCoating _frost = new();
    private bool _coated;
    private NativeHudPanel _note;
    private TextMeshProUGUI _label;

    internal float Remaining => Avatar != null && Avatar.isActiveAndEnabled && !Avatar.isDisabled && !Avatar.deadSet &&
        _scene == SceneManager.GetActiveScene().handle ? Mathf.Max(0f, _until - Time.time) : 0f;

    internal static void Apply(PlayerAvatar avatar, float seconds)
    {
        if (avatar == null || !avatar.isActiveAndEnabled || avatar.isDisabled || avatar.deadSet ||
            float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return;
        var effect = avatar.GetComponent<PlayerFreezeEffect>();
        if (effect == null) effect = avatar.gameObject.AddComponent<PlayerFreezeEffect>();
        effect.Avatar = avatar;
        int scene = SceneManager.GetActiveScene().handle;
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
        }
        _frost.Tick();
        var controller = PlayerController.instance;
        if (controller == null || controller.playerAvatarScript != Avatar) return;
        if (_note == null)
        {
            _note = NativeHudPanel.Create("IHM_PlayerFrozen", new Vector2(310f, 58f),
                new Vector2(.5f, .75f), Vector2.zero, .65f);
            if (_note == null) return;
            _label = _note.Label("Frozen", 26f, TextAlignmentOptions.Center);
            _label.color = new Color(.45f, .8f, 1f);
            _label.rectTransform.anchorMin = Vector2.zero;
            _label.rectTransform.anchorMax = Vector2.one;
            _label.rectTransform.offsetMin = new Vector2(8f, 4f);
            _label.rectTransform.offsetMax = new Vector2(-8f, -4f);
        }
        _label.text = $"FROZEN  {remaining:0.0}s";
        _note.Fit();
    }

    private void OnDisable()
    {
        Active.Remove(this);
        _until = 0f;
        _frost.Clear();
        _coated = false;
        if (_note != null) Destroy(_note.gameObject);
        _note = null;
    }

    private void OnDestroy() => OnDisable();
}
