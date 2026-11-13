using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IncreaseHardMod.Gear;

/// <summary>Host-confirmed glue hold. Movement is locked; attacks and gravity remain available.</summary>
internal sealed class StickyRoot : MonoBehaviour
{
    internal const byte EventCode = 157;
    private PlayerAvatar _player;
    private Enemy _enemy;
    private float _until, _began;
    private Scene _scene;
    private Vector3 _anchor;
    private Visuals.GlueBlob _blob;

    // Monsters stay glued a little longer than players: a friendly-fire hit should not cost a teammate more than 2 s.
    internal const float PlayerSeconds = 2f, MonsterSeconds = 3f;

    internal static void Send(PlayerAvatar player, Enemy enemy)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        var view = player != null ? player.photonView : enemy?.GetComponentInParent<EnemyParent>()?.GetComponent<PhotonView>();
        float seconds = player != null ? PlayerSeconds : MonsterSeconds;
        Apply(player, enemy, seconds);
        if (view == null || view.ViewID <= 0 || !PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(EventCode, new object[] { view.ViewID, seconds, PhotonNetwork.Time, Networking.ModNetwork.FreezeLevelKey() },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    internal static void Receive(EventData message)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null || message.Sender != PhotonNetwork.MasterClient.ActorNumber ||
            !PlayerFreezeEffect.ReadSnapshot(message.CustomData, PhotonNetwork.Time, Networking.ModNetwork.FreezeLevelKey(), out int id, out float seconds) ||
            seconds > MonsterSeconds) return;
        var view = PhotonView.Find(id);
        if (view != null) Apply(view.GetComponent<PlayerAvatar>(), view.GetComponentInChildren<Enemy>(true), seconds);
    }

    private static void Apply(PlayerAvatar player, Enemy enemy, float seconds)
    {
        if (player == null && enemy == null) return;
        if (player != null && (player.deadSet || player.isDisabled)) return;
        var target = player != null ? player.gameObject : enemy.gameObject;
        var effect = target.GetComponent<StickyRoot>() ?? target.AddComponent<StickyRoot>();
        effect._player = player; effect._enemy = enemy;
        effect._anchor = enemy != null && enemy.HasRigidbody ? enemy.Rigidbody.rb.position : target.transform.position;
        if (enemy != null && Physics.Raycast(effect._anchor + Vector3.up * .1f, Vector3.down, out var floor, 8f,
                SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
            effect._anchor.y = Mathf.Min(effect._anchor.y, floor.point.y + .7f);
        effect._began = Time.time;
        if (effect._blob != null) effect._blob.Refresh();
        effect._until = Time.time + seconds;
        effect._scene = SceneManager.GetActiveScene();
        effect.enabled = true;
    }

    private void FixedUpdate()
    {
        if (Time.time >= _until || _scene != SceneManager.GetActiveScene() ||
            (_player != null && (_player.deadSet || _player.isDisabled)) ||
            (_enemy != null && (!_enemy.isActiveAndEnabled || (_enemy.HasHealth && _enemy.Health.dead))))
        { enabled = false; return; }
        var controller = PlayerController.instance;
        if (_player != null && controller != null && controller.playerAvatarScript == _player)
        {
            controller.OverrideSpeed(0f, .12f);
            controller.OverrideJumpCooldown(.2f);
            controller.JumpImpulse = false;
            controller.JumpInputBuffer = 0f;
            controller.VelocityImpulse = Vector3.zero;
            Hold(controller.rb);
        }
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (_player != null && _player.isTumbling && _player.tumble != null) Hold(_player.tumble.GetComponent<Rigidbody>());
        if (_enemy == null) return;
        if (_enemy.HasNavMeshAgent) { _enemy.NavMeshAgent.Stop(.2f); _enemy.NavMeshAgent.ResetPath(); }
        if (_enemy.HasRigidbody)
        {
            Hold(_enemy.Rigidbody.rb);
            _enemy.Rigidbody.physGrabObject?.OverrideGrabDisable(.12f);
        }
    }

    private void Hold(Rigidbody body)
    {
        if (body == null || body.isKinematic) return;
        var delta = _anchor - body.position;
        float down = _enemy != null ? Mathf.Clamp(delta.y * 8f, -5f, 0f) : 0f;
        body.linearVelocity = new Vector3(Mathf.Clamp(delta.x * 8f, -3f, 3f), Mathf.Min(body.linearVelocity.y, down), Mathf.Clamp(delta.z * 8f, -3f, 3f));
    }

    // The look (every client): the gel puddle, strands and, on monsters, the gel coat (Visuals.GlueBlob).
    private void LateUpdate()
    {
        if (_blob != null) return;
        try
        {
            var renderers = new List<Renderer>();
            Bounds body;
            if (_enemy != null)
            {
                var mesh = _enemy.HasHealth && _enemy.Health != null ? _enemy.Health.meshParent : null;
                foreach (var r in (mesh != null ? mesh : _enemy.transform).GetComponentsInChildren<Renderer>())
                    if (r is MeshRenderer or SkinnedMeshRenderer) renderers.Add(r);
                body = renderers.Count > 0 ? renderers[0].bounds : new Bounds(transform.position + Vector3.up * .6f, Vector3.one);
                foreach (var r in renderers) body.Encapsulate(r.bounds);
            }
            else body = new Bounds(transform.position + Vector3.up * .9f, new Vector3(.55f, 1.8f, .55f));
            var view = GetComponentInParent<PhotonView>();
            _blob = Visuals.GlueBlob.Create(transform, body, view != null ? view.ViewID : GetInstanceID(), renderers, _enemy != null);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[Glue] Could not draw the glue: {e.Message}");
            _blob = null;
            enabled = false;
        }
    }

    private void OnDisable() { if (_blob != null) { _blob.Release(); _blob = null; } }
    private void OnDestroy() => OnDisable();
}
