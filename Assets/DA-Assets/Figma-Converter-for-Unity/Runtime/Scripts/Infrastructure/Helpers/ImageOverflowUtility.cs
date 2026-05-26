using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_2021_3_OR_NEWER && IMAGE_OVERFLOW_EXISTS
using DA_Assets.ImgOverflow;

namespace DA_Assets.FCU
{
    internal static class ImageOverflowUtility
    {
        private const float OverflowEpsilon = 0.01f;

        public static bool ShouldUseImageOverflow(FObject fobject, FigmaConverterUnity fcu)
        {
#if UNITY_2021_3_OR_NEWER
            return IsSupportedTarget(fobject, fcu) && TryGetOverflowPixels(fobject, out _);
#else
            return false;
#endif
        }

        public static void ApplyToImage(FObject fobject, FigmaConverterUnity fcu, GameObject target)
        {
#if UNITY_2021_3_OR_NEWER
            if (!ShouldUseImageOverflow(fobject, fcu) ||
                !target.TryGetComponent(out Image image) ||
                image.sprite == null ||
                image.type != Image.Type.Simple)
            {
                target.TryDestroyComponent<ImageOverflow>();
                return;
            }

            if (!TryGetOverflowPixels(fobject, out Vector4 overflow))
            {
                target.TryDestroyComponent<ImageOverflow>();
                return;
            }

            target.TryAddComponent(out ImageOverflow imageOverflow);

            imageOverflow.SetOverflowPixels(
                left: overflow.x,
                right: overflow.y,
                top: overflow.z,
                bottom: overflow.w);
#endif
        }

        private static bool IsSupportedTarget(FObject fobject, FigmaConverterUnity fcu)
        {
            if (!fcu.IsUGUI() || fcu.UsingRawImage())
            {
                return false;
            }

            if (!fobject.IsSprite())
            {
                return false;
            }

            if (fobject.Type == NodeType.TEXT)
            {
                return false;
            }

            if (fobject.ContainsTag(FcuTag.Slice9) || fobject.ContainsTag(FcuTag.AutoSlice9))
            {
                return false;
            }

            if (HasSelfOrAncestorRotation(fobject, fcu))
            {
                return false;
            }

            return fcu.UsingUnityImage() || fobject.CanUseUnityImage(fcu) || fobject.IsObjectMask();
        }

        private static bool HasSelfOrAncestorRotation(FObject fobject, FigmaConverterUnity fcu)
        {
            if (fobject.Data == null)
            {
                return false;
            }

            float selfAngle = fobject.Data.HasTransformComputationCache
                ? fobject.Data.CachedMatrixAngle
                : fobject.GetAngleFromMatrix();

            if (Mathf.Abs(selfAngle) > 0.001f)
            {
                return true;
            }

            int parentIndex = fobject.Data.ParentIndex;

            while (parentIndex >= 0 && fcu.CurrentProject.TryGetByIndex(parentIndex, out FObject parent))
            {
                float parentAngle = parent.Data != null && parent.Data.HasTransformComputationCache
                    ? parent.Data.CachedMatrixAngle
                    : parent.GetAngleFromMatrix();

                if (Mathf.Abs(parentAngle) > 0.001f)
                {
                    return true;
                }

                if (parent.Data == null)
                {
                    break;
                }

                parentIndex = parent.Data.ParentIndex;
            }

            return false;
        }

        private static bool TryGetOverflowPixels(FObject fobject, out Vector4 overflow)
        {
            overflow = default;

            if (!TryGetRect(fobject.AbsoluteBoundingBox, out Rect contentBounds) ||
                !TryGetRect(fobject.AbsoluteRenderBounds, out Rect renderBounds))
            {
                return false;
            }

            float left = Mathf.Max(0f, contentBounds.xMin - renderBounds.xMin);
            float right = Mathf.Max(0f, renderBounds.xMax - contentBounds.xMax);
            float top = Mathf.Max(0f, contentBounds.yMin - renderBounds.yMin);
            float bottom = Mathf.Max(0f, renderBounds.yMax - contentBounds.yMax);

            left = ClearTinyOverflow(left);
            right = ClearTinyOverflow(right);
            top = ClearTinyOverflow(top);
            bottom = ClearTinyOverflow(bottom);

            overflow = new Vector4(left, right, top, bottom);
            return left > 0f || right > 0f || top > 0f || bottom > 0f;
        }

        private static float ClearTinyOverflow(float value)
        {
            return value > OverflowEpsilon ? value : 0f;
        }

        private static bool TryGetRect(BoundingBox box, out Rect rect)
        {
            rect = default;

            if (!box.X.HasValue ||
                !box.Y.HasValue ||
                !box.Width.HasValue ||
                !box.Height.HasValue ||
                box.Width.Value <= 0f ||
                box.Height.Value <= 0f)
            {
                return false;
            }

            rect = new Rect(box.X.Value, box.Y.Value, box.Width.Value, box.Height.Value);
            return true;
        }
    }
}
#endif
