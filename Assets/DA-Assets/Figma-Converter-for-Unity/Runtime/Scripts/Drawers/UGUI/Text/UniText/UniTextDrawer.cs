using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITEXT
using LightSide;
using UniTextStyle = LightSide.Style;
#endif

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    [Serializable]
    public partial class UniTextDrawer : FcuBase
    {
#if UNITEXT
        // All modifier types managed by this drawer.
        private static readonly Type[] managedModifierTypes = new Type[]
        {
            typeof(LineHeightModifier),
            typeof(LetterSpacingModifier),
            typeof(SizeModifier),
            typeof(FontModifier),
            typeof(BoldModifier),
            typeof(ItalicModifier),
            typeof(UppercaseModifier),
            typeof(LowercaseModifier),
            typeof(SmallCapsModifier),
            typeof(ColorModifier),
            typeof(UnderlineModifier),
            typeof(StrikethroughModifier),
            typeof(LinkModifier),
            typeof(EllipsisModifier),
            typeof(ShadowModifier),
            typeof(OutlineModifier),
            typeof(GradientModifier),
        };

        public UniText Draw(FObject fobject)
        {
            fobject.Data.GameObject.TryAddGraphic(out UniText text);

            text.raycastTarget = monoBeh.Settings.UniTextSettings.RaycastTarget;

            UnregisterManagedModifiers(text);
            SetStyle(text, fobject);
            SetFont(text, fobject);

            text.HorizontalAlignment = fobject.GetUniTextHAlign();
            text.VerticalAlignment = fobject.GetUniTextVAlign();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(text);
#endif
            return text;
        }

        private void SetFont(UniText text, FObject fobject)
        {
            UniTextFontStack fontStack = ResolveFontStack(fobject);

            if (fontStack != null)
            {
                text.FontStack = fontStack;
            }
        }

        private void SetStyle(UniText text, FObject fobject)
        {
            text.AutoSize = false;
            text.MinFontSize = 1;
            text.MaxFontSize = fobject.Style.FontSize;
            text.WordWrap = monoBeh.Settings.UniTextSettings.WordWrap;

            // Figma uses the LeadingAbove model (all extra leading placed above the line).
            text.LeadingDistribution = LeadingDistribution.LeadingAbove;

            text.RenderMode = GetRenderMode(fobject);

            // Compute base color first so it can be propagated to both text.color and PopulateColor.
            FGraphic graphic = fobject.Data.Graphic;
            Color baseColor = default;

            if (graphic.Fill.HasSolid)
            {
                baseColor = graphic.Fill.SolidPaint.Color;
                text.color = baseColor;
            }
            else if (graphic.Fill.HasGradient)
            {
                List<GradientColorKey> gradientColorKeys = graphic.Fill.GradientPaint.ToGradientColorKeys();

                if (!gradientColorKeys.IsEmpty())
                {
                    baseColor = gradientColorKeys.First().color;
                    text.color = baseColor;
                }
            }

            PopulateVerticalTrim(text, fobject);
            PopulateLineHeightAndLetterSpacing(text, fobject);
            PopulateSize(text, fobject);
            PopulateFontModifier(text, fobject);
            PopulateBold(text, fobject);
            PopulateItalic(text, fobject);
            PopulateTextCase(text, fobject);
            // Pass baseColor so PopulateColor compares overrides against the value already set in text.color.
            PopulateColor(text, fobject, (Color32)baseColor);
            PopulateUnderline(text, fobject);
            PopulateStrikethrough(text, fobject);
            PopulateLink(text, fobject, (Color32)baseColor);
            PopulateEllipsis(text, fobject);
            PopulateShadow(text, fobject);
            PopulateOutline(text, fobject);
            PopulateGradient(text, fobject);

            // Apply TITLE case pre-transformation before setting the text.
            // This must happen after PopulateTextCase (which handles UPPER/LOWER/SMALL_CAPS via modifiers)
            // because TITLE case has no dedicated modifier — text content is transformed directly.
            string finalText = ApplyTitleCase(fobject.GetText(), fobject.Style.TextCase);
            text.Text = finalText;
            text.FontSize = fobject.Style.FontSize;
        }

        /// <summary>
        /// Removes all previously managed modifier registrations from the UniText component.
        /// </summary>
        private static void UnregisterManagedModifiers(UniText text)
        {
            var toRemove = new List<UniTextStyle>();

            foreach (var style in text.Styles)
            {
                if (style.Modifier != null && Array.IndexOf(managedModifierTypes, style.Modifier.GetType()) >= 0)
                    toRemove.Add(style);
            }

            foreach (var style in toRemove)
                text.RemoveStyle(style);
        }

        /// <summary>
        /// Creates a RangeRule paired with the given modifier and registers it.
        /// </summary>
        private static RangeRule RegisterRangeRule(UniText text, BaseModifier modifier)
        {
            var rule = new RangeRule();
            var style = new UniTextStyle { Modifier = modifier, Rule = rule };
            text.AddStyle(style);
            return rule;
        }

        /// <summary>
        /// Registers a modifier with pre-collected data only if the data list is not empty.
        /// </summary>
        private static void RegisterIfHasData(UniText text, BaseModifier modifier, List<RangeRule.Data> data)
        {
            if (data.Count == 0)
                return;

            RegisterRangeRule(text, modifier).data.AddRange(data);
        }

#endif
    }
}
