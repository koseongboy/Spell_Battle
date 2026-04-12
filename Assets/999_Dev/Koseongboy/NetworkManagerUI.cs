using Unity.Netcode;
using UnityEngine;

namespace Koseongboy.NetworkManagerUI
{
    public class NetworkManagerUI : MonoBehaviour
    {
        private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 30, 1500, 4500));
        
        // 버튼을 눌렀을 때 호스트(서버+플레이어)로 시작
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("방 만들기 (Host)"))
                NetworkManager.Singleton.StartHost();

            if (GUILayout.Button("참가하기 (Client)"))
                NetworkManager.Singleton.StartClient();
        }
        else
        {
            GUILayout.Label("상태: " + (NetworkManager.Singleton.IsHost ? "호스트" : "클라이언트"));
        }

        GUILayout.EndArea();
    }
    }
}
