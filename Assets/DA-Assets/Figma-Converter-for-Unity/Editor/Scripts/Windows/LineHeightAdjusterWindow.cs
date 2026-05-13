namespace DA_Assets.FCU
{
    /// <summary>
    /// Shared line height adjuster window for both TMP and UITK issues.
    /// Specific text is supplied through <see cref="LineHeightAdjusterWindowData"/>,
    /// and the concrete adjustment is resolved from the runtime issue type.
    /// </summary>
    internal class LineHeightAdjusterWindow : LineHeightAdjusterWindowBase<LineHeightAdjusterWindow>
    {
        protected override void ApplyAdjustment(LineHeightAdjustmentIssue issue)
        {
#if TextMeshPro
            if (issue is TmpLineHeightAdjustmentIssue tmpIssue && tmpIssue.FontAsset != null)
            {
                LineHeightAdjuster.ApplyAndSaveTmpLineHeight(
                    tmpIssue.FontAsset,
                    monoBeh.Settings.TextFontsSettings.LineHeightMode);
                return;
            }
#endif

            if (issue is UitkLineHeightAdjustmentIssue uitkIssue && uitkIssue.FontAsset != null)
            {
                LineHeightAdjuster.ApplyAndSaveUitkLineHeight(
                    uitkIssue.FontAsset,
                    monoBeh.Settings.TextFontsSettings.LineHeightMode);
            }
        }
    }
}
