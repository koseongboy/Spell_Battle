using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Models.Networks
{
    // ==========================================
    // 📦 API 응답 데이터를 담을 구조체
    // ==========================================
    [Serializable]
    public class UploadVoiceResponse
    {
        public string taskId;
        public string audioUrl;
    }

    [Serializable]
    public class TaskStatusResponse
    {
        public string status;
        public string message;
        public int score;
    }

    // 🌟 MonoBehaviour 상속을 제거한 순수 C# 클래스
    public class WebServerModel
    {
        // 1. 지연 초기화(Lazy Initialization)를 적용한 싱글톤
        private static WebServerModel instance;
        public static WebServerModel Instance => instance ??= new WebServerModel();

        [Header("서버 설정")]
        public readonly string baseUrl = "http://3.107.201.71:3000";

        // 생성자를 private으로 막아 외부에서 new로 생성하는 것을 방지합니다.
        private WebServerModel() 
        {
            Debug.Log("[WebServerModel] 순수 C# 웹 서버 통신 모델이 메모리에 로드되었습니다.");
        }

        // ==========================================
        // 🎙️ 1. 음성 바이너리 파일 & 메타데이터 업로드
        // ==========================================
        public async Task<UploadVoiceResponse> UploadVoiceAsync(byte[] wavBytes, string userId, string characterType, string script)
        {
            // 1. metadata를 JSON 문자열로 직접 포맷팅
            string metadataJson = $"{{\"userId\": \"{userId}\", \"characterType\": \"{characterType}\", \"script\": \"{script}\"}}";

            // 2. 파일과 텍스트를 함께 보내기 위한 폼(Form) 세팅
            WWWForm form = new WWWForm();
            form.AddField("metadata", metadataJson);
            form.AddBinaryData("audio", wavBytes, "voice.wav", "audio/wav"); 

            using (UnityWebRequest www = UnityWebRequest.Post($"{baseUrl}/upload-voice-async", form))
            {
                var operation = www.SendWebRequest();
                
                // MonoBehaviour의 코루틴(yield return) 대신 비동기 대기 사용
                while (!operation.isDone) await Task.Yield();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string responseText = www.downloadHandler.text;
                    Debug.Log($"[WebServerModel] 업로드 성공! 응답: {responseText}");
                    return JsonUtility.FromJson<UploadVoiceResponse>(responseText);
                }
                else
                {
                    Debug.LogError($"[WebServerModel] 음성 업로드 실패: {www.error}");
                    return null;
                }
            }
        }

        // ==========================================
        // ⏳ 2. 비동기 채점 결과 확인 (Polling)
        // ==========================================
        public async Task<int> WaitForScoreAsync(string taskId)
        {
            string requestUrl = $"{baseUrl}/tasks/{taskId}";

            while (true) // 점수가 나올 때까지 무한 반복
            {
                using (UnityWebRequest www = UnityWebRequest.Get(requestUrl))
                {
                    var operation = www.SendWebRequest();
                    
                    while (!operation.isDone) await Task.Yield();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = www.downloadHandler.text;
                        TaskStatusResponse res = JsonUtility.FromJson<TaskStatusResponse>(responseText);

                        if (res.status == "completed")
                        {
                            Debug.Log($"[WebServerModel] 채점 완료! 최종 점수: {res.score}점 ({res.message})");
                            return res.score;
                        }
                        else
                        {
                            Debug.Log("[WebServerModel] 아직 평가 중... 1초 뒤에 다시 물어봅니다.");
                            // 아직 처리 중이면 1초 대기 후 루프 재진입
                            await Task.Delay(1000); 
                        }
                    }
                    else
                    {
                        Debug.LogError($"[WebServerModel] 채점 확인 통신 에러: {www.error}");
                        // 통신 에러 시 -1을 반환
                        return -1; 
                    }
                }
            }
        }
    }
}