using System.Collections.Generic;
using Cards.PlayableCards;
using Controllers.PlayerController;
using Controllers.SpellControllers;
using Models.Networks;
using UnityEngine;
using Unity.Netcode;
using Models.TurnModel;
using Models.PlayerModels;
using Controllers.PlayerSetup;

namespace DefaultNamespace {
    /// <summary>
    /// 게임 페이즈(Mulligan → Draw → Select → Incantation → Battle → End)의 전환을 중앙에서 통제한다.
    /// 페이즈 값(TurnModel.CurrentPhase)의 쓰기는 서버에서만 일어나며,
    /// 클라이언트는 RPC로 전환을 요청하고 값 변경 이벤트를 구독해 각자의 연출을 수행한다.
    /// </summary>
    public class PhaseManager : NetworkBehaviour {

        [Header("배틀 씬 진짜 메인 카메라")]
        public GameObject MainCamera;
        [Header("인트로 연출용 카메라")]
        public GameObject IntroCamera;
        [Header("인트로 시간 조절")]
        public float introTime = 5.0f;

        [Header("사운드 세팅")]
        public AudioClip battleBGM;
        public static PhaseManager Instance { get; private set; }

        // 현재 페이즈의 연출/로딩을 마쳤다고 보고한 클라이언트 목록 (서버 전용)
        private HashSet<ulong> phaseReadyPlayers = new HashSet<ulong>();

        private void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        
        
        private System.Collections.IEnumerator IntroRoutine() {
            // introTime 동안 인트로 카메라 연출을 재생하며 초기 로딩을 가린다.
            yield return new WaitForSeconds(introTime);

            Debug.Log("[PhaseManager] 인트로 연출 종료. 게임 시작 (Mulligan 진입)");
            TurnModel.Instance.CurrentPhase.Value = GamePhase.Mulligan;
        }
        
        public override void OnNetworkSpawn() {
            // 페이즈 값 변경 시 서버/클라이언트 모두 HandlePhaseChanged에서 후속 처리
            TurnModel.Instance.CurrentPhase.OnValueChanged += HandlePhaseChanged;
            if (NetworkManager.Singleton != null) {
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            }
        }

        public override void OnNetworkDespawn() {
            if (TurnModel.Instance != null)
                TurnModel.Instance.CurrentPhase.OnValueChanged -= HandlePhaseChanged;
            if (NetworkManager.Singleton != null) {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            }
        }

        // ========================================================
        // 1. [서버] 상태 전환 통제
        // ========================================================

        /// <summary>MatchManager가 초기 드로우까지 마친 뒤 호출. 인트로 연출 후 Mulligan으로 진입한다.</summary>
        public void OnGameSetupCompleted() {
            if (!IsServer) return;
            Debug.Log("[PhaseManager] 세팅 완료. 5초간 인트로 연출 시작...");
            SetUpBGMClientRpc();
            StartCoroutine(IntroRoutine());
        }

        [Rpc(SendTo.Everyone)]
        public void SetUpBGMClientRpc(RpcParams rpc = default)
        {
            if (battleBGM != null && Managers.VoiceManagers.SoundManager.Instance != null) {
                Managers.VoiceManagers.SoundManager.Instance.SetBgmAudioClip(battleBGM);
            }
        }

        /// <summary>양측 멀리건 제출이 끝나면 선공 플레이어의 Draw 페이즈로 진입한다.</summary>
        public void OnMulliganCompleted(ulong firstPlayerId) {
            if (!IsServer) return;
            Debug.Log("[PhaseManager] 멀리건 완료. 1턴 Draw 페이즈 진입.");
            TurnModel.Instance.CurrentTurnPlayerId.Value = firstPlayerId;
            TurnModel.Instance.CurrentPhase.Value = GamePhase.Draw;
        }

