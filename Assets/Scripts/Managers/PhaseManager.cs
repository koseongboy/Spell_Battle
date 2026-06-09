using System.Collections.Generic;
using Cards.PlayableCards;
using Controllers.PlayerController;
using Controllers.SpellControllers;
using Models.Networks;
using UnityEngine;
using Unity.Netcode;
using Models.TurnModel;
using Models.PlayerModels;

namespace DefaultNamespace {
    public class PhaseManager : NetworkBehaviour {
        public static PhaseManager Instance { get; private set; }

        // PhaseManager.cs 내부 변수 추가
        private HashSet<ulong> phaseReadyPlayers = new HashSet<ulong>();

        private void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        
        
        public override void OnNetworkSpawn() {
            // 모델의 페이즈 값이 변할 때마다 클라이언트단 로직(UI 띄우기 등)을 실행하도록 구독
            TurnModel.Instance.CurrentPhase.OnValueChanged += HandlePhaseChanged;
        }

        public override void OnNetworkDespawn() {
            if (TurnModel.Instance != null)
                TurnModel.Instance.CurrentPhase.OnValueChanged -= HandlePhaseChanged;
        }

        // ========================================================
        // 1. [서버] 상태 전환 통제
        // ========================================================

        public void OnGameSetupCompleted() {
            if (!IsServer) return;
            Debug.Log("[PhaseManager] 세팅 완료. 멀리건 페이즈 진입.");
            TurnModel.Instance.CurrentPhase.Value = GamePhase.Mulligan;
        }

        public void OnMulliganCompleted(ulong firstPlayerId) {
            if (!IsServer) return;
            Debug.Log("[PhaseManager] 멀리건 완료. 1턴 Draw 페이즈 진입.");
            TurnModel.Instance.CurrentTurnPlayerId.Value = firstPlayerId;
            TurnModel.Instance.CurrentPhase.Value = GamePhase.Draw;
        }

        public void RequestIncantationPhase(ulong clientId) {
            RequestSpecificPhaseServerRpc(GamePhase.Incantation);
        }

        // ========================================================
        // 2. [클라이언트/서버 공통] 상태 변화 감지 후 액션 집행
        // ========================================================
        private void HandlePhaseChanged(GamePhase oldPhase, GamePhase newPhase) {
            bool isMyTurn = NetworkManager.Singleton.LocalClientId == TurnModel.Instance.CurrentTurnPlayerId.Value;

            switch (newPhase) {
                case GamePhase.Mulligan: {
                    NetworkObject localPlayerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();

                    if (localPlayerObj != null) {
                        // 내 캐릭터 프리팹에 붙어있는 모델과 핸들러를 연달아 가져온다.
                        PlayerModel myPlayer = localPlayerObj.GetComponent<PlayerModel>();
                        MulliganHandler myHandler = myPlayer.GetComponent<MulliganHandler>();

                        UILoader.Instance.ShowUI("Mulligan_FullScreen", myHandler);
                    }
                    else {
                        Debug.LogError("[PhaseManager] 내 로컬 네트워크 오브젝트를 찾을 수 없습니다!");
                    }

                    break;
                }

                case GamePhase.Draw: {
                    UILoader.Instance.HideUI("Mulligan_FullScreen");
                    UILoader.Instance.ShowUI("Ingame_FullScreen");
                    
                    // 턴 UI들 다 해주고
                    if (isMyTurn)
                    {
                        UILoader.Instance.HideUI("EnemyTurn_Top"); 
                        UILoader.Instance.ShowUI("MyTurn_Top");
                        if (UpperTurnUI.Instance != null) {
                            Debug.Log("진입");
                            UpperTurnUI.Instance.SetTurnState(true);
                        }
                    }
                    else
                    {
                        UILoader.Instance.HideUI("MyTurn_Top"); 
                        UILoader.Instance.ShowUI("EnemyTurn_Top");
                        if (UpperTurnUI.Instance != null) {
                            Debug.Log("진입");
                            UpperTurnUI.Instance.SetTurnState(false);
                        }
                    }
                    

                    StartCoroutine(WaitAndInjectUIData());

                    // 서버는 덱에서 카드를 뽑는 로직을 실행
                    if (IsServer) ExecuteDrawLogic(TurnModel.Instance.CurrentTurnPlayerId.Value);
                    break;
                }

                case GamePhase.Select:
                    break;

                case GamePhase.Incantation: {
                    if (isMyTurn) {

                    }
                    else {
                        CommonUIController.Instance.ShowBlackAlert("상대방이 영창 중입니다...");
                        Debug.Log("[Client] 상대 턴! 상대방의 영창을 기다립니다.");
                    }

                    break;
                }


                case GamePhase.Battle:
                    UILoader.Instance.HideUI("Spell_FullScreen");
                    UILoader.Instance.HideUI("SpellResult_FullScreen");
                    break;

                case GamePhase.End:
                    if(IsServer) ExecuteEndPhaseLogic();
                    break;
            }
        }

