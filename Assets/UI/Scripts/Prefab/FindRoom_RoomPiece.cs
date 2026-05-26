using System;
using Controllers.LobbyController;
using DG.Tweening;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class FindRoom_RoomPiece : MonoBehaviour
    {
        [Header("Element")]
        [SerializeField] private TextMeshProUGUI txt_RoomName;
        [SerializeField] private TextMeshProUGUI txt_HostName;
        [SerializeField] private TextMeshProUGUI txt_HostLevel;
        [SerializeField] private TextMeshProUGUI txt_Capacity;
        [SerializeField] private Button btn_roomPiece;

        [Header("Visual Feedback")]
        [SerializeField] private Image bgImage; // 색상을 변경할 배경 이미지 컴포넌트
        [SerializeField] private Color normalColor = Color.white; // 기본 상태 색상 (밝음)
        [SerializeField] private Color selectedColor = new Color(0.6f, 0.6f, 0.6f, 1f); // 선택 상태 색상 (어두움)
        [SerializeField] private float tweenDuration = 0.15f; // 색상이 변하는 시간
        
        private string currentLobbyId;
        private Action<string> pressedCallback;
        
        private bool isSelected = false;

        private void Start() {
            btn_roomPiece.onClick.AddListener(OnJoinClicked);
        }
        
        // 매니저에서 이 프리팹을 생성한 직후 호출할 초기화 함수
        public void SetUp(Lobby lobby, Action<string> action) {
            isSelected = false;
            bgImage.DOKill();
            bgImage.color = normalColor;
            
            currentLobbyId = lobby.Id;
            pressedCallback = action;
            
            UpdateUI(lobby);
        }

        private void UpdateUI(Lobby lobby) {
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
        
        private void OnJoinClicked()
        {
            // 연타 시 이전 트윈 연출이 꼬이는 현상을 방지하기 위해 기존 트윈 중지
            bgImage.DOKill();

            if (!isSelected) {
                isSelected = true;
            
                // 부드럽게 어두운 색상으로 변경
                bgImage.DOColor(selectedColor, tweenDuration);
            
                // 저장해둔 방 ID 전달
                pressedCallback.Invoke(currentLobbyId);
            }
            else {
                // 선택 취소
                isSelected = false;
            
                // 부드럽게 원래 밝은 색상으로 복구
                bgImage.DOColor(normalColor, tweenDuration);
            
                pressedCallback.Invoke(string.Empty);
            }
        }
    }
}
