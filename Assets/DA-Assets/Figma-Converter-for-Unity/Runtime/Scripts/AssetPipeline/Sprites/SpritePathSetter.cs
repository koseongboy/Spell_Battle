using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#pragma warning disable CS1998

namespace DA_Assets.FCU
{
    [Serializable]
    public class SpritePathSetter : FcuBase
    {
        public async Task SetSpritePaths(List<FObject> fobjects, SpriteIdentityCache cache, CancellationToken token)
        {
#if UNITY_EDITOR
            await Task.Yield();

            string[] assetSpritePaths;

            if (monoBeh.IsPlaying())
            {
                string root = Path.Combine(
                   Application.persistentDataPath,
                   monoBeh.Settings.ImageSpritesSettings.SpritesPath);

                assetSpritePaths = Directory.Exists(root)
                    ? Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                               .ToArray()
                    : Array.Empty<string>();
            }
            else
            {
                string[] searchInFolder = new string[]
                {
                    monoBeh.Settings.ImageSpritesSettings.SpritesPath
                };

                assetSpritePaths = FindSpriteAssetPaths(searchInFolder);
            }

            // Build a single O(1) lookup: renderKey -> existing asset path on disk.
            // This replaces the per-item linear scan of assetSpritePaths.
            cache.BuildExistingSpritePathLookup(assetSpritePaths);

            IReadOnlyList<FObject> uniqueRepresentatives = cache.UniqueRepresentatives;
            HashSet<string> reservedSpritePaths = new HashSet<string>(assetSpritePaths, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < uniqueRepresentatives.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                FObject item = uniqueRepresentatives[i];
                int renderKey = cache.GetRenderKey(item);

                bool imageFileExists;
                string spritePath;

                if (cache.TryGetExistingPath(renderKey, out string existingPath) &&
                    IsTargetExtension(item, existingPath))
                {
                    // Found a file for this render-key. Reuse its path, but redownload/regenerate
                    // if it was created from a smaller representative.
                    imageFileExists = IsExistingSpriteLargeEnough(item, existingPath);
                    spritePath = existingPath;
                }
                else
                {
                    // No matching file on disk — generate a target path.
                    imageFileExists = false;
                    spritePath = GetSpritePath(item, reservedSpritePaths);
                }

                reservedSpritePaths.Add(spritePath);
                SetNeedDownloadFileFlag(item, imageFileExists);
                SetNeedGenerateFlag(item, imageFileExists);

                // Propagate to all objects that share the same render-key (O(1) group lookup).
                IReadOnlyList<FObject> group = cache.GetGroup(renderKey);
                foreach (FObject fo in group)
                {
                    fo.Data.SpritePath = spritePath;
                }

                // Re-apply download/generate flags to all group members (representative already done above).
                for (int j = 1; j < group.Count; j++)
                {
                    SetNeedDownloadFileFlag(group[j], imageFileExists);
                    SetNeedGenerateFlag(group[j], imageFileExists);
                }

                if (i % 500 == 0)
                {
                    await Task.Yield();
                }
            }
#endif
        }

#if UNITY_EDITOR
        private static string[] FindSpriteAssetPaths(string[] searchInFolder)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", searchInFolder))
            {
                paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            }

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets($"t:{typeof(Sprite).Name}", searchInFolder))
            {
                paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            }

            return paths.ToArray();
        }
#endif

        private string GetSpritePath(FObject fobject, HashSet<string> reservedSpritePaths)
        {
            string spriteDir = fobject.Data.IsMutual
                ? "Mutual"
                : fobject.Data.RootFrame.Names.FolderName;

            string root = monoBeh.IsPlaying()
                ? Path.Combine(Application.persistentDataPath, monoBeh.Settings.ImageSpritesSettings.SpritesPath)
                : monoBeh.Settings.ImageSpritesSettings.SpritesPath.GetFullAssetPath();

            string absoluteFramePath = Path.Combine(root, spriteDir);
            absoluteFramePath.CreateFolderIfNotExists();

            string fileName = fobject.Data.Names.FileName;

            string spritePath = monoBeh.IsPlaying()
                ? Path.Combine(absoluteFramePath, fileName)
                : Path.Combine(monoBeh.Settings.ImageSpritesSettings.SpritesPath, spriteDir, fileName).ToUnityPath();

            return GetAvailableSpritePath(spritePath, reservedSpritePaths);
        }

        private string GetAvailableSpritePath(string spritePath, HashSet<string> reservedSpritePaths)
        {
            if (reservedSpritePaths.Add(spritePath))
                return spritePath;

            string directory = Path.GetDirectoryName(spritePath);
            string fileName = Path.GetFileNameWithoutExtension(spritePath);
            string extension = Path.GetExtension(spritePath);

            int index = 1;
            while (true)
            {
                string candidate = Path.Combine(directory, $"{fileName}-{index}{extension}");
                if (!monoBeh.IsPlaying())
                    candidate = candidate.ToUnityPath();

                if (reservedSpritePaths.Add(candidate))
                    return candidate;

                index++;
            }
        }

        private bool IsTargetExtension(FObject fobject, string spritePath)
        {
            string spriteExt = Path.GetExtension(spritePath);

            if (spriteExt.StartsWith(".") && spriteExt.Length > 1)
                spriteExt = spriteExt.Remove(0, 1);

            ImageFormat? targetExt = null;

            if (monoBeh.UsingSvgImage())
            {
                if (fobject.CanUseUnityImage(monoBeh))
                {
                    targetExt = ImageFormat.PNG;
                }
            }

            if (targetExt == null)
            {
                targetExt = monoBeh.Settings.ImageSpritesSettings.ImageFormat;
            }

            return spriteExt.ToLower() == targetExt.ToLower();
        }

