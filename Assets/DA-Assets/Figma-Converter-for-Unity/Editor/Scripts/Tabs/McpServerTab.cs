using DA_Assets.DAI;
using DA_Assets.Shared.MCP;
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    internal class McpServerTab : MonoBehaviourLinkerEditor<FcuSettingsWindow, FigmaConverterUnity>, IDisposable
    {
        private McpServerConfig _config;
        private Label _statusLabel;
        private Button _toggleButton;
        private bool _lastRunningState;

        public VisualElement Draw()
        {
            var root = new VisualElement();

            _config = FcuConfig.McpServerConfig as McpServerConfig;

            if (_config == null)
            {
                root.Add(new HelpBox("MCP server config is not assigned.", HelpBoxMessageType.Warning));
                return root;
            }

            var inspector = new InspectorElement(_config);
            inspector.style.paddingLeft = DAI_UitkConstants.MarginPadding;
            inspector.style.paddingRight = DAI_UitkConstants.MarginPadding;
            inspector.style.paddingTop = 0;
            inspector.style.paddingBottom = 0;
            root.Add(inspector);
            root.Add(uitk.Space10());

            _statusLabel = new Label
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    marginLeft = DAI_UitkConstants.MarginPadding,
                    marginRight = DAI_UitkConstants.MarginPadding,
                    marginBottom = 6
                }
            };
            root.Add(_statusLabel);

            var hint = new HelpBox(
                "MCP starts only after manual Start. After that it auto-starts after Unity reloads until you press Stop.",
                HelpBoxMessageType.Info);
            hint.style.marginLeft = DAI_UitkConstants.MarginPadding;
            hint.style.marginRight = DAI_UitkConstants.MarginPadding;
            root.Add(hint);

            EditorApplication.update += OnUpdate;
            RefreshUi();

            return root;
        }

        public VisualElement DrawFooter()
        {
            var footer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row }
            };
            UIHelpers.SetDefaultPadding(footer);
            footer.style.paddingTop = DAI_UitkConstants.SpacingXXS * 2;

            _toggleButton = uitk.Button("", OnToggleServer);
            _toggleButton.style.flexGrow = 1;

            footer.Add(_toggleButton);

            RefreshUi();

            return footer;
        }

        private void OnToggleServer()
        {
            if (_config == null)
            {
                return;
            }

            if (McpServerManager.IsRunning(_config.name))
                McpServerManager.Stop(_config.name);
            else
                McpServerManager.Start(_config);

            RefreshUi();
        }

        private void OnUpdate()
        {
            if (_config == null)
            {
                return;
            }

            bool isRunning = McpServerManager.IsRunning(_config.name);
            if (isRunning != _lastRunningState)
            {
                RefreshUi();
            }
        }

        private void RefreshUi()
        {
            if (_config == null)
            {
                return;
            }

            bool isRunning = McpServerManager.IsRunning(_config.name);

            if (_statusLabel != null)
            {
                _statusLabel.text = isRunning
                    ? FcuLocKey.label_mcp_status_running.Localize($"http://{_config.Host}:{_config.Port}/")
                    : FcuLocKey.label_mcp_status_stopped.Localize();
            }

            if (_toggleButton != null)
            {
                _toggleButton.text = isRunning
                    ? FcuLocKey.label_mcp_stop.Localize()
                    : FcuLocKey.label_mcp_start.Localize();
            }

            _lastRunningState = isRunning;
        }

        public void Dispose()
        {
            EditorApplication.update -= OnUpdate;
        }
    }
}
