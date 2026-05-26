using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using UnityEngine.UI;

namespace DA_Assets.FCU.Drawers.CanvasDrawers
{
    [Serializable]
    public class LayoutElementDrawer : FcuBase
    {
        public void Draw(FObject fobject, FObject parent)
        {
            fobject.Data.GameObject.TryAddComponent(out LayoutElement layoutElement);

            layoutElement.minWidth = -1;
            layoutElement.minHeight = -1;
            layoutElement.preferredWidth = -1;
            layoutElement.preferredHeight = -1;
            layoutElement.flexibleWidth = -1;
            layoutElement.flexibleHeight = -1;

            if (fobject.LayoutPositioning == LayoutPositioning.ABSOLUTE)
            {
                layoutElement.ignoreLayout = true;
                return;
            }

            layoutElement.ignoreLayout = false;

            bool isSpaceBetween = parent.PrimaryAxisAlignItems == PrimaryAxisAlignItem.SPACE_BETWEEN;

            if (isSpaceBetween)
            {
                // Inside SPACE_BETWEEN, use minWidth/minHeight along primary axis
                // so spacer objects can occupy remaining free space.
                if (parent.LayoutMode == LayoutMode.HORIZONTAL)
                {
                    layoutElement.minWidth = fobject.Size.x;
                }
                else if (parent.LayoutMode == LayoutMode.VERTICAL)
                {
                    layoutElement.minHeight = fobject.Size.y;
                }
            }
            else if (fobject.ContainsTag(FcuTag.Text))
            {
                // For text in non-SPACE_BETWEEN layouts, set preferred sizes
                // to ensure parent auto-layout computes correct dimensions
                // regardless of actual font metric differences.
                layoutElement.preferredWidth = fobject.Size.x;
                layoutElement.preferredHeight = fobject.Size.y;
            }

            if (parent.LayoutWrap == LayoutWrap.WRAP)
            {
                layoutElement.minWidth = fobject.Size.x;
                layoutElement.minHeight = fobject.Size.y;
                layoutElement.preferredWidth = fobject.Size.x;
                layoutElement.preferredHeight = fobject.Size.y;
            }

            if (!isSpaceBetween && parent.LayoutWrap != LayoutWrap.WRAP && fobject.LayoutGrow == 1)
            {
                if (parent.LayoutMode == LayoutMode.HORIZONTAL)
                {
                    layoutElement.minWidth = 0;
                    layoutElement.preferredWidth = 0;
                    layoutElement.flexibleWidth = 1;
                }
                else if (parent.LayoutMode == LayoutMode.VERTICAL)
                {
                    layoutElement.minHeight = 0;
                    layoutElement.preferredHeight = 0;
                    layoutElement.flexibleHeight = 1;
                }
            }
        }
    }
}
