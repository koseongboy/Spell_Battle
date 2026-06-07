using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Models.Networks
{
    // ==========================================
    // 📦 1. 웹 서버로 보낼 메타데이터 규격 (새로 추가)
    // ==========================================
    [Serializable]
    public class VoiceMetadata
    {
        public string userId;
        public string concept;
        public string prefix;
        public List<string> wordNames; // 카드의 wordName 리스트
    }
    // ==========================================
    // 📦 2. 서버 응답 데이터 규격
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
        public string recognizedSentence; // 🌟 서버가 STT로 인식한 플레이어의 실제 발음 문장!
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
        // 🎙️ 1. 음성 바이너리 & 메타데이터(단어 리스트) 업로드
        // ==========================================
        public async Task<UploadVoiceResponse> UploadVoiceAsync(byte[] wavBytes, string userId, string concept, string prefix, List<string> wordNames)
        {
            // 1. 객체를 생성하고 JsonUtility를 통해 완벽한 JSON 문자열로 변환
            VoiceMetadata metadata = new VoiceMetadata
            {
                userId = userId,
                concept = concept,
                prefix = prefix,
                wordNames = wordNames
            };
            string metadataJson = JsonUtility.ToJson(metadata);

            // 2. 폼 데이터 구성
            WWWForm form = new WWWForm();
            form.AddField("metadata", metadataJson);
            form.AddBinaryData("audio", wavBytes, "voice.wav", "audio/wav"); 

            using (UnityWebRequest www = UnityWebRequest.Post($"{baseUrl}/upload-voice-async", form))
            {
                var operation = www.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    return JsonUtility.FromJson<UploadVoiceResponse>(www.downloadHandler.text);
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
        // 🌟 반환형을 int(점수)에서 TaskStatusResponse(전체 결과 객체)로 변경하여, 문장도 함께 넘겨줍니다.
        public async Task<TaskStatusResponse> WaitForScoreAsync(string taskId)
        {
            string requestUrl = $"{baseUrl}/tasks/{taskId}";

            while (true)
            {
                using (UnityWebRequest www = UnityWebRequest.Get(requestUrl))
                {
                    var operation = www.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        TaskStatusResponse res = JsonUtility.FromJson<TaskStatusResponse>(www.downloadHandler.text);

                        if (res.status == "completed")
                        {
                            Debug.Log($"[WebServerModel] 채점 완료! 점수: {res.score}, 인식된 문장: {res.recognizedSentence}");
                            return res; // 점수와 문장이 모두 담긴 객체를 통째로 반환!
                        }
                        
                        // processing 상태면 1초 대기
                        await Task.Delay(1000); 
                    }
                    else
                    {
                        Debug.LogError($"[WebServerModel] 통신 에러: {www.error}");
                        return null; 
                    }
                }
            }
        }

        // ==========================================
        // 🎚️ 3. 초기 보이스 세팅 (디폴트 피치) 저장
        // ==========================================
        public async Task<bool> SetDefaultPitchAsync(string userId, float defaultPitch)
        {
            // JSON 바디 포맷팅
            string jsonBody = $"{{\"userId\": \"{userId}\", \"defaultPitch\": {defaultPitch}}}";
            
            using (UnityWebRequest www = new UnityWebRequest($"{baseUrl}/set-default-pitch", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                var operation = www.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[WebServerModel] 디폴트 피치({defaultPitch}Hz) 저장 완료: {www.downloadHandler.text}");
                    return true;
                }
                else
                {
                    Debug.LogError($"[WebServerModel] 디폴트 피치 저장 실패: {www.error}");
                    return false;
                }
            }
        }
    }
}