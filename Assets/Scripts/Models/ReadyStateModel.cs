using Unity.Netcode;
using UnityEngine;
using System;
using DefaultNamespace;

namespace Models.RelayMatchmakingService {
    public class ReadyStateModel : NetworkBehaviour {
        public NetworkVariable<bool> isGuestReady = new NetworkVariable<bool>(false);

        public Action<bool> OnGuestReadyChanged; //상태 동기화를 위해 액션 구독할 것

        public override void OnNetworkSpawn() {
            isGuestReady.OnValueChanged += (oldValue, newValue) => OnGuestReadyChanged?.Invoke(newValue);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ToggleReadyServerRpc() {
            isGuestReady.Value = !isGuestReady.Value; // true면 false로, false면 true로 뒤집기
        }

        public void ResetReadyState() {
            if (IsServer) isGuestReady.Value = false;
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void NotifyRoomClosedRpc() {
            if (RoomUIController.Instance != null) {
                RoomUIController.Instance.HandleHostClosedRoom();
            }
            else {
                Debug.LogError("[Guest Error] RoomUIController.Instance가 null입니다! (UI 이벤트를 넘겨줄 수 없음)");
            }
        }
        
        [Rpc(SendTo.Everyone)]
        public void ShowLoadingScreenRpc(bool isBGMOff = false) {
            if(isBGMOff) Managers.VoiceManagers.SoundManager.Instance.ToggleBGM();
            Debug.Log("[Client] 글로벌 로딩 화면 출력 명령 수신!");
            CommonUIController.Instance.ShowLoading();
        }
    }
}