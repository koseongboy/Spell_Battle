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
        public NetworkVariable<ulong> CurrentTurnPlayerId = new NetworkVariable<ulong>(0);
        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Wait);

        public event Action<GamePhase, bool> OnPhaseChangedEvent;

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
