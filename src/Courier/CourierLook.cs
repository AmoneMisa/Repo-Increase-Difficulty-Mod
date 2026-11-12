using UnityEngine;

namespace IncreaseHardMod.Courier;

/// <summary>Append-only courier catalogue, recolour swatches and the local player's saved look.</summary>
internal static class CourierLook
{
    internal static readonly string[] Keys = { "penguin", "chicken", "cat", "owl", "beaver", "sheep", "cow", "dog", "fairy", "devil", "angel", "dragon", "mermaid", "skeleton", "elemental", "phoenix" };
    private static readonly string[] NamesEn = { "Penguin", "Chicken", "Cat", "Owl", "Beaver", "Sheep", "Cow", "Dog", "Fairy", "Little Devil", "Little Angel", "Dragon", "Little Mermaid", "Skeleton", "Elemental", "Phoenix" };
    private static readonly string[] NamesRu = { "Пингвин", "Курочка", "Котик", "Сова", "Бобёр", "Овечка", "Коровка", "Собачка", "Фея", "Чертёнок", "Ангелочек", "Дракончик", "Русалочка", "Скелет", "Элементаль", "Феникс" };

    internal static string Voice(int animal) => Keys[Mathf.Clamp(animal, 0, Keys.Length - 1)];

    internal static string Name(int animal) =>
        (Localization.GameLanguage.Russian ? NamesRu : NamesEn)[Mathf.Clamp(animal, 0, Keys.Length - 1)];

    // Swatch order matches the concept's palette strip: cream, yellow, orange, red, pink, violet, blue, cyan, mint, green, charcoal, brown.
    internal static readonly Color[] Swatches =
    {
        new(.93f, .89f, .8f), new(.98f, .8f, .22f), new(.96f, .5f, .16f), new(.85f, .2f, .18f), new(.92f, .45f, .7f), new(.55f, .35f, .85f),
        new(.25f, .45f, .9f), new(.35f, .8f, .85f), new(.45f, .85f, .55f), new(.45f, .7f, .2f), new(.17f, .17f, .19f), new(.45f, .3f, .18f),
    };
    private static readonly string[] SwatchEn = { "Cream", "Yellow", "Orange", "Red", "Pink", "Violet", "Blue", "Cyan", "Mint", "Green", "Charcoal", "Brown" };
    private static readonly string[] SwatchRu = { "Кремовый", "Жёлтый", "Оранжевый", "Красный", "Розовый", "Фиолетовый", "Синий", "Голубой", "Мятный", "Зелёный", "Графит", "Коричневый" };

    internal static string SwatchName(int slot) => slot == CourierRules.DefaultSwatch
        ? (Localization.GameLanguage.Russian ? "Родной" : "Natural")
        : (Localization.GameLanguage.Russian ? SwatchRu : SwatchEn)[Mathf.Clamp(slot, 0, Swatches.Length - 1)];

    /// <summary>The colour a slot shows: a swatch, or <paramref name="natural"/> for the animal's own colour.</summary>
    internal static Color Resolve(int slot, Color natural) =>
        slot >= 0 && slot < Swatches.Length ? Swatches[slot] : natural;

    internal static int Local
    {
        get => ModConfig.CourierLook.Value;
        set { if (ModConfig.CourierLook.Value != value) ModConfig.CourierLook.Value = value; }
    }
}