#if UNITY_EDITOR
        private bool IsExistingSpriteLargeEnough(FObject fobject, string spritePath)
        {
            UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(spritePath) as UnityEditor.TextureImporter;
            if (importer == null)
                return false;

            importer.GetTextureSize(out int width, out int height);
            Vector2Int expectedSize = GetExpectedSpriteSize(fobject);

            return width >= expectedSize.x && height >= expectedSize.y;
        }
#endif

        private Vector2Int GetExpectedSpriteSize(FObject fobject)
        {
            Vector2 sourceSize = GetSpriteSourceSize(fobject);
            float scale = GetMaxAllowedScale(sourceSize);

            int width = Mathf.CeilToInt(sourceSize.x * scale);
            int height = Mathf.CeilToInt(sourceSize.y * scale);

            return new Vector2Int(width, height);
        }

        private Vector2 GetSpriteSourceSize(FObject fobject)
        {
            fobject.GetBoundingSize(out Vector2 boundingSize);
            fobject.GetRenderSize(out Vector2 renderSize);

            float width = Mathf.Max(boundingSize.x, renderSize.x, fobject.Size.x);
            float height = Mathf.Max(boundingSize.y, renderSize.y, fobject.Size.y);

            return new Vector2(width, height);
        }

        private float GetMaxAllowedScale(Vector2 imageSize)
        {
            Vector2 maxSpriteSizeSettings = monoBeh.Settings.ImageSpritesSettings.MaxSpriteSize;
            float scaleX = imageSize.x > 0 ? maxSpriteSizeSettings.x / imageSize.x : float.MaxValue;
            float scaleY = imageSize.y > 0 ? maxSpriteSizeSettings.y / imageSize.y : float.MaxValue;

            float maxScaleBySpriteSize = Mathf.Min(scaleX, scaleY);
            maxScaleBySpriteSize = Mathf.Max(FcuConfig.IMAGE_SCALE_MIN, maxScaleBySpriteSize);

            float clampedScale = Mathf.Clamp(maxScaleBySpriteSize, FcuConfig.IMAGE_SCALE_MIN, FcuConfig.IMAGE_SCALE_MAX);
            float roundedScale = (float)Math.Round(clampedScale, FcuConfig.Rounding.MaxAllowedScale);

            return Mathf.Min(roundedScale, monoBeh.Settings.ImageSpritesSettings.ImageScale);
        }

        // Kept for internal use by GetSpritePath; public overload with string[] removed
        // since all callers now use SpriteIdentityCache.TryGetExistingPath.
        public bool GetSpritePath(FObject fobject, string[] spritePathes, out string path)
        {
            int renderKey = SpriteRenderKeyUtility.GetSpriteRenderKey(fobject);

            foreach (string spritePath in spritePathes)
            {
                if (!IsTargetExtension(fobject, spritePath))
                {
                    continue;
                }

                if (!GuidMetaUtility.TryExtractData(
                     spritePath + ".meta",
                     out int hash))
                {
                    continue;
                }

                if (SpriteRenderKeyUtility.MatchesPackedGuid(renderKey, hash))
                {
                    path = spritePath;
                    return true;
                }
            }

            path = null;
            return false;
        }

        private void SetNeedDownloadFileFlag(FObject fobject, bool imageFileExists)
        {
            if (fobject.IsDownloadableType()/* || fobject.IsGenerativeType()*/)
            {
                if (monoBeh.Settings.ImageSpritesSettings.RedownloadSprites)
                {
                    fobject.Data.NeedDownload = true;
                }
                else if (imageFileExists)
                {
                    fobject.Data.NeedDownload = false;
                }
                else
                {
                    fobject.Data.NeedDownload = true;
                }
            }
            else
            {
                fobject.Data.NeedDownload = false;
            }
        }

        private void SetNeedGenerateFlag(FObject fobject, bool imageFileExists)
        {
            if (fobject.IsGenerativeType())
            {
                if (monoBeh.Settings.ImageSpritesSettings.RedownloadSprites)
                {
                    fobject.Data.NeedGenerate = true;
                }
                else if (imageFileExists)
                {
                    fobject.Data.NeedGenerate = false;
                }
                else
                {
                    fobject.Data.NeedGenerate = true;
                }
            }
            else
            {
                fobject.Data.NeedGenerate = false;
            }
        }
    }
}
