using Unity.Netcode;
using UnityEngine;
using Models.TurnModel;
using Views.TurnView;
using Models.PlayerModels;
using Models.CardDatabases;
using Cards.PlayableCards;
using Cards.CardUIDatas;
using System.Collections.Generic;
using Models.SpellPayloads;
using Newtonsoft.Json;


namespace Controllers.TurnController
{
    public class TurnController : NetworkBehaviour
    {
        public static TurnController Instance { get; private set; }

        [Header("연결된 플레이어 모델. 동적 할당이니 인스펙터에 박을 필요 X")]
        public PlayerModel MyPlayer;      // 클라이언트 본인
        public PlayerModel EnemyPlayer;   // 상대방

        [Header("MVP References")]
        [SerializeField] private TurnModel model;
        [SerializeField] private TurnView view;

        [Header("Spawning")]
        [SerializeField] private GameObject playerPrefab; // 플레이어 캐릭터 프리팹
        [SerializeField] private Transform hostSpawnPoint; // 방장 위치
        [SerializeField] private Transform guestSpawnPoint; // 손님 위치

        

        public void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // ==========================================
        // 💻 [클라이언트 영역] 카드를 내고 견적서 작성
        // ==========================================
        public void ProcessSpellCast(List<PlayableCard> selectedCards)
        {
            if (MyPlayer == null || EnemyPlayer == null)
            {
                Debug.LogError("플레이어가 아직 전장에 소환되지 않았습니다!");
                return;
            }
            // 1. 마나 코스트 사전 검증 (클라이언트 UI 피드백용)
            int totalCost = 0;
            List<int> selectedCardIds = new List<int>(); // 서버 재구성을 위한 ID 리스트
            
            foreach (var card in selectedCards)
            {
                totalCost += card.uiData.cost;
                selectedCardIds.Add(card.Id);
            }

            if (MyPlayer.CurrentMana.Value < totalCost)
            {
                Debug.LogWarning("마나가 부족합니다.");
                return;
            }

            // 2. 종합 서류철(Payload) 생성 및 조립
            SpellPayload payload = new SpellPayload();
            
            // 시스템이 정한 컨셉과 접두어 주입 (실제로는 매니저 등에서 동적으로 받아옴)
            payload.EvalData.Concept = "건방지게";
            payload.EvalData.RequiredPrefix = "칠흑의 심연에서 눈뜬 자여";

            // 카드들을 순회하며 커맨드와 영창 단어 조립
            foreach (var card in selectedCards)
            {
                // 이제 카드가 직접 대상(MyPlayer, EnemyPlayer)을 받아 커맨드에 구워버립니다.
                card.AddToPayload(payload, MyPlayer, EnemyPlayer);
            }

            // 3. 웹 서버 전송용 JSON (평가 데이터만 포함)
            string evalJson = payload.EvalData.ToJson();
            
            // 4. 서버에 집행 요청 (카드 ID 리스트와 평가용 JSON 전송)
            SubmitSpellServerRpc(selectedCardIds.ToArray(), evalJson, totalCost); 
        }

        // ==========================================
        // ☁️ [네트워크 영역] 클라이언트 -> 서버 전송
        // RequireOwnership = false로 두어야 턴 컨트롤러 주인이 아니어도 손님이 호출 가능
        // ==========================================
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSpellServerRpc(int[] cardIds, string evalJson, int declaredCost, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            PlayerModel caster = (senderId == NetworkManager.Singleton.LocalClientId) ? MyPlayer : EnemyPlayer;
            PlayerModel target = (senderId == NetworkManager.Singleton.LocalClientId) ? EnemyPlayer : MyPlayer;

            // 1. 서버 사이드 마나 검증 (보안)
            if (!caster.TryUseMana(declaredCost))
            {
                Debug.LogError($"[Server] Client {senderId} 마나 부족/핵 의심.");
                return;
            }

            // 2. 서버에서 Payload 재구성 (서버 권한으로 커맨드 생성)
            SpellPayload serverPayload = new SpellPayload();
            foreach (int id in cardIds)
            {
                var cardData = CardDatabase.GetCard(id);
                if (cardData != null)
                {
                    // 서버에서도 동일하게 커맨드 조립 (타겟은 senderId 기준 재배정)
                    cardData.AddToPayload(serverPayload, caster, target);
                }
            }

            // 3. TODO: evalJson과 녹음 파일을 웹 서버로 쏘고 배율(multiplier) 응답 대기
            float serverMultiplier = 1.0f; // 임시값

            // 4. 최종 집행
            ApplyPayloadToModels(serverPayload, serverMultiplier, caster);
        }

