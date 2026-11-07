using System.Collections.Generic;
using System.Text;
using IncreaseHardMod.Hud;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>
/// The skill tree screen: opened with the skill tree key (K) or the Esc menu's SKILL TREE button during a run.
/// Mouse: click a perk to learn a level, click a page tab, refund or close at the bottom. Keys: 1-6 or Q / E for
/// pages, K or Esc to close. Viewing works anywhere; spending only where the host allows it (shop and truck).
/// While open, the cursor is free and the player's movement, aim and Esc menu are held back.
/// </summary>
internal sealed class SkillTreeScreen : MonoBehaviour
{
    private static SkillTreeScreen _instance;
    private static bool _openRequested;

    private static readonly SkillPage[] Pages = { SkillPage.Recon, SkillPage.Combat, SkillPage.Survival, SkillPage.Utility, SkillPage.Arsenal, SkillPage.Contracts };
    private static readonly string[] PageNames = { "RECON", "COMBAT", "SURVIVAL", "UTILITY", "ARSENAL", "CONTRACTS" };
    private static readonly string[] TabLabels = { "1", "2", "3", "4", "5", "6" };
    private static readonly Color[] Accents =
    {
        new(.35f, .9f, 1f), new(1f, .42f, .32f), new(.3f, 1f, .78f), new(.82f, .5f, 1f), new(1f, .84f, .3f), new(1f, .62f, .85f),
    };
    private static readonly Dictionary<string, string> GroupNames = new()
    {
        ["Map"] = "MAP", ["Ping"] = "PING", ["Flashlight"] = "FLASHLIGHT", ["Damage"] = "DAMAGE", ["Guns"] = "GUN CHARGES",
        ["Tech"] = "TECH", ["Survival"] = "SURVIVAL", ["Gear"] = "GEAR", ["Support"] = "SUPPORT", ["Weapons"] = "WEAPON DAMAGE", ["Status"] = "STATUS EFFECTS",
        ["Contracts"] = "EARNED FROM SPECIAL CONTRACTS",
        ["TradeOffs1"] = "TRADE-OFFS I · PICK ONE", ["TradeOffs2"] = "TRADE-OFFS II · PICK ONE", ["TradeOffs3"] = "TRADE-OFFS III · PICK ONE",
    };

    private readonly HudTreeView _view = new("IHM Skill Tree");
    private bool _open;
    private int _page, _builtPage = -1;
    private float _refundArmedUntil, _tradeArmedUntil;
    private bool _hinted;
    private string _hintLevel;

    internal static bool IsOpen => _instance != null && _instance._open;

    /// <summary>Open from elsewhere (the Esc menu button). Applied on the next frame, after that menu has closed.</summary>
    internal static void Open() => _openRequested = true;

    private void Awake() => _instance = this;
    private void OnDestroy() { _view.Destroy(); if (_instance == this) _instance = null; }

