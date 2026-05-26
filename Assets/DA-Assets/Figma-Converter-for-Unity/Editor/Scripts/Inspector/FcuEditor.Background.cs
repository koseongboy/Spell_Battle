using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    public partial class FcuEditor
    {
        private VisualElement TopBannerOverlayCompat(Texture2D tex)
        {
            var wrap = new VisualElement()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = new StyleEnum<Position>(Position.Absolute),
                    left = new StyleLength(0f),
                    right = new StyleLength(0f),
                    top = new StyleLength(0f),
                    width = new StyleLength(Length.Percent(100)),
                    height = new StyleLength(StyleKeyword.Auto)
                }
            };

            if (tex == null)
            {
                wrap.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                return wrap;
            }

#if UNITY_6000_1_OR_NEWER
            var img = new Image()
            {
                image = tex,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = new StyleLength(Length.Percent(100)),
                    height = new StyleLength(StyleKeyword.Auto)
                }
            };

            img.style.aspectRatio = new StyleRatio((float)tex.width / tex.height);
            wrap.Add(img);
#else
            float aspect = (float)tex.height / tex.width;
            var img = new IMGUIContainer(() =>
            {
                float width = wrap.resolvedStyle.width;
                float height = wrap.resolvedStyle.height;

                if (Event.current.type == EventType.Repaint && width > 0f && height > 0f)
                {
                    GUI.DrawTexture(new Rect(0f, 0f, width, height), tex, ScaleMode.StretchToFill, true);
                }
            })
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = new StyleEnum<Position>(Position.Absolute),
                    left = new StyleLength(0f),
                    right = new StyleLength(0f),
                    top = new StyleLength(0f),
                    bottom = new StyleLength(0f)
                }
            };

            void Resize()
            {
                float width = wrap.parent?.resolvedStyle.width ?? 0f;
                if (float.IsNaN(width) || width <= 0f)
                {
                    width = wrap.resolvedStyle.width;
                }

                if (float.IsNaN(width) || width <= 0f)
                {
                    return;
                }

                wrap.style.width = new StyleLength(width);
                wrap.style.height = new StyleLength(width * aspect);
                img.MarkDirtyRepaint();
            }

            wrap.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                wrap.schedule.Execute(Resize);
                wrap.parent?.RegisterCallback<GeometryChangedEvent>(_ => Resize());
            });
            wrap.RegisterCallback<GeometryChangedEvent>(_ => Resize());
            wrap.Add(img);
#endif

            return wrap;
        }

        private VisualElement GradientBackgroundCompat(Texture2D tex)
        {
            var wrap = new VisualElement()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = new StyleEnum<Position>(Position.Absolute),
                    left = new StyleLength(0f),
                    right = new StyleLength(0f),
                    top = new StyleLength(0f),
                    bottom = new StyleLength(0f)
                }
            };

            if (tex == null)
            {
                wrap.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                return wrap;
            }

            var img = new IMGUIContainer(() =>
            {
                float width = wrap.resolvedStyle.width;
                float height = wrap.resolvedStyle.height;

                if (Event.current.type == EventType.Repaint && width > 0f && height > 0f)
                {
                    GUI.DrawTexture(new Rect(0f, 0f, width, height), tex, ScaleMode.StretchToFill, true);
                }
            })
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = new StyleEnum<Position>(Position.Absolute),
                    left = new StyleLength(0f),
                    right = new StyleLength(0f),
                    top = new StyleLength(0f),
                    bottom = new StyleLength(0f)
                }
            };

            wrap.RegisterCallback<GeometryChangedEvent>(_ => img.MarkDirtyRepaint());
            wrap.Add(img);
            return wrap;
        }

        private VisualElement NoiseOverlayCompat(Texture2D tex)
        {
            var wrap = new VisualElement()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = new StyleEnum<Position>(Position.Absolute),
                    left = new StyleLength(0f),
                    right = new StyleLength(0f),
                    top = new StyleLength(0f),
                    bottom = new StyleLength(0f)
                }
            };

            if (tex == null)
            {
                wrap.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                return wrap;
            }

            var img = new IMGUIContainer(() =>
            {
                float width = wrap.resolvedStyle.width;
                float height = wrap.resolvedStyle.height;

                if (Event.current.type != EventType.Repaint || width <= 0f || height <= 0f)
                {
                    return;
                }

                float tileW = Mathf.Max(1f, tex.width / EditorGUIUtility.pixelsPerPoint);
                float tileH = Mathf.Max(1f, tex.height / EditorGUIUtility.pixelsPerPoint);

                for (float y = 0f; y < height; y += tileH)
                {
                    for (float x = 0f; x < width; x += tileW)
                    {
                        GUI.DrawTexture(new Rect(x, y, tileW, tileH), tex, ScaleMode.StretchToFill, true);
                    }
                }
            })
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = new StyleEnum<Position>(Position.Absolute),
                    left = new StyleLength(0f),
                    right = new StyleLength(0f),
                    top = new StyleLength(0f),
                    bottom = new StyleLength(0f)
                }
            };

            wrap.RegisterCallback<GeometryChangedEvent>(_ => img.MarkDirtyRepaint());
            wrap.Add(img);
            return wrap;
        }

        private void AddDecorativeBackground(VisualElement root, bool includeTopBanner)
        {
            if (FcuConfig.ShowDecorativeEditorBackground == false)
            {
                return;
            }

            root.Add(GradientBackgroundCompat(_gradientBg));

            if (includeTopBanner)
            {
                root.Add(TopBannerOverlayCompat(_topBanner));
            }

            root.Add(NoiseOverlayCompat(_noise));
        }
    }
}
