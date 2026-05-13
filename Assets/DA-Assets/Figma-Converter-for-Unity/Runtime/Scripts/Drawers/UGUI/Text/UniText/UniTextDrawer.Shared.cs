using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System.Collections.Generic;
using UnityEngine;

#if UNITEXT
using LightSide;
using Style = DA_Assets.FCU.Model.Style;
#endif

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    public partial class UniTextDrawer
    {
#if UNITEXT
        /// <summary>
        /// Returns the effective Style for a given character index by merging
        /// the default style with the per-character override from styleOverrideTable.
        /// Key 0 means no override (use default).
        /// </summary>
        private static Style GetEffectiveStyle(
            int charIndex,
            Style defaultStyle,
            List<int> overrides,
            Dictionary<string, Style> overrideTable,
            int[] charToOverrideIndex = null)
        {
            int overrideIndex = charIndex;
            if (charToOverrideIndex != null && charIndex >= 0 && charIndex < charToOverrideIndex.Length)
                overrideIndex = charToOverrideIndex[charIndex];

            if (overrideIndex >= overrides.Count)
                return defaultStyle;

            int key = overrides[overrideIndex];
            if (key == 0)
                return defaultStyle;

            if (overrideTable.TryGetValue(key.ToString(), out Style ov))
            {
                Style result = defaultStyle;

                result.LetterSpacing = ov.LetterSpacing;

                if (ov.LineHeightPx > 0)
                    result.LineHeightPx = ov.LineHeightPx;

                if (ov.FontSize > 0)
                    result.FontSize = ov.FontSize;

                if (!ov.FontFamily.IsEmpty())
                    result.FontFamily = ov.FontFamily;

                if (!ov.FontStyle.IsEmpty())
                    result.FontStyle = ov.FontStyle;

                if (!ov.FontPostScriptName.IsEmpty())
                    result.FontPostScriptName = ov.FontPostScriptName;

                if (ov.FontWeight > 0)
                    result.FontWeight = ov.FontWeight;

                if (ov.Italic.HasValue)
                    result.Italic = ov.Italic;

                result.TextDecoration = ov.TextDecoration;
                result.TextCase = ov.TextCase;

                if (ov.Fills != null && ov.Fills.Count > 0)
                    result.Fills = ov.Fills;

                if (!string.IsNullOrEmpty(ov.Hyperlink.Url))
                    result.Hyperlink = ov.Hyperlink;

                return result;
            }

            return defaultStyle;
        }

        /// <summary>
        /// Maps each UTF-16 char index to the corresponding override index used by
        /// CharacterStyleOverrides. Surrogate pairs share a single override slot.
        /// </summary>
        private static int[] BuildCharToOverrideIndexMap(string chars)
        {
            if (string.IsNullOrEmpty(chars))
                return System.Array.Empty<int>();

            int[] map = new int[chars.Length];
            int overrideIndex = 0;
            int i = 0;

            while (i < chars.Length)
            {
                map[i] = overrideIndex;

                if (char.IsHighSurrogate(chars[i])
                    && i + 1 < chars.Length
                    && char.IsLowSurrogate(chars[i + 1]))
                {
                    map[i + 1] = overrideIndex;
                    i += 2;
                }
                else
                {
                    i++;
                }

                overrideIndex++;
            }

            return map;
        }

        /// <summary>
        /// Extracts the first SOLID fill color from Style.Fills as Color32.
        /// Returns transparent black if no solid fill exists.
        /// </summary>
        private static Color32 GetSolidFillColor32(Style style)
        {
            if (style.Fills != null)
            {
                foreach (Paint fill in style.Fills)
                {
                    if (fill.Type == PaintType.SOLID)
                        return fill.Color;
                }
            }

            return new Color32(0, 0, 0, 0);
        }

        /// <summary>
        /// Returns the hyperlink URL from a Style, or null if none.
        /// </summary>
        private static string GetHyperlinkUrl(Style style)
        {
            return string.IsNullOrEmpty(style.Hyperlink.Url) ? null : style.Hyperlink.Url;
        }

        /// <summary>
        /// Converts a Color32 to hex string in #RRGGBBAA format.
        /// </summary>
        private static string Color32ToHex(Color32 c)
        {
            return $"#{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}";
        }

        /// <summary>
        /// Compares two Color32 values for exact byte-level equality.
        /// </summary>
        private static bool Color32Equals(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        /// <summary>
        /// Iterates character overrides and groups consecutive runs with equal <typeparamref name="T"/> values.
        /// For each run where <paramref name="include"/> returns true, adds an entry to the returned list.
        /// Eliminates the duplicated while-loop pattern across all Populate* partial classes.
        /// </summary>
        private static List<RangeRule.Data> BuildRunRanges<T>(
            FObject fobject,
            System.Func<Style, T> selector,
            System.Func<T, string> toParameter,
            System.Func<T, bool> include,
            System.Collections.Generic.IEqualityComparer<T> comparer = null)
        {
            comparer ??= EqualityComparer<T>.Default;

            var result = new List<RangeRule.Data>();

            Style defaultStyle = fobject.Style;
            var overrides = fobject.CharacterStyleOverrides;
            var overrideTable = fobject.StyleOverrideTable;
            string chars = fobject.GetText();
            int len = chars.Length;

            bool hasOverrides = overrides != null && overrides.Count > 0
                                && overrideTable != null && overrideTable.Count > 0;

            if (!hasOverrides)
                return result;

            int[] charToOverrideIndex = BuildCharToOverrideIndexMap(chars);
            int i = 0;
            while (i < len)
            {
                Style eff = GetEffectiveStyle(i, defaultStyle, overrides, overrideTable, charToOverrideIndex);
                T value = selector(eff);
                int runStart = i;
                i++;

                while (i < len)
                {
                    T next = selector(GetEffectiveStyle(i, defaultStyle, overrides, overrideTable, charToOverrideIndex));
                    if (!comparer.Equals(next, value))
                        break;

                    i++;
                }

                if (include(value))
                    result.Add(new RangeRule.Data { range = $"{runStart}..{i}", parameter = toParameter(value) });
            }

            return result;
        }
#endif
    }
}
