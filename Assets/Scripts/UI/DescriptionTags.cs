using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>
/// Resolves {source:id.field} tokens in description strings against live
/// definition data, so displayed numbers can never drift from the JSON.
///
///   {effect:fence_damage.damageBase}            → 120
///   {behavior:rooted.duration}                  → 2.5
///   {ability:root_tower_shot.cost.cooldownDuration} → 2
///   {behavior:slowed.moveSpeedMultiplier|pct}   → 40%
///   {behavior:slowed.moveSpeedMultiplier|invpct}→ 60%
///   {behavior:vulnerable.damageTakenMultiplier|addpct} → +100%
///
/// Sources: effect, behavior, ability, tower, unit. Field paths may be
/// nested (dot-separated) and read public fields or properties.
/// Formats: raw number (default), pct (×100%), invpct ((1−x)×100%),
/// addpct ((x−1)×100% with sign). Unresolvable tokens render as "?" so
/// bad references are visible in-game instead of silently hidden.
/// </summary>
public static class DescriptionTags
{
    static readonly Regex TokenRx =
        new(@"\{(\w+):([\w-]+)\.([\w.]+)(?:\|(\w+))?\}", RegexOptions.Compiled);

    public static string Resolve(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;

        return TokenRx.Replace(text, m =>
        {
            object value = Lookup(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
            return value == null ? "?" : Format(value, m.Groups[4].Value);
        });
    }

    static object Lookup(string source, string id, string fieldPath)
    {
        object obj = null;
        switch (source)
        {
            case "effect":
                if (EffectLibrary.Instance != null && EffectLibrary.Instance.TryGet(id, out var e)) obj = e;
                break;
            case "behavior":
                if (BehaviorLibrary.Instance != null && BehaviorLibrary.Instance.TryGet(id, out var b)) obj = b;
                break;
            case "ability":
                if (AbilityLibrary.Instance != null && AbilityLibrary.Instance.TryGet(id, out var a)) obj = a;
                break;
            case "tower":
                if (TowerDefinitionLibrary.Instance != null && TowerDefinitionLibrary.Instance.TryGet(id, out var t)) obj = t;
                break;
            case "unit":
                if (UnitDefinitionLibrary.Instance != null && UnitDefinitionLibrary.Instance.TryGet(id, out var u)) obj = u;
                break;
        }
        if (obj == null) return null;

        // Walk the dotted path through public fields/properties
        foreach (string segment in fieldPath.Split('.'))
        {
            if (obj == null) return null;
            var type = obj.GetType();
            var f = type.GetField(segment, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { obj = f.GetValue(obj); continue; }
            var p = type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
            if (p != null) { obj = p.GetValue(obj); continue; }
            return null;
        }
        return obj;
    }

    static string Format(object value, string fmt)
    {
        if (value is float || value is double || value is int)
        {
            float v = System.Convert.ToSingle(value);
            switch (fmt)
            {
                case "pct":    return $"{v * 100f:0.#}%";
                case "invpct": return $"{(1f - v) * 100f:0.#}%";
                case "addpct": return $"{(v >= 1f ? "+" : "")}{(v - 1f) * 100f:0.#}%";
                default:       return $"{v:0.##}";
            }
        }
        return value.ToString();
    }
}
