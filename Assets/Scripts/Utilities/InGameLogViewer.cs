using UnityEngine;
using System.Collections.Generic;

public class InGameLogViewer : MonoBehaviour
{
    // 로그를 저장할 리스트 (최대 100개까지만 저장하여 메모리 관리)
    private List<string> logMessages = new List<string>();
    private Vector2 scrollPosition;
    private bool showConsole = false;

    // [핵심] 스크립트가 활성화될 때 유니티의 로그 이벤트에 HandleLog 함수를 연결해.
    void OnEnable() { Application.logMessageReceived += HandleLog; }
    void OnDisable() { Application.logMessageReceived -= HandleLog; }

    void Update()
    {
        // 키보드 숫자 1 왼쪽의 백틱(`) 키를 누르면 콘솔창이 켜지고 꺼지게 만들어.
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            showConsole = !showConsole;
        }
    }

    // 유니티에서 Debug.Log, LogWarning, LogError가 호출될 때마다 실행되는 함수야.
    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logMessages.Add($"[{type}] {logString}");
        
        // 로그가 너무 많이 쌓이면 오래된 것부터 지워줘.
        if (logMessages.Count > 100) 
        {
            logMessages.RemoveAt(0);
        }
    }

    // 화면에 직접 UI를 그리는 함수야.
    void OnGUI()
    {
        if (!showConsole) return;

        // 화면 왼쪽 상단에 화면 절반 크기의 박스를 그려.
        GUILayout.BeginArea(new Rect(10, 10, Screen.width / 2, Screen.height / 2), GUI.skin.box);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // 리스트에 저장된 로그들을 순서대로 텍스트로 출력해.
        foreach (var msg in logMessages)
        {
            GUILayout.Label(msg);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}