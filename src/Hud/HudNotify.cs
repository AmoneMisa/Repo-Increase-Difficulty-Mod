using System.Collections.Generic;
using IncreaseHardMod.Patches.Effects;
using TMPro;
using UnityEngine;

namespace IncreaseHardMod.Hud;

/// <summary>
/// Short native-style messages without backplates, in two channels:
/// <list type="bullet">
/// <item><see cref="Toast"/>: a line above the inventory bar for a few seconds ("Relocated stuck loot: ...").
/// Up to three stack, newest at the bottom.</item>
/// <item><see cref="Status"/>: a larger line in the upper centre while a state lasts ("FROZEN 2.4s"). Call it
/// every frame while the state applies; it fades out about half a second after the last call, or at once with
/// <see cref="Clear"/>.</item>
/// </list>
/// Messages with the same key replace each other instead of piling up. Everything is local to this player.
/// </summary>
internal static class HudNotify
{
    private sealed class Channel
    {
        public readonly HudMessageQueue<Color> Queue = new();
        public readonly List<(NativeHudPanel surface, TextMeshProUGUI label)> Slots = new();
        public HudCorner Corner;
        public string Name;
        public float Width, Height, FontSize;
    }

    private static readonly Channel Toasts = new() { Corner = HudCorner.BottomCentre, Name = "toast", Width = 720f, Height = 40f, FontSize = HudStyle.Toast };
    private static readonly Channel Statuses = new() { Corner = HudCorner.UpperCentre, Name = "status", Width = 520f, Height = 46f, FontSize = HudStyle.Status };

    /// <summary>A timed message above the inventory bar.</summary>
    internal static void Toast(string text, Color color, float seconds = 3f, string key = null) =>
        Toasts.Queue.Show(key, text, color, seconds, Time.unscaledTime);

    /// <summary>A state message in the upper centre; refresh it every frame while the state applies.</summary>
    internal static void Status(string key, string text, Color color, float seconds = .5f) =>
        Statuses.Queue.Show(key, text, color, seconds, Time.unscaledTime);

    internal static void Clear(string key)
    {
        Toasts.Queue.Clear(key);
        Statuses.Queue.Clear(key);
    }

    /// <summary>Driven by <see cref="HudRuntime"/> once per frame (LateUpdate).</summary>
    internal static void Tick()
    {
        Draw(Toasts);
        Draw(Statuses);
    }

    internal static void ClearAll()
    {
        Toasts.Queue.ClearAll();
        Statuses.Queue.ClearAll();
    }

    private static void Draw(Channel channel)
    {
        float now = Time.unscaledTime;
        var visible = channel.Queue.Visible(now);
        if (LevelInfoHud.MenuOpen) visible = System.Array.Empty<HudMessageQueue<Color>.Message>(); // menus cover the HUD
        for (int i = 0; i < Mathf.Max(visible.Count, channel.Slots.Count); i++)
        {
            string id = "notify." + channel.Name + "." + i;
            if (i >= visible.Count)
            {
                var (unused, _) = channel.Slots[i];
                if (unused != null) unused.gameObject.SetActive(false);
                HudStack.Release(id);
                continue;
            }
            var (surface, label) = Slot(channel, i);
            if (surface == null) continue;
            var message = visible[i];
            surface.gameObject.SetActive(true);
            label.text = message.Text;
            var colour = message.Color;
            colour.a *= HudMessageQueue<Color>.Alpha(message, now);
            label.color = colour;
            surface.Panel.sizeDelta = new Vector2(channel.Width, channel.Height);
            // Newest is closest to the stack's edge: bottom for toasts, top for statuses.
            HudStack.Place(surface, channel.Corner, id, i);
        }
    }

    private static (NativeHudPanel surface, TextMeshProUGUI label) Slot(Channel channel, int index)
    {
        while (channel.Slots.Count <= index) channel.Slots.Add((null, null));
        var slot = channel.Slots[index];
        if (slot.surface != null && !slot.surface.HasCurrentOwner)
        {
            Object.Destroy(slot.surface.gameObject);
            slot = (null, null);
        }
        if (slot.surface == null)
        {
            var surface = NativeHudPanel.Create("IHM_Notify_" + channel.Name + index, new Vector2(channel.Width, channel.Height),
                new Vector2(.5f, 0f), Vector2.zero, 0f);
            if (surface == null) return (null, null);
            var label = surface.Label("Message", channel.FontSize, TextAlignmentOptions.Center);
            label.richText = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 2f);
            label.rectTransform.offsetMax = new Vector2(-12f, -2f);
            slot = (surface, label);
        }
        channel.Slots[index] = slot;
        return slot;
    }
}
