using Unity.Netcode;
using UnityEngine;
using Models.TurnModel;
using Views.TurnView;


namespace Controllers.TurnController
{
    public class TurnController : NetworkBehaviour
    {
        [Header("MVP References")]
        [SerializeField] private TurnModel model;
        [SerializeField] private TurnView view;

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
