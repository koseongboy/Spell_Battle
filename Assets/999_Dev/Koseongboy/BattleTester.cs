using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using Managers.LocalDataManagers;
using Controllers.SpellControllers;
using DefaultNamespace;

// 🌟 데이터베이스 및 모델 접근을 위해 추가
using Models.PlayerModels;
using Models.CardDatabases;
using Models.SpellPayloads;

#if UNITY_EDITOR
using ParrelSync;
#endif

public class BattleTester : MonoBehaviour
{
    public LocalDataManager ldm;
    
    public static readonly List<int> FixedTestDeck = new List<int>
    {
        // 🔥 1. 사전 구성 (화염) 15장 (4001 ~ 4009)
        4001, 4001, 4002, 4002, 4003, 4003, 4004, 4004, 4005, 4005, 
        4006, 4006, 4007, 4008, 4008,

        // ⚔️ 2. 공격 카드 15장 (1001 ~ 1020 중 15종류 1장씩)
        1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010, 
        1011, 1012, 1013, 1014, 1015,

        // 🛡️ 3. 방어 카드 15장 (2012 ~ 2020)
        2012, 2012, 2013, 2013, 2014, 2014, 2015, 2015, 2016, 2016, 
        2017, 2017, 2018, 2019, 2020
    };

    // ==========================================
    // 🎬 로컬 VFX 연출 테스트 전용 세팅
    // ==========================================
    private bool isTestingRoutine = false;

    [Header("🔥 로컬 VFX 테스트 모드")]
    [Tooltip("체크하면 ParrelSync를 무시하고 혼자 접속하여 더미를 소환합니다.")]
    public bool isLocalVFXTestMode = false;
    public GameObject playerPrefab; // 인스펙터에서 플레이어 프리팹 할당!
    public List<int> testCardIds = new List<int>();

    private PlayerModel dummyCaster;
    private PlayerModel dummyTarget;

    public static BattleTester Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            GameObject nmPrefab = Resources.Load<GameObject>("NetworkManager");
            if (nmPrefab != null) Instantiate(nmPrefab);
            else return;
        }

        if (NetworkManager.Singleton.IsListening) return;

        // 🌟 VFX 테스트 모드일 경우: 혼자 호스트로 열고 더미 타겟 소환
        if (isLocalVFXTestMode)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = (request, response) => 
            {
                response.Approved = true;
                response.CreatePlayerObject = true;
                response.Pending = false;
            };

            NetworkManager.Singleton.StartHost();
            Debug.Log("🛠️ [VFX 테스트 모드] 싱글 호스트 접속 완료!");
            
            // 더미 소환 코루틴 실행
            StartCoroutine(SetupVFXTestEnvironment());
            return; // ⚠️ 중요: 아래 ParrelSync 로직을 타지 않게 여기서 종료
        }

#if UNITY_EDITOR
        if (ClonesManager.IsClone())
        {
            NetworkManager.Singleton.StartClient();
            Debug.Log("손님으로 접속 완");
        }
        else
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = (request, response) => 
            {
                response.Approved = true;           
                response.CreatePlayerObject = true; 
                response.Pending = false;           
            };
            NetworkManager.Singleton.StartHost();
            Debug.Log("방장으로 접속 완");
        }