    private static bool CanOpen()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || SemiFunc.PlayerAvatarLocal() == null) return false;
        if (!SemiFunc.RunIsLevel() && !SemiFunc.RunIsShop() && !SemiFunc.RunIsLobby()) return false;
        if (ChatManager.instance != null && ChatManager.instance.chatActive) return false;
        return true;
    }

    private static bool MenuOpen => MenuManager.instance != null && MenuManager.instance.currentMenuPage != null;

    private void Update()
    {
        Hint();
        bool toggle = Input.GetKeyDown(ModConfig.SkillTreeKey.Value);
        if (!_open)
        {
            if ((_openRequested || toggle && !MenuOpen) && CanOpen()) SetOpen(true);
            _openRequested = false;
            return;
        }
        _openRequested = false;
        // Not closed by an open menu page: when opened from the Esc menu, that page is still closing for a moment,
        // and while the tree is open the pause menu is held back anyway.
        if (!CanOpen() || toggle || Input.GetKeyDown(KeyCode.Escape)) { SetOpen(false); return; }

        HoldPlayer();
        for (int i = 0; i < Pages.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) _page = i;
        _page = Mathf.Clamp(_page, 0, Pages.Length - 1);
        if (Input.GetKeyDown(KeyCode.Q)) _page = (_page + Pages.Length - 1) % Pages.Length;
        if (Input.GetKeyDown(KeyCode.E)) _page = (_page + 1) % Pages.Length;

        _view.Tick(Input.mousePosition);
        if (Input.GetMouseButtonDown(0)) Click();
        Draw();
    }

    private void SetOpen(bool open)
    {
        _open = open;
        if (open)
        {
            _builtPage = -1;
            _view.Show();
            Draw();
        }
        else
        {
            _view.Hide();
            HoldEsc(); // the Esc that closed us must not also open the pause menu
        }
    }

    // Free cursor (the game's own menu cursor) and no walking, looking, grabbing or pause menu while open.
    private static void HoldPlayer()
    {
        SemiFunc.CursorUnlock(.1f);
        if (MenuCursor.instance != null) MenuCursor.instance.OverrideHide(); // the tree draws its own pointer on top
        if (InputManager.instance != null)
        {
            InputManager.instance.DisableMovement(.1f);
            InputManager.instance.DisableAiming(.1f);
        }
        if (PlayerController.instance != null) PlayerController.instance.InputDisable(.1f);
        HoldEsc();
    }

    private static void HoldEsc()
    {
        if (GameDirector.instance != null) GameDirector.instance.disableEscMenuTimer = Mathf.Max(GameDirector.instance.disableEscMenuTimer, .25f);
    }

    // ------------------------------------------------------------------ input

    private void Click()
    {
        if (_view.HoveredTab >= 0) { _page = _view.HoveredTab; return; }
        if (_view.HoveredButton == 0) { Refund(); return; }
        if (_view.HoveredButton == 1) { ResetTradeOffs(); return; }
        if (_view.HoveredButton == 2) { SetOpen(false); return; }
        var id = _view.HoveredNode;
        if (id == null) return;
        if (id == C.BeamColour && SkillEffects.LocalHas(C.BeamColour))
        {
            int next = (SkillEffects.BeamColourIndex(SemiFunc.PlayerAvatarLocal()) + 1) % SkillEffects.BeamColours.Length;
            SkillEffects.ChooseBeamColour(next);
            return;
        }
        SkillStore.RequestBuy(id);
    }

    private void Refund()
    {
        if (Time.unscaledTime > _refundArmedUntil) { _refundArmedUntil = Time.unscaledTime + 3f; return; } // click again to confirm
        _refundArmedUntil = 0f;
        SkillStore.RequestRespec();
    }

    private void ResetTradeOffs()
    {
        if (Time.unscaledTime > _tradeArmedUntil) { _tradeArmedUntil = Time.unscaledTime + 3f; return; } // click again to confirm
        _tradeArmedUntil = 0f;
        SkillStore.RequestTradeOffReset();
    }

    // ------------------------------------------------------------------ drawing

    private void Draw()
    {
        var local = SemiFunc.PlayerAvatarLocal();
        var loadout = SkillStore.LoadoutOf(local != null ? local.steamID : null);
        var accent = Accents[_page];
        var page = Pages[_page];

        if (_builtPage != _page)
        {
            var items = new List<HudTreeItem>();
            foreach (var node in C.All)
                if (node.Page == page) items.Add(new HudTreeItem(node.Id, node.Requires.Length > 0 ? node.Requires[0] : null, node.Branch));
            _view.BuildPage(items, g => GroupNames.TryGetValue(g, out var name) ? name : g.ToUpperInvariant(), accent);
            _builtPage = _page;
        }

        bool canSpend = SkillStore.SpendAllowedHere();
        string where = canSpend ? "CLICK A PERK TO LEARN IT" : "VIEW ONLY: SPEND POINTS IN THE SHOP OR TRUCK";
        _view.SetHeader("SKILL TREE", $"PAGE {_page + 1}/{Pages.Length} · {PageNames[_page]}   ·   {where}",
            $"{loadout.Available} POINT{(loadout.Available == 1 ? "" : "S")}  <size=70%><color=#BBBBBB>({loadout.Earned} EARNED)</color></size>");
        _view.SetTabs(TabLabels, _page, accent);

        float pulse = .75f + .25f * Mathf.Sin(Time.unscaledTime * 5f);
        foreach (var node in C.All)
        {
            if (node.Page != page) continue;
            int level = loadout.Level(node.Id);
            var block = SkillRules.CanBuy(loadout, node.Id);
            bool locked = block is BuyBlock.MissingPrerequisite or BuyBlock.GateNotMet or BuyBlock.NeedLowerTier or BuyBlock.TierTaken,
                maxed = level >= node.MaxLevel, affordable = block == BuyBlock.None;
            bool hover = _view.HoveredNode == node.Id;
            string value = node.Id == C.BeamColour && level > 0 ? SkillEffects.BeamColourNames[SkillEffects.BeamColourIndex(local)].ToUpperInvariant()
                : $"[{level}/{node.MaxLevel}]";
            // A trade-off still behind its page gate shows how far along the page is.
            if (node.Tier > 0 && level == 0 && block == BuyBlock.GateNotMet)
                value = $"{SkillRules.PageSpent(loadout, node.Page)}/{TradeOffRules.Gate(node.Page, node.Tier)}";
            var look = new HudTreeLook { Name = SkillText.Name(node), Value = value, Linked = level > 0 };
            if (maxed) { look.Edge = accent; look.Fill = accent * new Color(.38f, .38f, .38f, .95f); look.Text = Color.white; look.ValueColour = HudStyle.Title; }
            else if (level > 0) { look.Edge = accent; look.Fill = accent * new Color(.2f, .2f, .2f, .95f); look.Text = Color.white; look.ValueColour = Color.white; }
            else if (locked) { look.Edge = new Color(.35f, .35f, .35f, .9f); look.Fill = new Color(.05f, .05f, .05f, .95f); look.Text = new Color(.5f, .5f, .5f); look.ValueColour = look.Text; }
            else { look.Edge = accent * new Color(.7f, .7f, .7f, 1f); look.Fill = new Color(.06f, .06f, .07f, .95f); look.Text = new Color(.85f, .85f, .85f); look.ValueColour = look.Text; }
            if (affordable && canSpend && !maxed) look.Edge = Color.Lerp(look.Edge, Color.white, .35f * pulse);
            if (hover) { look.Edge = Color.white; look.Fill = Color.Lerp(look.Fill, accent, .25f); }
            _view.SetNode(node.Id, look);
        }

        _view.SetTooltip(Tooltip(loadout, canSpend));
        bool armed = Time.unscaledTime < _refundArmedUntil, tradeArmed = Time.unscaledTime < _tradeArmedUntil;
        int fee = Mathf.Max(0, ModConfig.SkillRespecCost.Value), tradeFee = Mathf.Max(0, ModConfig.SkillTradeOffResetCost.Value);
        _view.SetButtons(new[]
            {
                armed ? "CLICK AGAIN TO REFUND" : fee > 0 ? $"REFUND ALL (${fee}K)" : "REFUND ALL",
                tradeArmed ? "CLICK AGAIN TO RESET" : tradeFee > 0 ? $"RESET TRADE-OFFS (${tradeFee}K)" : "RESET TRADE-OFFS",
                "CLOSE  [K]",
            },
            new[] { armed ? HudStyle.Bad : HudStyle.Caution, tradeArmed ? HudStyle.Bad : HudStyle.Caution, new Color(.8f, .8f, .8f) });
    }

    private string Tooltip(SkillLoadout loadout, bool canSpend)
    {
        var node = C.Find(_view.HoveredNode);
        if (node == null)
            return "<color=#BBBBBB>Hover a perk to see what it does. Points come from the shop's Skill Point and from finished quests. " +
                   "1-6 / Q / E change page.</color>";
        int level = loadout.Level(node.Id);
        var text = new StringBuilder();
        text.Append("<color=#FFD966>").Append(SkillText.Name(node).ToUpperInvariant()).Append("</color>  [").Append(level).Append('/').Append(node.MaxLevel).Append("]   ");
        text.Append(SkillText.Describe(node.Id)).Append('\n');
        if (node.QuestOnly) text.Append("<color=#BBBBBB>Not for points: each finished special contract gives one level");
        else if (node.Tier > 0 && SkillText.Russian)
            text.Append("<color=#BBBBBB>Компромисс, уровень ").Append(node.Tier).Append(": ").Append(node.Cost).Append(node.Cost == 1 ? " очко" : " очка")
                .Append(", один выбор на уровень. Нужно ").Append(TradeOffRules.Gate(node.Page, node.Tier)).Append(" очков в навыках страницы")
                .Append(node.Tier > 1 ? " и выбор уровня " + (node.Tier - 1) : "");
        else if (node.Tier > 0)
            text.Append("<color=#BBBBBB>Trade-off tier ").Append(node.Tier).Append(": ").Append(node.Cost).Append(node.Cost == 1 ? " point" : " points")
                .Append(", one pick per tier. Needs ").Append(TradeOffRules.Gate(node.Page, node.Tier)).Append(" points in this page's perks")
                .Append(node.Tier > 1 ? " and a tier " + (node.Tier - 1) + " pick" : "");
        else text.Append("<color=#BBBBBB>Cost: ").Append(node.Cost).Append(node.Cost == 1 ? " point" : " points").Append(node.MaxLevel > 1 ? " per level" : "");
        foreach (var need in node.Requires) text.Append("   ·   Needs: ").Append(C.Find(need)?.Name);
        text.Append("</color>   ");
        string state = SkillRules.CanBuy(loadout, node.Id) switch
        {
            BuyBlock.None => canSpend ? "<color=#80FF80>Click to learn</color>" : "<color=#FFCC4D>Learn it in the shop or truck</color>",
            BuyBlock.Maxed => node.Id == C.BeamColour ? "<color=#80E6FF>Click to change colour</color>" : "<color=#FFD966>Maxed</color>",
            BuyBlock.MissingPrerequisite => "<color=#FF7373>Locked: learn the perk before it first</color>",
            BuyBlock.NotEnoughPoints => "<color=#FF7373>Not enough points</color>",
            BuyBlock.QuestOnly => "<color=#FF9ED8>Finish a special CONTRACT quest to earn a level</color>",
            BuyBlock.GateNotMet => $"<color=#FF7373>Locked: {SkillRules.PageSpent(loadout, node.Page)}/{TradeOffRules.Gate(node.Page, node.Tier)} points in this page</color>",
            BuyBlock.NeedLowerTier => "<color=#FF7373>Locked: pick a trade-off from the tier below first</color>",
            BuyBlock.TierTaken => "<color=#FFCC4D>You hold another trade-off on this tier: RESET TRADE-OFFS to swap</color>",
            _ => "",
        };
        return text.Append(state).ToString();
    }

    // In the shop or truck with points to spend: say once per visit how to open the tree.
    private void Hint()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || _open) return;
        string key = null;
        try { if (SemiFunc.RunIsShop() || SemiFunc.RunIsLobby()) key = RunManager.instance != null && RunManager.instance.levelCurrent != null ? RunManager.instance.levelCurrent.name + RunManager.instance.levelsCompleted : null; }
        catch { }
        if (key != _hintLevel) { _hintLevel = key; _hinted = false; }
        if (key == null || _hinted || !SkillStore.Ready || !LevelInfoHud_Ready()) return;
        var local = SemiFunc.PlayerAvatarLocal();
        int points = local != null ? SkillStore.LoadoutOf(local.steamID).Available : 0;
        _hinted = true;
        if (points > 0) HudNotify.Toast($"{points} skill point{(points == 1 ? "" : "s")} to spend: press {ModConfig.SkillTreeKey.Value}", HudStyle.Good, 5f, "skills.hint");
    }

    private static bool LevelInfoHud_Ready() => Patches.Effects.LevelInfoHud.GameplayReady;
}
