using System;
using System.IO;
using DA_Assets.Extensions;
using DA_Assets.Shared.Extensions;
using UnityEngine;

#if JSONNET_EXISTS
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

namespace DA_Assets.Shared.MCP
{
    /// <summary>
    /// Manages Python proxy files for IDE integrations.
    /// </summary>
    public static class McpProxyManager
    {
        private const string SharedProxyFileName = "DAAssetsMcpProxy.py";

        public static string GetIdeConfigPath(IdeName type)
        {
            var (_, configFile, _) = GetIdePaths(type);
            return configFile;
        }

        public static string GetIdeFolderPath(IdeName type)
        {
            var (dir, _, _) = GetIdePaths(type);
            return dir;
        }

        public static void UpdateProxies(McpServerConfig config)
        {
            if (config == null) return;

            string proxyContent = GetProxyTemplate();

            foreach (var ideName in config.SupportedAgents)
            {
                try
                {
                    if (McpIdePreferences.GetEnabled(config, ideName))
                        UpdateIdeConfig(ideName, config.ServerName, proxyContent, config.Host, config.Port);
                    else
                        RemoveIdeConfig(ideName, config.ServerName);
                }
                catch (Exception ex)
                {
                    Debug.LogError(SharedLocKey.log_mcp_failed_update.Localize("MCP", ideName, ex.Message));
                }
            }
        }

        public static void UpdateProxyForIde(McpServerConfig config, IdeName ideName)
        {
            if (config == null) return;

            string proxyContent = GetProxyTemplate();

            try
            {
                if (McpIdePreferences.GetEnabled(config, ideName))
                    UpdateIdeConfig(ideName, config.ServerName, proxyContent, config.Host, config.Port);
                else
                    RemoveIdeConfig(ideName, config.ServerName);
            }
            catch (Exception ex)
            {
                Debug.LogError(SharedLocKey.log_mcp_failed_update.Localize("MCP", ideName, ex.Message));
            }
        }

        public static void RemoveProxies(McpServerConfig config)
        {
            if (config == null) return;
            
            foreach (IdeName ideName in config.SupportedAgents)
                RemoveIdeConfig(ideName, config.ServerName);
        }

        private static void UpdateIdeConfig(IdeName ideName, string serverName, string proxyContent, string host, int port)
        {
            var (dir, configFile, isToml) = GetIdePaths(ideName);
            Directory.CreateDirectory(dir);

            string proxyPath = GetSharedProxyPath(dir);
            File.WriteAllText(proxyPath, proxyContent);
            DeleteLegacyProxy(dir, serverName);

            if (isToml)
                UpdateTomlConfig(configFile, serverName, proxyPath, host, port);
            else
                UpdateJsonConfig(configFile, serverName, proxyPath, host, port);

            Debug.Log(SharedLocKey.log_mcp_updated_config.Localize("MCP", ideName, serverName));
        }

        private static void RemoveIdeConfig(IdeName ideName, string serverName)
        {
            var (dir, configFile, isToml) = GetIdePaths(ideName);

            DeleteLegacyProxy(dir, serverName);

            if (isToml)
                RemoveFromToml(configFile, serverName);
            else
                RemoveFromJson(configFile, serverName);

            Debug.Log(SharedLocKey.log_mcp_updated_config.Localize("MCP", ideName, serverName));
        }

        private static (string dir, string configFile, bool isToml) GetIdePaths(IdeName ideName)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return ideName switch
            {
                IdeName.Antigravity => (
                    Path.Combine(home, ".gemini", "antigravity"),
                    Path.Combine(home, ".gemini", "antigravity", "mcp_config.json"),
                    false),
                IdeName.Codex => (
                    Path.Combine(home, ".codex"),
                    Path.Combine(home, ".codex", "config.toml"),
                    true),
                IdeName.Cursor => (
                    Path.Combine(home, ".cursor"),
                    Path.Combine(home, ".cursor", "mcp.json"),
                    false),
                IdeName.Coplay => (
                    Path.Combine(home, ".coplay"),
                    Path.Combine(UnityEngine.Application.dataPath, ".coplaymcp.json"),
                    false),
                IdeName.ClaudeCode => (
                    Path.Combine(home, ".claude"),
                    Path.Combine(home, ".claude.json"),
                    false),
                _ => throw new ArgumentException(SharedLocKey.error_mcp_unknown_tool_type.Localize(ideName))
            };
        }

