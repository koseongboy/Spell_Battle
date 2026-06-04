using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Models.TurnModel;
using Models.PlayerModels;

namespace DefaultNamespace {
    public class MatchManager : NetworkBehaviour {
        public static MatchManager Instance { get; private set; }

        [Header("Spawning")] [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform hostSpawnPoint;
        [SerializeField] private Transform guestSpawnPoint;

        [Header("상태 관리")] private HashSet<ulong> mulliganReadyPlayers = new HashSet<ulong>();
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

                SpawnPlayer(TurnModel.Instance.HostId.Value, hostSpawnPoint.position);
                SpawnPlayer(TurnModel.Instance.GuestId.Value, guestSpawnPoint.position);

                Debug.Log(
                    $"[Server] 방 세팅 완료. 호스트: {TurnModel.Instance.HostId.Value}, 게스트: {TurnModel.Instance.GuestId.Value}");

                StartCoroutine(WaitUntilDecksReadyAndStart());
            }
            else {
                Debug.LogWarning("[Server] 접속한 플레이어가 2명 미만입니다.");
            }
        }

        private void SpawnPlayer(ulong clientId, Vector3 position) {
            GameObject playerObj = Instantiate(playerPrefab, position, Quaternion.identity);
            NetworkObject networkObj = playerObj.GetComponent<NetworkObject>();
            networkObj.SpawnAsPlayerObject(clientId);

            // 스폰 직후 딕셔너리에 등록
            PlayerModel playerModel = playerObj.GetComponent<PlayerModel>();
            RegisterPlayer(clientId, playerModel);

            Debug.Log($"[Server] 플레이어 {clientId} 캐릭터 생성 완료");
        }

        // 플레이어 캐싱 등록/해제 관리
        public void RegisterPlayer(ulong clientId, PlayerModel playerModel) => activePlayers[clientId] = playerModel;
        public void UnregisterPlayer(ulong clientId) => activePlayers.Remove(clientId);

        public PlayerModel GetPlayerById(ulong clientId) {
            if (activePlayers.TryGetValue(clientId, out PlayerModel player)) return player;
            return null;
        }

        #endregion

        #region 2. 게임 준비 및 시작

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

            for (int i = 0; i < 4; i++) firstPlayer.Deck.DrawCard();
            for (int i = 0; i < 5; i++) secondPlayer.Deck.DrawCard();

            TurnModel.Instance.FirstPlayerId.Value = firstPlayerId;
            TurnModel.Instance.CurrentTurnPlayerId.Value = firstPlayerId;
            PhaseManager.Instance.OnGameSetupCompleted();
        }

        #endregion

        #region 3. 멀리건 시스템

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubmitMulliganServerRpc(int[] replaceCardIds, RpcParams rpcParams = default) {
            ulong clientId = rpcParams.Receive.SenderClientId;
            PlayerModel targetPlayer = GetPlayerById(clientId);

            List<int> tempPocket = new List<int>();

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
                // 직접 TurnModel을 건드리지 않고, PhaseManager에게 전환을 위임함!
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