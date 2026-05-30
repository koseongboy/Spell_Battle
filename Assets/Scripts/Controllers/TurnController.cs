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
using NUnit.Framework;

namespace Controllers.TurnControllers
{
    public class TurnController : NetworkBehaviour
    {
        #region 0. 테스트용 코드
        public void ManualStartBattleTest()
        {
            if (IsServer)
            {
                Debug.Log("🛠️ 수동으로 전투를 초기화합니다!");
                InitializeRoomAndSpawnPlayers();
            }
            else
            {
                Debug.LogWarning("방장(Host) 에디터에서만 실행할 수 있습니다!");
            }
        }
        #endregion


        #region 1. 싱글톤 및 기본 변수 세팅 (Initialization)
        
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
        
        [Header("멀리건 관련")]
        [SerializeField] private HashSet<ulong> mulliganReadyPlayers = new HashSet<ulong>();

        

        public void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            // Model의 데이터 변경 구독 -> View 업데이트
            model.OnPhaseChangedEvent += HandlePhaseChanged;
            if (IsServer)
            {
                InitializeRoomAndSpawnPlayers();
            }
        }

        
        
        #endregion

        #region 2. 게임 준비 및 스폰 (Ready & Spawn)

        private void InitializeRoomAndSpawnPlayers()
        {
            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
            
            if (connectedClients.Count >= 2)
            {
                // 1. ID 등록
                model.HostId.Value = connectedClients[0];
                model.GuestId.Value = connectedClients[1];

                // 2. 캐릭터 사전 소환! 
                SpawnPlayer(model.HostId.Value, hostSpawnPoint.position);
                SpawnPlayer(model.GuestId.Value, guestSpawnPoint.position);

                Debug.Log($"[Server] 방 세팅 완료. 호스트: {model.HostId.Value}, 게스트: {model.GuestId.Value}");
                
                // 🌟 3. 스폰이 끝나자마자 바로 '덱 제출 여부' 자동 감시 시작!
                StartCoroutine(WaitUntilDecksReadyAndStart());
            }
            else
            {
                Debug.LogWarning("[Server] 접속한 플레이어가 2명 미만입니다."); 
            }
        }

        // 🌟 버튼 대기(SubmitReady) 대신, 서버가 알아서 확인하고 넘겨주는 자동화 코루틴
        private System.Collections.IEnumerator WaitUntilDecksReadyAndStart()
        {
            Debug.Log("[Server] 플레이어들의 덱 세팅을 기다립니다...");

            PlayerModel host = null;
            PlayerModel guest = null;

            // 두 플레이어가 맵에 소환되었고, 둘 다 덱 세팅(IsDeckReady)이 완료될 때까지 기다림
            while (true)
            {
                host = GetPlayerById(model.HostId.Value);
                guest = GetPlayerById(model.GuestId.Value);

                if (host != null && guest != null && 
                    host.Deck.IsDeckReady.Value && guest.Deck.IsDeckReady.Value)
                {
                    break; // 모든 조건이 충족되면 루프 탈출!
                }
                yield return null;
            }

            // 대기 탈출! 유저들이 덱을 모두 제출했으므로 즉시 StartGame 실행
            StartGame();
        }

        // [서버 전용] 진짜 게임 룰 세팅 시작
        public void StartGame()
        {
           if (!IsServer) return;
            
            Debug.Log("[Server] 모두 준비 완료! 선후공 토스 및 초기 드로우를 시작합니다.");

            // 1. 코인 토스 (선후공 결정, todo: ui 전달. 함수로 따로 뺄 수도?)
            bool isHostFirst = Random.value > 0.5f;
            ulong firstPlayerId = isHostFirst ? model.HostId.Value : model.GuestId.Value;
            ulong secondPlayerId = isHostFirst ? model.GuestId.Value : model.HostId.Value;
            if(isHostFirst) Debug.Log("[Server] 방장 선턴! 방장 4장, 손님 5장 드로우");
            else Debug.Log("[Server] 손님 선턴! 방장 5장, 손님 4장 드로우");

            PlayerModel firstPlayer = GetPlayerById(firstPlayerId);
            PlayerModel secondPlayer = GetPlayerById(secondPlayerId);

            // 2. 초기 카드 지급 (선공 4장, 후공 5장)
            for (int i = 0; i < 4; i++) firstPlayer.Deck.DrawCard();
            for (int i = 0; i < 5; i++) secondPlayer.Deck.DrawCard();

            // 3. 페이즈 설정 (멀리건으로 진입)
            model.FirstPlayerId.Value = firstPlayerId; 
            model.CurrentTurnPlayerId.Value = firstPlayerId; 
            model.CurrentPhase.Value = GamePhase.Mulligan;
        }

        private void SpawnPlayer(ulong clientId, Vector3 position)
        {
            GameObject playerObj = Instantiate(playerPrefab, position, Quaternion.identity);
            NetworkObject networkObj = playerObj.GetComponent<NetworkObject>();
            networkObj.SpawnAsPlayerObject(clientId);
            Debug.Log($"[Server] 플레이어 {clientId} 캐릭터 생성 완료");
        }

        #endregion

        #region 3. 멀리건 시스템 (Mulligan)

