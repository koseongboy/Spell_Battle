using DA_Assets.FCU.Model;
using System;
using DA_Assets.FCU.Extensions;

#if UNITEXT
using LightSide;
#endif

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    public partial class UniTextDrawer
    {
#if UNITEXT
        /// <summary>
        /// Uses MSDF only when UniText can apply a visible supported outline with sharp joins.
        /// Rounded or absent outlines keep SDF rendering.
        /// </summary>
        private static UniTextRenderMode GetRenderMode(FObject fobject)
        {
            if (!TryGetVisibleSolidOutline(fobject, out _))
                return UniTextRenderMode.SDF;

            return string.Equals(fobject.StrokeJoin, "ROUND", StringComparison.OrdinalIgnoreCase)
                ? UniTextRenderMode.SDF
                : UniTextRenderMode.MSDF;
        }

        /// <summary>
        /// Returns the first visible solid stroke that UniText can convert into OutlineModifier.
        /// </summary>
        private static bool TryGetVisibleSolidOutline(FObject fobject, out Paint stroke)
        {
            stroke = default;

            if (fobject.StrokeWeight <= 0f || fobject.Strokes == null)
                return false;

            for (int i = 0; i < fobject.Strokes.Count; i++)
            {
                Paint candidate = fobject.Strokes[i];
                if (!candidate.IsVisible())
                    continue;

                if (candidate.Type != PaintType.SOLID)
                    continue;

                stroke = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Registers OutlineModifier from the first solid Stroke.
        /// </summary>
        private static void PopulateOutline(UniText text, FObject fobject)
        {
            if (!TryGetVisibleSolidOutline(fobject, out Paint stroke))
                return;

            string hex = Color32ToHex(stroke.Color);
            string parameter = FormattableString.Invariant($"{fobject.StrokeWeight},{hex}");

            RegisterRangeRule(text, new OutlineModifier { fixedPixelSize = true })
                .data.Add(new RangeRule.Data { range = string.Empty, parameter = parameter });
        }
#endif
    }
}
