using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// Little Red Riding Hood's basket: carried upright it keeps its five pies; tipped on its side or upside down
/// they fall out one by one, and each lost pie takes part of the basket's value with it.
/// The host decides (tilt, loss, price) and tells the others how many pies are left; everyone drops the pie locally.
/// </summary>
internal sealed class PieBasket : MonoBehaviour
{
    // Tipped past roughly 78 degrees for this long loses the next pie.
    private const float TippedUpDot = .2f, TipSeconds = .3f, ResendSeconds = 4f;
    private PhotonView _view;
    private ValuableObject _valuable;
    private PhysGrabObjectImpactDetector _impact;
    private PhysGrabObject _grab;
    private readonly Transform[] _pies = new Transform[FableLoot.Pies];
    private int _left = FableLoot.Pies;
    private float _tipped, _resend;

    private void Start()
    {
        _view = GetComponent<PhotonView>();
        _valuable = GetComponent<ValuableObject>();
        _impact = GetComponent<PhysGrabObjectImpactDetector>();
        _grab = GetComponent<PhysGrabObject>();
        var art = GetComponent<ValuableArtInstance>();
        if (art == null || !art.EnsureBindings() || art.bindings == null) return;
        foreach (var binding in art.bindings)
        {
            if (binding?.node == null || binding.group == null || !binding.group.StartsWith(FableLoot.PieGroup, System.StringComparison.Ordinal)) continue;
            if (int.TryParse(binding.group.Substring(FableLoot.PieGroup.Length), out int index) && index >= 0 && index < _pies.Length)
                _pies[index] = binding.node;
        }
    }

    private void FixedUpdate()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _grab == null || _grab.dead) return;
        if (_left > 0)
        {
            _tipped = Vector3.Dot(transform.up, Vector3.up) < TippedUpDot ? _tipped + Time.fixedDeltaTime : 0f;
            if (_tipped > TipSeconds) { _tipped = 0f; Lose(); }
        }
        // A player who joins later still has a full basket on screen: repeat the count now and then.
        if (_left < FableLoot.Pies && GameManager.Multiplayer() && (_resend -= Time.fixedDeltaTime) <= 0f)
        {
            _resend = ResendSeconds;
            _view.RPC(nameof(IHM_PieCount), RpcTarget.Others, _left);
        }
    }

    private void Lose()
    {
        Show(_left - 1);
        // The game's own break call updates the price, the HUD and every player; the basket itself always keeps some value.
        if (_valuable != null && _impact != null && _valuable.dollarValueSet)
        {
            float debit = Mathf.Min(Random.Range(500, 801), _valuable.dollarValueCurrent - 600f);
            if (debit >= 100f) _impact.Break(debit, transform.position, _impact.breakLevelLight, true);
        }
        _resend = ResendSeconds;
        if (GameManager.Multiplayer()) _view.RPC(nameof(IHM_PieCount), RpcTarget.Others, _left);
    }

    [PunRPC]
    private void IHM_PieCount(int left, PhotonMessageInfo info)
    {
        // Only the host's word counts, and pies never come back.
        if (info.Sender != PhotonNetwork.MasterClient || left < 0 || left >= _left) return;
        Show(left);
    }

    private void Show(int left)
    {
        for (int i = _left - 1; i >= left && i >= 0; i--)
        {
            var pie = _pies[i];
            if (pie == null || !pie.gameObject.activeSelf) continue;
            Drop(pie);
            pie.gameObject.SetActive(false);
        }
        _left = Mathf.Max(0, left);
    }

    // The pie that falls out is a short-lived local prop: it tumbles away and is gone a few seconds later.
    private void Drop(Transform pie)
    {
        var filter = pie.GetComponent<MeshFilter>();
        var renderer = pie.GetComponent<MeshRenderer>();
        if (filter == null || renderer == null || filter.sharedMesh == null) return;
        var centre = filter.sharedMesh.bounds.center;
        var fallen = new GameObject("IHM Fallen Pie");
        fallen.transform.SetPositionAndRotation(pie.TransformPoint(centre), pie.rotation);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(fallen.transform, false);
        visual.transform.localPosition = -centre;
        visual.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        visual.AddComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
        fallen.AddComponent<SphereCollider>().radius = .045f;
        var body = fallen.AddComponent<Rigidbody>();
        body.mass = .05f;
        var parent = GetComponent<Rigidbody>();
        body.linearVelocity = (parent != null ? parent.GetPointVelocity(fallen.transform.position) : Vector3.zero) - transform.up * .6f + Random.insideUnitSphere * .3f;
        body.angularVelocity = Random.insideUnitSphere * 6f;
        // Never pushes the basket it fell from.
        foreach (var own in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(fallen.GetComponent<Collider>(), own, true);
        Destroy(fallen, 6f);
    }
}
