using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Managers.LocalDataManagers;

namespace DefaultNamespace {
    // 1. 요청(Request) 데이터 포맷 (로그인, 회원가입 공통)
    [System.Serializable]
    public class AuthRequestDto {
        public string userId;
        public string password;
    }

    // 2. 회원가입 응답(Response) 데이터 포맷
    [System.Serializable]
    public class RegisterResponse {
        public string message;
    }

    // 3. 로그인 응답(Response) 데이터 포맷
    [System.Serializable]
    public class LoginResponse {
        public string token;
        public string userId;
        public int score;
        public string rank;
        public float defaultPitch;
    }

    public class AuthManager : MonoBehaviour {
        public static AuthManager Instance { get; private set; }

        [Header("서버 주소")] public string serverURL = "http://3.107.201.71:5000";

        void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// 로그인 통신 및 LocalDataManager 데이터 갱신
        /// </summary>
        public async Task<bool> RequestLoginAsync(string id, string pw) {
            AuthRequestDto requestData = new AuthRequestDto { userId = id, password = pw };
            string jsonData = JsonUtility.ToJson(requestData);

            using (UnityWebRequest request = new UnityWebRequest(serverURL + "/login", "POST")) {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"<color=#00FF00>[AuthManager] 로그인 서버 응답 수신 성공!</color>\nRaw JSON: {responseText}");

                    LoginResponse response = JsonUtility.FromJson<LoginResponse>(responseText);
                
                    var localData = LocalDataManager.Instance;
                    localData.userToken = response.token;
                    localData.userId = response.userId;
                    localData.nickname = response.userId; 
                    localData.score = response.score;
                    localData.rank = response.rank;
                    localData.defaultPitch = response.defaultPitch;

                    return true;
                }
                else
                {
                    // 🛠️ [수정 부분] 로그인 실패 시 에러 코드와 서버가 보낸 에러 메시지 Body를 같이 출력
                    string errorResponse = request.downloadHandler?.text;
                    Debug.LogError($"<color=#FF0000>[AuthManager] 로그인 서버 통신 실패.</color>\nError: {request.error}\nServer Message: {errorResponse}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 회원가입 통신
        /// </summary>
        public async Task<bool> RequestRegisterAsync(string id, string pw) {
            AuthRequestDto requestData = new AuthRequestDto { userId = id, password = pw };
            string jsonData = JsonUtility.ToJson(requestData);

            using (UnityWebRequest request = new UnityWebRequest(serverURL + "/register", "POST")) {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"<color=#00FF00>[AuthManager] 회원가입 서버 응답 수신 성공!</color>\nRaw JSON: {responseText}");
                    return true;
                }
                else
                {
                    string errorResponse = request.downloadHandler?.text;
                    Debug.LogError($"<color=#FF0000>[AuthManager] 회원가입 서버 통신 실패.</color>\nError: {request.error}\nServer Message: {errorResponse}");
                    return false;
                }
            }
        }
    }
}