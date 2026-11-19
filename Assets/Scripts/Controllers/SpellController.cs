using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Unity.Netcode;
using Models.TurnModel;
using Models.PlayerModels;
using Models.CardDatabases;
using Cards.PlayableCards;
using Models.SpellPayloads;
using DefaultNamespace;
using Managers.VoiceManagers;
using Cards.CardUIDatas;
using Models.Networks;
using System;

namespace Controllers.SpellControllers
{
    /// <summary>
    /// 주문 1회의 전체 라이프사이클을 담당한다.
    /// [클라이언트] 페이로드 조립 → 녹음 → 평가 서버 업로드/폴링 → 상대에게 음성 URL 공유
    /// [서버] 마나·평가 결과 재검증 → 커맨드 정렬/실행 → 손패·무덤 정리 → 다음 페이즈
    /// </summary>
    public class SpellController : NetworkBehaviour
    {
        public static SpellController Instance { get; private set; }
        
        [Header("평가 결과 폴링 최대 횟수 (1초 간격)")]
        public int maxAttempts = 20;
        [Header("배틀 씬 진짜 메인 카메라")]
        public Camera BattleMainCamera;

        
        [Header("연결된 플레이어 모델")]
        public PlayerModel MyPlayer;
        public PlayerModel EnemyPlayer;

        // ==========================================
        // 진행 중인 주문의 상태 (ClearSpellMemory에서 초기화)
        // ==========================================
        private List<PlayableCard> currentSelectedCards;
        private List<int> currentSelectedCardIds;
        private int currentTotalCost;
        private SpellPayload currentPayload;
        
        private string currentAudioUrl;
        private string currentTaskId;
        private AudioClip downloadedClip;

        [Header("녹음 데이터 보관")]
        public AudioClip LastRecordedClip { get; private set; } // 마지막으로 녹음한 내 음성 (영창자 측 재생용)
        public AudioSource audioSource;
        private float currentAudioLength = 3.5f; // 발동 연출 시간. 확정 시 실제 발화 길이로 갱신

        [Header("자동 캐싱 설정")]
        private Coroutine cacheCoroutine;
        private readonly float cacheInterval = 3.0f;

        public void Awake() 
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            PlayerModel.OnPlayerSpawned += HandlePlayerSpawned;
            PlayerModel.OnPlayerDespawned += HandlePlayerDespawned;
            if (audioSource == null) 
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
      

        private void Start() 
        {

        }

        public override void OnDestroy() 
        {
            PlayerModel.OnPlayerSpawned -= HandlePlayerSpawned;
            PlayerModel.OnPlayerDespawned -= HandlePlayerDespawned;
            if (cacheCoroutine != null) StopCoroutine(cacheCoroutine);
        }

        private void HandlePlayerSpawned(PlayerModel spawnedPlayer) {
            
            if (spawnedPlayer.IsOwner) {
                MyPlayer = spawnedPlayer;
                Debug.Log("[SpellController] 내 캐릭터 스폰 감지 완료! 자동 연결됨.");
            } 
            else {
                EnemyPlayer = spawnedPlayer;
                Debug.Log("[SpellController] 적 캐릭터 스폰 감지 완료! 자동 연결됨.");
            }
        }
        private void HandlePlayerDespawned(PlayerModel despawnedPlayer) {
            if (despawnedPlayer.IsOwner) {
                MyPlayer = null;
                Debug.LogWarning("[SpellController] 내 캐릭터 연결 끊김(Despawn) 감지. 참조를 비웁니다.");
            } 
            else {
                EnemyPlayer = null;
                Debug.LogWarning("[SpellController] 적 캐릭터 연결 끊김(Despawn) 감지. 참조를 비웁니다.");
            }
        }

        #region 1~5단계 영창 및 통신 라이프사이클

