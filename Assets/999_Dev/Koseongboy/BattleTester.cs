using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Managers.LocalDataManagers;
using Controllers.TurnControllers;
using DefaultNamespace;



#if UNITY_EDITOR
using ParrelSync;
#endif

public class BattleTester : MonoBehaviour
{
    public LocalDataManager ldm;
    public static readonly List<int> FixedTestDeck = new List<int>
    {
        // 🔥 1. 사전 구성 (화염) 15장 (4001 ~ 4009)
        // 최대 3장 중복 규칙 준수 (각 2장씩 + 1장씩)
        4001, 4001, 4002, 4002, 4003, 4003, 4004, 4004, 4005, 4005, 
        4006, 4006, 4007, 4008, 4009,

        // ⚔️ 2. 공격 카드 15장 (1001 ~ 1020 중 15종류 1장씩)
        1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010, 
        1011, 1012, 1013, 1014, 1015,

        // 🛡️ 3. 방어 카드 15장 (2012 ~ 2020)
        // 최대 3장 중복 규칙 준수 (각 2장씩 + 1장씩)
        2012, 2012, 2013, 2013, 2014, 2014, 2015, 2015, 2016, 2016, 
        2017, 2017, 2018, 2019, 2020
    };

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
        ldm.equippedDeck = FixedTestDeck;
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
    [ContextMenu("전투 강제 시작 (테스트용)")]
    public void battleStarte()
    {
        MatchManager.Instance.testGame();
    }
}