        public void RequestIncantationPhase(ulong clientId) {
            RequestSpecificPhaseServerRpc(GamePhase.Incantation);
        }
        /// <summary>
        /// 상대 이탈 시 남아 있는 쪽을 즉시 승리 처리한다.
        /// 정상적인 승패 판정(EndGame) 이후의 연결 해제는 무시한다.
        /// </summary>
        private void HandleClientDisconnect(ulong disconnectedClientId) {
            if (TurnModel.Instance.CurrentPhase.Value == GamePhase.EndGame) return;

            ulong myId = NetworkManager.Singleton.LocalClientId;

            if (IsServer) {
                // 호스트: 끊긴 ID가 자신이 아니면 게스트가 이탈한 것
                if (disconnectedClientId != myId) {
                    Debug.Log($"[PhaseManager] 🚨 게스트(ID: {disconnectedClientId}) 탈주 감지! 페이즈 이동 없이 즉시 승리 처리합니다.");
                    ForceLocalWinDueToDisconnect();
                }
            } 
            else {
                // 게스트: 이 콜백은 호스트(서버)와의 연결이 끊겼을 때 호출된다.
                Debug.Log($"[PhaseManager] 🚨 호스트(서버) 탈주 감지! 페이즈 이동 없이 즉시 승리 처리합니다.");
                ForceLocalWinDueToDisconnect();
            }
        }
        private void ForceLocalWinDueToDisconnect() {
            Debug.Log("[Client] 서버 연결 끊김. 강제 승리 UI를 호출합니다.");
            
            // 진행 중이던 인게임 UI를 모두 닫고 결과 화면만 남긴다.
            UILoader.Instance.HideUI("Ingame_FullScreen");
            UILoader.Instance.HideUI("MyTurn_Top");
            UILoader.Instance.HideUI("EnemyTurn_Top");
            UILoader.Instance.HideUI("Spell_FullScreen");
            UILoader.Instance.HideUI("SpellResult_FullScreen");
            UILoader.Instance.HideUI("Mulligan_FullScreen");
            UILoader.Instance.HideUI("SpellActive_FullScreen");


            UILoader.Instance.ShowUI("GameEnd_Top", GameEndType.Win);
        }
        