        private void ExecuteDrawLogic(ulong targetPlayerId) {
            PlayerModel targetPlayer = MatchManager.Instance.GetPlayerById(targetPlayerId);
            if (targetPlayer != null) {
                targetPlayer.Deck.DrawCard();
                Debug.Log($"[Server] 플레이어 {targetPlayerId} 대상 드로우 집행 완료.");
            }
        }


        // [클라이언트 UI 호출용] 턴 종료 버튼을 누르면 실행됨
        public void RequestEndTurn() {
            RequestSpecificPhaseServerRpc(GamePhase.End);
        }

        // [서버 전용] 마나 증가, 턴 권한 교체, 다음 턴 시작 처리
        private void ExecuteEndPhaseLogic() {
            if (!IsServer) return;

            // 1. 기존 TurnController에 있던 턴 종료 시 마나 최대치 1 증가 로직 유지
            // (MatchManager의 O(1) 캐싱 딕셔너리 활용)
            PlayerModel endingPlayer =
                MatchManager.Instance.GetPlayerById(TurnModel.Instance.CurrentTurnPlayerId.Value);
            if (endingPlayer != null) {
                endingPlayer.IncreaseMaxMana(3);
            }

            // 2. 상대방으로 턴 플레이어 ID 교체
            TurnModel.Instance.CurrentTurnPlayerId.Value =
                (TurnModel.Instance.CurrentTurnPlayerId.Value == TurnModel.Instance.HostId.Value)
                    ? TurnModel.Instance.GuestId.Value
                    : TurnModel.Instance.HostId.Value;

            // 3. 새로운 플레이어의 턴 시작 (Draw 페이즈)
            ServerSetPhase(GamePhase.Draw);
        }

        private System.Collections.IEnumerator WaitAndInjectUIData() {
            // 1. 비동기로 띄운 UI 싱글톤이 씬에 완전히 올라올 때까지 대기
            while (PlayerUI.Instance == null || EnemyUI.Instance == null) {
                yield return null;
            }

            // 2. 인스턴스가 확인되면 안전하게 로컬 및 적 데이터 주입
            NetworkObject localPlayerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            if (localPlayerObj != null) {
                PlayerController myController = localPlayerObj.GetComponent<PlayerController>();
                PlayerUI.Instance.ReceiveData(myController);
            }

            PlayerController enemyController = FindEnemyController();
            if (enemyController != null) {
                EnemyUI.Instance.ReceiveData(enemyController);
            }
            ReportPhaseReadyServerRpc(GamePhase.Draw);
        }

        private PlayerController FindEnemyController() {
            PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

            foreach (var player in allPlayers) {
                if (!player.IsOwner) {
                    return player;
                }
            }

            Debug.LogError("[PhaseManager] 씬에서 적 플레이어를 찾을 수 없습니다!");
            return null;
        }

        // [클라이언트 -> 서버] "나 해당 페이즈 연출(로딩) 다 끝났어!"
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ReportPhaseReadyServerRpc(GamePhase currentPhase, RpcParams rpcParams = default) {
            // 현재 페이즈와 보고된 페이즈가 다르면 무시 (비정상 통신 방어)
            if (TurnModel.Instance.CurrentPhase.Value != currentPhase) return;

            ulong senderId = rpcParams.Receive.SenderClientId;
            phaseReadyPlayers.Add(senderId);

            // 🌟 양쪽 모두 연출/로딩이 끝났다고 보고했다면?
            if (phaseReadyPlayers.Count == 2) {
                phaseReadyPlayers.Clear(); // 다음 페이즈를 위해 명단 초기화

                // Draw 페이즈 완료 보고가 모였으니, 진짜 Select 페이즈로 전환!
                if (currentPhase == GamePhase.Draw) {
                    Debug.Log("[Server] 양측 Draw 로딩 완료. Select 페이즈로 진입합니다.");
                    TurnModel.Instance.CurrentPhase.Value = GamePhase.Select;
                }
            }
        }

        // TODO : 리팩토링 필요
        public void StartSpell(List<PlayableCard> selectedCards) {
            var payload = SpellController.Instance.InitSpell(selectedCards);
            RequestSpecificPhaseServerRpc(GamePhase.Incantation);
            UILoader.Instance.HideUI("Ingame_FullScreen");
            UILoader.Instance.ShowUI("Spell_FullScreen", payload);
        }

