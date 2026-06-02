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
using System.Collections;

namespace Controllers.TurnControllers {
    public class TurnController : NetworkBehaviour {
        #region 0. 테스트용 코드

        public void ManualStartBattleTest() {
            if (IsServer) {
                Debug.Log("🛠️ 수동으로 전투를 초기화합니다!");
                InitializeRoomAndSpawnPlayers();
            }
            else {
                Debug.LogWarning("방장(Host) 에디터에서만 실행할 수 있습니다!");
            }
        }

        private System.Collections.IEnumerator DummyWaitTask(float delaySeconds)
        {
            Debug.Log($"[Server] ⏳ 테스트 모드: {delaySeconds}초 임시 대기 중...");
            yield return new WaitForSeconds(delaySeconds);
        }
        #endregion


        #region 1. 싱글톤 및 기본 변수 세팅 (Initialization)

        public static TurnController Instance { get; private set; }

        [Header("연결된 플레이어 모델. 동적 할당이니 인스펙터에 박을 필요 X")]
        public PlayerModel MyPlayer; // 클라이언트 본인

        public PlayerModel EnemyPlayer; // 상대방

        [Header("MVP References")] [SerializeField]
        private TurnModel model;

        [SerializeField] private TurnView view;

        [Header("Spawning")] [SerializeField] private GameObject playerPrefab; // 플레이어 캐릭터 프리팹
        [SerializeField] private Transform hostSpawnPoint; // 방장 위치
        [SerializeField] private Transform guestSpawnPoint; // 손님 위치

        [Header("멀리건 관련")] [SerializeField] private HashSet<ulong> mulliganReadyPlayers = new HashSet<ulong>();


        public void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn() {
            // Model의 데이터 변경 구독 -> View 업데이트
            model.OnPhaseChangedEvent += HandlePhaseChanged;
            if (IsServer) {
                InitializeRoomAndSpawnPlayers();
            }
        }

        #endregion

        #region 2. 게임 준비 및 스폰 (Ready & Spawn)

