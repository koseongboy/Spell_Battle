using DG.Tweening;
using Managers.LocalDataManagers;
using Managers.VoiceManagers;
using Models.RelayMatchmakingService;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            SoundManager.Instance.UpdateSettings();
            CloseAction();
        }

        public void SurrenderPressed() {
            ConfirmPopupData data = new ConfirmPopupData
            {
                message = "항복하고 게임에서 나가시겠습니까?",
                onConfirm = async () => {
                    if (RelayMatchmakingService.Instance != null) {
                        await RelayMatchmakingService.Instance.LeaveLobbyAsync();
                    }

                    // 2. Netcode(NGO) 안전 종료
                    if (NetworkManager.Singleton != null) {

                        NetworkManager.Singleton.Shutdown();

                        await System.Threading.Tasks.Task.Delay(100);
                        Destroy(NetworkManager.Singleton.gameObject);
                    }
                    
                    if (SoundManager.Instance != null) {
                        SoundManager.Instance.ToggleBGM();
                        if(SoundManager.Instance.isRecording) SoundManager.Instance.StopRecording();
                    }

                    SceneManager.LoadScene("01_Lobby_crocobob", LoadSceneMode.Single);
                },
                onCancel = () => { }
            };
            
            UILoader.Instance.ShowUI("Confirm_Popup", data);
        }

        public void VoiceSettingPressed() {
            CommonUIController.Instance.ChangeFullScreen("VoiceSetting_FullScreen");
            UILoader.Instance.HideUI("Option_Lobby_Popup");
        }

        public void TutorialPressed() {
            CommonUIController.Instance.ShowBlackAlert("미구현입니다. 첨부된 문서를 확인해주세요.");
        }

        public void LogoutPressed() {
            ConfirmPopupData data = new ConfirmPopupData {
                message = "로그아웃하시겠습니까?",
                onConfirm = AuthManager.Instance.Logout,
                onCancel = () => { }
            };

            UILoader.Instance.ShowUI("Confirm_Popup", data);
        }

        public void ExitGamePressed() {
            ConfirmPopupData data = new ConfirmPopupData
            {
                message = "게임을 종료하시겠습니까?",
                onConfirm = Application.Quit,
                onCancel = () => { }
            };

            UILoader.Instance.ShowUI("Confirm_Popup", data);
        }


        private void OnEnable() {
            lobbyUI.SetActive(isLobby);
            ingameUI.SetActive(!isLobby);

            originalMicVol = SoundManager.Instance.micVolumeMultiplier;
            originalOutputVol = SoundManager.Instance.outputVolume;
            micVolumeSlider.value = originalMicVol;
            outputVolumeSlider.value = originalOutputVol;

            gaugeBar.fillAmount = 0f;
            txt_test.text = "마이크 테스트";
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.OnMicVolumeChanged += UpdateGauge;
                outputVolumeSlider.onValueChanged.AddListener(OnVolumeSliderMoved);
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

            if (SoundManager.Instance.isRecording) OnTestButtonClicked();

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
            if (SoundManager.Instance != null && SoundManager.Instance.isRecording) {
                if (SoundManager.Instance.testAudioSource != null) {
                    SoundManager.Instance.testAudioSource.volume = val;
                }
            }
        }
        private void OnVolumeSliderMoved(float newValue)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetOutputVolume(newValue);
            }
        }

        // ==========================================
        // 🎙️ 마이크 테스트 버튼 로직
        // ==========================================
        public void OnTestButtonClicked() {
            if (SoundManager.Instance.isRecording) {
                SoundManager.Instance.StopMicTest();
                txt_test.text = "마이크 테스트";
                gaugeBar.fillAmount = 0f;

            }
            else {
                SoundManager.Instance.micVolumeMultiplier = micVolumeSlider.value;
                SoundManager.Instance.StartMicTest();
                txt_test.text = "테스트 중지";
            }
        }

        private void UpdateGauge(float volumeValue)
        {
            gaugeBar.fillAmount = volumeValue;
        }



        // 혹시 창이 비정상적으로 꺼졌을 때를 대비한 안전 장치
        public void OnDisable() {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.OnMicVolumeChanged -= UpdateGauge;
                if(SoundManager.Instance.isRecording)
                {
                    SoundManager.Instance.StopMicTest();
                    txt_test.text = "마이크 테스트";
                }
            }
        }
    }
}