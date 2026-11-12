using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using IncreaseHardMod.Valuables;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Cosmetics;

/// <summary>
/// Recolour channels for the mod's costume parts. The game gives every cosmetic slot one colour; a costume part can
/// split its tintable materials into up to three channels (tools/cosmetic_channels.py marks them in the model):
/// channel 0 is the game's own slot colour, channels 1 and 2 ("second" and "accent") are two more colours per slot kept
/// by the mod. An unset channel follows the slot colour, so a costume looks exactly as before until the player picks one.
/// The colours are indexes into the game's own palette (MetaManager.colors), picked on the game's colour page
/// (ChannelPicker adds the channel buttons to it), saved in the mod's config and shared with the other players as a
/// Photon player property, which late joiners get from the server without the host doing anything.
/// A player without the mod simply sees every channel in the slot colour.
/// </summary>
internal static class CostumeChannels
{
    internal const int Extra = 2;   // channels on top of the game's own slot colour
    internal const string PropertyKey = "ihmCC";
    private const string IdPrefix = "modded:IncreaseDifficultyMod.";

    internal static readonly int Slots = Enum.GetValues(typeof(SemiFunc.CosmeticType)).Length;

    private static int[] _local;      // [slot * Extra + channel - 1] = palette index, -1 = follows the slot colour
    private static byte[] _packed;    // the same, as sent to the other players (rebuilt after every change)
    private static readonly Dictionary<byte[], int[]> Remote = new();   // unpacked player properties, by Photon's array

    // ---------------------------------------------------------------- the local player's colours

    private static int[] Local
    {
        get
        {
            if (_local != null) return _local;
            _local = Parse(ModConfig.CostumeChannelColors != null ? ModConfig.CostumeChannelColors.Value : null);
            return _local;
        }
    }

    /// <summary>The local player's colour for a channel (1 or 2) of a slot, or -1 when it follows the slot colour.</summary>
    internal static int GetLocal(int slot, int channel) =>
        slot >= 0 && slot < Slots && channel >= 1 && channel <= Extra ? Local[slot * Extra + channel - 1] : -1;

    internal static void SetLocal(int slot, int channel, int colour)
    {
        if (slot < 0 || slot >= Slots || channel < 1 || channel > Extra) return;
        if (colour < 0 || colour > 253) colour = -1;
        int at = slot * Extra + channel - 1;
        if (Local[at] == colour) return;
        Local[at] = colour;
        _packed = null;
        if (ModConfig.CostumeChannelColors != null) ModConfig.CostumeChannelColors.Value = Format(Local);
    }

    /// <summary>"slot:channel:colour" triples; anything unreadable is skipped, so an edited config can't break the menu.</summary>
    internal static int[] Parse(string text)
    {
        var colours = new int[Slots * Extra];
        for (int i = 0; i < colours.Length; i++) colours[i] = -1;
        if (string.IsNullOrEmpty(text)) return colours;
        foreach (string entry in text.Split(','))
        {
            string[] bits = entry.Split(':');
            if (bits.Length != 3 || !int.TryParse(bits[0], out int slot) || !int.TryParse(bits[1], out int channel) || !int.TryParse(bits[2], out int colour)) continue;
            if (slot < 0 || slot >= Slots || channel < 1 || channel > Extra || colour < 0 || colour > 253) continue;
            colours[slot * Extra + channel - 1] = colour;
        }
        return colours;
    }

    internal static string Format(int[] colours)
    {
        var text = new StringBuilder();
        for (int i = 0; i < colours.Length; i++)
        {
            if (colours[i] < 0) continue;
            if (text.Length > 0) text.Append(',');
            text.Append(i / Extra).Append(':').Append(i % Extra + 1).Append(':').Append(colours[i]);
        }
        return text.ToString();
    }

    // ---------------------------------------------------------------- over the network

    /// <summary>One byte per channel: 0 = follows the slot colour, otherwise palette index + 1.</summary>
    internal static byte[] Pack(int[] colours)
    {
        var bytes = new byte[colours.Length];
        for (int i = 0; i < colours.Length; i++) bytes[i] = (byte)(colours[i] < 0 || colours[i] > 253 ? 0 : colours[i] + 1);
        return bytes;
    }

    /// <summary>Null unless it is exactly this version's layout (another mod version, or junk, changes nothing).</summary>
    internal static int[] Unpack(byte[] bytes)
    {
        if (bytes == null || bytes.Length != Slots * Extra) return null;
        var colours = new int[bytes.Length];
        for (int i = 0; i < bytes.Length; i++) colours[i] = bytes[i] - 1;
        return colours;
    }

