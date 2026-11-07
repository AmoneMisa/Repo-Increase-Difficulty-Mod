using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// "Item Unstuck" — a small safety net for valuables, inspired by the community mod of the
/// same name. Three independently config-gated parts:
///   • Relocate stuck loot — valuables that settle embedded inside world geometry are teleported
///     back to their last safe resting spot (anti-softlock). HOST-SIDE.
///   • Invulnerable until first grab — a valuable takes no damage / loses no value until a player
///     first grabs it, via the game's own <c>OverrideIndestructible</c> flag. HOST-SIDE.
///   • Undamaged-value HUD — while you hold a valuable, its ORIGINAL value is shown on-screen.
///     CLIENT-SIDE / cosmetic.
///
/// This controller lives on the mod's runtime object. On the host it attaches a
/// <see cref="ValuableGuard"/> to each live valuable (which owns the per-object logic); on every
/// client it draws the local grab HUD. Relocation/invulnerability are routed through the host so
/// they stay authoritative and desync-free.
/// </summary>
internal sealed class ItemUnstuckController : MonoBehaviour
{
    private const float EnsureInterval = 1f;
    private float _ensureTimer;

    // Brief on-screen note when the host relocates a stuck valuable (HUD toolkit toast; one line, replaced
    // rather than stacked when several valuables are relocated in a row).
    internal static void NotifyRelocated(ValuableObject v)
    {
        if (!ModConfig.ItemUnstuckEnabled.Value) return;
        Hud.HudNotify.Toast($"Relocated stuck loot: {Clean(v != null ? v.name : "valuable")}", Hud.HudStyle.Info, 3f, "unstuck");
    }

    private void Update()
    {
        if (!ModConfig.ItemUnstuckEnabled.Value)
            return;
        if (!SafeRunIsLevel())
            return;

        // Only the host needs guards — invulnerability and relocation are host-authoritative.
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;
        if (!ModConfig.ValuableInvulnerableUntilGrab.Value && !ModConfig.ValuableRelocateStuck.Value)
            return;

        _ensureTimer -= Time.deltaTime;
        if (_ensureTimer > 0f)
            return;
        _ensureTimer = EnsureInterval;

        var dir = ValuableDirector.instance;
        if (dir == null || dir.valuableList == null)
            return;

        foreach (var v in dir.valuableList)
        {
            if (v == null)
                continue;
            if (v.GetComponent<ValuableGuard>() == null)
                v.gameObject.AddComponent<ValuableGuard>();
        }
    }


    internal static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "valuable";
        int clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
        if (clone >= 0)
            name = name.Substring(0, clone);
        return PingController.WithoutModTag(name.Replace("Valuable ", "").Trim());
    }

    private static bool SafeRunIsLevel()
    {
        try { return SemiFunc.RunIsLevel(); }
        catch { return false; }
    }
}

/// <summary>
/// Host-side per-valuable logic, attached by <see cref="ItemUnstuckController"/>. Keeps a valuable
/// invulnerable until its first grab and relocates it if it settles embedded in geometry. Auto-
/// cleans with the valuable (it's a component on the same GameObject). All logic is gated to the
/// host, so clients never run these physics checks.
/// </summary>
internal sealed class ValuableGuard : MonoBehaviour
{
    private const float CheckInterval = 0.5f;
    private const float RestSpeed = 0.35f;     // below this the object is "settled"
    // How deep the item's OWN collider must be buried inside static geometry before we call it
    // embedded. Resting on/in furniture (a shelf, a drawer, a cabinet nook) only makes shallow
    // contact — a few mm of penetration at most. Being genuinely stuck inside a wall/floor buries
    // the collider much deeper. This depth threshold is what tells the two apart.
    private const float EmbedDepth = 0.12f;

    private ValuableObject _v;
    private PhysGrabObject _pgo;
    private Rigidbody _rb;

    private bool _grabbedOnce;
    private bool _relocated;   // relocate each valuable at most once per level
    private float _checkTimer;
    private float _stuckTimer;

    private Vector3 _safePos;
    private Quaternion _safeRot;
    private bool _haveSafe;
    private readonly List<Vector3> _contactNormals = new();
    private float _contactTime = -10f;
    private Vector3 _pullAnchor;
    private Quaternion _pullRotation;
    private bool _pullPending;

    private void Awake()
    {
        _v = GetComponent<ValuableObject>();
        _pgo = GetComponent<PhysGrabObject>();
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (_v == null || _pgo == null || !ModConfig.ItemUnstuckEnabled.Value)
        {
            _stuckTimer = 0f;
            _pullPending = false;
            return;
        }
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        bool onLevel;
        try { onLevel = SemiFunc.RunIsLevel(); }
        catch { return; }
        if (!onLevel)
            return;

        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f)
            return;
        _checkTimer = CheckInterval;

        bool grabbed = _pgo.grabbed;
        if (grabbed)
            _grabbedOnce = true;

