using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Models.TurnModel;
using Models.PlayerModels;

namespace DefaultNamespace {
    /// <summary>
    /// [서버] 대전 준비 단계를 담당한다.
    /// 플레이어 스폰 → 양측 덱 제출 대기 → 선후공 결정 및 초기 드로우 → 멀리건 처리 후 PhaseManager에 위임.
    /// </summary>
    public class MatchManager : NetworkBehaviour {
        public void testGame()
        {
            InitializeRoomAndSpawnPlayers();
        }
        public static MatchManager Instance { get; private set; }

        [Header("Spawning")] [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform hostSpawnPoint;
        [SerializeField] private Transform guestSpawnPoint;

        [Header("상태 관리")] private HashSet<ulong> mulliganReadyPlayers = new HashSet<ulong>();
        // ClientId → PlayerModel 캐시. 씬 탐색 없이 O(1)로 조회
        private Dictionary<ulong, PlayerModel> activePlayers = new Dictionary<ulong, PlayerModel>();

        private void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn() {
            if (IsServer) {
                InitializeRoomAndSpawnPlayers();
            }
        }

        #region 1. 방 초기화 및 스폰

        private void InitializeRoomAndSpawnPlayers() {
            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;

            if (connectedClients.Count >= 2) {
                TurnModel.Instance.HostId.Value = connectedClients[0];
                TurnModel.Instance.GuestId.Value = connectedClients[1];

                SpawnPlayer(TurnModel.Instance.HostId.Value, hostSpawnPoint.position, hostSpawnPoint.rotation);
                SpawnPlayer(TurnModel.Instance.GuestId.Value, guestSpawnPoint.position, guestSpawnPoint.rotation);


                Debug.Log(
                    $"[Server] 방 세팅 완료. 호스트: {TurnModel.Instance.HostId.Value}, 게스트: {TurnModel.Instance.GuestId.Value}");

                StartCoroutine(WaitUntilDecksReadyAndStart());
            }
            else {
                Debug.LogWarning("[Server] 접속한 플레이어가 2명 미만입니다.");
            }
        }

        private void SpawnPlayer(ulong clientId, Vector3 position, Quaternion rotation) {
            GameObject playerObj = Instantiate(playerPrefab, position, rotation);
            NetworkObject networkObj = playerObj.GetComponent<NetworkObject>();
            networkObj.SpawnAsPlayerObject(clientId);

            PlayerModel playerModel = playerObj.GetComponent<PlayerModel>();
            RegisterPlayer(clientId, playerModel);

            Debug.Log($"[Server] 플레이어 {clientId} 캐릭터 생성 완료");
        }

        public void RegisterPlayer(ulong clientId, PlayerModel playerModel) => activePlayers[clientId] = playerModel;
        public void UnregisterPlayer(ulong clientId) => activePlayers.Remove(clientId);

        public PlayerModel GetPlayerById(ulong clientId) {
            if (activePlayers.TryGetValue(clientId, out PlayerModel player)) return player;
            return null;
        }

        #endregion

        #region 2. 게임 준비 및 시작

        // 각 클라이언트의 덱 제출(SubmitDeckServerRpc)이 끝날 때까지 대기
        private IEnumerator WaitUntilDecksReadyAndStart() {
            Debug.Log("[Server] 플레이어들의 덱 세팅을 기다립니다...");

            while (true) {
                PlayerModel host = GetPlayerById(TurnModel.Instance.HostId.Value);
                PlayerModel guest = GetPlayerById(TurnModel.Instance.GuestId.Value);

                if (host != null && guest != null &&
                    host.Deck.IsDeckReady.Value && guest.Deck.IsDeckReady.Value) {
                    break;
                }

                yield return null;
            }

            StartGame();
        }

        private void StartGame() {
            if (!IsServer) return;

            Debug.Log("[Server] 모두 준비 완료! 선후공 토스 및 초기 드로우를 시작합니다.");

            bool isHostFirst = Random.value > 0.5f;
            ulong firstPlayerId = isHostFirst ? TurnModel.Instance.HostId.Value : TurnModel.Instance.GuestId.Value;
            ulong secondPlayerId = isHostFirst ? TurnModel.Instance.GuestId.Value : TurnModel.Instance.HostId.Value;

            PlayerModel firstPlayer = GetPlayerById(firstPlayerId);
            PlayerModel secondPlayer = GetPlayerById(secondPlayerId);

            // 선공 4장, 후공 5장 (후공 보정)
            for (int i = 0; i < 4; i++) firstPlayer.Deck.DrawCard();
            for (int i = 0; i < 5; i++) secondPlayer.Deck.DrawCard();

            TurnModel.Instance.FirstPlayerId.Value = firstPlayerId;
            TurnModel.Instance.CurrentTurnPlayerId.Value = firstPlayerId;
            PhaseManager.Instance.OnGameSetupCompleted();
        }

        #endregion

        #region 3. 멀리건 시스템

        /// <summary>
        /// [클라이언트 → 서버] 멀리건 제출.
        /// 교체 카드를 손패에서 빼 둔 채로 같은 수만큼 새로 뽑고, 그 뒤 덱에 되돌려 섞는다.
        /// (먼저 되돌리면 방금 버린 카드를 다시 뽑을 수 있기 때문)
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubmitMulliganServerRpc(int[] replaceCardIds, RpcParams rpcParams = default) {
            ulong clientId = rpcParams.Receive.SenderClientId;
            PlayerModel targetPlayer = GetPlayerById(clientId);

            List<int> tempPocket = new List<int>();

            // 서버 손패에 실제로 있는 카드만 교체 대상으로 인정
            foreach (int id in replaceCardIds) {
                if (targetPlayer.Hand.RemoveCardFromServerHand(id)) tempPocket.Add(id);
            }

            for (int i = 0; i < tempPocket.Count; i++) targetPlayer.Deck.DrawCard();
            foreach (int id in tempPocket) targetPlayer.Deck.InsertCard(id, shuffleAfter: false);

            targetPlayer.Deck.Shuffle();

            Debug.Log($"[Server] 플레이어 {clientId}의 멀리건 완료. (교체된 카드 수: {tempPocket.Count})");
            ReportMulliganReady(clientId);
        }

        private void ReportMulliganReady(ulong clientId) {
            if (!IsServer) return;

            mulliganReadyPlayers.Add(clientId);

            if (mulliganReadyPlayers.Count == 2)
            {
                // 페이즈 전환은 PhaseManager로 일원화
                PhaseManager.Instance.OnMulliganCompleted(TurnModel.Instance.FirstPlayerId.Value);
            }
        }

        #endregion


        #region DEV

        [ContextMenu("전투 강제 시작")]
        public void ManualStartBattleTest() 
        {
            if (IsServer) 
            {
                Debug.Log("🛠️ 수동으로 전투를 초기화합니다!");
                InitializeRoomAndSpawnPlayers();
            }
            else 
            {
                Debug.LogWarning("방장(Host) 에디터에서만 실행할 수 있습니다!");
            }
        }
        
        #endregion
    }
}