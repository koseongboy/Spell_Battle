using System;
using Unity.Netcode;
using UnityEngine;

namespace Models.TurnModel
{
    public enum GamePhase
    {
        Wait, Draw, Select, Incantation, Battle, End
    }
    public class TurnModel : NetworkBehaviour
    {
        public static TurnModel Instance { get; private set; }
        public NetworkVariable<ulong> CurrentTurnPlayerId = new NetworkVariable<ulong>(0);
        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Wait);

        public event Action<GamePhase, bool> OnPhaseChangedEvent;

        private void Awake()
        {
            if (Instance == null) 
            {
                Instance = this;
            }
            else 
            {
                Destroy(gameObject); // 혹시 두 개가 생기면 하나를 파괴 (안전장치)
            }
        }

        public override void OnNetworkSpawn()
        {
            CurrentPhase.OnValueChanged += (oldPhase, newPhase) =>
            {
              bool isMyTurn = NetworkManager.Singleton.LocalClientId == CurrentTurnPlayerId.Value;
              OnPhaseChangedEvent?.Invoke(newPhase, isMyTurn);
            };
        }
    }
}