        // [클라이언트 -> 서버] 하스스톤 식 멀리건 집행
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubmitMulliganServerRpc(int[] replaceCardIds, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            PlayerModel targetPlayer = GetPlayerById(clientId);

            List<int> tempPocket = new List<int>();

            foreach (int id in replaceCardIds)
            {
                if(targetPlayer.Hand.RemoveCardFromServerHand(id)) tempPocket.Add(id);
                else Debug.LogWarning("[Server] 보안경고! 없는 카드를 멀리건 하려 하고 있다!!!!!!!");
            }

            for (int i = 0; i < tempPocket.Count; i++)
            {
                targetPlayer.Deck.DrawCard(); 
            }

            foreach (int id in tempPocket)
            {
                targetPlayer.Deck.InsertCard(id, shuffleAfter: false); 
            }
            
            targetPlayer.Deck.Shuffle();

            Debug.Log($"[Server] 플레이어 {clientId}의 멀리건 완료. (교체된 카드 수: {tempPocket.Count})");
            ReportMulliganReady(clientId);
        }

        // 🌟 에러 원인 2: 멀리건 완료 검사 로직 추가
        // [서버 전용] 양측 플레이어가 모두 멀리건을 마쳤는지 확인하고 1턴 시작
        public void ReportMulliganReady(ulong clientId)
        {
            if (!IsServer) return;

            mulliganReadyPlayers.Add(clientId);

            if (mulliganReadyPlayers.Count == 2)
            {
                Debug.Log("[Server] 양측 멀리건 완료! 진짜 1턴(Draw Phase) 시작!");
                model.CurrentTurnPlayerId.Value = model.FirstPlayerId.Value;
                model.CurrentPhase.Value = GamePhase.Draw;
                
                // 선공 플레이어에게 1턴 알림 드로우
                GetPlayerById(model.FirstPlayerId.Value).Deck.DrawCard();
            }
        }


        
        #endregion

        #region 4. 페이즈 흐름 제어 (Phase Management)

        private void HandlePhaseChanged(GamePhase newPhase, bool isMyTurn)
        {
            view.UpdateUI(newPhase, isMyTurn);

            switch (newPhase)
            {
                case GamePhase.Draw:
                    if (isMyTurn) view.LogMessage("내 턴입니다! 카드를 뽑으세요.");
                    break;
                case GamePhase.Incantation:
                    if (isMyTurn) view.LogMessage("스페이스바를 눌러 마법을 영창하세요!");
                    break;
                case GamePhase.Battle:
                    if (IsServer) Invoke(nameof(ForceAdvancePhaseForBattle), 2f); 
                    break;
            }
        }

        public void RequestAdvancePhase()
        {
            AdvancePhaseServerRpc(); 
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void AdvancePhaseServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (senderId != model.CurrentTurnPlayerId.Value) return;

            AdvancePhaseLogic();
        }

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

        #endregion

        #region 5. 마법 영창 및 집행 (Spell Casting)

        // [클라이언트 전용] 페이로드 조립
        public void ProcessSpellCast(List<PlayableCard> selectedCards)
        {
            if (MyPlayer == null || EnemyPlayer == null)
            {
                Debug.LogError("플레이어가 아직 전장에 소환되지 않았습니다!");
                return;
            }
            
            int totalCost = 0;
            List<int> selectedCardIds = new List<int>(); 
            
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

            SpellPayload payload = new SpellPayload();
            
            payload.EvalData.Concept = "건방지게";
            payload.EvalData.RequiredPrefix = "칠흑의 심연에서 눈뜬 자여";

            foreach (var card in selectedCards)
            {
                card.AddToPayload(payload, MyPlayer, EnemyPlayer);
            }

            string evalJson = payload.EvalData.ToJson();
            SubmitSpellServerRpc(selectedCardIds.ToArray(), evalJson, totalCost); 
        }

        // [클라이언트 -> 서버] 서류철 제출
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSpellServerRpc(int[] cardIds, string evalJson, int declaredCost, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            PlayerModel caster = (senderId == NetworkManager.Singleton.LocalClientId) ? MyPlayer : EnemyPlayer;
            PlayerModel target = (senderId == NetworkManager.Singleton.LocalClientId) ? EnemyPlayer : MyPlayer;

            if (!caster.TryUseMana(declaredCost))
            {
                Debug.LogError($"[Server] Client {senderId} 마나 부족/핵 의심.");
                return;
            }

            SpellPayload serverPayload = new SpellPayload();
            foreach (int id in cardIds)
            {
                var cardData = CardDatabase.GetCardById(id);
                if (cardData != null)
                {
                    cardData.AddToPayload(serverPayload, caster, target);
                }
            }

            float serverMultiplier = 1.0f; // 임시값
            ApplyPayloadToModels(serverPayload, serverMultiplier, caster);
        }

        // [서버 전용] 효과 집행 및 카드 무덤행
        private void ApplyPayloadToModels(SpellPayload payload, float multiplier, PlayerModel caster)
        {
            if (!IsServer) return;

            foreach (var command in payload.Commands) 
            {
                command.Execute(multiplier);
            }
            
            payload.CalculateMainProperty();

            if (payload.MainProperty != Property.None)
            {
                caster.LastProperty.Value = payload.MainProperty;
                Debug.Log($"[Server] {caster.OwnerClientId}의 속성이 {payload.MainProperty}로 갱신되었습니다."); 
            }

            // 사용된 카드를 서버 손패에서 지우고 무덤으로 이동
            foreach (int cardId in payload.UsedCardIds)
            {
                caster.Hand.RemoveCardFromServerHand(cardId);
                caster.Graveyard.AddCardToGraveyard(cardId);
            }

            Debug.Log("[Server] 주문 집행 및 속성/묘지 기록 완료.");
        }

        #endregion

        #region 6. 유틸리티 (Utilities)

        // 🌟 에러 원인 1: 플레이어 ID로 오브젝트를 찾아주는 함수 추가
        public PlayerModel GetPlayerById(ulong clientId)
        {
            PlayerModel[] players = FindObjectsByType<PlayerModel>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (p.OwnerClientId == clientId) return p;
            }
            return null;
        }

        #endregion
    }
}