        // ========================================================
        // 2. [클라이언트/서버 공통] 상태 변화 감지 후 액션 집행
        // ========================================================
        private void HandlePhaseChanged(GamePhase oldPhase, GamePhase newPhase) {

            ulong currentTurnPlayerId = TurnModel.Instance.CurrentTurnPlayerId.Value;

            // [서버] 페이즈 진입 시점의 상태이상 처리(발화 피해, 지속시간 감소, 마나 회복 등)를 양측에 적용
            if (IsServer)
            {
                PlayerModel hostPlayer = MatchManager.Instance.GetPlayerById(TurnModel.Instance.HostId.Value); 
                PlayerModel guestPlayer = MatchManager.Instance.GetPlayerById(TurnModel.Instance.GuestId.Value); 

                if (hostPlayer != null)
                {
                    bool isHostTurn = currentTurnPlayerId == TurnModel.Instance.HostId.Value; 
                    hostPlayer.HandlePhaseEffects(newPhase, isHostTurn);
                }

                if (guestPlayer != null)
                {
                    bool isGuestTurn = currentTurnPlayerId == TurnModel.Instance.GuestId.Value; 
                    guestPlayer.HandlePhaseEffects(newPhase, isGuestTurn);
                }
            }
            bool isMyTurn = NetworkManager.Singleton.LocalClientId == TurnModel.Instance.CurrentTurnPlayerId.Value;

            switch (newPhase) {
                case GamePhase.Mulligan: {
                    if (IntroCamera != null) IntroCamera.SetActive(false);
                    if (MainCamera != null) 
                    {
                        MainCamera.SetActive(true);
                    }


                    NetworkObject localPlayerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();

                    if (localPlayerObj != null) {
                        // 로컬 플레이어 오브젝트에서 멀리건 핸들러를 꺼내 멀리건 UI에 주입
                        PlayerModel myPlayer = localPlayerObj.GetComponent<PlayerModel>();
                        MulliganHandler myHandler = myPlayer.GetComponent<MulliganHandler>();
                        PlayerSetup playerSetupManager = localPlayerObj.GetComponent<PlayerSetup>();
                        StartCoroutine(playerSetupManager.SetupCameraRoutine());

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
                    
                    // 턴 주인에 따라 내 턴/상대 턴 배너 전환
                    if (isMyTurn)
                    {
                        UILoader.Instance.HideUI("EnemyTurn_Top"); 
                        UILoader.Instance.ShowUI("MyTurn_Top");
                        if (UpperTurnUI.Instance != null) {
                            UpperTurnUI.Instance.SetTurnState(true);
                        }
                    }
                    else
                    {
                        UILoader.Instance.HideUI("MyTurn_Top"); 
                        UILoader.Instance.ShowUI("EnemyTurn_Top");
                        if (UpperTurnUI.Instance != null) {
                            UpperTurnUI.Instance.SetTurnState(false);
                        }
                    }
                    

                    // UI 바인딩이 끝나면 각 클라이언트가 준비 완료를 보고하고, 양측이 모이면 Select로 진입
                    StartCoroutine(WaitAndInjectUIData());

                    // 드로우는 서버 권한으로만 수행
                    if (IsServer) ExecuteDrawLogic(TurnModel.Instance.CurrentTurnPlayerId.Value);
                    break;
                }

                case GamePhase.Select:
                    UILoader.Instance.ShowUI("Ingame_FullScreen");
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

                case GamePhase.EndGame:
                    UILoader.Instance.HideUI("Ingame_FullScreen");
                    UILoader.Instance.HideUI("MyTurn_Top");
                    UILoader.Instance.HideUI("EnemyTurn_Top");
                    UILoader.Instance.HideUI("Spell_FullScreen");
                    UILoader.Instance.HideUI("SpellResult_FullScreen");
                    UILoader.Instance.HideUI("Mulligan_FullScreen");

                    
                    UILoader.Instance.ShowUI("GameEnd_Top", DetermineMyGameResult());
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


        // [클라이언트] 턴 종료 버튼에서 호출
        public void RequestEndTurn() {
            RequestSpecificPhaseServerRpc(GamePhase.End);
        }

        /// <summary>
        /// [서버] 턴 종료 처리: 사망자 확인 → 턴 종료 플레이어의 최대 마나 증가 → 턴 교체 → 다음 Draw.
        /// </summary>
        private void ExecuteEndPhaseLogic() {
            if (!IsServer) return;

            // End 진입 시 HandlePhaseEffects에서 발화 피해가 먼저 적용되므로 여기서 사망 여부를 확인한다.
            if (CheckGameEndCondition()) {
                Debug.Log("[PhaseManager] 🚨 발화 데미지로 사망자 발생! EndGame 페이즈로 진입합니다.");
                ServerSetPhase(GamePhase.EndGame);
                return;
            }

            // 1. 턴을 마친 플레이어의 최대 마나 +3 (FinalMana 상한)
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

        /// <summary>
        /// 비동기로 로드되는 플레이어/적 UI가 준비될 때까지 기다린 뒤 컨트롤러를 바인딩하고,
        /// 서버에 Draw 페이즈 준비 완료를 보고한다.
        /// </summary>
        private System.Collections.IEnumerator WaitAndInjectUIData() {
            while (PlayerUI.Instance == null || EnemyUI.Instance == null) {
                yield return null;
            }

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

        /// <summary>
        /// [클라이언트 → 서버] 해당 페이즈의 연출/로딩 완료 보고.
        /// 양측 보고가 모두 모였을 때만 다음 페이즈로 넘어가 연출 도중 전환되는 것을 막는다.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ReportPhaseReadyServerRpc(GamePhase currentPhase, RpcParams rpcParams = default) {
            // 이미 지나간 페이즈에 대한 늦은 보고는 무시
            if (TurnModel.Instance.CurrentPhase.Value != currentPhase) return;

            ulong senderId = rpcParams.Receive.SenderClientId;
            phaseReadyPlayers.Add(senderId);

            if (phaseReadyPlayers.Count == 2) {
                phaseReadyPlayers.Clear();

                if (currentPhase == GamePhase.Draw) {
                    Debug.Log("[Server] 양측 Draw 로딩 완료. Select 페이즈로 진입합니다.");
                    TurnModel.Instance.CurrentPhase.Value = GamePhase.Select;
                }
            }
        }


        /// <summary>[클라이언트] 선택한 카드로 주문을 조립하고 영창 화면으로 전환한다.</summary>
        public void StartSpell(List<PlayableCard> selectedCards) {
            var payload = SpellController.Instance.InitSpell(selectedCards);
            RequestSpecificPhaseServerRpc(GamePhase.Incantation);
            UILoader.Instance.HideUI("Ingame_FullScreen");
            UILoader.Instance.ShowUI("Spell_FullScreen", payload);
        }

        /// <summary>[클라이언트] AI 평가 결과 수신 후 결과 화면을 띄운다. (재녹음/확정 선택)</summary>
        public void DoneEval(TaskStatusResponse evalResult) {
            CommonUIController.Instance.DoneLoading();
            
            UILoader.Instance.ShowUI("SpellResult_FullScreen", evalResult);
        }

        // ========================================================
        // 페이즈 전환 중앙 통제
        // 클라이언트는 요청만 보내고, 실제 NetworkVariable 쓰기는 서버(ServerSetPhase)에서만 수행한다.
        // ========================================================

        #region 1. Client -> Server Requests (클라이언트의 페이즈 전환 요청)

        // [클라이언트 → 서버] 다음 페이즈 진행 요청 (턴 플레이어만 허용)
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestAdvancePhaseServerRpc(RpcParams rpcParams = default) {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (TurnModel.Instance.CurrentTurnPlayerId.Value != senderId) {
                Debug.LogWarning($"[Server] 턴 플레이어가 아닌 클라이언트({senderId})의 페이즈 진행 요청 거부.");
                return;
            }
            ServerAdvancePhase();
        }

        // [클라이언트 → 서버] 특정 페이즈로의 전환 요청 (턴 플레이어만 허용)
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

        // [서버] 정해진 흐름에 따라 다음 페이즈로 진행
        public void ServerAdvancePhase() {
            if (!IsServer) return;
            AdvancePhaseLogic();
        }

        // [서버] 지정한 페이즈로 전환. 페이즈 값 쓰기의 단일 진입점이다.
        public void ServerSetPhase(GamePhase newPhase) {
            if (!IsServer) return;

            GamePhase oldPhase = TurnModel.Instance.CurrentPhase.Value;
            if (oldPhase == newPhase) return; // 동일 페이즈로의 중복 전환 방지

            Debug.Log($"[Server] 🔄 페이즈 전환: {oldPhase} -> {newPhase}");
            TurnModel.Instance.CurrentPhase.Value = newPhase;
        }

        // [서버] 현재 페이즈 기준으로 다음 페이즈를 결정
        private void AdvancePhaseLogic() {
            if (!IsServer) return;

            GamePhase currentPhase = TurnModel.Instance.CurrentPhase.Value;
            GamePhase nextPhase;

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
                    // 주문 피해로 사망자가 나왔으면 게임 종료, 아니면 같은 턴에 추가 주문을 고를 수 있도록 Select로 복귀
                    if (CheckGameEndCondition()) {
                        nextPhase = GamePhase.EndGame;
                    } else {
                        nextPhase = GamePhase.Select;
                    }
                    break;

                case GamePhase.End:
                    // 턴 교체 및 다음 Draw 전환은 ExecuteEndPhaseLogic에서 직접 처리
                    ExecuteEndPhaseLogic();
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

            return host.CurrentHealth.Value <= 0 || guest.CurrentHealth.Value <= 0;
        }
        /// <summary>
        /// 로컬 플레이어 기준 승패를 판정한다.
        /// 양측 체력 차(호스트 - 게스트)로 비교하므로 동시 사망 등 체력이 같으면 무승부가 된다.
        /// </summary>
        private GameEndType DetermineMyGameResult() {
            PlayerModel host = MatchManager.Instance.GetPlayerById(TurnModel.Instance.HostId.Value);
            PlayerModel guest = MatchManager.Instance.GetPlayerById(TurnModel.Instance.GuestId.Value);

            if (host == null || guest == null) return GameEndType.Lose;

            int hpDifference = host.CurrentHealth.Value - guest.CurrentHealth.Value;

            ulong myId = NetworkManager.Singleton.LocalClientId;
            bool amIHost = myId == TurnModel.Instance.HostId.Value;

            if (hpDifference == 0) {
                return GameEndType.Draw;
            }
            else if (hpDifference > 0) {
                // 호스트 승리
                return amIHost ? GameEndType.Win : GameEndType.Lose;
            }
            else {
                // 게스트 승리
                return amIHost ? GameEndType.Lose : GameEndType.Win;
            }
        }

        #endregion
    }

}