using DA_Assets.FCU.Model;
using DA_Assets.Figmage;
using UnityEngine;

namespace DA_Assets.FCU
{
    public static class FcuFigmageSpriteBaker
    {
        public static byte[] BakePngBytes(FObject fobject, float scale)
        {
            return BakePngBytes(fobject, scale, null);
        }

        public static byte[] BakePngBytes(FObject fobject, float scale, Rect? canvasBoundsOverride)
        {
            FigmageNode node = FcuFigmageNodeMapper.ToFigmageNode(fobject);
            return FigmageBakeUtility.BakePngBytes(node, scale, canvasBoundsOverride);
        }

        public static void BakePng(FObject fobject, float scale, string outputPath, Rect? canvasBoundsOverride = null)
        {
            FigmageNode node = FcuFigmageNodeMapper.ToFigmageNode(fobject);
            FigmageBakeUtility.BakePng(node, outputPath, scale, canvasBoundsOverride);
        }
    }
}