        // ==========================================
        // 1. InitSpell() : 선택 카드로 페이로드 조립
        // ==========================================
        /// <summary>
        /// [클라이언트] 선택한 카드의 코스트·ID를 기록하고, 랜덤 컨셉/선주문과 카드 단어로
        /// 영창 UI에 표시할 SpellPayload를 만든다. (실제 효과 계산은 서버에서 다시 수행)
        /// </summary>
        public SpellPayload InitSpell(List<PlayableCard> selectedCards)
        {
            if (MyPlayer == null || EnemyPlayer == null)
            {
                ForceReconnectPlayers();

                // 재탐색 후에도 없으면 스폰 지연/파괴 상황이므로 영창을 취소
                if (MyPlayer == null || EnemyPlayer == null)
                {
                    Debug.LogError("[SpellController] 🚨 씬에서 플레이어를 찾을 수 없어 영창을 취소합니다! (스폰 지연 또는 파괴됨)");
                    return null;
                }
            }

            currentSelectedCards = selectedCards;
            currentSelectedCardIds = new List<int>();
            currentTotalCost = 0;

            foreach (var card in selectedCards) 
            {
                currentTotalCost += card.uiData.cost;
                currentSelectedCardIds.Add(card.Id);
            }

            // 이번 영창의 연기 컨셉과 필수 선주문을 랜덤 배정
            currentPayload = new SpellPayload();
            var randomIncantation = Managers.IncantationManager.Instance.GetRandomIncantation();
            
            currentPayload.EvalData.Concept = randomIncantation.concept;
            currentPayload.EvalData.RequiredPrefix = randomIncantation.prefix;

            foreach (var card in currentSelectedCards) 
            {
                card.AddToPayload(currentPayload, MyPlayer, EnemyPlayer);
            }
            
            Debug.Log("[SpellController] 1. 스펠 초기화 및 페이로드 조립 완료.");
            return currentPayload;
        }

        // 스폰 이벤트를 놓쳐 참조가 비었을 때 씬을 직접 탐색해 복구하는 폴백
        public void ForceReconnectPlayers()
        {
            Debug.LogWarning("[SpellController] 🚨 플레이어 연결 끊김 감지! 씬에서 강제 탐색 및 재연결을 시도합니다.");

            PlayerModel[] allPlayers = FindObjectsByType<PlayerModel>(FindObjectsSortMode.None);

            foreach (var player in allPlayers)
            {
                if (player.IsOwner) 
                {
                    MyPlayer = player;
                    Debug.Log($"[SpellController] 🔗 MyPlayer 강제 연결 복구 완료: {player.gameObject.name}");
                }
                else 
                {
                    EnemyPlayer = player;
                    Debug.Log($"[SpellController] 🔗 EnemyPlayer 강제 연결 복구 완료: {player.gameObject.name}");
                }
            }
        }


        // ==========================================
        // 2. StartRecording() : 마이크 녹음 시작 (재녹음 시 반복 호출)
        // ==========================================
        public void StartRecording()
        {
            Debug.Log("[SpellController] 2. 영창 녹음 시작!");
            SoundManager.Instance.StartRecording();
        }

