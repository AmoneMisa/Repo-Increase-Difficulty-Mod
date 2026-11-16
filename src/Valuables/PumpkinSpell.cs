using System.Collections.Generic;
using IncreaseHardMod.Visuals;
using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Valuables;

/// <summary>
/// A secret of Fable Realm, told nowhere in the game: a pumpkin left on the floor with two mice beside it turns
/// into the royal coach. The host decides (it watches the pumpkin, removes the three and spawns the coach);
/// everyone else only shows the spell, which the host starts with one message.
/// </summary>
internal sealed class PumpkinSpell : MonoBehaviour
{
    internal const int MiceNeeded = 2;
    // The mice must lie within this reach of the pumpkin, all three still, for a moment before the spell starts.
    private const float Reach = 1f, RestSpeedSquared = .3f, SettleSeconds = 1.2f, CastSeconds = 2.1f, ScanSeconds = .3f;
    private const int Sparks = 14;
    private PhotonView _view;
    private PhysGrabObject _grab;
    private ValuableObject _valuable;
    private PhysGrabObjectImpactDetector _impact;
    private Transform _visual;
    private Vector3 _visualScale = Vector3.one;
    private readonly List<PhysGrabObject> _mice = new();
    private readonly Transform[] _sparks = new Transform[Sparks];
    private Light _glow;
    private float _scan, _settled, _castStart = -1f;
    private static Material _sparkMaterial;
    private static Mesh _sparkMesh;

    private void Start()
    {
        _view = GetComponent<PhotonView>();
        _grab = GetComponent<PhysGrabObject>();
        _valuable = GetComponent<ValuableObject>();
        _impact = GetComponent<PhysGrabObjectImpactDetector>();
        var art = GetComponent<ValuableArtInstance>();
        _visual = art != null && art.EnsureBindings() ? art.visualRoot : null;
        if (_visual != null) _visualScale = _visual.localScale;
    }

    private bool Casting => _castStart >= 0f;

