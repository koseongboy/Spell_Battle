using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    public static class SpriteDataCalculator
    {
        public static async Task CalculateAndSetSpriteData(IEnumerable<FObject> fobjects, FigmaConverterUnity monoBeh, CancellationToken token)
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(fobjects.Where(x => x.IsSprite()), fobject =>
                {
                    fobject.Data.MaxSpriteSize = GetMaxSpriteSize(fobject);
                    fobject.Data.Scale = GetMaxAllowedScale(fobject, monoBeh);
                });
            }, token);
        }

        private static Vector2 GetMaxSpriteSize(FObject fobject)
        {
            fobject.GetBoundingSize(out Vector2 bSize);
            fobject.GetRenderSize(out Vector2 rSize);

            float maxX = Mathf.Max(bSize.x, rSize.x, fobject.Size.x);
            float maxY = Mathf.Max(bSize.y, rSize.y, fobject.Size.y);

            return new Vector2(maxX, maxY);
        }

        public static bool TryReduceScale(FObject fobject, FigmaConverterUnity monoBeh)
        {
            if (IsSvg(fobject, monoBeh))
            {
                fobject.Data.Scale = 1f;
                return false;
            }

            float currentScale = fobject.Data.Scale;
            if (currentScale <= FcuConfig.IMAGE_SCALE_MIN)
                return false;

            float reducedScale = Mathf.Max(FcuConfig.IMAGE_SCALE_MIN, currentScale * 0.5f);
            reducedScale = (float)System.Math.Round(reducedScale, FcuConfig.Rounding.MaxAllowedScale);

            if (reducedScale >= currentScale)
                return false;

            fobject.Data.Scale = reducedScale;
            return true;
        }

        private static float GetMaxAllowedScale(FObject fobject, FigmaConverterUnity monoBeh)
        {
            if (IsSvg(fobject, monoBeh))
                return 1f;

            Vector2 imageSize = fobject.Data.MaxSpriteSize;
            Vector2 maxSpriteSizeSettings = monoBeh.Settings.ImageSpritesSettings.MaxSpriteSize;
            float scaleX = imageSize.x > 0 ? maxSpriteSizeSettings.x / imageSize.x : float.MaxValue;
            float scaleY = imageSize.y > 0 ? maxSpriteSizeSettings.y / imageSize.y : float.MaxValue;

            float maxScaleBySpriteSize = Mathf.Min(scaleX, scaleY);
            maxScaleBySpriteSize = Mathf.Max(FcuConfig.IMAGE_SCALE_MIN, maxScaleBySpriteSize);

            float clampedScale = Mathf.Clamp(maxScaleBySpriteSize, FcuConfig.IMAGE_SCALE_MIN, FcuConfig.IMAGE_SCALE_MAX);
            float roundedScale = (float)System.Math.Round(clampedScale, FcuConfig.Rounding.MaxAllowedScale);

            return Mathf.Min(roundedScale, monoBeh.Settings.ImageSpritesSettings.ImageScale);
        }

        private static bool IsSvg(FObject fobject, FigmaConverterUnity monoBeh) =>
            monoBeh.UsingSVG() || fobject.Data.ImageFormat == ImageFormat.SVG;
    }

}