        // ==========================================
        // 🛡️ [서버 영역] 실제 집행 (오직 서버만 실행 가능)
        // ==========================================
        private void ApplyPayloadToModels(SpellPayload payload, float multiplier, PlayerModel caster)
        {
            if (!IsServer) return;

            // 1. 캡슐화된 커맨드들을 그냥 순서대로 실행 (배율 적용)
            foreach (var command in payload.Commands) command.Execute(multiplier);
            // 2. 영창의 속성 계산
            payload.CalculateMainProperty();

            // 3. 플레이어 모델에 전달
            if (payload.MainProperty != Property.None)
            {
                caster.LastProperty.Value = payload.MainProperty;
                Debug.Log($"[Server] {caster.OwnerClientId}의 속성이 {payload.MainProperty}로 갱신되었습니다."); //(todo) UI에게 전파
            }

            Debug.Log("[Server] 주문 집행 및 속성 기록 완료.");
        }

        public override void OnNetworkSpawn()
        {
            // 1. Model의 데이터 변경 구독 -> View 업데이트
            model.OnPhaseChangedEvent += HandlePhaseChanged;

            // 2. view 버튼 클릭 구독 (todo) -> StartGame(게임 시작하기), AdvancePhaseServerRpc(페이즈 넘기기)
        }

        // ==========================================
        // [로컬] 데이터가 바뀌면 화면과 로직을 제어함
        // ==========================================
        private void HandlePhaseChanged(GamePhase newPhase, bool isMyTurn)
        {
            // View에게 UI 업데이트 지시
            view.UpdateUI(newPhase, isMyTurn);

            // 페이즈별 클라이언트 로직 처리
            switch (newPhase)
            {
                case GamePhase.Draw:
                    if (isMyTurn) view.LogMessage("내 턴입니다! 카드를 뽑으세요.");
                    break;
                case GamePhase.Incantation:
                    if (isMyTurn) view.LogMessage("스페이스바를 눌러 마법을 영창하세요!");
                    break;
                case GamePhase.Battle:
                    if (IsServer) Invoke(nameof(ForceAdvancePhaseForBattle), 2f); // 2초 뒤 자동 턴 종료
                    break;
            }
        }

        // ==========================================
        // [통신] 버튼 클릭 시 서버에 페이즈 전환 요청
        // ==========================================
        public void RequestAdvancePhase()
        {
            AdvancePhaseServerRpc(); // 서버로 RPC 발송
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void AdvancePhaseServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            
            // 권한 체크: 현재 턴인 사람만 페이즈를 넘길 수 있음
            if (senderId != model.CurrentTurnPlayerId.Value) return;

            // 페이즈 진행 로직 (상태 전이)
            AdvancePhaseLogic();
        }

        // 배틀 페이즈 등 서버가 강제로 페이즈를 넘겨야 할 때 사용
        private void ForceAdvancePhaseForBattle()
        {
            if (IsServer) AdvancePhaseLogic();
        }

        private void AdvancePhaseLogic()
        {
            switch (model.CurrentPhase.Value)
            {
                case GamePhase.Wait: model.CurrentPhase.Value = GamePhase.Draw; break;
                case GamePhase.Draw: model.CurrentPhase.Value = GamePhase.Select; break;
                case GamePhase.Select: model.CurrentPhase.Value = GamePhase.Incantation; break;
                case GamePhase.Incantation: model.CurrentPhase.Value = GamePhase.Battle; break;
                case GamePhase.Battle: model.CurrentPhase.Value = GamePhase.End; break;
                case GamePhase.End:
                    model.CurrentTurnPlayerId.Value = 
                        (model.CurrentTurnPlayerId.Value == model.HostId.Value) 
                        ? model.GuestId.Value 
                        : model.HostId.Value;

                    model.CurrentPhase.Value = GamePhase.Draw;
                    break;
            }
        }

        // (방장 전용) 게임 시작 함수
        public void StartGame()
        {
            if (!IsServer) return;
            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
            if(connectedClients.Count >= 2)
            {
                model.HostId.Value = connectedClients[0];
                model.GuestId.Value = connectedClients[1];

                SpawnPlayer(model.HostId.Value, hostSpawnPoint.position);
                SpawnPlayer(model.GuestId.Value, guestSpawnPoint.position);

                model.CurrentTurnPlayerId.Value = model.HostId.Value;    
                model.CurrentPhase.Value = GamePhase.Draw;

                Debug.Log($"게임시작. 호스트: {model.HostId.Value}, 게스트: {model.GuestId.Value}");
            }
            else
            {
                Debug.LogWarning("플레이어 수 부족"); //todo: ui에게 알려주기
            }
            
        }

        private void SpawnPlayer(ulong clientId, Vector3 position)
        {
            // 서버에서 프리팹 생성
            GameObject playerObj = Instantiate(playerPrefab, position, Quaternion.identity);
            
            // 네트워크 상에 스폰하며, 해당 클라이언트에게 '소유권'을 넘깁니다.
            NetworkObject networkObj = playerObj.GetComponent<NetworkObject>();
            networkObj.SpawnAsPlayerObject(clientId);
            
            Debug.Log($"[Server] 플레이어 {clientId} 캐릭터 생성 완료");
        }
    }
}