        // ==========================================
        // 3. EndRecording() : 녹음 종료 → 평가 서버 업로드 → 결과 폴링
        // ==========================================
        public async Task EndRecording()
        {
            Debug.Log("[SpellController] 3. 영창 녹음 종료. 서버 전송 시작...");
            CommonUIController.Instance.ShowLoading();

            // 3-1. 녹음 데이터 추출 및 로컬 재생용 클립 보관
            byte[] myWavData = SoundManager.Instance.StopRecording();

            LastRecordedClip = CreateClipFromWavBytes(myWavData);
            if (LastRecordedClip != null)
            {
                Debug.Log("[SpellController] 방금 녹음한 WAV 데이터를 AudioClip으로 변환 보관 완료.");
            }

            // 3-2. 무음이면 불필요한 AI 평가 요청을 보내지 않고 재녹음 유도
            if (SoundManager.Instance.IsAudioSilent(LastRecordedClip, 0.04f))
            {
                Debug.LogWarning("[SpellController] 🛑 녹음된 목소리가 감지되지 않았습니다. 서버 전송을 취소합니다.");
                
                await Task.Delay(2000);
                CommonUIController.Instance.DoneLoading();
                CommonUIController.Instance.ShowRedAlert("목소리가 작습니다. 다시 녹음해주세요");
                return; 
            }

            // 3-3. 평가에 필요한 카드 단어 목록 구성
            List<string> selectedWordNames = new List<string>();
            foreach(var card in currentSelectedCards) 
            {
                selectedWordNames.Add(card.uiData.wordName);
            } 

            // 3-4. 평가 서버 업로드 (음성 URL과 평가 TaskID 수신)
            var uploadResult = await WebServerModel.Instance.UploadVoiceAsync(
                myWavData, 
                Managers.LocalDataManagers.LocalDataManager.Instance.userId,
                currentPayload.EvalData.Concept, 
                currentPayload.EvalData.RequiredPrefix, 
                selectedWordNames
            );
            
            if (uploadResult != null)
            {
                currentAudioUrl = uploadResult.audioUrl;
                currentTaskId = uploadResult.taskId;

                Debug.Log($"[SpellController] 서버 업로드 성공. TaskID: {currentTaskId}, 음성 url {currentAudioUrl}");
                // 상대가 발동 시점에 바로 재생할 수 있도록 음성을 미리 내려받게 한다.
                ShareAudioUrlServerRpc(currentAudioUrl);

                // 3-5. AI 평가는 비동기 작업이므로 완료될 때까지 1초 간격으로 폴링
                TaskStatusResponse evalResult = null;

                int attempt = 0;

                while (evalResult == null && attempt < maxAttempts)
                {
                    await Task.Delay(1000); 
                    evalResult = await WebServerModel.Instance.GetEvaluationResultAsync(currentTaskId);
                    attempt++;
                }
                
                PhaseManager.Instance.DoneEval(evalResult);
            }
            else
            {
                Debug.LogError("[SpellController] 서버 업로드 실패.");
            }
        }
        // 16bit PCM WAV 바이트 배열을 AudioClip으로 변환 (표준 44바이트 헤더 가정)
        private AudioClip CreateClipFromWavBytes(byte[] wavData)
        {
            try
            {
                if (wavData == null || wavData.Length <= 44) return null;

                // WAV 헤더 정보 추출 (22: 채널 수, 24: 주파수, 40: 데이터 크기)
                int channels = wavData[22];
                int frequency = BitConverter.ToInt32(wavData, 24);
                int dataSize = BitConverter.ToInt32(wavData, 40);
                int samples = dataSize / (channels * 2); // 16bit(2byte) 기준 샘플 수 계산

                AudioClip clip = AudioClip.Create("MyRecordedVoice", samples, channels, frequency, false);
                float[] floatData = new float[samples * channels];

                // 16비트 PCM 데이터를 float(-1.0f ~ 1.0f)로 변환
                for (int i = 0; i < floatData.Length; i++)
                {
                    int byteIndex = 44 + i * 2; // 44바이트 헤더 스킵
                    if (byteIndex + 1 < wavData.Length)
                    {
                        short s = BitConverter.ToInt16(wavData, byteIndex);
                        floatData[i] = s / 32768f;
                    }
                }
                
                clip.SetData(floatData, 0);
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpellController] WAV 파싱 중 에러 발생: {e.Message}");
                return null;
            }
        }


        // 주문 1회가 끝난 뒤 상태와 오디오 리소스를 정리
        public void ClearSpellMemory()
        {
            currentSelectedCards?.Clear();
            currentSelectedCardIds?.Clear();
            
            currentTotalCost = 0;
            currentPayload = null;
            currentAudioUrl = string.Empty;
            currentTaskId = string.Empty;

            // 런타임 생성 AudioClip은 GC 대상이 아니므로 명시적으로 Destroy
            if (downloadedClip != null)
            {
                Destroy(downloadedClip);
                downloadedClip = null;
            }
            if (LastRecordedClip != null)
            {
                Destroy(LastRecordedClip);
                LastRecordedClip = null;
            }
            currentAudioLength = 3.5f;

            Debug.Log("[SpellController] 🧹 영창 및 마법 데이터 초기화 완료. 다음 턴 준비!");
        }

        // [서버 → 전체] 양측 클라이언트의 주문 상태 정리
        [Rpc(SendTo.Everyone)]
        private void ClearSpellMemoryClientRpc()
        {
            ClearSpellMemory();
        }

        #endregion

         #region 음성 파일 제어 로직

