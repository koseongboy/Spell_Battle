using UnityEngine;
using UnityEditor;
using System.IO;

public class FindPrefabWithScript : Editor
{
    // 🌟 유니티 메뉴 바 [Tools] -> [Find Prefabs With Loading_Common]를 생성합니다.
    [MenuItem("Tools/Find Prefabs With Loading_Common")]
    public static void FindPrefabs()
    {
        // 프로젝트 내의 모든 프리팹 GUID를 가져옵니다.
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int count = 0;

        Debug.Log("======== 🔍 Loading_Common 검색 시작 ========");

        foreach (string guid in prefabGuids)
        {
            // GUID를 실제 에셋 경로로 변환
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // 프리팹 오브젝트 로드
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                // 🌟 Loading_Common 컴포넌트가 붙어있는지 확인 (자식 오브젝트까지 포함해서 검색)
                if (prefab.GetComponentInChildren<Loading_Common>(true) != null)
                {
                    Debug.Log($"<color=yellow>[발견]</color> {prefab.name} -> 경로: {path}", prefab);
                    count++;
                }
            }
        }

        Debug.Log($"======== 🏁 검색 완료! 총 {count}개의 프리팹을 찾았습니다. ========");
    }
}