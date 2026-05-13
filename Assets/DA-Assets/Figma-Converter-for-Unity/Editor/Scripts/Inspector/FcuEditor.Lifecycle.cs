using DA_Assets.DAI;
using UnityEditor;

namespace DA_Assets.FCU
{
    public partial class FcuEditor
    {
        protected override void OnDisable()
        {
            base.OnDisable();

            FcuConfig.Instance.Localizator.OnLanguageChanged -= RebuildUI;
            FigmaConverterUnity.OnResetPerformed -= OnFcuReset;
            monoBeh.InspectorDrawer.OnFramesChanged -= RefreshFrames;
            monoBeh.InspectorDrawer.OnScrollContentUpdated -= this.FrameList.UpdateScrollContent;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            FcuConfig.Instance.Localizator.OnLanguageChanged += RebuildUI;
            FigmaConverterUnity.OnResetPerformed += OnFcuReset;
            monoBeh.InspectorDrawer.OnFramesChanged += RefreshFrames;
            monoBeh.InspectorDrawer.OnScrollContentUpdated += this.FrameList.UpdateScrollContent;

            monoBeh.EditorDelegateHolder.SetSpriteRects = SpriteEditorUtility.SetSpriteRects;
            monoBeh.EditorDelegateHolder.ShowDifferenceChecker = ShowDifferenceChecker;
            monoBeh.EditorDelegateHolder.ShowRateLimitWindow = ShowRateLimitDialog;
            monoBeh.EditorDelegateHolder.ShowLineHeightAdjusterWindow = ShowLineHeightAdjusterWindow;
            monoBeh.EditorDelegateHolder.ShowSpriteDuplicateFinder = ShowSpriteDuplicateFinder;
            monoBeh.EditorDelegateHolder.SetGameViewSize = GameViewUtils.SetGameViewSize;
            monoBeh.EditorDelegateHolder.StartProgress = (target, category, totalItems, indeterminate) =>
                EditorProgressBarManager.StartProgress(target, category, totalItems, indeterminate);
            monoBeh.EditorDelegateHolder.UpdateProgress = (target, category, itemsDone) =>
                EditorProgressBarManager.UpdateProgress(target, category, itemsDone);
            monoBeh.EditorDelegateHolder.CompleteProgress = (target, category) =>
                EditorProgressBarManager.CompleteProgress(target, category);
            monoBeh.EditorDelegateHolder.StopAllProgress = target =>
                EditorProgressBarManager.StopAllProgress(target);

            _ = monoBeh.Authorizer.TryRestoreSession();
        }

        private void OnFcuReset(FigmaConverterUnity resetInstance)
        {
            if (resetInstance == monoBeh)
            {
                ForceRebuild();
            }
        }
    }
}
