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

    private GUIStyle _flashStyle;

    // Brief on-screen note when the host relocates a stuck valuable.
    private static string _relocText;
    private static float _relocUntil;

    internal static void NotifyRelocated(ValuableObject v)
    {
        _relocText = $"Relocated stuck loot: {Clean(v != null ? v.name : "valuable")}";
        _relocUntil = Time.time + 3f;
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

    private void OnGUI()
    {
        if (!ModConfig.ItemUnstuckEnabled.Value)
            return;

        if (_relocText != null && Time.time < _relocUntil)
            DrawFlash();
    }

    private void DrawFlash()
    {
        EnsureStyles();
        var rect = new Rect(0f, Screen.height * 0.66f, Screen.width, 22f);
        DrawShadowed(rect, _relocText, _flashStyle, new Color(0.5f, 0.9f, 1f));
    }

    private static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color)
    {
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = prev;
    }

    private void EnsureStyles()
    {
        _flashStyle ??= new GUIStyle
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            richText = false,
        };
    }

    internal static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "valuable";
        int clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
        if (clone >= 0)
            name = name.Substring(0, clone);
        return name.Replace("Valuable ", "").Trim();
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

    private void Awake()
    {
        _v = GetComponent<ValuableObject>();
        _pgo = GetComponent<PhysGrabObject>();
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (_v == null || _pgo == null)
            return;
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

        // Don't touch loot that's held, dead/broken, or riding in a cart.
        bool inCart = _pgo.impactDetector != null && _pgo.impactDetector.inCart;
        if (grabbed || _pgo.dead || inCart)
        {
            _stuckTimer = 0f;
            return;
        }

        bool resting = _rb == null || _rb.velocity.sqrMagnitude < RestSpeed * RestSpeed;
        bool embedded = IsEmbedded();

        if (embedded && resting)
        {
            _stuckTimer += CheckInterval;
            if (_stuckTimer >= Mathf.Max(2f, ModConfig.ValuableStuckSeconds.Value))
            {
                Relocate();
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
            // Remember the last free, settled spot so we can send a stuck one back to it.
            if (!embedded && resting)
            {
                _safePos = _pgo.transform.position;
                _safeRot = _pgo.transform.rotation;
                _haveSafe = true;
            }
        }
    }

    // True only when the item's OWN collider is buried DEEP inside static geometry (a wall/floor),
    // rather than merely touching or resting inside furniture. We measure the actual penetration
    // depth between each of the item's colliders and every overlapping static (non-grabbable,
    // rigidbody-less) collider via Physics.ComputePenetration. Resting on a shelf, in a drawer, or
    // in a cabinet nook produces only shallow contact penetration (well under EmbedDepth), so those
    // legitimately-placed valuables are (correctly) left alone. An item that has fallen/clipped INTO
    // level geometry overlaps it deeply and crosses the threshold. This is far more reliable than
    // point-sampling, which couldn't distinguish "buried in a wall" from "surrounded by furniture".
    private bool IsEmbedded()
    {
        // A resting valuable — even one sitting right against a couch, table, or rug — keeps its
        // collision CENTER in open air above whatever it rests on. Only an item that has fallen or
        // clipped INTO a wall/floor has its center buried inside solid geometry. Requiring the center
        // to be buried is what stops us from "rescuing" loot that's merely on the floor next to
        // furniture (the Mortar Vase Big false positive). We then still confirm a deep penetration.
        if (!CenterBuried())
            return false;

        var myCols = GetComponentsInChildren<Collider>();
        if (myCols == null || myCols.Length == 0)
            return false;

        foreach (var mine in myCols)
        {
            if (mine == null || mine.isTrigger || !mine.enabled)
                continue;

            var b = mine.bounds;
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
                        out _, out float depth) && depth >= EmbedDepth)
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

    private void Relocate()
    {
        Vector3 target = _haveSafe ? _safePos : _pgo.transform.position + Vector3.up * 1.5f;
        Quaternion rot = _haveSafe ? _safeRot : _pgo.transform.rotation;

        _pgo.Teleport(target, rot);
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        _relocated = true;
        ItemUnstuckController.NotifyRelocated(_v);
    }
}
