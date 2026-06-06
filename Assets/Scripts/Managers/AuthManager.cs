using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Managers.LocalDataManagers;
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
        public string token;
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
                    localData.userToken = response.token;
                    localData.userId = response.userId;
                    localData.nickname = response.userId; 
                    localData.score = response.score;
                    localData.rank = response.rank;
                    localData.defaultPitch = response.defaultPitch;
                    
                    // TODO : 이거 ES3 쓴다고?
                    PlayerPrefs.SetString("Saved_JWT_Token", response.token);
                    PlayerPrefs.Save(); // 디스크에 즉시 기록

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
            // 친구가 만들어준 토큰 검증용 엔드포인트 주소 (예시: /me)
            using (UnityWebRequest request = UnityWebRequest.Get(serverURL + "/me"))
            {
                // 🛠️ [수정 부분] 헤더에 JWT 토큰을 Bearer 규격으로 첨부합니다.
                request.SetRequestHeader("Authorization", "Bearer " + token);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"<color=#00FF00>[AuthManager] 자동 로그인 성공!</color>\nRaw JSON: {responseText}");

                    // 로그인과 동일하게 응답 데이터를 파싱하여 로컬 데이터 매니저에 캐싱
                    LoginResponse response = JsonUtility.FromJson<LoginResponse>(responseText);
            
                    var localData = Managers.LocalDataManagers.LocalDataManager.Instance;
                    localData.userToken = token; // 매개변수로 받은 토큰 유지
                    localData.userId = response.userId;
                    localData.nickname = response.userId; 
                    localData.score = response.score;
                    localData.rank = response.rank;
                    localData.defaultPitch = response.defaultPitch;

                    return true;
                }
                else
                {
                    Debug.LogWarning($"[AuthManager] 자동 로그인 실패 (토큰 만료 또는 서버 에러): {request.error}");
                    PlayerPrefs.DeleteKey("Saved_JWT_Token");
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
            string token = LocalDataManager.Instance.userToken;
    
            if (string.IsNullOrEmpty(token)) return null;

            using (UnityWebRequest request = UnityWebRequest.Get(serverURL + "/users/" + targetUserId))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    return JsonUtility.FromJson<UserProfileResponse>(responseText);
                }
                else
                {
                    Debug.LogError($"[AuthManager] 유저 프로필 조회 실패 ({targetUserId}): {request.error}");
                    return null;
                }
            }
        }

        public void Logout() {
            PlayerPrefs.DeleteKey("Saved_JWT_Token");
            PlayerPrefs.Save();

            LocalDataManager.Instance.ClearData();

            // TODO : 서버 소켓이 연결되어 있다면 여기서 끊어주는 로직 추가 (필요 시)
            // NetworkManager.Instance.Disconnect(); 

            CommonUIController.Instance.InitFullScreenStack();
            CommonUIController.Instance.ChangeFullScreen("LoginUI_FullScreen");
        }
    }
}