using DA_Assets.FCU.Model;
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DA_Assets.FCU.EditorTools
{
    public static class FcuShaderRuntimeEditorExport
    {
        [MenuItem("Tools/D.A. Assets/FCU: Run Figmage Runtime Export")]
        public static void Run()
        {
            GameObject runnerObject = new GameObject("FCU Shader Runtime Editor Export");

            try
            {
                FcuShaderRuntimeExportRunner runner = runnerObject.AddComponent<FcuShaderRuntimeExportRunner>();
                MethodInfo runExport = typeof(FcuShaderRuntimeExportRunner).GetMethod("RunExport", BindingFlags.Instance | BindingFlags.NonPublic);
                runExport.Invoke(runner, null);
                Debug.Log("FCU shader runtime editor export completed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runnerObject);
                AssetDatabase.Refresh();
            }
        }

        public static void RenderLayerBlurCanvasVariants()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string cacheRoot = Path.Combine(projectRoot, "CodexTemp", "Builds", "OutputFCU_recopied", "NodeJsonCache");
            string outputRoot = Path.Combine(projectRoot, "CodexTemp", "CanvasVariantTests");
            Directory.CreateDirectory(outputRoot);

            RenderCanvasVariants(cacheRoot, outputRoot, "494:87", "494:86", "Layer Blur 008 Progressive");
            RenderCanvasVariants(cacheRoot, outputRoot, "497:78", "497:77", "Layer Blur 068 Progressive O");

            AssetDatabase.Refresh();
            Debug.Log($"FCU canvas variant export completed. output={outputRoot}");
        }

        static void RenderCanvasVariants(string cacheRoot, string outputRoot, string nodeId, string parentNodeId, string folderName)
        {
            FigmaProject nodeProject = LoadProject(cacheRoot, nodeId);
            FigmaProject parentProject = LoadProject(cacheRoot, parentNodeId);

            if (!TryGetRect(nodeProject.Document.AbsoluteBoundingBox, out Rect childBBox))
                throw new InvalidOperationException($"Missing child bbox: {nodeId}");
            if (!TryGetRect(nodeProject.Document.AbsoluteRenderBounds, out Rect childRender))
                throw new InvalidOperationException($"Missing child render bounds: {nodeId}");
            if (!TryGetRect(parentProject.Document.AbsoluteBoundingBox, out Rect parentBBox))
                throw new InvalidOperationException($"Missing parent bbox: {parentNodeId}");
            if (!TryGetRect(parentProject.Document.AbsoluteRenderBounds, out Rect parentRender))
                throw new InvalidOperationException($"Missing parent render bounds: {parentNodeId}");

            string caseOutput = Path.Combine(outputRoot, folderName);
            Directory.CreateDirectory(caseOutput);

            RenderVariant(nodeProject.Document, Path.Combine(caseOutput, "none.png"), null);
            RenderVariant(nodeProject.Document, Path.Combine(caseOutput, "child_bbox.png"), childBBox);
            RenderVariant(nodeProject.Document, Path.Combine(caseOutput, "child_render.png"), childRender);
            RenderVariant(nodeProject.Document, Path.Combine(caseOutput, "parent_bbox.png"), parentBBox);
            RenderVariant(nodeProject.Document, Path.Combine(caseOutput, "parent_render.png"), parentRender);
        }

        static void RenderVariant(FObject node, string outputPath, Rect? canvasBounds)
        {
            FcuFigmageSpriteBaker.BakePng(node, 4f, outputPath, canvasBounds);
        }

        static FigmaProject LoadProject(string cacheRoot, string nodeId)
        {
            string path = Path.Combine(cacheRoot, $"{nodeId.Replace(':', '-')}.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("Cache not found.", path);

            return DAJson.FromJson<FigmaProject>(File.ReadAllText(path));
        }

        static bool TryGetRect(BoundingBox box, out Rect rect)
        {
            rect = default;

            if (!box.X.HasValue || !box.Y.HasValue || !box.Width.HasValue || !box.Height.HasValue)
                return false;

            rect = new Rect(box.X.Value, box.Y.Value, box.Width.Value, box.Height.Value);
            return true;
        }
    }
}