        // ==========================================
        // 4. DownloadAudio() : 상대 영창 음성 사전 다운로드
        // ==========================================
        public async Task DownloadAudio(string audioUrl)
        {
            Debug.Log($"[SpellController] 4. 오디오 다운로드 시작: {audioUrl}");

            // 상대가 재녹음하면 URL이 다시 오므로 이전 클립을 해제해 누수 방지
            if (downloadedClip != null)
            {
                Destroy(downloadedClip);
                downloadedClip = null;
            }

            try
            {
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.WAV))
                {
                    var operation = www.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        downloadedClip = DownloadHandlerAudioClip.GetContent(www);
                        Debug.Log("[SpellController] 오디오 백그라운드 다운로드(덮어쓰기) 완료 준비 대기!");
                    }
                    else
                    {
                        // 다운로드 실패가 게임 진행을 막지 않도록 무음 클립으로 대체
                        Debug.LogWarning($"[SpellController] 오디오 다운로드 통신 실패 (무시됨): {www.error}");
                        downloadedClip = AudioClip.Create("DummySilentClip", 44100, 1, 44100, false);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SpellController] 오디오 다운로드 중 예외 발생 (무시됨): {e.Message}");
                downloadedClip = AudioClip.Create("DummySilentClip", 44100, 1, 44100, false);
            }
        }

       
        // ==========================================
        // 5. PlayVoice() : 상대 측에서 다운로드한 영창 음성 재생
        // ==========================================
        public void PlayVoice()
        {
            UILoader.Instance.HideUI("Ingame_FullScreen");
            if (downloadedClip == null)
            {
                Debug.LogWarning("[SpellController] 재생할 오디오 클립이 없습니다.");
                return;
            }

            Debug.Log("[SpellController] 5. 상대방에게 음성 재생 시작.");
            
            // 출력 볼륨은 옵션에서 설정한 SoundManager 값을 따름
            if (SoundManager.Instance.testAudioSource != null)
            {
                audioSource.clip = downloadedClip;
                if (SoundManager.Instance != null)
                {
                    audioSource.volume = SoundManager.Instance.outputVolume;
                }
                audioSource.Play();
            }
        }

        // 영창자 측: 서버를 거치지 않고 로컬에 보관한 녹음본을 재생
        public void PlayRecordedAudio()
        {
            if (LastRecordedClip != null)
            {
                audioSource.clip = LastRecordedClip;

                if (SoundManager.Instance != null)
                {
                    audioSource.volume = SoundManager.Instance.outputVolume;
                }

                audioSource.Play();
                Debug.Log("[SpellController] 보관된 녹음 파일을 설정된 볼륨으로 재생합니다.");
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ShareAudioUrlServerRpc(string audioUrl, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            // 발신자의 상대 클라이언트에게만 다운로드 지시
            ulong targetClientId = (senderId == TurnModel.Instance.HostId.Value) ? TurnModel.Instance.GuestId.Value : TurnModel.Instance.HostId.Value;

            DownloadAudioClientRpc(audioUrl, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void DownloadAudioClientRpc(string audioUrl, RpcParams rpcParams = default)
        {
            Debug.Log($"[Client] 📥 상대방의 (재)녹음 오디오 URL 수신! 백그라운드 다운로드 시작...");
            
            // 재생은 주문 확정 후 ShowIncantationPopupClientRpc에서 수행
            _ = DownloadAudio(audioUrl);
        }

        // 녹음 끝부분의 무음 구간을 제외한 실제 발화 길이(초)를 구한다. 발동 연출 시간 산정에 사용
        private float GetActualSpeechLength(AudioClip clip)
        {
            if (clip == null) return 2.0f;

            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            float threshold = 0.02f;  // 이 진폭 이하는 무음(배경 잡음)으로 간주
            int lastSpokenSample = 0;

            // 뒤에서부터 탐색해 임계값을 넘는 마지막 샘플 위치를 찾는다.
            for (int i = samples.Length - 1; i >= 0; i--)
            {
                if (Mathf.Abs(samples[i]) > threshold)
                {
                    lastSpokenSample = i;
                    break;
                }
            }
            
            float actualLength = (float)lastSpokenSample / (clip.frequency * clip.channels);
            return actualLength;
        }
        #endregion

        // =========================================================================
        // 주문 확정 이후의 서버 권한 로직 (검증 → 발동 → 정리)
        // =========================================================================
        #region RPC 및 턴 제어 로직

        // [클라이언트] 결과 화면에서 '확정' 시 호출. 연출 시간을 실제 발화 길이에 맞춰 서버에 제출
        public void SubmitConfirmedSpell()
        {
            if (LastRecordedClip != null) 
            {
                float trueLength = GetActualSpeechLength(LastRecordedClip);
                currentAudioLength = Mathf.Clamp(trueLength + 0.5f, 2.0f, 10.0f);
                Debug.Log($"[SpellController] 내 오디오 클립 길이 측정 완료: {currentAudioLength}초");
            }
            SubmitSpellServerRpc(currentSelectedCardIds.ToArray(), currentTotalCost, currentTaskId, currentAudioLength);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSpellServerRpc(int[] cardIds, int declaredCost, string taskId, float audioLength, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            
            _ = ExecuteSpellVerificationAndBattleAsync(cardIds, declaredCost, taskId, senderId, audioLength);
        }

        /// <summary>
        /// [서버] 클라이언트가 보낸 값을 신뢰하지 않고 재검증한 뒤 주문을 발동한다.
        /// 마나는 서버 상태로 차감하고, 효과는 카드 ID로 서버에서 다시 조립하며,
        /// 점수는 TaskID로 평가 서버에서 직접 조회한다.
        /// </summary>
        private async Task ExecuteSpellVerificationAndBattleAsync(int[] cardIds, int declaredCost, string taskId, ulong senderId, float audioLength)
        {
            PlayerModel caster = MatchManager.Instance.GetPlayerById(senderId);
            ulong targetClientId = (senderId == TurnModel.Instance.HostId.Value) ? TurnModel.Instance.GuestId.Value : TurnModel.Instance.HostId.Value;
            PlayerModel target = MatchManager.Instance.GetPlayerById(targetClientId);

            // 1. 마나 검증 및 차감
            if (caster == null || !caster.TryUseMana(declaredCost)) return;

            // 2. 서버 측 페이로드 조립 및 커맨드 정렬
            SpellPayload serverPayload = new SpellPayload();
            foreach (int id in cardIds) {
                var card = CardDatabase.Instance.GetCardById(id);
                if (card != null) serverPayload.EnqueuePendingCard(card);
            }
            serverPayload.CompileSpell(caster, target);

            // 3. 평가 결과를 서버가 직접 조회 (클라이언트가 점수를 조작할 수 없도록)
            TaskStatusResponse evalResult = await WebServerModel.Instance.GetEvaluationResultAsync(taskId);

            // 조회 실패 시에도 게임이 멈추지 않도록 기본값으로 진행
            string verifiedSentence = evalResult != null ? evalResult.recognizedSentence : "알 수 없는 주문의 힘이 발동합니다!";
            float verifiedScore = evalResult != null ? evalResult.score : 0f;

            Debug.Log($"[Server] 🛡️ 검증 완료! 진짜 문장: {verifiedSentence}, 진짜 점수: {verifiedScore}");

            // 4. Battle 페이즈 전환 후 양측에 발동 연출(인식 문장 + 음성 재생) 지시
            PhaseManager.Instance.ServerSetPhase(GamePhase.Battle);

            ShowIncantationPopupClientRpc(verifiedSentence, serverPayload.MainProperty, senderId, audioLength);

            // 5. 연출이 끝나면 효과 실행
            StartCoroutine(ExecuteBattleRoutine(serverPayload, caster, verifiedScore, audioLength));
        }

        [Rpc(SendTo.Everyone)]
        private void ShowIncantationPopupClientRpc(string recognizedSentence, Property property, ulong casterId, float audioLength)
        {
            UILoader.Instance.ShowUI("SpellActive_FullScreen", (recognizedSentence, property));
            SoundManager.Instance.ToggleBGM(); // 영창 음성이 잘 들리도록 BGM 일시 정지
            if (NetworkManager.Singleton.LocalClientId == casterId)
            {
                PlayRecordedAudio(); // 영창자: 로컬 녹음본
            }
            else
            {
                PlayVoice(); // 상대: 미리 다운로드한 음성
            }
            StartCoroutine(HidePopupRoutine(audioLength));
        }
        private IEnumerator HidePopupRoutine(float delayTime)
        {
            // 음성 재생이 끝나는 시점에 팝업을 닫고 BGM 복구
            yield return new WaitForSeconds(delayTime);
            Debug.Log("[Client] 음성 재생 완료. 팝업 연출 종료!");
            SoundManager.Instance.ToggleBGM();
            UILoader.Instance.HideUI("SpellActive_FullScreen");
        }

        /// <summary>
        /// [서버] 발동 연출(음성 길이)만큼 대기 후, 정렬된 커맨드를 하나씩 순차 실행한다.
        /// 각 커맨드의 VFX가 끝난 뒤 다음 커맨드로 넘어가 효과가 한눈에 구분되도록 한다.
        /// </summary>
        private IEnumerator ExecuteBattleRoutine(SpellPayload payload, PlayerModel caster, float finalScore, float audioLength)
        {
            yield return new WaitForSeconds(audioLength);

            float scoreMultiplier = CalculateMultiplierFromScore(finalScore);
            foreach (var command in payload.Commands)
            {
                yield return StartCoroutine(command.ExecuteRoutine(scoreMultiplier));
            }

            AfterExecutingAllCards(payload, caster);

            yield return new WaitForSeconds(1.5f);

            ClearSpellMemoryClientRpc();
            PhaseManager.Instance.ServerAdvancePhase();
        }

        // 평가 점수 → 효과 배율 변환. 밸런스 조정 중으로 현재는 배율 1 고정
        private float CalculateMultiplierFromScore(float score)
        {
            // return Mathf.Clamp(score / 100f, 0.1f, 1.5f);
            return 1f;
        }

        // [서버] 주문 후처리: 마지막 사용 속성 기록, 사용한 카드를 손패에서 무덤으로 이동
        private void AfterExecutingAllCards(SpellPayload payload, PlayerModel caster)
        {
            if (!IsServer) return;

            payload.CalculateMainProperty();
            if (payload.MainProperty != Property.None) 
            {
                caster.LastProperty.Value = payload.MainProperty;
            }

            foreach (int cardId in payload.UsedCardIds) 
            {
                caster.Hand.RemoveCardFromServerHand(cardId);
                caster.Graveyard.AddCardToGraveyard(cardId);
            }
        }

        // [서버 → 게스트] 커맨드 VFX 재생 지시. 호스트는 호출부에서 직접 재생하므로 제외
        // 대상은 NetworkObjectId로 전달해 각 클라이언트에서 로컬 PlayerModel로 역참조한다.
        [Rpc(SendTo.NotServer)]
        public void PlayVisualEffectClientRpc(Models.EffectCommands.VFXType vfxType, Models.PlayerModels.StatusType relatedStatus, ulong targetNetworkObjectId)
        {
            if (Managers.VFX.BattleVFXManager.Instance == null) {
                Debug.LogError("[Client] 🚨 BattleVFXManager.Instance가 null입니다! 게스트 씬에 매니저가 있는지 확인하세요.");
                return;
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject netObj))
            {
                PlayerModel targetPlayer = netObj.GetComponent<PlayerModel>();
                if (targetPlayer != null)
                {
                    Debug.Log($"[Client] 🎬 넷코드 수신 성공! {targetPlayer.gameObject.name}에게 {vfxType} 연출을 재생합니다!");
                    StartCoroutine(Managers.VFX.BattleVFXManager.Instance.PlayVFXRoutine(vfxType, relatedStatus, targetPlayer));
                }
                else
                {
                    Debug.LogError("[Client] 🚨 NetworkObject를 찾았으나 PlayerModel 컴포넌트가 없습니다!");
                }
            }
            else
            {
                Debug.LogError($"[Client] 🚨 ID가 {targetNetworkObjectId}인 대상을 씬에서 찾을 수 없습니다!");
            }
        }

        


        #endregion
    }
}