        private void InitializeRoomAndSpawnPlayers() {
            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;

            if (connectedClients.Count >= 2) {
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
            else {
                Debug.LogWarning("[Server] 접속한 플레이어가 2명 미만입니다.");
            }
        }

        // 🌟 버튼 대기(SubmitReady) 대신, 서버가 알아서 확인하고 넘겨주는 자동화 코루틴
        private System.Collections.IEnumerator WaitUntilDecksReadyAndStart() {
            Debug.Log("[Server] 플레이어들의 덱 세팅을 기다립니다...");

            PlayerModel host = null;
            PlayerModel guest = null;

            // 두 플레이어가 맵에 소환되었고, 둘 다 덱 세팅(IsDeckReady)이 완료될 때까지 기다림
            while (true) {
                host = GetPlayerById(model.HostId.Value);
                guest = GetPlayerById(model.GuestId.Value);

                if (host != null && guest != null &&
                    host.Deck.IsDeckReady.Value && guest.Deck.IsDeckReady.Value) {
                    break; // 모든 조건이 충족되면 루프 탈출!
                }

                yield return null;
            }

            // 대기 탈출! 유저들이 덱을 모두 제출했으므로 즉시 StartGame 실행
            StartGame();
        }

        // [서버 전용] 진짜 게임 룰 세팅 시작
        public void StartGame() {
            if (!IsServer) return;

            Debug.Log("[Server] 모두 준비 완료! 선후공 토스 및 초기 드로우를 시작합니다.");

            // 1. 코인 토스 (선후공 결정, todo: ui 전달. 함수로 따로 뺄 수도? + 처음 카드 뽑는거도 애니메이션으로 가는 지)
            bool isHostFirst = Random.value > 0.5f;
            ulong firstPlayerId = isHostFirst ? model.HostId.Value : model.GuestId.Value;
            ulong secondPlayerId = isHostFirst ? model.GuestId.Value : model.HostId.Value;
            if (isHostFirst) Debug.Log("[Server] 방장 선턴! 방장 4장, 손님 5장 드로우");
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

        private void SpawnPlayer(ulong clientId, Vector3 position) {
            GameObject playerObj = Instantiate(playerPrefab, position, Quaternion.identity);
            NetworkObject networkObj = playerObj.GetComponent<NetworkObject>();
            networkObj.SpawnAsPlayerObject(clientId);
            Debug.Log($"[Server] 플레이어 {clientId} 캐릭터 생성 완료");
        }

        #endregion

        #region 3. 멀리건 시스템 (Mulligan)

        // [클라이언트 -> 서버] 하스스톤 식 멀리건 집행
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubmitMulliganServerRpc(int[] replaceCardIds, RpcParams rpcParams = default) {
            ulong clientId = rpcParams.Receive.SenderClientId;
            PlayerModel targetPlayer = GetPlayerById(clientId);

            List<int> tempPocket = new List<int>();

            foreach (int id in replaceCardIds) {
                if (targetPlayer.Hand.RemoveCardFromServerHand(id)) tempPocket.Add(id);
                else Debug.LogWarning("[Server] 보안경고! 없는 카드를 멀리건 하려 하고 있다!!!!!!!");
            }

            for (int i = 0; i < tempPocket.Count; i++) {
                targetPlayer.Deck.DrawCard();
            }

            foreach (int id in tempPocket) {
                targetPlayer.Deck.InsertCard(id, shuffleAfter: false);
            }

            targetPlayer.Deck.Shuffle();

            Debug.Log($"[Server] 플레이어 {clientId}의 멀리건 완료. (교체된 카드 수: {tempPocket.Count})");
            ReportMulliganReady(clientId);
        }

        // 🌟 에러 원인 2: 멀리건 완료 검사 로직 추가
        // [서버 전용] 양측 플레이어가 모두 멀리건을 마쳤는지 확인하고 1턴 시작
        public void ReportMulliganReady(ulong clientId) {
            if (!IsServer) return;

            mulliganReadyPlayers.Add(clientId);

            if (mulliganReadyPlayers.Count == 2) {
                Debug.Log("[Server] 양측 멀리건 완료! 진짜 1턴(Draw Phase) 시작!");
                model.CurrentTurnPlayerId.Value = model.FirstPlayerId.Value;
                model.CurrentPhase.Value = GamePhase.Draw;
            }
        }

        #endregion

        #region 4. 페이즈 흐름 제어 (Phase Management)
        // ui띄우는 건 여기다 하면 됨 (todo)
        private void HandlePhaseChanged(GamePhase newPhase, bool isMyTurn)
        {
            view.UpdateUI(newPhase, isMyTurn);

            switch (newPhase) {
                case GamePhase.Draw:
                    if (isMyTurn) view.LogMessage("내 턴 시작! 카드를 드로우합니다.");
                    
                    // 🌟 서버인 경우: 현재 턴인 플레이어 모델을 찾아서 인자로 넘겨줍니다!
                    if (IsServer) 
                    {
                        ulong currentPlayerId = model.CurrentTurnPlayerId.Value;
                        PlayerModel currentPlayer = GetPlayerById(currentPlayerId);
                        
                        if (currentPlayer != null)
                        {
                            ExecuteAutoDraw(currentPlayer); //알아서 여기서 기다렸다가 다음 페이즈로 넘어감.
                        }
                    }
                    break;
                case GamePhase.Incantation:
                    if (isMyTurn) view.LogMessage("스페이스바를 눌러 마법을 영창하세요!");
                    break;
                case GamePhase.Battle:
                    //todo 배틀 어쩌고 하기
                    break;
            }
        }

        public void RequestAdvancePhase() {
            AdvancePhaseServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void AdvancePhaseServerRpc(RpcParams rpcParams = default) {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (senderId != model.CurrentTurnPlayerId.Value) return;

            AdvancePhaseLogic();
        }

        private IEnumerator ForceAdvancePhaseAfterTask(IEnumerator task, GamePhase expectedPhase)
        {
            // 1. 외부에서 넘겨받은 작업(애니메이션 or 테스트 딜레이)이 완전히 끝날 때까지 대기!
            if (task != null)
            {
                yield return StartCoroutine(task);
            }

            // 2. 작업이 끝난 후, 예상했던 페이즈에 안전하게 머물러 있는지 체크하고 넘김
            if (IsServer && model.CurrentPhase.Value == expectedPhase)
            {
                AdvancePhaseLogic();
            }
        }

        private void AdvancePhaseLogic()
        {
            switch (model.CurrentPhase.Value)
            {
                case GamePhase.Wait: model.CurrentPhase.Value = GamePhase.Draw; break;
                case GamePhase.Draw: model.CurrentPhase.Value = GamePhase.Select; break;
                case GamePhase.Select: model.CurrentPhase.Value = GamePhase.Incantation; break;
                case GamePhase.Incantation: model.CurrentPhase.Value = GamePhase.Battle; break;
                case GamePhase.Battle: model.CurrentPhase.Value = GamePhase.Select; break; // 일단 기본적으로는 select로 돌아감.
                case GamePhase.End:
                    PlayerModel endingPlayer = GetPlayerById(model.CurrentTurnPlayerId.Value);
                    if (endingPlayer != null)
                    {
                        endingPlayer.IncreaseMaxMana(1);
                    }
                    model.CurrentTurnPlayerId.Value = 
                        (model.CurrentTurnPlayerId.Value == model.HostId.Value) 
                        ? model.GuestId.Value 
                        : model.HostId.Value;

                    model.CurrentPhase.Value = GamePhase.Draw;
                    break;
            }
        }

        private void ExecuteAutoDraw(PlayerModel targetPlayer)
        {
            targetPlayer.Deck.DrawCard();
            Debug.Log($"[Server] 플레이어 {targetPlayer.OwnerClientId} 대상 자동 드로우 완료.");

            // 🌟 나중에 진짜 애니메이션이 생기면 아래처럼 교체하시면 됩니다.
            // IEnumerator animationTask = targetPlayer.View.PlayDrawAnimation();
            // StartCoroutine(ForceAdvancePhaseAfterTask(animationTask, GamePhase.Draw));

            // 지금은 테스트 중이므로, 임시로 1초 대기하는 작업을 넘겨줍니다.
            System.Collections.IEnumerator testTask = DummyWaitTask(1f);
            StartCoroutine(ForceAdvancePhaseAfterTask(testTask, GamePhase.Draw));
        }

        //턴 엔드 로직
        public void RequestEndTurn()
        {
            EndTurnServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void EndTurnServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            
            // 1. 방어선: 내 턴이 아니거나, 선택 페이즈가 아니면 무시
            if (senderId != model.CurrentTurnPlayerId.Value) return;
            if (model.CurrentPhase.Value != GamePhase.Select) return;

            Debug.Log($"[Server] 🛑 플레이어 {senderId}가 턴을 종료했습니다.");
            
            // 2. 현재 페이즈를 End로 강제로 밀어넣고, 다음 사람의 턴으로 넘김
            model.CurrentPhase.Value = GamePhase.End;
            AdvancePhaseLogic(); 
        }

        #endregion

        #region 5. 마법 영창 및 집행 (Spell Casting)

        // [클라이언트 전용] 페이로드 조립
        public void ProcessSpellCast(List<PlayableCard> selectedCards) {
            if (MyPlayer == null || EnemyPlayer == null) {
                Debug.LogError("플레이어가 아직 전장에 소환되지 않았습니다!");
                return;
            }

            int totalCost = 0;
            List<int> selectedCardIds = new List<int>();

            foreach (var card in selectedCards) {
                totalCost += card.uiData.cost;
                selectedCardIds.Add(card.Id);
            }

            // 🌟 1. 마나 1차 검증 (UI 통과 여부)
            if (MyPlayer.CurrentMana.Value < totalCost)
            {
                Debug.LogWarning($"마나가 부족합니다. (필요: {totalCost} / 보유: {MyPlayer.CurrentMana.Value})");
                return;
            }
            // 배틀 페이즈로
            RequestAdvancePhase();

            // 검증을 통과했다면 영창(마이크 대기) 코루틴으로 진입!
            StartCoroutine(IncantationRoutine(selectedCards, selectedCardIds, totalCost));
        }
        // ==========================================
        // 🎙️ [수정됨] 웹 서버 통신 및 게임 서버 연동 코루틴
        // ==========================================
        private IEnumerator IncantationRoutine(List<PlayableCard> selectedCards, List<int> selectedCardIds, int totalCost)
        {
            SpellPayload payload = new SpellPayload();
            
            //todo ㅜㅜ
            payload.EvalData.Concept = "건방지게";
            payload.EvalData.RequiredPrefix = "칠흑의 심연에서 눈뜬 자여";

            List<string> keywordList = new List<string>();
            foreach (var card in selectedCards)
            {
                card.AddToPayload(payload, MyPlayer, EnemyPlayer);
                keywordList.Add(card.uiData.wordName); 
            }

            //todo ui에 표시해야 함.
            string fullIncantation = $"접두어: {payload.EvalData.RequiredPrefix}\n 영창용 단어들: {string.Join(", ", keywordList)}";

            Debug.Log($"[Incantation UI] 🗣️ 낭독할 문장: {fullIncantation}");

            // ----------------------------------------------------
            // 🎙️ STEP 1: 마이크 녹음 대기 (어절 단위 UI 피드백 연동)
            // ----------------------------------------------------
            bool isRecordingFinished = false;
            byte[] recordedWavData = null; 

            Debug.Log("[Client] 🎤 마이크 녹음을 시작합니다...");
            // TODO: 실제 녹음 로직 실행 및 어절 단위 UI 게이지바 갱신
            // yield return StartCoroutine(VoiceManager.RecordAudioCoroutine((data) => { recordedWavData = data; isRecordingFinished = true; }));
            
            // (임시 테스트용 대기)
            yield return new WaitForSeconds(1f); 
            
            // ----------------------------------------------------
            // 🌐 STEP 2: 웹 서버로 JSON 전송 & 다운로드 URL 즉시 수신
            // ----------------------------------------------------
            string audioDownloadUrl = "";
            string evaluationTaskId = "";
            
            Debug.Log("[Client] 🌐 웹 서버로 음성 데이터 및 평가 JSON을 전송합니다...");
            // TODO: UnityWebRequest 등을 통해 웹 서버로 POST 요청
            // yield return StartCoroutine(WebManager.UploadVoiceData(recordedWavData, payload.EvalData.ToJson(), (url) => { audioDownloadUrl = url; }));

            // (임시 URL 할당)
            audioDownloadUrl = "http://mywebserver.com/audio/test1234.wav";
            evaluationTaskId = "TASK_987654321";
            Debug.Log($"임시 url: {audioDownloadUrl}, 임시 테스크 id: {evaluationTaskId}를 받음");

            // ----------------------------------------------------
            // 🎮 STEP 3: 게임 서버로 최종 데이터 제출 (배틀 돌입)
            // ----------------------------------------------------
            string evalJson = payload.EvalData.ToJson();
            SubmitSpellServerRpc(selectedCardIds.ToArray(), evalJson, totalCost, audioDownloadUrl, evaluationTaskId);
        }


        
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSpellServerRpc(int[] cardIds, string evalJson, int declaredCost, string audioUrl, string taskId, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            PlayerModel caster = GetPlayerById(senderId);
            ulong targetClientId = (senderId == model.HostId.Value) ? model.GuestId.Value : model.HostId.Value;
            PlayerModel target = GetPlayerById(targetClientId);
            
            if (!caster.TryUseMana(declaredCost)) return;

            // 1. 상대방에게 오디오 URL 공유 (즉시 실행)
            PlayOpponentAudioClientRpc(audioUrl, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));

            SpellPayload serverPayload = new SpellPayload();
            foreach (int id in cardIds)
            {
                var card = CardDatabase.GetCardById(id) as PlayableCard;
                if (card != null) 
                {
                    serverPayload.EnqueuePendingCard(card); //
                }
            }

            serverPayload.CompileSpell(caster, target);

            // 2. 서버가 직접 웹 서버에 점수를 물어보러 출발!
            StartCoroutine(FetchScoreAndExecuteBattle(serverPayload, caster, targetClientId, taskId));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void PlayOpponentAudioClientRpc(string audioUrl, RpcParams rpcParams = default)
        {
            Debug.Log($"[Client] 🔊 상대방의 영창이 들려옵니다! URL 다운로드 및 재생 시작: {audioUrl}");
            // TODO: 해당 URL에서 오디오를 다운받아 AudioSource로 재생하는 로직
        }


        // ==========================================
        // 🛡️ [서버 전용] 웹 서버와 S2S 직접 통신 및 배틀 집행
        // ==========================================
        private IEnumerator FetchScoreAndExecuteBattle(SpellPayload serverPayload, PlayerModel caster, ulong targetId, string taskId)
        {
            float finalScore = 0f;
            bool isEvaluationDone = false;
            PlayerModel target = GetPlayerById(targetId);

            Debug.Log($"[Server] 🛡️ 웹 서버에 Task ID({taskId})의 평가 결과를 직접 요청합니다...");

            // 폴링(Polling) 루프: 평가가 끝날 때까지 1초마다 물어봅니다.
            while (!isEvaluationDone)
            {
                // 실제로는 아래와 같이 UnityWebRequest를 사용해 웹 서버를 찌릅니다.
                /*
                using (UnityEngine.Networking.UnityWebRequest webRequest = UnityEngine.Networking.UnityWebRequest.Get($"http://mywebserver.com/api/score?taskId={taskId}"))
                {
                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        // JSON 응답 파싱 (예: {"status":"done", "score":95.5})
                        // 파싱 결과 status가 done이면 루프 탈출
                        finalScore = 95.5f; 
                        isEvaluationDone = true;
                    }
                }
                */

                // 임시 테스트용 (2초 대기 후 강제 성공)
                yield return new WaitForSeconds(2f);
                finalScore = 95.5f; // 임시 점수
                isEvaluationDone = true;
                
                // 아직 안 끝났다면 1초 대기 후 다시 요청
                if (!isEvaluationDone) yield return new WaitForSeconds(1f);
            }

            Debug.Log($"[Server] 🛡️ 검증 완료! 조작 없는 순수 점수 확보: {finalScore}");

            model.CurrentPhase.Value = GamePhase.Battle;

            float serverMultiplier = CalculateMultiplierFromScore(finalScore); 
            ApplyPayloadToModels(serverPayload, serverMultiplier, caster);

            IEnumerator battleTask = DummyWaitTask(2f);
            StartCoroutine(ForceAdvancePhaseAfterTask(battleTask, GamePhase.Battle));
        }
        // 기획에 따라 점수를 배율로 바꿔주는 헬퍼 함수 (todo)
        private float CalculateMultiplierFromScore(float score)
        {
            // 임시 공식: 기본 1.0배 (점수에 따라 0.5배 ~ 1.5배까지 변동)
            return Mathf.Clamp(score / 100f, 0.5f, 1.5f);
        }

        // [서버 전용] 효과 집행 및 카드 무덤행
        private void ApplyPayloadToModels(SpellPayload payload, float multiplier, PlayerModel caster) {
            if (!IsServer) return;

            foreach (var command in payload.Commands) {
                command.Execute(multiplier);
            }

            payload.CalculateMainProperty();

            if (payload.MainProperty != Property.None) {
                caster.LastProperty.Value = payload.MainProperty;
                Debug.Log($"[Server] {caster.OwnerClientId}의 속성이 {payload.MainProperty}로 갱신되었습니다.");
            }

            // 사용된 카드를 서버 손패에서 지우고 무덤으로 이동
            foreach (int cardId in payload.UsedCardIds) {
                caster.Hand.RemoveCardFromServerHand(cardId);
                caster.Graveyard.AddCardToGraveyard(cardId);
            }

            Debug.Log("[Server] 주문 집행 및 속성/묘지 기록 완료.");
        }

        #endregion

        #region 6. 유틸리티 (Utilities)

        // 🌟 에러 원인 1: 플레이어 ID로 오브젝트를 찾아주는 함수 추가
        public PlayerModel GetPlayerById(ulong clientId) {
            PlayerModel[] players = FindObjectsByType<PlayerModel>(FindObjectsSortMode.None);
            foreach (var p in players) {
                if (p.OwnerClientId == clientId) return p;
            }

            return null;
        }

        #endregion
    }
}