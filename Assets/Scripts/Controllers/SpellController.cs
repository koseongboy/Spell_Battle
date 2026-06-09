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

namespace Controllers.SpellControllers 
{
    public class SpellController : NetworkBehaviour 
    {
        public static SpellController Instance { get; private set; }
        
        [Header("배틀 씬 진짜 메인 카메라")]
        public Camera BattleMainCamera;
        
        [Header("연결된 플레이어 모델")]
        public PlayerModel MyPlayer;
        public PlayerModel EnemyPlayer;

        // ==========================================
        // 📦 임시 기억 변수들
        // ==========================================
        private List<PlayableCard> currentSelectedCards;
        private List<int> currentSelectedCardIds;
        private int currentTotalCost;
        private SpellPayload currentPayload;
        
        private string currentAudioUrl;
        private string currentTaskId;
        private AudioClip downloadedClip;

        public void Awake() 
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        #region 1~5단계 영창 및 통신 라이프사이클

        // ==========================================
        // 🛠️ 1. InitSpell() : 데이터 조립 및 페이로드 반환
        // ==========================================
        public SpellPayload InitSpell(List<PlayableCard> selectedCards) 
        {
            if (MyPlayer == null || EnemyPlayer == null) return null;

            currentSelectedCards = selectedCards;
            currentSelectedCardIds = new List<int>();
            currentTotalCost = 0;

            foreach (var card in selectedCards) 
            {
                currentTotalCost += card.uiData.cost;
                currentSelectedCardIds.Add(card.Id);
            }

            // 페이로드 및 랜덤 지시문 조립
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

        // ==========================================
        // 🎙️ 2. StartRecording() : 마이크 녹음 시작 (재시도 시 반복 호출됨)
        // ==========================================
        public void StartRecording()
        {
            Debug.Log("[SpellController] 2. 영창 녹음 시작!");
            VoiceManager.Instance.StartRecording();
        }

        // ==========================================
        // ⏹️ 3. EndRecording() : 녹음 종료, 서버 전송 및 평가 대기
        // ==========================================
        public async Task EndRecording() 
        {
            Debug.Log("[SpellController] 3. 영창 녹음 종료. 서버 전송 시작...");
            CommonUIController.Instance.ShowLoading();

            // 1. 녹음 데이터 추출
            byte[] myWavData = VoiceManager.Instance.StopRecording();

            // 2. 단어 리스트 추출
            List<string> selectedWordNames = new List<string>();
            foreach(var card in currentSelectedCards) 
            {
                selectedWordNames.Add(card.uiData.wordName);
            } 

            // 3. 웹 서버로 전송 (URL과 TaskID 확보)
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

                Debug.Log($"[SpellController] 서버 업로드 성공. TaskID: {currentTaskId}");
                ShareAudioUrlServerRpc(currentAudioUrl);

                TaskStatusResponse evalResult = null;
                int maxAttempts = 5;
                int attempt = 0;

                while (evalResult == null && attempt < maxAttempts)
                {
                    await Task.Delay(1000); 
                    evalResult = await WebServerModel.Instance.GetEvaluationResultAsync(currentTaskId);
                    attempt++;
                }
                
                // 5. 평가가 완료되면 PhaseManager 호출 (아직 구현 안됨)
                PhaseManager.Instance.DoneEval(evalResult);
                Debug.Log($"[SpellController] 평가 완료 통보 (PhaseManager 연동 예정). 점수: {evalResult}");
            }
            else
            {
                Debug.LogError("[SpellController] 서버 업로드 실패.");
            }
        }


        public void ClearSpellMemory()
        {
            currentSelectedCards?.Clear();
            currentSelectedCardIds?.Clear();
            
            currentTotalCost = 0;
            currentPayload = null;
            currentAudioUrl = string.Empty;
            currentTaskId = string.Empty;

            // 🌟 오디오 클립은 참조만 끊는게 아니라 파괴해서 메모리를 환원합니다.
            if (downloadedClip != null)
            {
                Destroy(downloadedClip);
                downloadedClip = null;
            }

            Debug.Log("[SpellController] 🧹 영창 및 마법 데이터 초기화 완료. 다음 턴 준비!");
        }

        // 서버가 명령하면 모든 클라이언트(Host, Guest)가 각자의 메모리를 비우는 통신 함수
        [Rpc(SendTo.Everyone)]
        private void ClearSpellMemoryClientRpc()
        {
            ClearSpellMemory();
        }

        #endregion

         #region 음성 파일 제어 로직

