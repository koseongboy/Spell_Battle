using System;
using System.Text;
using System.Threading.Tasks;
using Managers;
using UnityEngine;
using UnityEngine.Networking;
using Managers.LocalDataManagers;
using Models.CardDatabases;
using Models.Networks;
using Unity.Netcode;

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
        public string message;
        public string token;
        public UserDataDto userData;
    }
    
    [System.Serializable]
    public class UserDataDto {
        public string userId;
        public int score;
        public string rank;
        public float defaultPitch;
    }

    public class AuthManager : MonoBehaviour {
        public static AuthManager Instance { get; private set; }

        [Header("서버 주소")] public string serverURL = "http://3.107.201.71:3000";

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
                    localData.LoadData();
                    
                    localData.userToken = response.token;
                    localData.userId = response.userData.userId;
                    localData.nickname = response.userData.userId; 
                    localData.score = response.userData.score;
                    localData.rank = response.userData.rank;
                    localData.defaultPitch = response.userData.defaultPitch;
                    localData.SaveData();

                    _ = DeckManager.Instance.LoadDecksFromServerAsync();
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
        /// 로컬에 저장된 토큰을 이용해 서버에 자동 로그인을 요청합니다.
        /// </summary>
        public async Task<bool> RequestAutoLoginAsync(string token)
        {
            // 🌟 1. 서버가 요구하는 대로 JSON 바디에 토큰을 담습니다.
            string jsonBody = $"{{\"token\": \"{token}\"}}";

            // 🌟 2. GET이 아닌 POST 메서드로 변경합니다.
            using (UnityWebRequest request = new UnityWebRequest(serverURL + "/auto-login", "POST"))
            {
                // JSON 데이터를 바이트로 변환하여 업로드 핸들러에 장착
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
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
                    localData.LoadData();
                    
                    localData.userToken = token; 
                    localData.userId = response.userData.userId;
                    localData.nickname = response.userData.userId; 
                    localData.score = response.userData.score;
                    localData.rank = response.userData.rank;
                    localData.defaultPitch = response.userData.defaultPitch;
                    
                    localData.SaveData();
                    
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[AuthManager] 자동 로그인 실패 (토큰 만료 또는 서버 에러): {request.error}");
                    LocalDataManager.Instance.ClearData();
                    LocalDataManager.Instance.SaveData();
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
            Debug.Log($"[디버그] 요청 주소 확인: {serverURL + "/register"}");
            

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
        
        public async Task<UserProfileResponse> RequestUserProfileAsync(string targetUserId)
        {
            // 1. 방어 로직: 조회할 타겟 ID가 비정상적이면 서버에 불필요한 요청을 보내지 않음
            if (string.IsNullOrEmpty(targetUserId))
            {
                Debug.LogError("[AuthManager] 조회할 타겟 유저 ID가 비어있습니다.");
                return null;
            }

            string token = LocalDataManager.Instance.userToken;

            if (string.IsNullOrEmpty(token)) 
            {
                Debug.LogError("[AuthManager] 인증 토큰이 없습니다. 로그인이 풀렸는지 확인하세요.");
                return null;
            }

            string requestUrl = $"{serverURL}/load/{targetUserId}";

            using (UnityWebRequest request = UnityWebRequest.Get(requestUrl))
            {
                request.SetRequestHeader("Authorization", $"Bearer {token}");

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    return JsonUtility.FromJson<UserProfileResponse>(responseText);
                }
                else
                {
                    // 3. 에러 발생 시 responseCode와 서버가 보낸 에러 메시지(Body)를 함께 찍어 디버깅을 용이하게 함
                    Debug.LogError($"[AuthManager] 유저 프로필 조회 실패 ({targetUserId}) - 상태 코드: {request.responseCode}");
                    Debug.LogError($"[AuthManager] 에러 내용: {request.error} | 서버 응답: {request.downloadHandler.text}");
                    return null;
                }
            }
        }

        public void Logout() {
            LocalDataManager.Instance.ClearData();
            LocalDataManager.Instance.SaveData();

            // TODO : 서버 소켓이 연결되어 있다면 여기서 끊어주는 로직 추가 (필요 시)
            // NetworkManager.Instance.Disconnect(); 

            CommonUIController.Instance.InitFullScreenStack();
            CommonUIController.Instance.ChangeFullScreen("LoginUI_FullScreen");
        }
    }
}