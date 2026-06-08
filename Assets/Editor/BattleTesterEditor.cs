using UnityEngine;
using UnityEditor;
using System;
using UnityEditorInternal;

[CustomEditor(typeof(BattleTester))]
public class BattleTesterEditor : Editor
{
    private ReorderableList reorderableList;

    private void OnEnable()
    {
        // 1. target에서 serializedObject를 가져옵니다.
        // 이것을 사용해야 유니티가 'Undo(Ctrl+Z)'를 기억하고 인스펙터 값을 저장합니다.
        SerializedProperty listProperty = serializedObject.FindProperty("testCardIds");

        // 2. 리스트 초기화
        reorderableList = new ReorderableList(serializedObject, listProperty, true, true, true, true);

        // 3. 리스트 헤더 그리기
        reorderableList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "🔥 테스트할 카드 ID 목록");
        };

        // 4. 리스트 요소 그리기
        reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            var element = listProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
        };
    }
    public override void OnInspectorGUI()
    {
        // 1. 기존 BattleTester에 있던 변수들(Test Card Id, Prefab 등)을 그대로 그려줍니다.
        base.OnInspectorGUI();
        
        serializedObject.Update();

        // 🌟 예쁜 리스트 출력
        reorderableList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();

        BattleTester tester = BattleTester.Instance;
        if (tester == null)
        {
            EditorGUILayout.HelpBox("씬에 BattleTester 인스턴스가 없습니다. 플레이 버튼을 눌러주세요.", MessageType.Error);
            return;
        }

        // ==========================================
        // ⚔️ 1. 전투 및 연출 테스트 패널
        // ==========================================
        EditorGUILayout.Space(15);
        EditorGUILayout.HelpBox("👇 전투 및 연출 테스트 패널 👇", MessageType.Info);

        // [버튼 1] 특정 카드 연출 테스트
        if (GUILayout.Button("🔥 이 카드 연출 실행! (TriggerTestCard)", GUILayout.Height(40)))
        {
            if (Application.isPlaying)
            {

                tester.TriggerTestCardFromEditor();
            }
            else
            {
                Debug.LogWarning("🚨 게임을 실행(Play)한 상태에서만 작동합니다!");
            }
        }

        EditorGUILayout.Space(5);

        // [버튼 2] 전체 전투 강제 시작
        if (GUILayout.Button("⚔️ 전투 강제 시작 (Battle Start)", GUILayout.Height(35)))
        {
            if (Application.isPlaying)
            {
                tester.battleStarte();
            }
            else
            {
                Debug.LogWarning("🚨 게임을 실행(Play)한 상태에서만 작동합니다!");
            }
        }


        // ==========================================
        // 🚨 2. 네트워크 디버그 제어 패널
        // ==========================================
        EditorGUILayout.Space(15);
        EditorGUILayout.HelpBox("👇 네트워크 환경 관리 👇", MessageType.Warning);

        // [버튼 3] 응급조치 포트 강제 종료 
        // (이 기능은 유니티가 재생 중이 아닐 때 포트가 먹통이 된 경우에도 작동해야 하므로 플레이 체크를 하지 않습니다.)
        if (GUILayout.Button("🚨 응급조치: 열린 네트워크 포트 강제로 닫기", GUILayout.Height(35)))
        {
            tester.EmergencyShutdown();
        }
    }
}