        public void DoneEval(TaskStatusResponse evalResult) {
            CommonUIController.Instance.DoneLoading();
            
            UILoader.Instance.ShowUI("SpellResult_FullScreen", evalResult);
        }

        // ========================================================
        // 🛡️ NGO 철칙 적용: 페이즈 전환 중앙 통제 시스템
        // ========================================================

        #region 1. Client -> Server Requests (클라이언트의 페이즈 전환 요청)

        // [클라이언트 -> 서버] "다음 페이즈로 넘겨주세요" 요청
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestAdvancePhaseServerRpc(RpcParams rpcParams = default) {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (TurnModel.Instance.CurrentTurnPlayerId.Value != senderId) {
                Debug.LogWarning($"[Server] 턴 플레이어가 아닌 클라이언트({senderId})의 페이즈 진행 요청 거부.");
                return;
            }
            ServerAdvancePhase();
        }

        // [클라이언트 -> 서버] "특정 페이즈로 가주세요" 요청
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestSpecificPhaseServerRpc(GamePhase targetPhase, RpcParams rpcParams = default) {
            ulong senderId = rpcParams.Receive.SenderClientId;
            
            if (TurnModel.Instance.CurrentTurnPlayerId.Value != senderId) {
                Debug.LogWarning($"[Server] 권한 없는 클라이언트({senderId})의 강제 전환 요청 거부.");
                return;
            }
            ServerSetPhase(targetPhase);
        }

        #endregion


        #region 2. Server-Only Phase Controls (서버 단독 페이즈 제어)

        // [서버 전용] "다음 페이즈로 넘기기" 트리거
        public void ServerAdvancePhase() {
            if (!IsServer) return;
            AdvancePhaseLogic();
        }

        // [서버 전용] "특정 페이즈로 가기" 강제 실행 (모든 .Value 수정은 여기서만 일어납니다)
        public void ServerSetPhase(GamePhase newPhase) {
            if (!IsServer) return;
            
            GamePhase oldPhase = TurnModel.Instance.CurrentPhase.Value;
            if (oldPhase == newPhase) return; // 중복 호출 방지

            Debug.Log($"[Server] 🔄 페이즈 전환: {oldPhase} -> {newPhase}");
            TurnModel.Instance.CurrentPhase.Value = newPhase;
        }

        // [서버 전용] 페이즈 순서 연산 및 전환 내부 로직
        private void AdvancePhaseLogic() {
            if (!IsServer) return;

            GamePhase currentPhase = TurnModel.Instance.CurrentPhase.Value;
            GamePhase nextPhase;

            // 🌟 게임의 페이즈 흐름 정의 
            switch (currentPhase) {
                case GamePhase.Mulligan:
                    nextPhase = GamePhase.Draw;
                    break;
                case GamePhase.Draw:
                    nextPhase = GamePhase.Select;
                    break;
                case GamePhase.Select:
                    nextPhase = GamePhase.Incantation;
                    break;
                case GamePhase.Incantation:
                    nextPhase = GamePhase.Battle;
                    break;
                case GamePhase.Battle:
                    // 🌟 배틀 페이즈 직후: 타격 데미지로 누군가 죽었는지 체크!
                    if (CheckGameEndCondition()) {
                        nextPhase = GamePhase.EndGame;
                    } else {
                        nextPhase = GamePhase.Select; // (또는 기획에 따라 End)
                    }
                    break;

                case GamePhase.End:
                    // 🌟 엔드 페이즈 직후: 발화(Ignite) 도트 데미지로 누군가 죽었는지 체크!
                    if (CheckGameEndCondition()) {
                        ServerSetPhase(GamePhase.EndGame);
                    } else {
                        ExecuteEndPhaseLogic(); // 아무도 안 죽었으면 정상적으로 다음 턴으로
                    }
                    return; 
                    
                default:
                    nextPhase = GamePhase.Select;
                    break;
            }

            ServerSetPhase(nextPhase);
        }
        private bool CheckGameEndCondition() {

            PlayerModel host = MatchManager.Instance.GetPlayerById(TurnModel.Instance.HostId.Value);
            PlayerModel guest = MatchManager.Instance.GetPlayerById(TurnModel.Instance.GuestId.Value);

            if (host == null || guest == null) return false;

            return (host.CurrentHealth.Value <= 0 || guest.CurrentHealth.Value <= 0);
        }

        #endregion
    }

}