    internal static byte[] LocalPacked => _packed ??= Pack(Local);

    internal static byte[] PropertyOf(PlayerAvatar avatar)
    {
        var owner = avatar != null && avatar.photonView != null ? avatar.photonView.Owner : null;
        return owner != null && owner.CustomProperties.TryGetValue(PropertyKey, out object value) ? value as byte[] : null;
    }

    /// <summary>The channel colours of whoever wears this: the local store for our own bodies and menu avatars.</summary>
    private static int[] ColoursOf(PlayerCosmetics cosmetics)
    {
        PlayerAvatar avatar = null;
        if (cosmetics.deathHead != null && cosmetics.deathHead.playerAvatar != null) avatar = cosmetics.deathHead.playerAvatar;
        else if (cosmetics.playerAvatarVisuals != null) avatar = cosmetics.playerAvatarVisuals.playerAvatar;
        if (avatar == null || avatar.isLocal || !SemiFunc.IsMultiplayer()) return Local;   // menu avatars have no player
        var bytes = PropertyOf(avatar);
        if (bytes == null) return null;
        if (Remote.TryGetValue(bytes, out var colours)) return colours;
        if (Remote.Count > 64) Remote.Clear();   // old arrays of players who changed their colours or left
        return Remote[bytes] = Unpack(bytes);
    }

    /// <summary>Apply the wearer's colours again (the game sets every material, and the patch below adds the channels).</summary>
    internal static void Reapply(PlayerCosmetics cosmetics)
    {
        if (cosmetics == null || cosmetics.firstSetup || cosmetics.colorsEquipped == null) return;
        foreach (int colour in cosmetics.colorsEquipped) if (colour < 0) return;   // the game has not set them yet
        cosmetics.SetupColorsLogic((int[])cosmetics.colorsEquipped.Clone());
    }

    internal static void ReapplyLocal()
    {
        var meta = MetaManager.instance;
        if (meta != null) meta.CosmeticPlayerUpdateLocal(false);   // the menu avatar
        if (PlayerController.instance == null) return;
        var avatar = PlayerController.instance.playerAvatarScript;
        if (avatar == null) return;
        Reapply(avatar.playerCosmetics);
        if (avatar.playerDeathHead != null) Reapply(avatar.playerDeathHead.playerCosmetics);
        if (PlayerExpressionsUI.instance != null && PlayerExpressionsUI.instance.playerAvatarVisuals != null)
            Reapply(PlayerExpressionsUI.instance.playerAvatarVisuals.playerCosmetics);
    }

    // ---------------------------------------------------------------- which parts have channels

    /// <summary>
    /// The channels of the mod costume part with this model key, in order: 0 (the slot colour) when it has tintable
    /// fabric, then 1 and 2 for a second and accent fabric. Empty for anything else.
    /// </summary>
    internal static List<int> Channels(string modelKey)
    {
        var found = new List<int>();
        try
        {
            foreach (var material in ValuableVisuals.Load(modelKey).materials)
            {
                int channel = material.tint ? Mathf.Clamp(material.channel, 0, Extra) : material.channel >= 1 && material.channel <= Extra ? material.channel : -1;
                if (channel >= 0 && !found.Contains(channel)) found.Add(channel);
            }
            found.Sort();
        }
        catch (Exception) { found.Clear(); }
        return found;
    }

    /// <summary>The channels of the costume part the local player wears in a slot (empty when it is not one of ours).</summary>
    internal static List<int> ChannelsForSlot(int slot)
    {
        var meta = MetaManager.instance;
        if (meta == null || meta.cosmeticEquipped == null || meta.cosmeticAssets == null) return new List<int>();
        foreach (int index in meta.cosmeticEquipped)
        {
            if (index < 0 || index >= meta.cosmeticAssets.Count) continue;
            var asset = meta.cosmeticAssets[index];
            if (asset == null || (int)asset.type != slot || asset.assetId == null || !asset.assetId.StartsWith(IdPrefix, StringComparison.Ordinal)) continue;
            return Channels(asset.assetId.Substring(IdPrefix.Length));
        }
        return new List<int>();
    }

    // ---------------------------------------------------------------- patches