        // Keep it indestructible until first grab (the game auto-resets the flag once our
        // refreshed timer runs out after the first grab, so no explicit reset is needed).
        if (ModConfig.ValuableInvulnerableUntilGrab.Value && !_grabbedOnce)
            _pgo.OverrideIndestructible(CheckInterval + 0.25f);

        if (ModConfig.ValuableRelocateStuck.Value)
            StuckLogic(grabbed);
        else
        {
            _stuckTimer = 0f;
            _pullPending = false;
        }
    }

    private void StuckLogic(bool grabbed)
    {
        // Relocate each valuable AT MOST ONCE per level. If the recovery spot itself is embedded
        // (or the item re-settles into the same bad pocket), the stuck timer would rebuild and we'd
        // teleport it forever — the "vase respawning repeatedly" bug. One rescue attempt, then leave
        // it be; a player can still pick it up. Guards are per-object and rebuilt each level, so the
        // budget naturally resets on the next level.
        if (_relocated)
            return;

        // A held object needs a sustained failed pull plus opposing contacts or deep body penetration.
        bool inCart = _pgo.impactDetector != null && _pgo.impactDetector.inCart;
        if (_pgo.dead || inCart)
        {
            _stuckTimer = 0f;
            _pullPending = false;
            return;
        }

        if (grabbed)
        {
            CheckFailedPull();
            return;
        }
        if (_pullPending) _stuckTimer = 0f;
        _pullPending = false;

        bool resting = _rb == null || _rb.velocity.sqrMagnitude < RestSpeed * RestSpeed;
        bool embedded = IsEmbedded();

        if (embedded && resting)
        {
            _stuckTimer += CheckInterval;
            if (_stuckTimer >= Mathf.Max(2f, ModConfig.ValuableStuckSeconds.Value))
            {
                Relocate(Vector3.zero, Vector3.zero, false);
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
            // Remember the last free, settled spot so we can send a stuck one back to it.
            if (!embedded && resting && IsClear(transform.position, transform.rotation))
            {
                _safePos = _pgo.transform.position;
                _safeRot = _pgo.transform.rotation;
                _haveSafe = true;
            }
        }
    }

    private bool IsWorld(Collider other) => other != null && !other.isTrigger &&
        !other.transform.IsChildOf(transform) && other.attachedRigidbody == null &&
        other.GetComponentInParent<PhysGrabObject>() == null;

    private void OnCollisionStay(Collision collision)
    {
        if (_pgo == null || !ModConfig.ItemUnstuckEnabled.Value ||
            !ModConfig.ValuableRelocateStuck.Value || !SemiFunc.IsMasterClientOrSingleplayer() ||
            !IsWorld(collision.collider)) return;
        if (_contactTime != Time.fixedTime)
        {
            _contactNormals.Clear();
            _contactTime = Time.fixedTime;
        }
        for (int i = 0; i < collision.contactCount && _contactNormals.Count < 24; i++)
            _contactNormals.Add(collision.GetContact(i).normal);
    }

    private void CheckFailedPull()
    {
        Vector3 pull = Vector3.zero;
        Vector3 eye = Vector3.zero;
        if (_pgo.playerGrabbing != null)
            foreach (var grabber in _pgo.playerGrabbing)
            {
                if (grabber == null || grabber.grabbedPhysGrabObject != _pgo || grabber.physGrabPoint == null) continue;
                var stretch = grabber.physGrabPointPullerPosition - grabber.physGrabPoint.position;
                if (stretch.sqrMagnitude <= pull.sqrMagnitude) continue;
                pull = stretch;
                eye = grabber.playerAvatar != null && grabber.playerAvatar.localCamera != null
                    ? grabber.playerAvatar.localCamera.GetOverrideTransform().position
                    : grabber.transform.position;
            }
        bool strained = pull.sqrMagnitude >= 0.25f;
        bool trapped = strained && ((Time.fixedTime - _contactTime <= 0.75f &&
            UnstuckGeometry.Opposed(_contactNormals)) || IsEmbedded(false));
        if (!trapped)
        {
            _stuckTimer = 0f;
            _pullPending = false;
            return;
        }
        if (!_pullPending || Vector3.Distance(transform.position, _pullAnchor) > 0.05f ||
            Quaternion.Angle(transform.rotation, _pullRotation) > 5f)
        {
            _pullPending = true;
            _pullAnchor = transform.position;
            _pullRotation = transform.rotation;
            _stuckTimer = 0f;
            return;
        }
        _stuckTimer += CheckInterval;
        if (_stuckTimer < Mathf.Max(2f, ModConfig.ValuableStuckSeconds.Value)) return;
        Relocate(pull.normalized, eye, true);
        _stuckTimer = 0f;
    }

    private bool IsClear(Vector3 position, Quaternion rotation) =>
        UnstuckGeometry.IsClear(transform, GetComponentsInChildren<Collider>(), position, rotation,
            other => other != null && !other.isTrigger && !other.transform.IsChildOf(transform));

    private bool TryRecoveryPose(Vector3 pull, Vector3 eye, bool held, out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;
        if (!held && _haveSafe && Vector3.Distance(position, _safePos) <= 4f && IsClear(_safePos, _safeRot))
        {
            position = _safePos;
            rotation = _safeRot;
            return true;
        }
        var centreOffset = _pgo.centerPoint - transform.position;
        var directions = held
            ? new[] { pull, (pull + Vector3.up * 0.35f).normalized,
                (pull + Vector3.Cross(Vector3.up, pull) * 0.4f).normalized,
                (pull - Vector3.Cross(Vector3.up, pull) * 0.4f).normalized }
            : new[] { Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
                (Vector3.up + Vector3.right).normalized, (Vector3.up + Vector3.left).normalized,
                (Vector3.up + Vector3.forward).normalized, (Vector3.up + Vector3.back).normalized };
        for (float distance = 0.25f; distance <= 2f; distance += 0.25f)
            foreach (var direction in directions)
            {
                var candidate = transform.position + direction * distance;
                if (!IsClear(candidate, rotation)) continue;
                if (held && !UnstuckGeometry.Visible(eye, candidate + centreOffset, IsWorld)) continue;
                position = candidate;
                return true;
            }
        return false;
    }

    // Idle rescue requires a buried centre. During a failed grab we also permit partial body
    // embedding, including a small valuable wedged into a shelf with its centre still outside.
    // True only when the item's OWN collider is buried DEEP inside static geometry (a wall/floor),
    // rather than merely touching or resting inside furniture. We measure the actual penetration
    // depth between each of the item's colliders and every overlapping static (non-grabbable,
    // rigidbody-less) collider via Physics.ComputePenetration. Resting on a shelf, in a drawer, or
    // in a cabinet nook produces only shallow contact penetration (well under EmbedDepth), so those
    // legitimately-placed valuables are (correctly) left alone. An item that has fallen/clipped INTO
    // level geometry overlaps it deeply and crosses the threshold. This is far more reliable than
    // point-sampling, which couldn't distinguish "buried in a wall" from "surrounded by furniture".
    private bool IsEmbedded(bool requireBuriedCentre = true)
    {
        // A resting valuable — even one sitting right against a couch, table, or rug — keeps its
        // collision CENTER in open air above whatever it rests on. Only an item that has fallen or
        // clipped INTO a wall/floor has its center buried inside solid geometry. Requiring the center
        // to be buried is what stops us from "rescuing" loot that's merely on the floor next to
        // furniture (the Mortar Vase Big false positive). We then still confirm a deep penetration.
        if (requireBuriedCentre && !CenterBuried())
            return false;

        var myCols = GetComponentsInChildren<Collider>();
        if (myCols == null || myCols.Length == 0)
            return false;

        foreach (var mine in myCols)
        {
            if (mine == null || mine.isTrigger || !mine.enabled)
                continue;

            var b = mine.bounds;
            float requiredDepth = requireBuriedCentre ? EmbedDepth : UnstuckGeometry.PullEmbedDepth(b.size);
            var overlaps = Physics.OverlapBox(
                b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var other in overlaps)
            {
                if (other == null || other == mine || other.isTrigger)
                    continue;
                if (other.attachedRigidbody != null)                     // dynamic body, not geometry
                    continue;
                if (other.GetComponentInParent<PhysGrabObject>() != null) // another grabbable
                    continue;

                if (Physics.ComputePenetration(
                        mine, mine.transform.position, mine.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out _, out float depth) && depth >= requiredDepth)
                    return true;
            }
        }
        return false;
    }

    // True when the item's collision CENTER sits inside static (non-grabbable, rigidbody-less)
    // geometry — i.e. the middle of the object is actually inside a wall/floor. A small probe
    // radius tolerates thin surfaces without flagging an item that merely touches one.
    private bool CenterBuried()
    {
        Vector3 c = _pgo != null ? _pgo.centerPoint : transform.position;
        var hits = Physics.OverlapSphere(c, 0.06f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null)
            return false;

        foreach (var other in hits)
        {
            if (other == null || other.isTrigger)
                continue;
            if (other.attachedRigidbody != null)                     // dynamic body, not geometry
                continue;
            if (other.GetComponentInParent<PhysGrabObject>() != null) // another grabbable
                continue;
            return true;
        }
        return false;
    }

    private void Relocate(Vector3 pull, Vector3 eye, bool held)
    {
        if (!TryRecoveryPose(pull, eye, held, out var target, out var rot)) return;

        // Release through the native network path so the old grab anchor cannot pull it back.
        if (_pgo.playerGrabbing != null)
            foreach (var grabber in _pgo.playerGrabbing.ToArray())
                if (grabber != null)
                    grabber.OverrideGrabRelease(_pgo.photonView != null ? _pgo.photonView.ViewID : -1, 0.75f);
        _pgo.OverrideGrabDisable(0.75f);
        _pgo.OverrideGrabForceZero();
        _pgo.OverrideIndestructible(2f);
        _pgo.OverrideBreakEffects(2f);
        _pgo.Teleport(target, rot);
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.WakeUp();
        }
        _relocated = true;
        ItemUnstuckController.NotifyRelocated(_v);
    }
}
