using System.Collections;
using System.Collections.Generic;
using Cards.CardUIDatas;
using Unity.Netcode;
using UnityEngine;
using Models.TurnModel;
using Models.PlayerModels;
using Models.CardDatabases;
using Cards.PlayableCards;
using Models.SpellPayloads;
using DefaultNamespace;
using Managers.VoiceManagers;
using System.Threading.Tasks;

namespace Controllers.TurnControllers 
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
        // 📦 영창 페이즈 동안 정보를 기억해 둘 임시 변수들
        // ==========================================
        private List<PlayableCard> currentSelectedCards;
        private List<int> currentSelectedCardIds;
        private int currentTotalCost;
        private SpellPayload currentPayload;

        public void Awake() 
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // =========================================================================
        // 🚨 아래부터는 추후 'VoiceIncantationClient'와 'SpellExecutionServer'로 
        // 완벽히 쪼개어 분리할 '영창 및 전투 집행' 관련 찌꺼기 코드들입니다.
        // =========================================================================

        #region 마법 영창 및 집행 (현재 분리 대기 중)

        // [클라이언트 전용] 페이로드 조립
        public void ProcessSpellCast(List<PlayableCard> selectedCards) 
        {
            if (MyPlayer == null || EnemyPlayer == null) return;

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
            
            // 🌟 수정됨: 기존 RequestAdvancePhase 대신 PhaseManager를 직접 호출
            PhaseManager.Instance.RequestIncantationPhase(NetworkManager.Singleton.LocalClientId);
            StartIncantation(selectedCards, selectedCardIds, totalCost);
        }


        public void StartIncantation(List<PlayableCard> selectedCards, List<int> selectedCardIds, int totalCost)
        {
            // 1. 다음 함수에서 쓸 수 있게 데이터 임시 저장
            currentSelectedCards = selectedCards;
            currentSelectedCardIds = selectedCardIds;
            currentTotalCost = totalCost;

            // 2. 컨셉/접두어 및 페이로드 조립
            currentPayload = new SpellPayload();
            var randomIncantation = Managers.IncantationManager.Instance.GetRandomIncantation();
            
            currentPayload.EvalData.Concept = randomIncantation.concept;
            currentPayload.EvalData.RequiredPrefix = randomIncantation.prefix;

            foreach (var card in currentSelectedCards) 
            {
                card.AddToPayload(currentPayload, MyPlayer, EnemyPlayer);
            }
            
            // 3. 🌟 유저에게 읽을 대본(UI)을 화면에 띄웁니다.
            UILoader.Instance.ShowUI("SpellWordPiece", currentPayload);

            // 4. 🌟 대본이 떴으니, 마이크 녹음을 본격적으로 시작합니다!
            VoiceManager.Instance.StartRecording();
            
            Debug.Log("[Incantation] 영창 시작! 유저의 음성 입력을 대기합니다...");
            // 여기서 코드는 끝납니다. 이제 유저가 말을 다 하고 버튼을 누를 때까지 기다립니다.
        }

        // ==========================================
        // 🚀 2. 영창 완료 및 서버 전송 (UI의 "완료" 버튼 클릭 시 호출)
        // ==========================================
        // IEnumerator 대신 순수 async Task를 사용하여 빨간 줄을 없앱니다!
        public async Task FinishIncantationAsync() 
        {
            Debug.Log("[Incantation] 영창 종료 버튼 클릭. 서버 전송을 시작합니다.");

            // 1. 🌟 녹음을 즉시 종료하고 바이트 데이터를 뽑아옵니다.
            byte[] myWavData = VoiceManager.Instance.StopRecording();

            // (선택) 더 이상 필요 없는 영창 UI를 닫아줍니다.
            // UILoader.Instance.HideUI("SpellWordPiece");

            // 2. 단어 리스트 추출
            List<string> selectedWordNames = new List<string>();
            foreach(var card in currentSelectedCards) 
            {
                selectedWordNames.Add(card.uiData.wordName);
            } 

            // 3. 🌐 AWS 웹 서버로 전송 (await로 결과를 기다림)
            var uploadResult = await Models.Networks.WebServerModel.Instance.UploadVoiceAsync(
                myWavData, 
                Managers.LocalDataManagers.LocalDataManager.Instance.userId, // 캐싱해둔 진짜 유저 ID 사용
                currentPayload.EvalData.Concept, 
                currentPayload.EvalData.RequiredPrefix, 
                selectedWordNames
            );
            
            // 4. 통신 성공 시 게임 서버(Netcode)로 결과 공유
            if (uploadResult != null)
            {
                string audioDownloadUrl = uploadResult.audioUrl;
                string evaluationTaskId = uploadResult.taskId;
                
                SubmitSpellServerRpc(currentSelectedCardIds.ToArray(), currentTotalCost, audioDownloadUrl, evaluationTaskId);
            }
            else
            {
                Debug.LogError("[Incantation] 웹 서버 업로드 실패로 인해 마법 발동이 취소되었습니다.");
                // (선택) 실패 시 마나를 롤백하거나 에러 팝업을 띄우는 로직
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSpellServerRpc(int[] cardIds, int declaredCost, string audioUrl, string taskId, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            // 🌟 수정됨: MatchManager의 O(1) 캐싱 딕셔너리로 플레이어 탐색 최적화
            PlayerModel caster = MatchManager.Instance.GetPlayerById(senderId);
            ulong targetClientId = (senderId == TurnModel.Instance.HostId.Value) ? TurnModel.Instance.GuestId.Value : TurnModel.Instance.HostId.Value;
            PlayerModel target = MatchManager.Instance.GetPlayerById(targetClientId);
            
            if (!caster.TryUseMana(declaredCost)) return;

            PlayOpponentAudioClientRpc(audioUrl, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));

            SpellPayload serverPayload = new SpellPayload();
            foreach (int id in cardIds) {
                var card = CardDatabase.Instance.GetCardById(id);
                if (card != null) serverPayload.EnqueuePendingCard(card);
            }

            serverPayload.CompileSpell(caster, target);
            StartCoroutine(FetchScoreAndExecuteBattle(serverPayload, caster, targetClientId, taskId));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void PlayOpponentAudioClientRpc(string audioUrl, RpcParams rpcParams = default)
        {
            Debug.Log($"[Client] 🔊 상대방 영창 URL 수신: {audioUrl}");
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
            ApplyPayloadToModels(serverPayload, serverMultiplier, caster);

            // 🌟 수정됨: 기존 ForceAdvancePhaseAfterTask 삭제로 인한 수동 턴 넘김 임시 처리
            yield return new WaitForSeconds(2f);
            TurnModel.Instance.CurrentPhase.Value = GamePhase.Select;
        }

        private float CalculateMultiplierFromScore(float score)
        {
            return Mathf.Clamp(score / 100f, 0.5f, 1.5f);
        }

        private void ApplyPayloadToModels(SpellPayload payload, float multiplier, PlayerModel caster) 
        {
            if (!IsServer) return;

            foreach (var command in payload.Commands) command.Execute(multiplier);

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