    /// <summary>
    /// A material on the second or accent channel takes that channel's colour instead of the slot colour. A tintable
    /// one follows the slot colour while the channel is unset; a fixed one (hair, lining, leather, metal: not tintable
    /// for the game) keeps its own colour until then.
    /// </summary>
    [HarmonyPatch(typeof(PlayerMaterial), nameof(PlayerMaterial.ColorSet))]
    private static class ChannelColour
    {
        private static void Prefix(PlayerMaterial __instance, int _albedoColor, int _emissionColor, ref int _colorIndex)
        {
            try
            {
                var tag = __instance.GetComponent<CostumeChannelTag>();
                if (tag == null || tag.channel < 1 || tag.channel > Extra) return;
                var cosmetics = __instance.cosmetic != null ? __instance.cosmetic.playerCosmetics : null;
                var colours = cosmetics != null ? ColoursOf(cosmetics) : null;   // menu icons and world props: no wearer
                int slot = (int)__instance.cosmeticType;
                int colour = colours != null && slot >= 0 && slot < Slots ? colours[slot * Extra + tag.channel - 1] : -1;
                var palette = MetaManager.instance != null ? MetaManager.instance.colors : null;
                if (palette == null || colour >= palette.Count) colour = -1;
                if (!tag.optional)
                {
                    if (colour >= 0) _colorIndex = colour;
                    return;
                }
                var material = __instance.material;
                if (material == null) return;
                Color shown = colour >= 0 ? palette[colour].color : tag.natural;
                material.SetColor(_albedoColor, new Color(shown.r, shown.g, shown.b, material.GetColor(_albedoColor).a));
                if (tag.emission > 0f) material.SetColor(_emissionColor, shown * tag.emission);
            }
            catch (Exception error) { Plugin.Log?.LogWarning("[Cosmetics] Channel colour failed: " + error.Message); }
        }
    }

    /// <summary>A colour clicked while the second or accent channel is selected goes to that channel.</summary>
    [HarmonyPatch(typeof(MetaManager), nameof(MetaManager.CosmeticColorSet))]
    private static class ChannelClick
    {
        private static bool Prefix(int _index, int _colorID)
        {
            if (ChannelPicker.Channel < 1 || ChannelPicker.Slot != _index) return true;
            SetLocal(_index, ChannelPicker.Channel, _colorID);
            ChannelPicker.Dirty = true;
            return false;   // the page still repaints the menu avatar and marks the look for saving
        }
    }

    /// <summary>The colour page's "selected" marker follows the channel being edited.</summary>
    [HarmonyPatch(typeof(MenuPageColor), nameof(MenuPageColor.SelectedColorIDGet))]
    private static class ChannelSelected
    {
        private static void Postfix(MenuPageColor __instance, ref int __result)
        {
            if (ChannelPicker.Channel < 1 || ChannelPicker.Slot != __instance.colorKey) return;
            int colour = GetLocal(ChannelPicker.Slot, ChannelPicker.Channel);
            if (colour >= 0) __result = colour;
        }
    }

    [HarmonyPatch(typeof(MenuPageColor), nameof(MenuPageColor.Start))]
    private static class ChannelButtons
    {
        private static void Postfix(MenuPageColor __instance)
        {
            try { ChannelPicker.Attach(__instance); }
            catch (Exception error) { Plugin.Log?.LogError("[Cosmetics] Channel buttons failed: " + error); }
        }
    }

    // ---------------------------------------------------------------- sync

    /// <summary>
    /// Publishes the local colours as a player property and re-colours a player whose property changed. Polled, and
    /// only while in a room: the mod must not touch Photon on the main menu.
    /// </summary>
    internal sealed class Sync : MonoBehaviour
    {
        private float _next;
        private string _room;
        private byte[] _sent;
        private readonly Dictionary<PlayerAvatar, byte[]> _applied = new();
        private readonly List<PlayerAvatar> _gone = new();

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + .5f;
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null)
            {
                if (_room != null) { _room = null; _sent = null; _applied.Clear(); Remote.Clear(); }
                return;
            }
            try
            {
                string room = PhotonNetwork.CurrentRoom.Name;
                var packed = LocalPacked;
                if (room != _room || !ReferenceEquals(packed, _sent))
                {
                    _room = room; _sent = packed;
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropertyKey, packed } });
                }
                var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
                if (players == null) return;
                foreach (var player in players)
                {
                    if (player == null || player.isLocal) continue;
                    var bytes = PropertyOf(player);
                    bool known = _applied.TryGetValue(player, out var last);
                    if (known && ReferenceEquals(last, bytes)) continue;
                    _applied[player] = bytes;
                    if (!known && bytes == null) continue;   // nothing to show for a player without the property
                    Reapply(player.playerCosmetics);
                    if (player.playerDeathHead != null) Reapply(player.playerDeathHead.playerCosmetics);
                }
                foreach (var entry in _applied) if (entry.Key == null) _gone.Add(entry.Key);
                foreach (var player in _gone) _applied.Remove(player);
                _gone.Clear();
            }
            catch (Exception error) { Plugin.Log?.LogWarning("[Cosmetics] Channel sync failed: " + error.Message); }
        }
    }
}

