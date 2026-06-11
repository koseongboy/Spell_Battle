using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Models.Networks
{
    // ==========================================
    // 📦 1. 웹 서버로 보낼 메타데이터 규격 
    // ==========================================
    [Serializable]
    public class VoiceMetadata
    {
        public string userId;
        public string concept;
        public string prefix;
        public List<string> wordNames; 
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
        public string recognizedSentence; 
    }

    [System.Serializable]
    public class DefaultPitchResponse
    {
        public string userId;
        public float defaultPitch;
    }

    public class WebServerModel
    {
        private static WebServerModel instance;
        public static WebServerModel Instance => instance ??= new WebServerModel();

        [Header("서버 설정")]
        public readonly string baseUrl = "http://3.107.201.71:3000";

        private WebServerModel() 
        {
            Debug.Log("[WebServerModel] 순수 C# 웹 서버 통신 모델이 메모리에 로드되었습니다.");
        }

        // ==========================================
        // 🎙️ 1. 음성 바이너리 & 메타데이터 업로드
        // ==========================================
        public async Task<UploadVoiceResponse> UploadVoiceAsync(byte[] wavBytes, string userId, string concept, string prefix, List<string> wordNames)
        {
            try
            {
                VoiceMetadata metadata = new VoiceMetadata
                {
                    userId = userId,
                    concept = concept,
                    prefix = prefix,
                    wordNames = wordNames
                };
                string metadataJson = JsonUtility.ToJson(metadata);

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
                        // 🌟 Error 대신 Warning으로 변경하고 가짜 응답을 넘겨 멈추지 않게 함
                        Debug.LogWarning($"[WebServerModel] 음성 업로드 통신 실패 (무시됨): {www.error}");
                        return new UploadVoiceResponse { taskId = "dummy_task", audioUrl = "" };
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebServerModel] 업로드 중 예외 발생 (무시됨): {e.Message}");
                return new UploadVoiceResponse { taskId = "dummy_task", audioUrl = "" };
            }
        }

        // ==========================================
        // ⏳ 2. 비동기 채점 결과 확인 (Polling)
        // ==========================================
        public async Task<TaskStatusResponse> WaitForScoreAsync(string taskId)
        {
            // 통신 실패나 더미 태스크일 경우 무한 루프에 빠지는 것을 막기 위한 안전장치
            if (taskId == "dummy_task")
            {
                Debug.LogWarning("[WebServerModel] 더미 태스크입니다. 무조건 100점 패스 처리합니다.");
                return new TaskStatusResponse { status = "completed", score = 100, recognizedSentence = "서버 통신 실패로 강제 통과됨" };
            }

            string requestUrl = $"{baseUrl}/tasks/{taskId}";
            int retryCount = 0; // 무한 폴링 방지용 카운터

            while (retryCount < 10) // 최대 10번까지만 물어보고 실패 처리 (10초)
            {
                try
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
                                Debug.Log($"[WebServerModel] 채점 완료! 점수: {res.score}");
                                return res; 
                            }
                            await Task.Delay(1000); 
                            retryCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"[WebServerModel] 통신 에러 (무시하고 패스처리): {www.error}");
                            break; // 에러 나면 폴링 중단하고 아래 더미 데이터 반환
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WebServerModel] 폴링 중 예외 발생 (무시하고 패스처리): {e.Message}");
                    break;
                }
            }

            // 실패했거나 너무 오래 걸렸을 경우 게임 진행을 위해 무조건 통과!
            Debug.LogWarning("[WebServerModel] 채점 응답 실패/지연으로 인해 강제로 100점 통과 처리합니다.");
            return new TaskStatusResponse { status = "completed", score = 100, recognizedSentence = "서버 통신 실패로 강제 통과됨" };
        }

        public async Task<TaskStatusResponse> GetEvaluationResultAsync(string taskId)
        {
            if (taskId == "dummy_task")
            {
                return new TaskStatusResponse { status = "completed", score = 100, recognizedSentence = "서버 통신 실패로 강제 통과됨" };
            }

            try
            {
                using (UnityWebRequest www = UnityWebRequest.Get($"{baseUrl}/evaluation-result?taskId={taskId}"))
                {
                    var operation = www.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        string jsonResult = www.downloadHandler.text;
                        var responseData = JsonUtility.FromJson<TaskStatusResponse>(jsonResult);
                        
                        if (responseData.status == "PENDING") return null; 
                        return responseData;
                    }
                    else
                    {
                        Debug.LogWarning($"[WebServerModel] 평가 결과 조회 실패 (무시됨): {www.error}");
                        return new TaskStatusResponse { status = "completed", score = 100, recognizedSentence = "조회 실패로 강제 통과됨" };
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebServerModel] 평가 결과 조회 중 예외 (무시됨): {e.Message}");
                return new TaskStatusResponse { status = "completed", score = 100, recognizedSentence = "예외 발생으로 강제 통과됨" };
            }
        }

        // ==========================================
        // 🎚️ 3. 초기 보이스 세팅 (디폴트 피치) 저장
        // ==========================================
        public async Task<bool> SetDefaultPitchAsync(string userId, float defaultPitch)
        {
            try
            {
                string jsonBody = $"{{\"userId\": \"{userId}\", \"defaultPitch\": {defaultPitch}}}";
                
                using (UnityWebRequest www = new UnityWebRequest($"{baseUrl}/default-pitch", "PUT"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                    www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");

                    var operation = www.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("디폴트 피치 입력 성공~");
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"[WebServerModel] 디폴트 피치 저장 실패 (무시됨): {www.error}");
                        return true; // 실패해도 성공한 척 다음으로 넘어감
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebServerModel] 피치 저장 예외 (무시됨): {e.Message}");
                return true; 
            }
        }

        // ==========================================
        // 🎚️ 4. 초기 보이스 세팅 (디폴트 피치) 조회
        // ==========================================
        public async Task<float> GetDefaultPitchAsync(string userId)
        {
            try
            {
                string url = $"{baseUrl}/default-pitch?userId={userId}";

                using (UnityWebRequest www = UnityWebRequest.Get(url))
                {
                    var operation = www.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        DefaultPitchResponse response = JsonUtility.FromJson<DefaultPitchResponse>(www.downloadHandler.text);
                        Debug.Log("디폴트 피치 조회 성공");
                        return response.defaultPitch;
                    }
                    else
                    {
                        Debug.LogWarning($"[WebServerModel] 피치 조회 실패 (무시됨, 기본값 반환): {www.error}");
                        return 0f; // 실패 시 게임이 터지지 않도록 기본 피치값(0) 반환
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebServerModel] 피치 조회 중 예외 (무시됨, 기본값 반환): {e.Message}");
                return 0f; 
            }
        }
    }
}