        // ==========================================
        // ⬇️ 4. DownloadAudio() : URL에서 오디오 다운로드 (재시도 불필요)
        // ==========================================
        public async Task DownloadAudio(string audioUrl)
        {
            Debug.Log($"[SpellController] 4. 오디오 다운로드 시작: {audioUrl}");
            
            // 🌟 핵심: 재녹음으로 인해 이미 다운받은 파일이 있다면 메모리에서 날려버립니다 (메모리 누수 방지)
            if (downloadedClip != null)
            {
                Destroy(downloadedClip);
                downloadedClip = null;
            }

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
                    Debug.LogError($"[SpellController] 오디오 다운로드 실패: {www.error}");
                }
            }
        }

       
        // ==========================================
        // 🔊 5. PlayVoice() : 다운로드된 음성 재생 (재시도 불필요)
        // ==========================================
        public void PlayVoice()
        {
            if (downloadedClip == null)
            {
                Debug.LogWarning("[SpellController] 재생할 오디오 클립이 없습니다.");
                return;
            }

            Debug.Log("[SpellController] 5. 상대방에게 음성 재생 시작.");
            
            // 기존에 만들어둔 VoiceManager의 스피커를 활용하여 재생합니다. todo: 음성 연결해야 함
            if (VoiceManager.Instance.testAudioSource != null)
            {
                VoiceManager.Instance.testAudioSource.clip = downloadedClip;
                VoiceManager.Instance.testAudioSource.Play();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ShareAudioUrlServerRpc(string audioUrl, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            // 발송자가 Host면 Guest에게, Guest면 Host에게 보낼 타겟 ID 설정
            ulong targetClientId = (senderId == TurnModel.Instance.HostId.Value) ? TurnModel.Instance.GuestId.Value : TurnModel.Instance.HostId.Value;

            // 타겟(상대방) 클라이언트에게만 다운로드 명령 하달
            DownloadAudioClientRpc(audioUrl, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void DownloadAudioClientRpc(string audioUrl, RpcParams rpcParams = default)
        {
            Debug.Log($"[Client] 📥 상대방의 (재)녹음 오디오 URL 수신! 백그라운드 다운로드 시작...");
            
            // 수신받은 URL로 오디오를 몰래 다운로드해둡니다. (재생은 아직 안 함!)
            _ = DownloadAudio(audioUrl);
        }
        #endregion

        // =========================================================================
        // 🌐 게임 서버(Netcode) 로직 : 확정된 마법을 전송하고 발동 (기존 로직 유지)
        // =========================================================================
        #region RPC 및 턴 제어 로직

        // 확정(최종 제출) 시 UI에서 호출할 임시 래퍼 함수
        public void SubmitConfirmedSpell()
        {
            SubmitSpellServerRpc(currentSelectedCardIds.ToArray(), currentTotalCost, currentTaskId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSpellServerRpc(int[] cardIds, int declaredCost, string taskId, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            PlayerModel caster = MatchManager.Instance.GetPlayerById(senderId);
            ulong targetClientId = (senderId == TurnModel.Instance.HostId.Value) ? TurnModel.Instance.GuestId.Value : TurnModel.Instance.HostId.Value;
            PlayerModel target = MatchManager.Instance.GetPlayerById(targetClientId);
            
            if (!caster.TryUseMana(declaredCost)) return;

            SpellPayload serverPayload = new SpellPayload();
            foreach (int id in cardIds) {
                var card = CardDatabase.Instance.GetCardById(id);
                if (card != null) serverPayload.EnqueuePendingCard(card);
            }

            serverPayload.CompileSpell(caster, target);
            StartCoroutine(FetchScoreAndExecuteBattle(serverPayload, caster, targetClientId, taskId));
        }


        private IEnumerator FetchScoreAndExecuteBattle(SpellPayload serverPayload, PlayerModel caster, ulong targetId, string taskId)
        {
            float finalScore = 0f;
            bool isEvaluationDone = false;

            while (!isEvaluationDone)
            {
                yield return new WaitForSeconds(2f);
                finalScore = 95.5f; 
                isEvaluationDone = true;
            }

            TurnModel.Instance.CurrentPhase.Value = GamePhase.Battle;

            float serverMultiplier = CalculateMultiplierFromScore(finalScore); 
            foreach (var command in serverPayload.Commands) 
            {
                yield return StartCoroutine(command.ExecuteRoutine(serverMultiplier));
            }
            AfterExecutingAllCards(serverPayload, caster);

            yield return new WaitForSeconds(2f);

            ClearSpellMemoryClientRpc();

            TurnModel.Instance.CurrentPhase.Value = GamePhase.Select;
        }

        private float CalculateMultiplierFromScore(float score)
        {
            return Mathf.Clamp(score / 100f, 0.5f, 1.5f);
        }
        //카드들 실행하고 핸드에서 무덤으로 보내는 등의 후처리
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

        #endregion
    }
}