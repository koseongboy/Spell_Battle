using DA_Assets.DAI;
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static DA_Assets.DAI.DAInspectorUITK;

namespace DA_Assets.FCU
{
    public partial class FcuEditor
    {
        private VisualElement BuildUrlAndActionsRow()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart,
                    backgroundColor = uitk.ColorScheme.CLEAR,
                    flexWrap = Wrap.Wrap
                }
            };

            bool isOfflineMode = monoBeh.Settings.MainSettings.ImportMode == ImportMode.Zip;
            var inputContainer = BuildInputContainer(isOfflineMode);

            var btnRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = uitk.ColorScheme.CLEAR,
                    flexShrink = 0,
                    flexWrap = Wrap.Wrap
                }
            };

            _btnRecent = uitk.SquareIconButton(
                ShowRecentProjectsPopup_OnClick,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.ImgViewRecent),
                FcuLocKey.tooltip_recent_projects.Localize());

            var btnDownload = uitk.SquareIconButton(
                OnDownloadButtonClick,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.IconDownload),
                FcuLocKey.tooltip_download_project.Localize());

            var btnImport = uitk.SquareIconButton(
                monoBeh.EditorEventHandlers.ImportSelectedFrames_OnClick,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.IconImport),
                FcuLocKey.tooltip_import_frames.Localize());

            var btnStop = uitk.SquareIconButton(
                monoBeh.EditorEventHandlers.StopImport_OnClick,
                gui.Resources.IconStop,
                FcuLocKey.tooltip_stop_import.Localize());

            var btnSettings = uitk.SquareIconButton(
                () => EditorApplication.delayCall += SettingsWindow.Show,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.IconSettings),
                FcuLocKey.tooltip_open_settings_window.Localize());

            void UpdateSettingsVisibility()
            {
                bool show = !monoBeh.Settings.MainSettings.WindowMode;
                SetDisplay(btnSettings, show);
            }

            var btnToggle = uitk.SquareIconButton(
                ToggleWindowModeAndRebuild,
                EditorTextureUtils.RecolorToEditorSkin(gui.Resources.IconExpandWindow),
                FcuLocKey.tooltip_change_window_mode.Localize());

            var spaceAfterRecent = uitk.Space5();

            btnRow.Add(_btnRecent);
            btnRow.Add(spaceAfterRecent);
            btnRow.Add(btnDownload);
            btnRow.Add(uitk.Space5());
            btnRow.Add(btnImport);
            btnRow.Add(uitk.Space5());
            btnRow.Add(btnStop);
            btnRow.Add(uitk.Space5());
            btnRow.Add(btnSettings);

            _btnRecent.style.display = isOfflineMode ? DisplayStyle.None : DisplayStyle.Flex;
            spaceAfterRecent.style.display = isOfflineMode ? DisplayStyle.None : DisplayStyle.Flex;

            UpdateSettingsVisibility();

            btnRow.Add(uitk.Space5());
            btnRow.Add(btnToggle);

            row.Add(inputContainer);
            row.Add(uitk.Space5());
            row.Add(btnRow);
            return row;
        }

        private void OnDownloadButtonClick()
        {
            bool offline = monoBeh.Settings.MainSettings.ImportMode == ImportMode.Zip;

            if (offline)
            {
                if (string.IsNullOrEmpty(monoBeh.Settings.MainSettings.OfflineArchivePath))
                {
                    Debug.Log("[FCU Offline] Please drop a ZIP archive first.");
                    return;
                }
            }
            else
            {
                if (!monoBeh.Authorizer.IsAuthed())
                {
                    Debug.Log(FcuLocKey.log_not_authorized.Localize());
                    return;
                }

                if (string.IsNullOrEmpty(monoBeh.Settings.MainSettings.ProjectId))
                {
                    Debug.Log(FcuLocKey.log_incorrent_project_url.Localize());
                    return;
                }
            }

            monoBeh.EditorEventHandlers.DownloadProject_OnClick();
        }

        private VisualElement BuildInputContainer(bool isOfflineMode)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minWidth = 50,
                    flexBasis = 0
                }
            };

            if (isOfflineMode)
                container.Add(BuildOfflineDropZone());
            else
                container.Add(BuildOnlineUrlField());

            return container;
        }

        private VisualElement BuildOfflineDropZone()
        {
            var dropZone = uitk.DropZoneElement("Drop ZIP archive here", ".zip");
            dropZone.style.height = 32;
            dropZone.style.marginTop = 0;
            dropZone.style.marginBottom = 0;
            dropZone.style.borderTopLeftRadius = 0;
            dropZone.style.borderTopRightRadius = 0;
            dropZone.style.borderBottomLeftRadius = 0;
            dropZone.style.borderBottomRightRadius = 0;

            void SetTransparentBackground()
            {
                dropZone.style.backgroundColor = uitk.ColorScheme.CLEAR;
            }

            SetTransparentBackground();
            dropZone.RegisterCallback<DragLeaveEvent>(_ => SetTransparentBackground());
            dropZone.RegisterCallback<DragExitedEvent>(_ => SetTransparentBackground());
            dropZone.RegisterCallback<DragPerformEvent>(_ => SetTransparentBackground());

            string currentPath = monoBeh.Settings.MainSettings.OfflineArchivePath;
            if (!string.IsNullOrEmpty(currentPath))
                dropZone.SetDroppedFile(System.IO.Path.GetFileName(currentPath));

            dropZone.OnFilesDropped += (paths) =>
            {
                if (paths.Length > 0)
                {
                    monoBeh.Settings.MainSettings.OfflineArchivePath = paths[0];
                    dropZone.SetDroppedFile(System.IO.Path.GetFileName(paths[0]));
                    Debug.Log($"[FCU Offline] Archive selected: {paths[0]}");
                }
            };

            return dropZone;
        }

        private VisualElement BuildOnlineUrlField()
        {
            string url = monoBeh?.Settings?.MainSettings?.ProjectUrl;
            string val = url == null ? "" : url;

            _projectUrlField = new TextField
            {
                value = val,
                multiline = false,
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minWidth = 50,
                    flexBasis = 0,
                    height = 32,
                    backgroundColor = uitk.ColorScheme.CLEAR,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap
                }
            };

            _projectUrlField.ClearClassList();
            var input = _projectUrlField.Q<VisualElement>(null, "unity-text-field__input") ?? _projectUrlField.Q<VisualElement>(null, "unity-text-input");
            input?.ClearClassList();
            if (input != null)
            {
                input.style.backgroundColor = uitk.ColorScheme.CLEAR;
                input.style.height = Length.Percent(100);
                input.style.unityTextAlign = TextAnchor.MiddleLeft;
                input.style.paddingLeft = 8;
                input.style.paddingRight = 8;
                input.style.marginLeft = 0;
                input.style.marginRight = 0;
                input.style.overflow = Overflow.Hidden;
            }

            UIHelpers.SetRadius(_projectUrlField, 0);
            UIHelpers.SetBorderWidth(_projectUrlField, DAI_UitkConstants.BorderWidth);
            UIHelpers.SetBorderColor(_projectUrlField, uitk.ColorScheme.OUTLINE);
            UIHelpers.SetZeroMarginPadding(_projectUrlField);

            _projectUrlField.RegisterValueChangedCallback(e =>
            {
                monoBeh.Settings.MainSettings.ProjectUrl = e.newValue;
                _projectUrlField.SetValueWithoutNotify(monoBeh.Settings.MainSettings.ProjectUrl);
            });

            return _projectUrlField;
        }

        private void ShowRecentProjectsPopup_OnClick()
        {
            List<RecentProject> recentProjects = monoBeh.ProjectCacher.GetRecentProjects();

            List<GUIContent> options = new List<GUIContent>();

            if (recentProjects.IsEmpty())
            {
                options.Add(new GUIContent(
                    FcuLocKey.label_no_recent_projects.Localize(),
                    FcuLocKey.tooltip_no_recent_projects.Localize()));
            }
            else
            {
                foreach (RecentProject project in recentProjects)
                {
                    options.Add(new GUIContent(project.Name));
                }
            }

            Rect anchor = _btnRecent.worldBound;
            Rect pos = new Rect(anchor.xMin, anchor.yMax, 0, 0);
            EditorUtility.DisplayCustomMenu(pos, options.ToArray(), -1, (userData, ops, selected) =>
            {
                RecentProject recentProject = recentProjects[selected];
                monoBeh.Settings.MainSettings.ProjectUrl = recentProject.Url;
                _projectUrlField.SetValueWithoutNotify(monoBeh.Settings.MainSettings.ProjectUrl);
                monoBeh.EditorEventHandlers.DownloadProject_OnClick();
            }, null);
        }
    }
}
