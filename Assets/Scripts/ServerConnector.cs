using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ServerConnector : MonoBehaviour
{
    private string baseUrl = "http://localhost:3000"; // 기본 주소

    // 로그인 기능 호출
    public void Login(string id, string pw) {
        StartCoroutine(LoginRoutine(id, pw));
    }

    IEnumerator LoginRoutine(string id, string pw) {
        // 1. 로그인 정보 JSON 생성
        string json = "{\"userId\":\"" + id + "\", \"password\":\"" + pw + "\"}";

        using (UnityWebRequest request = new UnityWebRequest(baseUrl + "/login", "POST")) {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // 서버 응답 대기
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success) {
                // 2. 서버 응답 해석 (JsonUtility 활용)
                UserResponse res = JsonUtility.FromJson<UserResponse>(request.downloadHandler.text);
                
                if (res.success) {
                    Debug.Log($"로그인 성공! 점수: {res.score}, 랭크: {res.rank}");
                    
                    // 3. 인증 토큰 저장 (로그인 상태 유지용)
                    PlayerPrefs.SetString("Token", res.token);
                    
                    // 4. 캐릭터 상태 복구 (실제 게임 매니저 등에 대입)
                    // GameManager.instance.score = res.score; 
                    // GameManager.instance.rank = res.rank;
                }
            } else {
                Debug.LogError("로그인 실패: " + request.downloadHandler.text);
            }
        }
    }
}

// 반드시 클래스 외부에 작성 (혹은 별도 파일로 분리)
[System.Serializable]
public class UserResponse {
    public bool success;
    public string token;
    public int score;
    public int rank;
    public string message;
}