        private static void UpdateJsonConfig(string path, string serverName, string proxyPath, string host, int port)
        {
#if JSONNET_EXISTS
            JObject config = File.Exists(path)
                ? TryParseJson(File.ReadAllText(path))
                : new JObject { ["mcpServers"] = new JObject() };

            var servers = config["mcpServers"] as JObject ?? new JObject();
            servers[serverName] = new JObject
            {
                ["command"] = SharedConfig.PythonCommand,
                ["args"] = new JArray { proxyPath.ToUnityPath(), "--url", BuildEndpoint(host, port), "--server-name", serverName }
            };
            config["mcpServers"] = servers;

            CreateConfigBackup(path);
            File.WriteAllText(path, config.ToString(Formatting.Indented));
#endif
        }

        private static void RemoveFromJson(string path, string serverName)
        {
#if JSONNET_EXISTS
            if (!File.Exists(path)) return;

            var config = TryParseJson(File.ReadAllText(path));
            if (config["mcpServers"] is JObject servers && servers.ContainsKey(serverName))
            {
                servers.Remove(serverName);
                CreateConfigBackup(path);
                File.WriteAllText(path, config.ToString(Formatting.Indented));
            }
#endif
        }

#if JSONNET_EXISTS
        private static JObject TryParseJson(string json)
        {
            try { return JObject.Parse(json); }
            catch { return new JObject { ["mcpServers"] = new JObject() }; }
        }
#endif
        private static void UpdateTomlConfig(string path, string serverName, string proxyPath, string host, int port)
        {
            string toml = File.Exists(path) ? File.ReadAllText(path) : "";
            string header = $"[mcp_servers.{serverName}]";
            string section = $"{header}\ncommand = \"{SharedConfig.PythonCommand}\"\nargs = [\"{proxyPath.ToUnityPath()}\", \"--url\", \"{BuildEndpoint(host, port)}\", \"--server-name\", \"{serverName}\"]";

            int start = toml.IndexOf(header, StringComparison.Ordinal);
            if (start >= 0)
            {
                int end = toml.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);
                toml = toml.Remove(start, (end < 0 ? toml.Length : end) - start).TrimEnd();
            }

            toml = (toml.Length > 0 ? toml + "\n\n" : "") + section + "\n";
            CreateConfigBackup(path);
            File.WriteAllText(path, toml);
        }

        private static void RemoveFromToml(string path, string serverName)
        {
            if (!File.Exists(path)) return;

            string toml = File.ReadAllText(path);
            string header = $"[mcp_servers.{serverName}]";

            int start = toml.IndexOf(header, StringComparison.Ordinal);
            if (start < 0) return;

            int end = toml.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);
            toml = toml.Remove(start, (end < 0 ? toml.Length : end) - start).Trim();
            
            CreateConfigBackup(path);
            File.WriteAllText(path, toml);
        }

        private static void CreateConfigBackup(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir)) return;

            Directory.CreateDirectory(dir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string backupFile = $"{baseName}_{timestamp}{extension}.bak";
            string backupPath = Path.Combine(dir, backupFile);
            string content = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

            File.WriteAllText(backupPath, content);
        }

        private static string GetSharedProxyPath(string dir) => Path.Combine(dir, SharedProxyFileName);

        private static string GetLegacyProxyPath(string dir, string serverName) => Path.Combine(dir, $"McpProxy_{serverName}.py");

        private static void DeleteLegacyProxy(string dir, string serverName)
        {
            string proxyPath = GetLegacyProxyPath(dir, serverName);
            if (File.Exists(proxyPath))
                File.Delete(proxyPath);
        }

        private static string BuildEndpoint(string host, int port) => $"http://{host}:{port}/";

        private static string GetProxyTemplate() => SharedConfig.PythonProxyTemplate?.text ?? "";

    }
}
