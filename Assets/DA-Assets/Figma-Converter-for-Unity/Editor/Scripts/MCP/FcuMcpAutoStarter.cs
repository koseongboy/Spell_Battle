using DA_Assets.Shared.MCP;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace DA_Assets.FCU.MCP
{
    [InitializeOnLoad]
    internal static class FcuMcpAutoStarter
    {
        private static int _attempts;

        static FcuMcpAutoStarter()
        {
            QueueStart();
        }

        [InitializeOnLoadMethod]
        private static void QueueStart()
        {
            EditorApplication.update -= TryStart;
            EditorApplication.update += TryStart;
        }

        [DidReloadScripts]
        private static void QueueStartAfterScriptsReload()
        {
            QueueStart();
        }

        private static void TryStart()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            _attempts++;

            if (FcuConfig.McpServerConfig is McpServerConfig config)
            {
                EditorApplication.update -= TryStart;
                _attempts = 0;
                if (McpServerManager.ShouldAutoStart(config))
                {
                    McpServerManager.Start(config);
                    Debug.Log($"[FCU MCP] Auto-start requested for {config.ServerName} on {config.Host}:{config.Port}.");
                }
                return;
            }

            if (_attempts > 30)
            {
                EditorApplication.update -= TryStart;
                _attempts = 0;
                Debug.LogWarning("[FCU MCP] Auto-start skipped: MCP server config is not assigned.");
            }
        }
    }
}
