using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DA_Assets.FCU.MCP
{
    internal static class FcuMcpContext
    {
        private const string SelectedFcuKeyPrefix = "FCU.MCP.SelectedFcu.";

        public static IReadOnlyList<FcuMcpInstanceInfo> GetInstances()
        {
            return UnityEngine.Object
                .FindObjectsOfType<FigmaConverterUnity>(true)
                .Where(x => x != null && x.gameObject.scene.IsValid())
                .Select(CreateInfo)
                .OrderBy(x => x.scene)
                .ThenBy(x => x.path)
                .ToList();
        }

        public static FigmaConverterUnity GetSelectedOrThrow()
        {
            string id = GetSelectedId();
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException("No FCU instance selected. Call list_fcu_instances, ask the user which FCU to use, then call select_fcu_instance.");
            }

            FigmaConverterUnity selected = UnityEngine.Object
                .FindObjectsOfType<FigmaConverterUnity>(true)
                .FirstOrDefault(x => x != null && x.Guid == id);

            if (selected == null)
            {
                throw new InvalidOperationException($"Selected FCU instance '{id}' was not found in loaded scenes. Call list_fcu_instances and select_fcu_instance again.");
            }

            selected.InitServices();
            return selected;
        }

        public static bool TrySelect(string id, out FcuMcpInstanceInfo selected)
        {
            selected = default;

            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            FcuMcpInstanceInfo match = GetInstances().FirstOrDefault(x => x.id == id);
            if (match == null)
            {
                return false;
            }

            EditorPrefs.SetString(GetSelectedKey(), id);
            selected = match;
            selected.is_active = true;
            return true;
        }

        public static string GetSelectedId()
        {
            return EditorPrefs.GetString(GetSelectedKey(), string.Empty);
        }

        private static FcuMcpInstanceInfo CreateInfo(FigmaConverterUnity fcu)
        {
            string id = fcu.Guid;
            return new FcuMcpInstanceInfo
            {
                id = id,
                name = fcu.name,
                scene = string.IsNullOrEmpty(fcu.gameObject.scene.path) ? fcu.gameObject.scene.name : fcu.gameObject.scene.path,
                path = GetPath(fcu.transform),
                is_active = id == GetSelectedId()
            };
        }

        private static string GetPath(Transform transform)
        {
            var names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string GetSelectedKey()
        {
            string projectId = Hash128.Compute(Application.dataPath).ToString();
            return $"{SelectedFcuKeyPrefix}{projectId}";
        }
    }

    [Serializable]
    internal class FcuMcpInstanceInfo
    {
        public string id;
        public string name;
        public string scene;
        public string path;
        public bool is_active;
    }
}
