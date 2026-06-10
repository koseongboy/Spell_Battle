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

        // PhaseManager.cs 내부 변수 추가
        private HashSet<ulong> phaseReadyPlayers = new HashSet<ulong>();

        private void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        
        
        private System.Collections.IEnumerator IntroRoutine() {
            // 5초 동안 대기 (이 시간 동안 인트로 카메라 애니메이션이 재생되며 로딩을 숨깁니다)
            yield return new WaitForSeconds(introTime);
            
            Debug.Log("[PhaseManager] 인트로 연출 종료. 게임 시작 (Mulligan 진입)");
            // 5초 뒤에 드디어 페이즈를 넘겨줍니다.
            TurnModel.Instance.CurrentPhase.Value = GamePhase.Mulligan;
        }
        
        public override void OnNetworkSpawn() {
            // 모델의 페이즈 값이 변할 때마다 클라이언트단 로직(UI 띄우기 등)을 실행하도록 구독
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

        public void OnGameSetupCompleted() {
            if (!IsServer) return;
            Debug.Log("[PhaseManager] 세팅 완료. 5초간 인트로 연출 시작...");
            if (battleBGM != null && Managers.VoiceManagers.SoundManager.Instance != null) {
                Managers.VoiceManagers.SoundManager.Instance.PlayBGM(battleBGM, 1.0f);
            }
            StartCoroutine(IntroRoutine());
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
        private void HandleClientDisconnect(ulong disconnectedClientId) {
            // 이미 게임이 정상적인 승패 판정으로 끝났다면 중복 처리 방지
            if (TurnModel.Instance.CurrentPhase.Value == GamePhase.EndGame) return;

            ulong myId = NetworkManager.Singleton.LocalClientId;

            if (IsServer) {
                // 👑 [호스트 입장] 나간 클라이언트가 내가 아니라면? 👉 게스트가 나간 것!
                if (disconnectedClientId != myId) {
                    Debug.Log($"[PhaseManager] 🚨 게스트(ID: {disconnectedClientId}) 탈주 감지! 페이즈 이동 없이 즉시 승리 처리합니다.");
                    ForceLocalWinDueToDisconnect();
                }
            } 
            else {
                // 👥 [게스트 입장] 서버가 터지거나 호스트가 나가면 게스트의 Disconnect 콜백이 호출됩니다.
                // 내가 스스로 '나가기'를 누른 게 아니라면 호스트가 튕긴 것이므로 즉시 승리 처리합니다.
                Debug.Log($"[PhaseManager] 🚨 호스트(서버) 탈주 감지! 페이즈 이동 없이 즉시 승리 처리합니다.");
                ForceLocalWinDueToDisconnect();
            }
        }
        private void ForceLocalWinDueToDisconnect() {
            Debug.Log("[Client] 서버 연결 끊김. 강제 승리 UI를 호출합니다.");
            
            // 기존에 떠 있던 불필요한 UI들을 모조리 가려줍니다.
            UILoader.Instance.HideUI("Ingame_FullScreen");
            UILoader.Instance.HideUI("MyTurn_Top");
            UILoader.Instance.HideUI("EnemyTurn_Top");
            UILoader.Instance.HideUI("Spell_FullScreen");
            UILoader.Instance.HideUI("SpellResult_FullScreen");
            UILoader.Instance.HideUI("Mulligan_FullScreen");
            UILoader.Instance.HideUI("SpellActive_FullScreen");
            

            // 🏆 묻지도 따지지도 않고 내가 이긴 것으로 UI 출력!
            UILoader.Instance.ShowUI("GameEnd_Top", GameEndType.Win);
        }
        

        // ========================================================
        // 2. [클라이언트/서버 공통] 상태 변화 감지 후 액션 집행
        // ========================================================
        private void HandlePhaseChanged(GamePhase oldPhase, GamePhase newPhase) {

            ulong currentTurnPlayerId = TurnModel.Instance.CurrentTurnPlayerId.Value; //
            
            // 🌟 [서버 전용 권한] 페이즈가 바뀔 때마다 모든 플레이어에게 페이즈 효과를 링크해 줍니다.
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
                    if (MainCamera != null) MainCamera.SetActive(true);


                    NetworkObject localPlayerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();

                    if (localPlayerObj != null) {
                        // 내 캐릭터 프리팹에 붙어있는 모델과 핸들러를 연달아 가져온다.
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
                    
                    // 턴 UI들 다 해주고
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
                    

                    StartCoroutine(WaitAndInjectUIData());

                    // 서버는 덱에서 카드를 뽑는 로직을 실행
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


        // [클라이언트 UI 호출용] 턴 종료 버튼을 누르면 실행됨
        public void RequestEndTurn() {
            RequestSpecificPhaseServerRpc(GamePhase.End);
        }

        // [서버 전용] 마나 증가, 턴 권한 교체, 다음 턴 시작 처리
        private void ExecuteEndPhaseLogic() {
            if (!IsServer) return;

            if (CheckGameEndCondition()) {
                Debug.Log("[PhaseManager] 🚨 발화 데미지로 사망자 발생! EndGame 페이즈로 진입합니다.");
                ServerSetPhase(GamePhase.EndGame);
                return;
            }

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
                    ExecuteEndPhaseLogic(); // 아무도 안 죽었으면 정상적으로 다음 턴으로
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
        private GameEndType DetermineMyGameResult() {
            // 1. 호스트와 게스트의 플레이어 모델을 가져옵니다.
            PlayerModel host = MatchManager.Instance.GetPlayerById(TurnModel.Instance.HostId.Value);
            PlayerModel guest = MatchManager.Instance.GetPlayerById(TurnModel.Instance.GuestId.Value);

            // 예외 방어 (씬에서 플레이어를 못 찾은 경우)
            if (host == null || guest == null) return GameEndType.Lose; 

            // 🌟 2. 기획하신 핵심 로직: 호스트 체력 - 게스트 체력
            int hpDifference = host.CurrentHealth.Value - guest.CurrentHealth.Value;

            // 3. 내가 현재 호스트인지 게스트인지 판별합니다.
            ulong myId = NetworkManager.Singleton.LocalClientId;
            bool amIHost = myId == TurnModel.Instance.HostId.Value;

            // 4. 차이값에 따라 승/무/패를 결정합니다.
            if (hpDifference == 0) {
                // 차이가 0이면 무승부
                return GameEndType.Draw;
            }
            else if (hpDifference > 0) {
                // 양수(0 초과)면 호스트 승리
                // 내가 호스트라면 Win, 게스트라면 Lose를 반환합니다.
                return amIHost ? GameEndType.Win : GameEndType.Lose;
            }
            else {
                // 음수(0 미만)면 게스트 승리
                // 내가 호스트라면 Lose, 게스트라면 Win을 반환합니다.
                return amIHost ? GameEndType.Lose : GameEndType.Win;
            }
        }

        #endregion
    }

}