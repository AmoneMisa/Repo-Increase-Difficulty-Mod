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

    private GUIStyle _valueStyle;
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

        if (ModConfig.ValuableShowUndamagedValue.Value && SafeRunIsLevel())
            DrawGrabValue();

        if (_relocText != null && Time.time < _relocUntil)
            DrawFlash();
    }

    // Show the undamaged (original) value of whatever valuable the LOCAL player is holding.
    private void DrawGrabValue()
    {
        var dir = ValuableDirector.instance;
        if (dir == null || dir.valuableList == null)
            return;

        ValuableObject held = null;
        foreach (var v in dir.valuableList)
        {
            if (v == null)
                continue;
            var pgo = v.physGrabObject;
            if (pgo != null && pgo.grabbedLocal)
            {
                held = v;
                break;
            }
        }
        if (held == null)
            return;

        EnsureStyles();

        int original = Mathf.RoundToInt(held.dollarValueOriginal);
        int current = Mathf.RoundToInt(held.dollarValueCurrent);
        string text = current < original
            ? $"Value: ${current}  (undamaged ${original})"
            : $"Value: ${original}";

        var rect = new Rect(0f, Screen.height * 0.72f, Screen.width, 24f);
        DrawShadowed(rect, text, _valueStyle, new Color(1f, 0.85f, 0.35f));
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
        _valueStyle ??= new GUIStyle
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            richText = false,
        };
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
    private const float EmbedRadius = 0.18f;   // sphere at the object's centre to test for solids
    private const float RestSpeed = 0.35f;     // below this the object is "settled"

    private ValuableObject _v;
    private PhysGrabObject _pgo;
    private Rigidbody _rb;

    private bool _grabbedOnce;
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
        // Don't touch loot that's held, dead/broken, or riding in a cart.
        bool inCart = _pgo.impactDetector != null && _pgo.impactDetector.inCart;
        if (grabbed || _pgo.dead || inCart)
        {
            _stuckTimer = 0f;
            return;
        }

        Vector3 center = _pgo.impactDetector != null ? _pgo.impactDetector.centerPoint : Vector3.zero;
        if (center == Vector3.zero)
            center = _pgo.transform.position;

        bool resting = _rb == null || _rb.velocity.sqrMagnitude < RestSpeed * RestSpeed;
        bool embedded = IsEmbedded(center);

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

    // True when the object's centre sits inside a static, non-trigger collider (i.e. embedded in
    // level geometry). Own colliders and other dynamic/grabbable bodies are ignored.
    private bool IsEmbedded(Vector3 center)
    {
        var hits = Physics.OverlapSphere(center, EmbedRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in hits)
        {
            if (c == null || c.isTrigger)
                continue;
            if (c.attachedRigidbody != null)          // dynamic body, not static geometry
                continue;
            if (c.GetComponentInParent<PhysGrabObject>() != null) // another grabbable, not geometry
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
        ItemUnstuckController.NotifyRelocated(_v);
    }
}