    private void FixedUpdate()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _grab == null || _grab.dead) return;
        if (Casting)
        {
            if (Time.time - _castStart >= CastSeconds) Finish();
            return;
        }
        if ((_scan -= Time.fixedDeltaTime) > 0f) return;
        _scan = ScanSeconds;
        _settled = Ready() ? _settled + ScanSeconds : 0f;
        if (_settled < SettleSeconds) return;
        _settled = 0f;
        if (GameManager.Multiplayer()) _view.RPC(nameof(IHM_PumpkinSpell), RpcTarget.All);
        else Begin();
    }

    // The pumpkin stands still on the floor (not in hands, a cart or an extraction point) with two still mice beside it.
    private bool Ready()
    {
        _mice.Clear();
        if (!Still(_grab) || (_impact != null && _impact.inCart) ||
            (_valuable != null && _valuable.roomVolumeCheck != null && _valuable.roomVolumeCheck.inExtractionPoint)) return false;
        foreach (var collider in Physics.OverlapSphere(_grab.centerPoint, Reach, SemiFunc.LayerMaskGetPhysGrabObject(), QueryTriggerInteraction.Ignore))
        {
            var other = collider.GetComponentInParent<PhysGrabObject>();
            if (other == null || other == _grab || _mice.Contains(other) || !Still(other) ||
                !other.TryGetComponent<ValuableArtInstance>(out var art) || art.assetKey != FableLoot.Mouse) continue;
            _mice.Add(other);
            if (_mice.Count == MiceNeeded) return true;
        }
        return false;
    }

    private static bool Still(PhysGrabObject body) =>
        body != null && !body.dead && !body.grabbed && body.playerGrabbing.Count == 0 && body.rb != null &&
        body.rb.linearVelocity.sqrMagnitude < RestSpeedSquared;

    [PunRPC]
    private void IHM_PumpkinSpell(PhotonMessageInfo info = default)
    {
        if (SemiFunc.MasterOnlyRPC(info)) Begin();
    }

    // The spell as everyone sees and hears it: sparks spiral up around the pumpkin while it swells and glows.
    private void Begin()
    {
        if (Casting) return;
        _castStart = Time.time;
        ItemAudio.Cue("valuable_spell", .34f).Play(transform.position);
        _sparkMaterial ??= GlowTextures.Sprite(GlowTextures.Star, "IHM Pumpkin Spell Spark", 3110);
        _sparkMesh ??= ProcMesh.UnitQuad("IHM Pumpkin Spell Spark");
        for (int i = 0; i < Sparks; i++)
        {
            var spark = new GameObject("IHM Pumpkin Spell Spark");
            spark.transform.SetParent(transform, false);
            spark.AddComponent<Decoration>();
            spark.AddComponent<MeshFilter>().sharedMesh = _sparkMesh;
            var renderer = spark.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _sparkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sparks[i] = spark.transform;
        }
        var glow = new GameObject("IHM Pumpkin Spell Glow");
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = Vector3.up * .3f;
        _glow = glow.AddComponent<Light>();
        _glow.type = LightType.Point; _glow.color = new Color(.75f, .85f, 1f); _glow.range = 4f; _glow.intensity = 0f;
    }

    private void LateUpdate()
    {
        if (!Casting) return;
        float t = Mathf.Clamp01((Time.time - _castStart) / CastSeconds);
        if (_visual != null) _visual.localScale = _visualScale * (1f + .22f * t * t + .04f * Mathf.Sin(t * 40f) * t);
        if (_glow != null) _glow.intensity = 3.5f * t * t;
        var camera = Camera.main;
        for (int i = 0; i < Sparks; i++)
        {
            var spark = _sparks[i];
            if (spark == null) continue;
            // Each spark climbs its own turn of a widening spiral and twinkles.
            float phase = (t * 1.6f + i / (float)Sparks) % 1f, angle = i * 2.4f + t * 9f;
            float radius = .32f + .25f * phase;
            spark.position = transform.position + new Vector3(Mathf.Cos(angle) * radius, .05f + 1.1f * phase, Mathf.Sin(angle) * radius);
            spark.localScale = Vector3.one * (.11f * Mathf.Sin(phase * Mathf.PI) * (.6f + .4f * Mathf.Sin(t * 30f + i)));
            if (camera != null) spark.rotation = camera.transform.rotation;
        }
    }

    // Host: the pumpkin and its mice are gone and the coach stands where the pumpkin was.
    private void Finish()
    {
        _castStart = -1f;
        var prefab = InteractiveValuables.RitualPrefab(FableLoot.Coach);
        _mice.RemoveAll(mouse => mouse == null || mouse.dead);
        if (prefab == null || prefab.Prefab == null || _mice.Count < MiceNeeded)
        {
            Fizzle();
            return;
        }
        var rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        var floor = new Vector3(_grab.centerPoint.x, transform.position.y, _grab.centerPoint.z);
        var position = Challenges.MapRitual.OfferingPosition(floor, rotation, prefab.Prefab.GetComponent<PhysGrabObject>(), .12f);
        foreach (var mouse in _mice) mouse.DestroyPhysGrabObject();
        _mice.Clear();
        if (REPOLib.Modules.Valuables.SpawnValuable(prefab, position, rotation) == null)
            Plugin.Log.LogError("[Fable] The royal coach could not be spawned.");
        _grab.DestroyPhysGrabObject();
    }

    // The spell broke (a mouse was taken or lost): the pumpkin is a pumpkin again, for everyone.
    private void Fizzle()
    {
        if (GameManager.Multiplayer()) _view.RPC(nameof(IHM_PumpkinFizzle), RpcTarget.All);
        else End();
    }

    [PunRPC]
    private void IHM_PumpkinFizzle(PhotonMessageInfo info = default)
    {
        if (SemiFunc.MasterOnlyRPC(info)) End();
    }

    private void End()
    {
        _castStart = -1f;
        if (_visual != null) _visual.localScale = _visualScale;
        if (_glow != null) Destroy(_glow.gameObject);
        foreach (var spark in _sparks) if (spark != null) Destroy(spark.gameObject);
    }
}
