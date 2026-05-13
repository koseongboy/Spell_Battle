#if UNITY_EDITOR

using DA_Assets.FCU.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DA_Assets.FCU
{
    /// <summary>
    /// Shared logic for showing the line height adjuster window.
    /// Handles the wait loop, play-mode skip, and delegate null-check.
    /// </summary>
    public static class LineHeightAdjusterWindowHelperBase
    {
        public static bool CanShowWindow(
            FigmaConverterUnity monoBeh,
            LineHeightAdjusterWindowData data,
            Action<LineHeightAdjusterWindowData, Action<LineHeightAdjusterWindowResult>> showDelegate)
        {
            if (monoBeh.IsPlaying())
            {
                return false;
            }

            if (showDelegate == null)
            {
                return false;
            }

            if (data.Fonts == null || data.Fonts.Count == 0)
            {
                return false;
            }

            return true;
        }

        public static async Task<LineHeightAdjusterWindowResult> ShowWindow(
            FigmaConverterUnity monoBeh,
            LineHeightAdjusterWindowData data,
            Action<LineHeightAdjusterWindowData, Action<LineHeightAdjusterWindowResult>> showDelegate,
            CancellationToken token)
        {
            var result = new LineHeightAdjusterWindowResult
            {
                Action = LineHeightAdjusterAction.None
            };

            if (!CanShowWindow(monoBeh, data, showDelegate))
            {
                return new LineHeightAdjusterWindowResult
                {
                    Action = LineHeightAdjusterAction.ContinueImport
                };
            }

            await monoBeh.AssetTools.ReselectFcu(token);

            showDelegate(data, output => result = output);

            while (result.Action == LineHeightAdjusterAction.None)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1000, token);
            }

            return result;
        }
    }
}
#endif