#endif
        ldm.equippedDeck = FixedTestDeck;
    }

    private IEnumerator SetupVFXTestEnvironment()
    {
        Debug.Log("⏳ [1/5] 인스펙터 프리팹 할당 확인 중...");
        if (playerPrefab == null)
        {
            Debug.LogError("🚨 [실패] BattleTester 인스펙터의 'Player Prefab' 칸이 비어있습니다!");
            yield break;
        }

        Debug.Log("⏳ [2/5] 방장(Host) 로컬 플레이어 스폰 대기 중...");
        NetworkObject localPlayerObj = null;
        float timeout = 5f; // 🌟 5초 타임아웃 방지턱
        float timer = 0f;

        while (localPlayerObj == null)
        {
            timer += Time.deltaTime;
            if (timer > timeout)
            {
                // 5초가 넘어가면 무한 루프를 강제로 끊고 에러를 띄웁니다!
                Debug.LogError("🚨 [실패] 5초가 지났지만 방장 플레이어가 소환되지 않았습니다!\n" +
                               "👉 해결법: NetworkManager 컴포넌트의 'Player Prefab' 슬롯이 채워져 있는지 꼭 확인하세요!");
                yield break;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
            {
                localPlayerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            }
            yield return null;
        }

        Debug.Log("✅ [2/5 성공] 방장 플레이어 스폰 완료!");
        dummyCaster = localPlayerObj.GetComponent<PlayerModel>();
        dummyCaster.transform.position = new Vector3(0, 0, -4f);
        dummyCaster.transform.rotation = Quaternion.Euler(0, 0, 0);

        Debug.Log("⏳ [3/5] 타겟 더미 플레이어 스폰 중...");
        GameObject enemyObj = Instantiate(playerPrefab, new Vector3(0, 0, 4f), Quaternion.Euler(0, 180f, 0));
        enemyObj.GetComponent<NetworkObject>().Spawn(); 
        dummyTarget = enemyObj.GetComponent<PlayerModel>();
        Debug.Log("✅ [3/5 성공] 타겟 더미 스폰 완료!");

        Debug.Log("⏳ [4/5] SpellController 대기 중...");
        timeout = 3f; timer = 0f;
        while (SpellController.Instance == null)
        {
            timer += Time.deltaTime;
            if (timer > timeout)
            {
                Debug.LogError("🚨 [실패] SpellController가 화면에 존재하지 않습니다!");
                yield break;
            }
            yield return null;
        }

        Debug.Log("✅ [5/5] 타겟팅 세팅 완료. 모든 준비 끝!");
        SpellController.Instance.MyPlayer = dummyCaster;
        SpellController.Instance.EnemyPlayer = dummyTarget;

       
    }

    // ==========================================
    // 🚀 버튼 클릭(ContextMenu)으로 연출 발동!
    // ==========================================
    public void TriggerTestCardFromEditor()
    {
        // 씬 전체에서 dummyTarget 태그나 이름을 가진 진짜 오브젝트를 찾아 강제 갱신
        PlayerModel[] foundPlayers = FindObjectsOfType<PlayerModel>();
        foreach (var p in foundPlayers)
        {
            if (p == dummyTarget) { /* 갱신 완료 */ }
        }
        
        // 이렇게 실제 씬에 있는 좌표를 한 번 건드려주면(Dirty 시키면) 
        // 에디터 캐시가 강제로 갱신됩니다.
        Debug.Log($"[강제 동기화] 타겟 실제 위치: {dummyTarget.transform.position}");
        
        TriggerTestCard();
    }

    [ContextMenu("👉 이 카드 연출 실행! (TriggerTestCard)")]
    public void TriggerTestCard()
    {
        if (!isLocalVFXTestMode || dummyCaster == null || dummyTarget == null) return;
        Debug.Log($"테스트 시작 시 타겟 좌표: {dummyTarget.transform.position}");

        // 🌟 자물쇠 체크: 이미 실행 중이면 무시합니다!
        if (isTestingRoutine)
        {
            Debug.LogWarning("⏳ 아직 연출이 재생 중입니다. 끝나고 눌러주세요!");
            return; 
        }

        StartCoroutine(ExecuteCardRoutine());
    }

    private IEnumerator ExecuteCardRoutine()
    {
        isTestingRoutine = true;

        foreach (int cardId in testCardIds)
        {
            Debug.Log($"🔥 카드 ID [{cardId}] 연출 테스트 시작!");
            
            SpellPayload testPayload = new SpellPayload();
            var card = CardDatabase.Instance.GetCardById(cardId);

            if (card != null)
            {
                testPayload.EnqueuePendingCard(card);
                testPayload.CompileSpell(dummyCaster, dummyTarget);

                foreach (var command in testPayload.Commands)
                {
                    yield return StartCoroutine(command.ExecuteRoutine(1.0f));
                }
            }
            // 🌟 카드 연출 사이의 짧은 휴식 (다음 카드 준비)
            yield return new WaitForSeconds(0.5f); 
        }

        Debug.Log("✅ 모든 카드 테스트 종료!");
        isTestingRoutine = false;
    }

    private void OnApplicationQuit()
    {
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
            Debug.Log("네트워크 매니저를 강제 종료하여 포트를 비웠습니다!");
        }
    }

    [ContextMenu("전투 강제 시작 (테스트용)")]
    public void battleStarte()
    {
        MatchManager.Instance.testGame();
    }
}