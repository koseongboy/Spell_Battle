using Unity.Netcode;
using UnityEngine;
using Models.TurnModel;
using Views.TurnView;
using Models.PlayerModel;
using Cards.PlayableCards;
using System.Collections.Generic;
using Models.SpellPayloads;
using Newtonsoft.Json;


namespace Controllers.TurnController
{
    public class TurnController : NetworkBehaviour
    {
        public static TurnController Instance { get; private set; }

        [Header("연결된 플레이어 모델")]
        public PlayerModel MyPlayer;      // 클라이언트 본인
        public PlayerModel EnemyPlayer;   // 상대방

        [Header("MVP References")]
        [SerializeField] private TurnModel model;
        [SerializeField] private TurnView view;

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
            // 🌟 1. 총 마나 코스트 계산
            int totalCost = 0;
            foreach (var card in selectedCards)
            {
                totalCost += card.uiData.cost; // uiData 구조체에 있던 코스트 값
            }

            // 🌟 2. 마나 검증 (내 지갑 확인)
            if (MyPlayer.CurrentMana.Value < totalCost)
            {
                Debug.LogWarning("마나가 부족하여 영창할 수 없습니다!");
                // TODO: 화면에 "마나 부족!" UI 띄우기
                return; // 여기서 컷! 견적서도 안 만듦.
            }
            SpellPayload payload = new SpellPayload();
            payload.SetPrefix("칠흑의 심연에서 눈뜬 자여"); //todo
            payload.SetConcept("건방지게");

            foreach (var card in selectedCards)
            {
                card.AddToPayload(payload, MyPlayer); 
            }

            // 1. 견적서를 JSON 문자열로 변환!
            string jsonPayload = payload.ToJson();
            
            // 2. 🌟 서버에게 JSON을 넘기며 결재(집행)를 요청!
            SubmitSpellServerRpc(jsonPayload, totalCost); 
        }

        // ==========================================
        // ☁️ [네트워크 영역] 클라이언트 -> 서버 전송
        // RequireOwnership = false로 두어야 턴 컨트롤러 주인이 아니어도 손님이 호출 가능
        // ==========================================
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSpellServerRpc(string jsonPayload, int declaredCost, RpcParams rpcParams = default)
        {
            // 이 함수 안쪽은 오직 서버(방장) 컴퓨터에서만 실행됩니다!
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[서버] 클라이언트 {senderClientId}로부터 주문 JSON을 받았습니다!");

            // 이 주문을 요청한 플레이어 모델 찾기
            PlayerModel casterModel = (senderClientId == NetworkManager.Singleton.LocalClientId) ? MyPlayer : EnemyPlayer;

            // 🌟 1. 서버 측 최종 마나 검증 및 차감 (PlayerModel에 만들어둔 TryUseMana 활용!)
            if (!casterModel.TryUseMana(declaredCost))
            {
                // 핵이거나, 동기화 지연으로 마나가 안 맞음 -> 강제 취소!
                Debug.LogError($"[서버] 클라이언트 {senderClientId}의 마나가 부족하거나 위조되었습니다. 주문을 취소합니다.");
                return; 
            }

            // 1. JSON을 다시 SpellPayload 객체로 복원(역직렬화)
            SpellPayload payload = JsonConvert.DeserializeObject<SpellPayload>(jsonPayload);

            // 2. TODO: 여기서 웹 서버(LLM)에 JSON을 쏘고 배율을 받아오는 비동기 통신을 진행합니다.
            float serverMultiplier = 1.0f; // 임시 통과 배율

            // 3. 서버의 권한으로 드디어 실제 모델에 적용!
            ApplyPayloadToModels(payload, serverMultiplier, senderClientId);
        }

        // ==========================================
        // 🛡️ [서버 영역] 실제 집행 (오직 서버만 실행 가능)
        // ==========================================
        private void ApplyPayloadToModels(SpellPayload payload, float multiplier, ulong casterId)
        {
            if (!IsServer) return; // 철통 보안

            // 🌟 주의: 멀티플레이 환경에서는 누가 Caster고 Target인지
            // 클라이언트 ID(casterId)를 기준으로 정확히 판별해야 합니다!
            PlayerModel casterModel = (casterId == NetworkManager.Singleton.LocalClientId) ? MyPlayer : EnemyPlayer;
            PlayerModel targetModel = (casterId == NetworkManager.Singleton.LocalClientId) ? EnemyPlayer : MyPlayer;

            // 1. 적(Target)에게 효과 적용
            if (payload.TargetPayload.TotalDamage > 0)
            {
                int finalDamage = Mathf.RoundToInt(payload.TargetPayload.TotalDamage * multiplier);
                targetModel.TakeDamage(finalDamage);
            }
            if (payload.TargetPayload.TotalHeal > 0) targetModel.Heal(payload.TargetPayload.TotalHeal);
            if (payload.TargetPayload.TotalShield > 0) targetModel.AddShield(payload.TargetPayload.TotalShield);

            foreach (var status in payload.TargetPayload.StatusEffectsToApply)
            {
                targetModel.AddStatus(status.Type, status.Stacks, status.Duration);
            }

            // 2. 나(Caster)에게 효과 적용
            if (payload.CasterPayload.TotalDamage > 0) casterModel.TakeDamage(payload.CasterPayload.TotalDamage);
            if (payload.CasterPayload.TotalHeal > 0) casterModel.Heal(payload.CasterPayload.TotalHeal);
            if (payload.CasterPayload.TotalShield > 0) targetModel.AddShield(payload.CasterPayload.TotalShield);
            
            foreach (var status in payload.CasterPayload.StatusEffectsToApply)
            {
                casterModel.AddStatus(status.Type, status.Stacks, status.Duration);
            }
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
            switch (model.CurrentPhase.Value)
            {
                case GamePhase.Wait: model.CurrentPhase.Value = GamePhase.Draw; break;
                case GamePhase.Draw: model.CurrentPhase.Value = GamePhase.Select; break;
                case GamePhase.Select: model.CurrentPhase.Value = GamePhase.Incantation; break;
                case GamePhase.Incantation: model.CurrentPhase.Value = GamePhase.Battle; break;
                case GamePhase.Battle: model.CurrentPhase.Value = GamePhase.End; break;
                case GamePhase.End:
                    // 턴 교대 후 드로우 페이즈로
                    model.CurrentTurnPlayerId.Value = model.CurrentTurnPlayerId.Value == 0 ? 1ul : 0ul;
                    model.CurrentPhase.Value = GamePhase.Draw;
                    break;
            }
        }

        // 배틀 페이즈 등 서버가 강제로 페이즈를 넘겨야 할 때 사용
        private void ForceAdvancePhaseForBattle() { AdvancePhaseServerRpc(new RpcParams { Receive = new RpcReceiveParams { SenderClientId = model.CurrentTurnPlayerId.Value }}); }

        // (방장 전용) 게임 시작 함수
        public void StartGame()
        {
            if (IsServer) model.CurrentPhase.Value = GamePhase.Draw;
        }
    }
}
