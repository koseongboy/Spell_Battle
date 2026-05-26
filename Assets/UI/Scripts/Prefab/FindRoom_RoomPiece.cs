using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace DefaultNamespace
{
    public class FindRoom_RoomPiece : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txt_RoomName;
        [SerializeField] private TextMeshProUGUI txt_HostName;
        [SerializeField] private TextMeshProUGUI txt_HostLevel;
        [SerializeField] private TextMeshProUGUI txt_Capacity;
        
        // 매니저에서 이 프리팹을 생성한 직후 호출할 초기화 함수
        public void UpdateUI(Lobby lobby) {
            // 1. 방 이름 적용 (기본 프로퍼티)
            txt_RoomName.text = lobby.Name;

            // 2 & 3. 커스텀 데이터 (방장 이름, 방장 레벨) 가져오기
            // 방 생성 시 LobbyOptions.Data에 담아둔 값을 TryGetValue로 안전하게 추출합니다.
            if (lobby.Data != null) {
                // 방장 이름
                if (lobby.Data.TryGetValue("HostName", out DataObject hostNameObj)) {
                    txt_HostName.text = hostNameObj.Value;
                }
                else {
                    txt_HostName.text = "Unknown";
                }

                // 방장 레벨
                if (lobby.Data.TryGetValue("HostLevel", out DataObject hostLevelObj)) {
                    txt_HostLevel.text = $"Lv.{hostLevelObj.Value}";
                }
                else {
                    txt_HostLevel.text = "Lv.-";
                }
            }
            else {
                txt_HostName.text = "Unknown";
                txt_HostLevel.text = "Lv.-";
            }

            // 4. 인원수 적용 (기본 프로퍼티 활용)
            // 현재 인원 = 최대 인원(MaxPlayers) - 남은 자리(AvailableSlots)
            int currentPlayers = lobby.MaxPlayers - lobby.AvailableSlots;
            txt_Capacity.text = $"{currentPlayers} / {lobby.MaxPlayers}";

            // 꽉 찼을 경우 텍스트 색상을 붉은색으로 변경
            if (currentPlayers >= lobby.MaxPlayers) {
                txt_Capacity.color = Color.red;
            }
            else {
                txt_Capacity.color = Color.white;
            }
        }
    }
}
