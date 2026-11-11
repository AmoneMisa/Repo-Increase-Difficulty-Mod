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
    private GameObject _glue;
    private static Material _material;

    internal static void Send(PlayerAvatar player, Enemy enemy)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        var view = player != null ? player.photonView : enemy?.GetComponentInParent<EnemyParent>()?.GetComponent<PhotonView>();
        Apply(player, enemy, 2f);
        if (view == null || view.ViewID <= 0 || !PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(EventCode, new object[] { view.ViewID, 2f, PhotonNetwork.Time, Networking.ModNetwork.FreezeLevelKey() },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    internal static void Receive(EventData message)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null || message.Sender != PhotonNetwork.MasterClient.ActorNumber ||
            !PlayerFreezeEffect.ReadSnapshot(message.CustomData, PhotonNetwork.Time, Networking.ModNetwork.FreezeLevelKey(), out int id, out float seconds) ||
            seconds > 2f) return;
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
        body.velocity = new Vector3(Mathf.Clamp(delta.x * 8f, -3f, 3f), Mathf.Min(body.velocity.y, down), Mathf.Clamp(delta.z * 8f, -3f, 3f));
    }

    private void LateUpdate()
    {
        if (_glue == null)
        {
            _glue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _glue.name = "Sticky glue puddle";
            var collider = _glue.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            if (_material == null)
            {
                _material = new Material(Shader.Find("Standard"));
                _material.color = new Color(.9f, .35f, .65f);
                _material.SetFloat("_Glossiness", .85f);
            }
            _glue.GetComponent<Renderer>().sharedMaterial = _material;
        }
        Vector3 pos = transform.position;
        if (Physics.Raycast(pos + Vector3.up * .5f, Vector3.down, out var hit, 5f, SemiFunc.LayerMaskGetVisionObstruct(), QueryTriggerInteraction.Ignore))
            pos = hit.point + Vector3.up * .025f;
        _glue.transform.position = pos;
        float grow = Mathf.Clamp01((Time.time - _began) / .18f);
        float fade = Mathf.Clamp01((_until - Time.time) / .25f);
        _glue.transform.localScale = new Vector3(1.15f, .11f, 1.15f) * grow * fade;
    }

    private void OnDisable() { if (_glue != null) Destroy(_glue); }
    private void OnDestroy() => OnDisable();
}
