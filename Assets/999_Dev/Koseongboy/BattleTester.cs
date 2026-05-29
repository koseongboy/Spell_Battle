using UnityEngine;
using Unity.Netcode;

#if UNITY_EDITOR
using ParrelSync;
#endif

public class BattleTester : MonoBehaviour
{
    private void Start()
    {
        // 1. 네트워크 매니저 강제 생성 로직 (기존 유지)
        if (NetworkManager.Singleton == null)
        {
            GameObject nmPrefab = Resources.Load<GameObject>("NetworkManager");
            if (nmPrefab != null) Instantiate(nmPrefab);
            else return;
        }

        if (NetworkManager.Singleton.IsListening) return;

#if UNITY_EDITOR
        if (ClonesManager.IsClone())
        {
            // 클론 에디터: 손님으로 접속
            NetworkManager.Singleton.StartClient();
            Debug.Log("손님으로 접속 완");
        }
        else
        {
            // 🌟 [핵심 추가] 호스트를 켜기 전에 무조건 접속을 승인하는 '프리패스' 콜백을 달아줍니다!
            NetworkManager.Singleton.ConnectionApprovalCallback = (request, response) => 
            {
                response.Approved = true;           // 무조건 승인!
                response.CreatePlayerObject = true; // 플레이어 오브젝트 생성 허락
                response.Pending = false;           // 대기 안 함
            };

            // 방장 에디터: 호스트로 켜기
            NetworkManager.Singleton.StartHost();
            Debug.Log("방장으로 접속 완");
        }



#endif
    }

    private void OnApplicationQuit()
    {
        // 네트워크 매니저가 살아있고, 통신 중(Listening)이라면 강제 셧다운!
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("🛠️ [테스트 모드 종료] 열려있는 네트워크 포트를 안전하게 닫습니다.");
            NetworkManager.Singleton.Shutdown();
        }
    }

    [ContextMenu("응급조치: 열린 네트워크 포트 강제로 닫기")]
    public void EmergencyShutdown()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
            Debug.Log("네트워크 매니저를 강제 종료하여 7777번 포트를 비웠습니다!");
        }
    }
}