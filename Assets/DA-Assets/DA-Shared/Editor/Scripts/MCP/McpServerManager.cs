using DA_Assets.Shared.Extensions;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace DA_Assets.Shared.MCP
{
    [InitializeOnLoad]
    public static class McpServerManager
    {
        private static readonly Dictionary<string, McpServer> _servers = new();

        private const string RunningPrefix = "MCP.Running.";
        private const string OwnerPrefix = "MCP.Owner.";

        [InitializeOnLoadMethod]
        private static void QueueStartAll()
        {
            EditorApplication.delayCall += StartAll;
        }

        [DidReloadScripts]
        private static void QueueStartAllAfterScriptsReload()
        {
            EditorApplication.delayCall += StartAll;
        }

        public static void StartAll()
        {
            foreach (McpServerConfig config in GetAllConfigs())
            {
                if (!ShouldAutoStart(config))
                {
                    continue;
                }

                try
                {
                    Start(config);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public static bool IsRunning(string configName) => 
            !string.IsNullOrEmpty(configName) && _servers.ContainsKey(configName) && _servers[configName].IsRunning;

        public static bool ShouldAutoStart(McpServerConfig config) =>
            config != null && EditorPrefs.GetBool(GetRunningKey(config.name), false);

        public static McpServer Start(McpServerConfig config)
        {
            if (config == null)
            {
                Debug.LogError(SharedLocKey.log_mcp_config_owner_null.Localize("MCP"));
                return null;
            }

            string name = config.name;

            if (_servers.ContainsKey(name))
                Stop(name);

            var server = new McpServer(config);

            config.RegisterTools(server);
            config.RegisterResources(server);

            server.Start();
            _servers[name] = server;
            EditorPrefs.SetBool(GetRunningKey(name), true);
            
            return server;
        }

        public static void Stop(string configName)
        {
            if (string.IsNullOrEmpty(configName)) return;

            if (_servers.TryGetValue(configName, out var server))
            {
                server.Stop();
                server.Dispose();
                _servers.Remove(configName);
            }
            
            ClearState(configName);
        }

        public static void StopAll()
        {
            foreach (var name in new List<string>(_servers.Keys))
                Stop(name);
        }

        public static UnityEngine.Object GetOwner(string configName)
        {
            return null;
        }

        public static IEnumerable<McpServerConfig> GetAllConfigs() => 
            Resources.LoadAll<McpServerConfig>("MCP");

        public static bool HasPortConflict(int port, string excludeConfig)
        {
            foreach (var config in GetAllConfigs())
            {
                if (config.name != excludeConfig && config.Port == port && IsRunning(config.name))
                    return true;
            }
            return false;
        }


        private static void SaveOwner(string configName, UnityEngine.Object owner)
        {
            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(owner);
            EditorPrefs.SetString(GetOwnerKey(configName), globalId.ToString());
        }

        private static UnityEngine.Object LoadOwner(string configName)
        {
            var str = EditorPrefs.GetString(GetOwnerKey(configName), "");
            if (string.IsNullOrEmpty(str)) return null;
            
            if (GlobalObjectId.TryParse(str, out var globalId))
                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
            
            return null;
        }

        private static void ClearState(string configName)
        {
            EditorPrefs.SetBool(GetRunningKey(configName), false);
            EditorPrefs.DeleteKey(GetOwnerKey(configName));
        }

        private static string GetRunningKey(string configName) => GetProjectScopedKey(RunningPrefix, configName);

        private static string GetOwnerKey(string configName) => GetProjectScopedKey(OwnerPrefix, configName);

        private static string GetProjectScopedKey(string prefix, string configName)
        {
            string projectId = Hash128.Compute(Application.dataPath).ToString();
            return $"{prefix}{projectId}.{configName}";
        }
    }
}
