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

namespace Controllers.TurnControllers 
{
    public class TurnController : NetworkBehaviour 
    {
        public static TurnController Instance { get; private set; }
        
        [Header("배틀 씬 진짜 메인 카메라")]
        public Camera BattleMainCamera;
        
        [Header("연결된 플레이어 모델")]
        public PlayerModel MyPlayer;
        public PlayerModel EnemyPlayer;

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
            StartCoroutine(IncantationRoutine(selectedCards, selectedCardIds, totalCost));
        }

        private IEnumerator IncantationRoutine(List<PlayableCard> selectedCards, List<int> selectedCardIds, int totalCost)
        {
            SpellPayload payload = new SpellPayload();
            var randomIncantation = Managers.IncantationManager.Instance.GetRandomIncantation();
            
            payload.EvalData.Concept = randomIncantation.concept;
            payload.EvalData.RequiredPrefix = randomIncantation.prefix;

            foreach (var card in selectedCards) card.AddToPayload(payload, MyPlayer, EnemyPlayer);
            
            UILoader.Instance.ShowUI("SpellWordPiece", payload);

            // 마이크 녹음 대기 (임시)
            yield return new WaitForSeconds(1f); 
            
            string audioDownloadUrl = "http://mywebserver.com/audio/test1234.wav";
            string evaluationTaskId = "TASK_987654321";
            
            SubmitSpellServerRpc(selectedCardIds.ToArray(), payload.EvalData.ToJson(), totalCost, audioDownloadUrl, evaluationTaskId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSpellServerRpc(int[] cardIds, string evalJson, int declaredCost, string audioUrl, string taskId, RpcParams rpcParams = default)
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