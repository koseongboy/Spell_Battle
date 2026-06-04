#if UNITY_EDITOR
using System.IO;
using Models.Databases;
using UnityEditor;
using UnityEngine;

namespace DefaultNamespace.Editor
{
    public class CSVToIncantationGenerator : EditorWindow
    {
        [MenuItem("Tools/영창 자동 생성기")]
        public static void ShowWindow()
        {
            GetWindow<CSVToIncantationGenerator>("영창 자동 생성기");
        }

        private TextAsset csvFile;
        // 생성될 최상위 베이스 경로
        private string basePath = "Assets/Resources/Incantations"; 

        private void OnGUI()
        {
            GUILayout.Label("CSV 파일로 영창 SO 자동 생성", EditorStyles.boldLabel);

            csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV 파일", csvFile, typeof(TextAsset), false);

            if (GUILayout.Button("생성 시작"))
            {
                if (csvFile == null)
                {
                    Debug.LogError("CSV 파일을 등록해주세요!");
                    return;
                }
                GenerateIncantations();
            }
        }

        private void GenerateIncantations()
        {
            // 목표 폴더 경로
            string conceptPath = $"{basePath}/Concepts";
            string prefixPath = $"{basePath}/Prefixes";

            // 폴더가 없으면 자동 생성
            if (!AssetDatabase.IsValidFolder(conceptPath)) Directory.CreateDirectory(conceptPath);
            if (!AssetDatabase.IsValidFolder(prefixPath)) Directory.CreateDirectory(prefixPath);

            // 줄바꿈 단위로 분리 (1행은 헤더이므로 인덱스 1부터 시작)
            string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < lines.Length; i++)
            {
                string[] row = lines[i].Split(',');

                // 1. A열(0번 인덱스): 컨셉 파싱
                if (row.Length > 0 && !string.IsNullOrWhiteSpace(row[0]))
                {
                    string conceptText = row[0].Trim();
                    CreateAsset<ConceptData>(conceptPath, $"Concept_{i}", conceptText);
                }

                // 2. B열(1번 인덱스): 접두어 파싱
                if (row.Length > 1 && !string.IsNullOrWhiteSpace(row[1]))
                {
                    string prefixText = row[1].Trim();
                    CreateAsset<PrefixData>(prefixPath, $"Prefix_{i}", prefixText);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("영창 데이터(Concepts, Prefixes) 생성이 완료되었습니다!");
        }

        // 제네릭을 사용하여 ConceptData와 PrefixData를 유연하게 생성하는 헬퍼 함수
        private void CreateAsset<T>(string folderPath, string fileName, string textValue) where T : ScriptableObject
        {
            string assetPath = $"{folderPath}/{fileName}.asset";
            T existingAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (existingAsset != null)
            {
                SetText(existingAsset, textValue);
                EditorUtility.SetDirty(existingAsset);
            }
            else
            {
                T newAsset = ScriptableObject.CreateInstance<T>();
                SetText(newAsset, textValue);
                AssetDatabase.CreateAsset(newAsset, assetPath);
            }
        }

        private void SetText(ScriptableObject obj, string text)
        {
            if (obj is ConceptData c) c.text = text;
            else if (obj is PrefixData p) p.text = text;
        }
    }
}
#endif