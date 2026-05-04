using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ServerConnector : MonoBehaviour
{
    // 서버 주소 (내 컴퓨터에서 테스트 중이므로 localhost)
    private string serverUrl = "http://localhost:3000/unity";

    void Start()
    {
        // 게임이 시작되자마자 서버에 데이터를 보냅니다.
        StartCoroutine(SendDataToServer("Player_01", 100));
    }

    IEnumerator SendDataToServer(string id, int score)
    {
        // 1. 보낼 데이터를 JSON 형식으로 만듭니다.
        string json = "{\"userId\":\"" + id + "\", \"score\":" + score + "}";

        // 2. 요청 객체를 생성합니다 (POST 방식)
        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            
            // 헤더 설정: "나 지금 JSON 데이터 보낸다!"
            request.SetRequestHeader("Content-Type", "application/json");

            // 3. 서버에 전송하고 응답을 기다립니다.
            yield return request.SendWebRequest();

            // 4. 결과 확인
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("서버 응답: " + request.downloadHandler.text);
            }
            else
            {
                Debug.LogError("에러 발생: " + request.error);
            }
        }
    }
}