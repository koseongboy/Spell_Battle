using Unity.Netcode;
using UnityEngine;
using System;

namespace Models.RelayMatchmakingService
{
    public class ReadyStateModel : NetworkBehaviour
    {
        public NetworkVariable<bool> isGuestReady = new NetworkVariable<bool>(false);

        public Action<bool> OnGuestReadyChanged;  //상태 동기화를 위해 액션 구독할 것

        public override void OnNetworkSpawn()
        {
            isGuestReady.OnValueChanged += (oldValue, newValue) => OnGuestReadyChanged?.Invoke(newValue);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ToggleReadyServerRpc()
        {
            isGuestReady.Value = !isGuestReady.Value; // true면 false로, false면 true로 뒤집기
        }

        public void ResetReadyState()
        {
            if(IsServer) isGuestReady.Value = false;
        }
    }
}
