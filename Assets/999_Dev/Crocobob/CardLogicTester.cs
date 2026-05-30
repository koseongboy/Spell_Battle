using UnityEngine;
using Models.SpellPayloads;
using Models.PlayerModels;
using Models.CardDatabases;
using Unity.Netcode;

public class CardLogicTester : MonoBehaviour
{
    [Header("테스트 환경 세팅")]
    public PlayerModel casterPrefab;
    public PlayerModel enemyPrefab;
    
    [Header("테스트할 카드 ID 입력")]
    public int[] testCardIds = new int[] { 4001, 4003 }; // 예: 발화(4001), 분출(4003)

    private PlayerModel currentCaster;
    private PlayerModel currentEnemy;

    private void Update()
    {
        // 스페이스바를 누르면 테스트 시작
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RunTest();
        }
    }

    private void RunTest()
    {
        // 1. 네트워크 호스트 시작 (이미 실행 중이면 패스)
        if (!NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.StartHost();
            
            // 더미 플레이어 스폰
            currentCaster = Instantiate(casterPrefab);
            currentCaster.GetComponent<NetworkObject>().Spawn();
            
            currentEnemy = Instantiate(enemyPrefab);
            currentEnemy.GetComponent<NetworkObject>().Spawn();
            
            Debug.Log("[Test] 더미 플레이어 스폰 완료");
        }

        Debug.Log("=====================================");
        Debug.Log("[Test] 영창 테스트 시작!");

        // 2. SpellPayload 생성
        SpellPayload payload = new SpellPayload();

        // 3. 테스트할 카드들을 CardDataManager에서 불러와 Payload에 적재
        foreach (int id in testCardIds)
        {
            var card = CardDatabase.GetCardById(id);
            if (card != null)
            {
                payload.EnqueuePendingCard(card);
                Debug.Log($"[Test] 카드 적재됨: {card.uiData.wordName} (속성: {card.uiData.property})");
            }
        }

        // 4. 컴파일 진행 (메인 속성 계산 및 Command 생성/정렬)
        payload.CompileSpell(currentCaster, currentEnemy);
        
        Debug.Log($"[Test] 메인 속성 판정 결과: {payload.MainProperty}");
        Debug.Log($"[Test] 총 {payload.Commands.Count}개의 커맨드가 큐에 쌓였습니다.");

        // 5. 커맨드 순차 실행 (실제 데미지, 상태이상 등 적용)
        foreach (var command in payload.Commands)
        {
            Debug.Log($"[Test] 커맨드 실행 중: {command.GetType().Name} (우선순위: {command.Priority})");
            command.Execute();
        }

        // 6. 결과 검증 로그
        Debug.Log($"[Test] 결과 - 타겟 남은 체력: {currentEnemy.CurrentHealth.Value}");
        Debug.Log($"[Test] 결과 - 타겟 발화 스택: {currentEnemy.GetStatusStack(StatusType.Ignite)}");
        Debug.Log($"[Test] 결과 - 타겟 분출(증폭) 스택: {currentEnemy.GetStatusStack(StatusType.IgniteDamageMultiplier)}");
        Debug.Log("=====================================");
    }
}