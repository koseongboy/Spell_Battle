using DG.Tweening;
using Managers.VoiceManagers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace {
    public class Option_Popup : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<bool> {
        public EUILayer TargetLayer => EUILayer.Popup;

        public GameObject lobbyUI;
        public GameObject ingameUI;

        public Slider outputVolumeSlider; // 출력 볼륨 조절 슬라이더
        public Slider micVolumeSlider; // 마이크 볼륨 조절 슬라이더
        public Image gaugeBar; // 마이크 음량 게이지 바
        public TextMeshProUGUI txt_test; // 테스트 버튼 내부 텍스트

        private float originalMicVol;
        private float originalOutputVol;

        private CanvasGroup canvasGroup;
        private RectTransform popupRect;

        [Header("Animation Settings")] [SerializeField]
        private float animDuration = 0.25f; // 옵션창은 보통 더 빠르고 경쾌하게 띄움

        [SerializeField] private Vector3 startScale = new Vector3(0.8f, 0.8f, 0.8f);

        private bool isLobby = true;


        private void Awake() {
            canvasGroup = GetComponent<CanvasGroup>();
            popupRect = GetComponent<RectTransform>();
        }


        public void CloseUI() {
            CloseAction();
        }

        public void SurrenderPressed() {
            Debug.Log("[Option_Lobby] Surrender Pressed");
        }

        public void VoiceSettingPressed() {
            CommonUIController.Instance.ChangeFullScreen("VoiceSetting_FullScreen");
            UILoader.Instance.HideUI("Option_Lobby_Popup");
        }

        public void TutorialPressed() {
            Debug.Log("[Option_Lobby] Tutorial Pressed");
        }

        public void LogoutPressed() {
            ConfirmPopupData data = new ConfirmPopupData {
                message = "덱을 삭제하시겠습니까?",
                onConfirm = AuthManager.Instance.Logout,
                onCancel = () => { }
            };

            UILoader.Instance.ShowUI<ConfirmPopupData>("Confirm_Popup", data);
        }

        public void ExitGamePressed() {
            Debug.Log("[Option_Lobby] Exit Game Pressed");
        }


        private void OnEnable() {
            lobbyUI.SetActive(isLobby);
            ingameUI.SetActive(!isLobby);

            originalMicVol = VoiceManager.Instance.micVolumeMultiplier;
            originalOutputVol = VoiceManager.Instance.outputVolume;
            micVolumeSlider.value = originalMicVol;
            outputVolumeSlider.value = originalOutputVol;

            gaugeBar.fillAmount = 0f;
            txt_test.text = "마이크 테스트";
            if (VoiceManager.Instance != null)
            {
                VoiceManager.Instance.OnMicVolumeChanged += UpdateGauge;
            }

            OpenAction();
        }

        public void ReceiveData(bool isLobby) {
            this.isLobby = isLobby;

            if (isLobby) {
                lobbyUI.gameObject.SetActive(true);
                ingameUI.gameObject.SetActive(false);
            }
            else {
                lobbyUI.gameObject.SetActive(false);
                ingameUI.gameObject.SetActive(true);
            }
        }


        private void OpenAction() {
            popupRect.DOKill();
            canvasGroup.DOKill();

            // 1. 초기 상태 세팅 (이동 없음, 작아진 크기, 투명함)
            popupRect.localScale = startScale;
            canvasGroup.alpha = 0f;

            // 2. 목표 상태로 애니메이션 (원래 크기로, 불투명하게)
            popupRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuint);
            canvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutQuint);
        }

        private void CloseAction() {
            popupRect.DOKill();
            canvasGroup.DOKill();

            if (VoiceManager.Instance.isRecording) OnTestButtonClicked();
            SaveVolumeSetting();

            // 목표 상태로 애니메이션 (다시 작아지게, 투명하게)
            popupRect.DOScale(startScale, animDuration).SetEase(Ease.InQuint);

            canvasGroup.DOFade(0f, animDuration).SetEase(Ease.InQuint).OnComplete(() => {
                UILoader.Instance.HideUI("Option_Lobby_Popup");
            });
        }


        // ==========================================
        // 🎚️ 실시간 슬라이더 반응 로직
        // ==========================================
        public void OnMicVolumeChanged(float val) {
            // 마이크 테스트 중에 슬라이더를 움직이면, 
            // 저장하지 않고도 내 스피커에 들리는 목소리 크기가 바로바로 바뀌도록 적용!
            if (VoiceManager.Instance != null && VoiceManager.Instance.isRecording) {
                if (VoiceManager.Instance.testAudioSource != null) {
                    VoiceManager.Instance.testAudioSource.volume = val;
                }
            }
        }

        // ==========================================
        // 🎙️ 마이크 테스트 버튼 로직
        // ==========================================
        public void OnTestButtonClicked() {
            if (VoiceManager.Instance.isRecording) {
                VoiceManager.Instance.StopMicTest();
                txt_test.text = "마이크 테스트";
                gaugeBar.fillAmount = 0f;

            }
            else {
                VoiceManager.Instance.micVolumeMultiplier = micVolumeSlider.value;
                VoiceManager.Instance.StartMicTest();
                txt_test.text = "테스트 중지";
            }
        }

        private void UpdateGauge(float volumeValue)
        {
            gaugeBar.fillAmount = volumeValue;
        }

        // ==========================================
        // 💾 저장 및 취소 버튼 로직
        // ==========================================
        public void SaveVolumeSetting() {
            VoiceManager.Instance.UpdateSettings(
                VoiceManager.Instance.micDeviceIndex, // 마이크 기기는 기존 것 유지 (추가 기획 시 드롭다운 연결)
                micVolumeSlider.value,
                outputVolumeSlider.value
            );
        }


        // 혹시 창이 비정상적으로 꺼졌을 때를 대비한 안전 장치
        public void OnDisable() {
            if (VoiceManager.Instance != null)
            {
                VoiceManager.Instance.OnMicVolumeChanged -= UpdateGauge;
                if(VoiceManager.Instance.isRecording)
                {
                    VoiceManager.Instance.StopMicTest();
                    txt_test.text = "마이크 테스트";
                }
            }
        }
    }
}