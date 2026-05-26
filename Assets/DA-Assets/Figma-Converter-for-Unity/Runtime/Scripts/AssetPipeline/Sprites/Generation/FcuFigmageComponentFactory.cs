using DA_Assets.FCU.Model;
using DA_Assets.Figmage;
using UnityEngine;
using UnityEngine.UI;
using FigmageComponent = DA_Assets.Figmage.Figmage;

namespace DA_Assets.FCU
{
    public static class FcuFigmageComponentFactory
    {
        public static FigmageComponent Create(FObject fobject, RectTransform parent, Rect? renderBoundsOverride = null)
        {
            FigmageNode node = FcuFigmageNodeMapper.ToFigmageNode(fobject);
            GameObject gameObject = new GameObject(string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name, typeof(RectTransform), typeof(Image), typeof(FigmageComponent));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            FigmageComponent figmage = gameObject.GetComponent<FigmageComponent>();
            figmage.ApplyNode(node, renderBoundsOverride);
            return figmage;
        }

        public static void Apply(FigmageComponent figmage, FObject fobject, Rect? renderBoundsOverride = null)
        {
            FigmageNode node = FcuFigmageNodeMapper.ToFigmageNode(fobject);
            figmage.ApplyNode(node, renderBoundsOverride);
        }
    }
}
