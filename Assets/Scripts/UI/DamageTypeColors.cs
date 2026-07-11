using UnityEngine;

/// <summary>
/// Canonical display colors for damage types, used anywhere a panel names a
/// damage type. Core three match the balance-type conventions (Physical red,
/// Arcane blue, Elemental ember); text variants are brightened for legibility
/// on dark panels.
/// </summary>
public static class DamageTypeColors
{
    public static Color Of(DamageType type)
    {
        switch (type)
        {
            case DamageType.Physical:  return new Color(1.00f, 0.35f, 0.30f);
            case DamageType.Arcane:    return new Color(0.45f, 0.62f, 1.00f);
            case DamageType.Elemental: return new Color(0.78f, 0.56f, 0.34f);
            case DamageType.Piercing:  return new Color(1.00f, 0.60f, 0.10f);
            case DamageType.Poison:    return new Color(0.55f, 1.00f, 0.45f);
            case DamageType.Pure:      return new Color(1.00f, 0.85f, 0.25f);
            default:                   return new Color(0.75f, 0.75f, 0.75f);
        }
    }

    /// <summary>Rich-text colored type name, e.g. "&lt;color=#8CFF73&gt;Poison&lt;/color&gt;".</summary>
    public static string Tag(DamageType type) =>
        $"<color=#{ColorUtility.ToHtmlStringRGB(Of(type))}>{type}</color>";
}