/// <summary>Marks a costume mesh on the second (1) or accent (2) channel; main-channel meshes carry no tag.</summary>
internal sealed class CostumeChannelTag : MonoBehaviour
{
    // serialized, so they survive Instantiate
    public int channel;
    public bool optional;    // a fixed fabric: its own colour until the channel is set (the game never tints it)
    public Color natural;
    public float emission;
}

/// <summary>
/// The channel row on the game's colour page: MAIN, SECOND, ACCENT and RESET under the Confirm button, shown when the
/// slot holds a mod costume part with a second or accent channel (a part of fixed fabrics only has no MAIN). The buttons are copies of the page's own Confirm button,
/// so they keep the game's font, material, hover sound and gamepad navigation.
/// </summary>
internal sealed class ChannelPicker : MonoBehaviour
{
    /// <summary>The slot whose colour page is open (-1: none) and the channel a clicked colour goes to (0 = main).</summary>
    internal static int Slot = -1, Channel;
    /// <summary>A channel colour changed while this page was open: our own body is re-coloured when it closes.</summary>
    internal static bool Dirty;

    private const float RowY = 72f, CentreX = 420f, Gap = 22f, MaxWidth = 410f;   // page units: the panel spans x 195..645, Confirm sits at y 112

    private MenuPageColor _page;
    private List<int> _channels;   // the part's channels, in button order
    private readonly List<MenuButton> _buttons = new();   // one per channel, then RESET

    internal static void Attach(MenuPageColor page)
    {
        Slot = -1; Channel = 0; Dirty = false;
        if (page == null || page.colorKey < 0) return;   // the "all colours" pages keep the game's behaviour
        var channels = CostumeChannels.ChannelsForSlot(page.colorKey);
        // Nothing to choose between on a part with only the slot colour.
        if (channels.Count == 0 || channels.Count == 1 && channels[0] == 0) return;
        MenuButton confirm = null;
        foreach (Transform child in page.transform)
        {
            if (child == page.colorButtonHolder || child.GetComponent<MenuButtonColor>() != null) continue;
            confirm = child.GetComponent<MenuButton>();
            if (confirm != null) break;
        }
        if (confirm == null || confirm.GetComponent<Button>() == null) return;
        var picker = page.gameObject.AddComponent<ChannelPicker>();
        picker._page = page; picker._channels = channels;
        Slot = page.colorKey; Channel = channels[0];
        picker.Build(confirm);
    }

