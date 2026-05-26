using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;

#if UNITEXT
using LightSide;
using Style = DA_Assets.FCU.Model.Style;
#endif

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    public partial class UniTextDrawer
    {
#if UNITEXT
        private void PopulateItalic(UniText text, FObject fobject)
        {
            Style defaultStyle = fobject.Style;
            var overrides = fobject.CharacterStyleOverrides;
            var overrideTable = fobject.StyleOverrideTable;
            bool hasOverrides = overrides != null && overrides.Count > 0
                                && overrideTable != null && overrideTable.Count > 0;

            if (!hasOverrides)
            {
                FontMetadata defaultFont = defaultStyle.GetFontMetadata();

                if (monoBeh.FontLoader.NeedsUniTextItalicModifier(defaultFont))
                    RegisterRangeRule(text, new ItalicModifier())
                        .data.Add(new RangeRule.Data { range = string.Empty, parameter = string.Empty });

                return;
            }

            var data = BuildRunRanges(
                fobject,
                selector: style => style.GetFontMetadata(),
                toParameter: _ => string.Empty,
                include: metadata => monoBeh.FontLoader.NeedsUniTextItalicModifier(metadata));

            RegisterIfHasData(text, new ItalicModifier(), data);
        }
#endif
    }
}
