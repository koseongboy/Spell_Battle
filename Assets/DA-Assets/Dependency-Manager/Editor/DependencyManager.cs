using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditorInternal;
using UnityEngine;

namespace DA_Assets.DM
{
    public partial class DependencyManager : AssetPostprocessor
    {
        private static bool _debug = false;

        // Lock object for serializing file read-modify-write operations.
        private static readonly object _fileLock = new object();

        private static readonly HashSet<string> _pendingUpmPackageInstalls = new HashSet<string>();

        #region Constants

        public const string PathNotFound = "Not found";
        internal const string EnabledManuallyLabel = "Enabled manually";
        internal const string DisabledManuallyLabel = "Disabled manually";
        internal const string SourceNotFoundLabel = "Source not found (likely built-in or in-memory)";
        internal const string DefaultAssemblyName = "Assembly-CSharp";
        internal const string GuidPrefix = "GUID:";
        internal const string ExtDll = ".dll";
        internal const string ExtCs = ".cs";
        internal const string ExtAsmdef = ".asmdef";
        internal const string MonoScriptFilter = "t:MonoScript";
        internal const string AsmdefAssetFilter = "t:AssemblyDefinitionAsset";
        internal const string StatusEnabled = "ENABLED";
        internal const string StatusDisabled = "DISABLED";

        #endregion

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            // Re-subscribe on every domain reload because delegates are reset after recompile.
            Events.registeredPackages += OnPackagesRegistered;

            // Full check on domain reload (covers project open, script changes, etc.)
            CheckAllDependencies();
        }

        private static void OnPackagesRegistered(PackageRegistrationEventArgs args)
        {
            // Triggered after package install/remove is complete and domain reload happened.
            if (args.added.Any() || args.removed.Any() || args.changedTo.Any())
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_package_change_detected.Localize());
                CheckAllDependencies();
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool assetsChanged =
                importedAssets.Any(IsRelevantAsset) ||
                deletedAssets.Any(IsRelevantAsset) ||
                movedAssets.Any(IsRelevantAsset);