    private void Build(MenuButton confirm)
    {
        bool ru = Localization.GameLanguage.Russian;
        var labels = new List<string>();
        foreach (int channel in _channels)
            labels.Add(channel == 0 ? ru ? "ОСНОВА" : "MAIN" : channel == 1 ? ru ? "ВТОРОЙ" : "SECOND" : ru ? "АКЦЕНТ" : "ACCENT");
        labels.Add(ru ? "СБРОС" : "RESET");
        for (int i = 0; i < labels.Count; i++)
        {
            var copy = Instantiate(confirm.gameObject, confirm.transform.parent);
            copy.name = "IHM Channel Button " + i;
            // The copy must keep our label when the game's language changes.
            foreach (var localized in copy.GetComponentsInChildren<LocalizeChangedUI>(true)) DestroyImmediate(localized);
            foreach (var localized in copy.GetComponentsInChildren<LocalizationChangedEvent>(true)) DestroyImmediate(localized);
            var button = copy.GetComponent<MenuButton>();
            button.menuButtonPopUp = null;
            button.buttonTextString = labels[i];
            button.buttonText.text = labels[i];
            int index = i;
            var click = copy.GetComponent<Button>();
            click.onClick = new Button.ButtonClickedEvent();   // drops the copied Confirm action
            click.onClick.AddListener(() => Click(index));
            _buttons.Add(button);
        }
        Layout();
        // Gamepad and keyboard: down from Confirm into the row, left and right along it.
        var above = confirm.menuSelectableElement;
        for (int i = 0; i < _buttons.Count; i++)
        {
            var element = _buttons[i].menuSelectableElement;
            if (element == null) continue;
            element.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = above,
                selectOnLeft = i > 0 ? _buttons[i - 1].menuSelectableElement : null,
                selectOnRight = i < _buttons.Count - 1 ? _buttons[i + 1].menuSelectableElement : null,
            };
        }
        if (above != null && _buttons[0].menuSelectableElement != null)
        {
            var navigation = above.navigation;
            navigation.selectOnDown = _buttons[0].menuSelectableElement;
            above.navigation = navigation;
        }
        Refresh();
    }

    /// <summary>One centred row; the labels shrink together if a language makes the row wider than the panel.</summary>
    private void Layout()
    {
        for (int pass = 0; pass < 2; pass++)
        {
            float total = Gap * (_buttons.Count - 1);
            foreach (var button in _buttons) { button.ResizeToText(); total += button.rectTransform.sizeDelta.x; }
            if (total <= MaxWidth || pass == 1)
            {
                float x = CentreX - total / 2f;
                foreach (var button in _buttons)
                {
                    button.rectTransform.anchoredPosition = new Vector2(x, RowY);
                    button.originalButtonPosition = button.rectTransform.localPosition;   // where a later ResizeToText puts it back
                    x += button.rectTransform.sizeDelta.x + Gap;
                }
                return;
            }
            foreach (var button in _buttons)
            {
                button.originalAutoSizing = false;
                button.originalFontSize *= MaxWidth / total;
            }
        }
    }

    private void Click(int index)
    {
        bool reset = index == _buttons.Count - 1;
        if (reset)
        {
            if (Channel < 1) return;
            CostumeChannels.SetLocal(Slot, Channel, -1);   // back to following the slot colour
            Dirty = true;
            // The cosmetics page saves and sends the look when it closes, like after a normal colour change.
            var under = _page != null && _page.menuPage != null && _page.menuPage.pageUnderThisPage != null
                ? _page.menuPage.pageUnderThisPage.GetComponent<MenuPageCosmetics>() : null;
            if (under != null) under.shouldSave = true;
            CostumeChannels.ReapplyLocal();
        }
        else Channel = _channels[index];
        Refresh();
        MoveMarker();
    }

    /// <summary>The selected channel is white, the others the game's idle grey; RESET is dark while there is nothing to reset.</summary>
    private void Refresh()
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            bool reset = i == _buttons.Count - 1;
            bool usable = !reset || Channel >= 1 && CostumeChannels.GetLocal(Slot, Channel) >= 0;
            button.disabled = !usable;
            button.colorNormal = !usable ? new Color(.25f, .25f, .25f) : !reset && _channels[i] == Channel ? Color.white : Color.gray;
            button.colorHover = usable ? Color.white : button.colorNormal;
        }
    }

    private void Update()
    {
        // RESET lights up as soon as the channel has a colour of its own.
        if (_buttons.Count == 0) return;
        var reset = _buttons[_buttons.Count - 1];
        bool usable = Channel >= 1 && CostumeChannels.GetLocal(Slot, Channel) >= 0;
        if (reset.disabled == usable) Refresh();
    }

    /// <summary>Move the page's "selected colour" ring to the colour of the channel now being edited.</summary>
    private void MoveMarker()
    {
        if (_page == null || _page.menuColorSelected == null || _page.colorButtonHolder == null || MetaManager.instance == null) return;
        int colour = _page.SelectedColorIDGet();
        foreach (var swatch in _page.colorButtonHolder.GetComponentsInChildren<MenuButtonColor>())
        {
            if (swatch.colorID != colour) continue;
            var rect = swatch.GetComponent<RectTransform>();
            _page.menuColorSelected.gameObject.SetActive(true);
            _page.menuColorSelected.SetColor(MetaManager.instance.colors[colour].color,
                rect.position + new Vector3(rect.rect.width / 2f, rect.rect.height / 2f, 0f));
            return;
        }
    }

    private void OnDestroy()
    {
        Slot = -1; Channel = 0;
        if (Dirty) CostumeChannels.ReapplyLocal();   // our own body in the lobby or the truck
        Dirty = false;
    }
}