            if (assetsChanged)
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_script_change_detected.Localize());
                CheckAllDependencies();
            }
        }

        private static bool IsRelevantAsset(string path)
        {
            return path.EndsWith(ExtDll, StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(ExtCs, StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(ExtAsmdef, StringComparison.OrdinalIgnoreCase);
        }

        public static void CheckSingleItem(DependencyItem item)
        {
            if (_debug)
                Debug.Log(DependencyManagerLocKey.log_manual_check_started.Localize(item.name));

            if (ProcessSingleItem(item))
            {
                ApplyDefines(GetAllItems());
                AssetDatabase.SaveAssets();
            }
            else
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_no_status_change.Localize(item.name));
            }
        }

        public static void CheckAllDependencies()
        {
            List<DependencyItem> allItems = GetAllItems();

            if (allItems.Count == 0)
            {
                return;
            }

            if (_debug)
                Debug.Log(DependencyManagerLocKey.log_dependency_items_found.Localize(allItems.Count));

            bool hasChanges = false;
            foreach (DependencyItem item in allItems)
            {
                if (ProcessSingleItem(item))
                {
                    hasChanges = true;
                }
            }

            // Always reconcile defines with current DI state.
            // SetDefines already skips PlayerSettings update when nothing changed,
            // so this won't cause unnecessary recompilations.
            ApplyDefines(allItems);

            if (hasChanges)
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_changes_detected.Localize());
                AssetDatabase.SaveAssets();
            }
            else
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_dependencies_up_to_date.Localize());
            }
        }

        public static IReadOnlyList<DependencyItem> GetDependencyItems()
        {
            return GetAllItems();
        }

        public static void ForceEnableDependency(DependencyItem item)
        {
            SetDependencyState(item, true);
        }

        public static void ForceDisableDependency(DependencyItem item)
        {
            SetDependencyState(item, false);
        }

        public static bool ProcessSingleItem(DependencyItem item)
        {
            if (string.IsNullOrWhiteSpace(item.TypeAndAssembly) || string.IsNullOrWhiteSpace(item.ScriptingDefineSymbol))
                return false;

            bool oldStatus = item.IsEnabled;
            string oldPath = item.ScriptPath;

            // 1. Resolve the type using unified search.
            var resolved = ResolveType(item);

            bool newStatus = resolved.found;
            string newPath = resolved.scriptPath ?? PathNotFound;

            if (!newStatus)
            {
                TryInstallUpmPackage(item);
            }

            // 2. Sync asmdef references (create/add or remove/delete).
            if (!string.IsNullOrWhiteSpace(item.GeneratedAsmdefName))
            {
                SyncAsmdefRefs(item, newStatus, resolved.asmdefPath, newPath);
            }

            // 2.5. Asmdef gate: block the define if any required asmdef is missing.
            if (newStatus && !item.AreAllRequiredAsmdefsPresent())
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_asmdef_not_ready.Localize(item.name));
                newStatus = false;
            }

            // 3. Manual override.
            if (item.DisabledManually && newStatus)
            {
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_manual_removal_protection.Localize(item.name));
                newStatus = false;
            }

            // 4. Save if changed.
            if (oldStatus != newStatus || oldPath != newPath)
            {
                string statusString = newStatus ? StatusEnabled : StatusDisabled;
                if (_debug)
                    Debug.Log(DependencyManagerLocKey.log_status_changed.Localize(item.name, item.ScriptingDefineSymbol, statusString, newPath));

                item.IsEnabled = newStatus;
                item.ScriptPath = newPath;
                EditorUtility.SetDirty(item);
                return true;
            }

            return false;
        }

        private static void TryInstallUpmPackage(DependencyItem item)
        {
            if (item == null ||
                item.DisabledManually ||
                !item.InstallUpmPackageAutomatically ||
                string.IsNullOrWhiteSpace(item.UpmPackageId))
            {
                return;
            }

            string packageId = item.UpmPackageId.Trim();

            if (!_pendingUpmPackageInstalls.Add(packageId))
            {
                return;
            }

            string itemName = item.name;
            Debug.Log($"Dependency Manager: dependency '{itemName}' was not found. Queuing UPM package install '{packageId}'.");
            EditorApplication.delayCall += () => StartUpmPackageInstall(packageId, itemName);
        }

        private static void StartUpmPackageInstall(string packageId, string itemName)
        {
            UnityEditor.PackageManager.Requests.AddRequest request;

            try
            {
                request = Client.Add(packageId);
            }
            catch (Exception ex)
            {
                _pendingUpmPackageInstalls.Remove(packageId);
                Debug.LogWarning($"Dependency Manager failed to start UPM package install '{packageId}': {ex.Message}");
                return;
            }

            if (_debug)
            {
                Debug.Log($"Dependency Manager is installing missing UPM package '{packageId}' for '{itemName}'.");
            }

            Debug.Log($"Dependency Manager: installing UPM package '{packageId}' for dependency '{itemName}'.");

            EditorApplication.CallbackFunction update = null;
            update = () =>
            {
                if (!request.IsCompleted)
                {
                    return;
                }

                EditorApplication.update -= update;
                _pendingUpmPackageInstalls.Remove(packageId);

                if (request.Status == StatusCode.Success)
                {
                    Debug.Log($"Dependency Manager: UPM package '{packageId}' was installed for dependency '{itemName}'. Unity will refresh dependencies after package registration.");
                }
                else
                {
                    string message = request.Error != null ? request.Error.message : "Unknown error";
                    Debug.LogWarning($"Dependency Manager: failed to install UPM package '{packageId}' for dependency '{itemName}': {message}");
                }
            };

            EditorApplication.update += update;
        }

        /// <summary>
        /// Sync asmdef references: ensure or clean up based on whether the dependency was found.
        /// </summary>
        public static void SyncAsmdefRefs(DependencyItem item, bool found, string asmdefPath, string scriptPath)
        {
            if (found)
            {
                string targetAsmdefPath = asmdefPath;

                if (targetAsmdefPath == null && !string.IsNullOrWhiteSpace(scriptPath) && scriptPath != PathNotFound)
                {
                    string scriptDir = Path.GetDirectoryName(scriptPath);
                    if (!string.IsNullOrEmpty(scriptDir))
                    {
                        string[] refGuids = ResolveAsmdefGuids(item.GeneratedAsmdefReferences);
                        targetAsmdefPath = CreateGeneratedAsmdef(scriptDir, item.GeneratedAsmdefName, refGuids);
                        if (_debug)
                            Debug.Log(DependencyManagerLocKey.log_asmdef_created.Localize(item.GeneratedAsmdefName, scriptDir));
                    }
                }

                if (targetAsmdefPath != null)
                {
                    string asmdefGuid = AssetDatabase.AssetPathToGUID(targetAsmdefPath);

                    if (!string.IsNullOrWhiteSpace(asmdefGuid))
                    {
                        EnsureDependentReferences(item.DependentAsmdefAssets, asmdefGuid);
                    }
                }

                // --- Editor asmdef ---
                if (!string.IsNullOrWhiteSpace(item.GeneratedEditorAsmdefName) && targetAsmdefPath != null)
                {
                    string runtimeDir = Path.GetDirectoryName(targetAsmdefPath).Replace('\\', '/');
                    string editorDir = Path.Combine(runtimeDir, "Editor").Replace('\\', '/');

                    // Only create if the Editor folder actually exists.
                    string editorFullDir = Path.GetFullPath(editorDir);
                    if (Directory.Exists(editorFullDir))
                    {
                        string runtimeAsmdefGuid = AssetDatabase.AssetPathToGUID(targetAsmdefPath);

                        // Build references: runtime asmdef + any additional editor refs.
                        var editorRefGuids = new List<string>();
                        if (!string.IsNullOrWhiteSpace(runtimeAsmdefGuid))
                            editorRefGuids.Add(runtimeAsmdefGuid);

                        string[] extraEditorGuids = ResolveAsmdefGuids(item.GeneratedEditorAsmdefReferences);
                        editorRefGuids.AddRange(extraEditorGuids);

                        CreateGeneratedAsmdef(
                            editorFullDir,
                            item.GeneratedEditorAsmdefName,
                            editorRefGuids.ToArray(),
                            new[] { "Editor" });

                        if (_debug)
                            Debug.Log(DependencyManagerLocKey.log_asmdef_created.Localize(
                                item.GeneratedEditorAsmdefName, editorDir));
                    }
                }
            }
            else
            {
                string generatedAsmdef = FindAsmdefByAssemblyName(item.GeneratedAsmdefName);
                string generatedGuid = generatedAsmdef != null
                    ? AssetDatabase.AssetPathToGUID(generatedAsmdef)
                    : null;

                if (!string.IsNullOrWhiteSpace(generatedGuid))
                {
                    RemoveDependentReferences(item.DependentAsmdefAssets, generatedGuid);
                }

                if (generatedAsmdef != null)
                {
                    AssetDatabase.DeleteAsset(generatedAsmdef);
                    if (_debug)
                        Debug.Log(DependencyManagerLocKey.log_asmdef_deleted.Localize(generatedAsmdef));
                }

                // --- Cleanup Editor asmdef ---
                if (!string.IsNullOrWhiteSpace(item.GeneratedEditorAsmdefName))
                {
                    string generatedEditorAsmdef = FindAsmdefByAssemblyName(item.GeneratedEditorAsmdefName);
                    if (generatedEditorAsmdef != null)
                    {
                        AssetDatabase.DeleteAsset(generatedEditorAsmdef);
                        if (_debug)
                            Debug.Log(DependencyManagerLocKey.log_asmdef_deleted.Localize(generatedEditorAsmdef));
                    }
                }
            }
        }

        public static List<DependencyItem> GetAllItems()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(DependencyItem)}");
            List<DependencyItem> items = new List<DependencyItem>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<DependencyItem>(path);

                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private static string[] ResolveAsmdefGuids(List<AssemblyDefinitionAsset> assets)
        {
            if (assets == null || assets.Count == 0)
                return new string[0];

            var guids = new List<string>(assets.Count);
            foreach (AssemblyDefinitionAsset asset in assets)
            {
                if (asset == null) continue;
                string path = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(path)) continue;
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                    guids.Add(guid);
            }
            return guids.ToArray();
        }

        private static void SetDependencyState(DependencyItem item, bool isEnabled)
        {
            if (item == null)
            {
                return;
            }

            bool shouldSave = false;

            if (isEnabled)
            {
                if (item.DisabledManually || !item.IsEnabled)
                {
                    item.DisabledManually = false;
                    item.IsEnabled = true;
                    shouldSave = true;
                }

                if (string.IsNullOrEmpty(item.ScriptPath))
                {
                    item.ScriptPath = EnabledManuallyLabel;
                    shouldSave = true;
                }
            }
            else
            {
                if (!item.DisabledManually || item.IsEnabled)
                {
                    item.DisabledManually = true;
                    item.IsEnabled = false;
                    shouldSave = true;
                }

                if (string.IsNullOrEmpty(item.ScriptPath) || item.ScriptPath == EnabledManuallyLabel)
                {
                    item.ScriptPath = DisabledManuallyLabel;
                    shouldSave = true;
                }
            }

            if (!shouldSave)
            {
                return;
            }

            EditorUtility.SetDirty(item);
            ApplyDefines(GetAllItems());
            AssetDatabase.SaveAssets();
        }
    }


    [Serializable]
    internal struct VersionDefineData
    {
        public string name;
        public string expression;
        public string define;
    }

    [Serializable]
    internal struct AsmdefData
    {
        public string name;
        public string rootNamespace;
        public string[] references;
        public string[] includePlatforms;
        public string[] excludePlatforms;
        public bool allowUnsafeCode;
        public bool overrideReferences;
        public string[] precompiledReferences;
        public bool autoReferenced;
        public string[] defineConstraints;
        public VersionDefineData[] versionDefines;
        public bool noEngineReferences;
    }
}
