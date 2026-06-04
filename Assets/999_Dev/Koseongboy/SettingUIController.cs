using UnityEngine;
using UnityEngine.UI;
using Managers.VoiceManagers;
using TMPro;

public class SettingUIController : MonoBehaviour
{
    [Header("UI 계층 구조 연결")]
    public GameObject settingUIPanel;       // 전체 설정 패널
    public Slider outputVolumeSlider;       // 출력 볼륨 조절 슬라이더
    public Slider micVolumeSlider;          // 마이크 볼륨 조절 슬라이더
    public Image gaugeBar;                  // 마이크 음량 게이지 바
    public Button btn_test;                 // 테스트 시작/중지 버튼
    public TextMeshProUGUI btn_test_text;              // 테스트 버튼 내부 텍스트
    public Button btn_save;                 // 저장 버튼
    public Button btn_cancel;               // 취소 버튼

    // 취소 버튼을 눌렀을 때 되돌릴 원본 데이터를 저장할 변수
    private float originalMicVol;
    private float originalOutputVol;

    private void Awake()
    {
        // 1. 버튼 및 슬라이더 이벤트 리스너 등록
        btn_test.onClick.AddListener(OnTestButtonClicked);
        btn_save.onClick.AddListener(OnSaveButtonClicked);
        btn_cancel.onClick.AddListener(OnCancelButtonClicked);
        
        micVolumeSlider.onValueChanged.AddListener(OnMicVolumeChanged);
    }

    // 🌟 패널이 활성화(켜질 때)마다 실행되는 함수
    private void OnEnable()
    {
        if (VoiceManager.Instance == null) return;

        // 1. 패널을 열 때, 현재 VoiceManager에 저장된 진짜 세팅값을 가져옵니다.
        originalMicVol = VoiceManager.Instance.micVolumeMultiplier;
        originalOutputVol = VoiceManager.Instance.outputVolume;

        // 2. 슬라이더 위치를 현재 설정값에 맞게 동기화합니다.
        micVolumeSlider.value = originalMicVol;
        outputVolumeSlider.value = originalOutputVol;

        // 3. UI 초기화
        gaugeBar.fillAmount = 0f;
        btn_test_text.text = "마이크 테스트";
    }

    // ==========================================
    // 🎚️ 실시간 슬라이더 반응 로직
    // ==========================================
    private void OnMicVolumeChanged(float val)
    {
        // 마이크 테스트 중에 슬라이더를 움직이면, 
        // 저장하지 않고도 내 스피커에 들리는 목소리 크기가 바로바로 바뀌도록 적용!
        if (VoiceManager.Instance != null && VoiceManager.Instance.isTesting)
        {
            if (VoiceManager.Instance.testAudioSource != null)
            {
                VoiceManager.Instance.testAudioSource.volume = val;
            }
        }
    }

    // ==========================================
    // 🎙️ 마이크 테스트 버튼 로직
    // ==========================================
    private void OnTestButtonClicked()
    {
        if (VoiceManager.Instance.isTesting)
        {
            VoiceManager.Instance.StopMicTest();
            btn_test_text.text = "마이크 테스트";
            gaugeBar.fillAmount = 0f;
        }
        else
        {
            // 테스트를 켤 때, 현재 슬라이더의 값을 임시로 적용해서 테스트를 엽니다.
            VoiceManager.Instance.micVolumeMultiplier = micVolumeSlider.value;
            VoiceManager.Instance.StartMicTest();
            btn_test_text.text = "테스트 중지";
        }
    }

    private void Update()
    {
        // 🌟 마이크 테스트 중일 때만 매 프레임 게이지 바 갱신!
        if (VoiceManager.Instance != null && VoiceManager.Instance.isTesting)
        {
            gaugeBar.fillAmount = VoiceManager.Instance.GetMicVolumeGauge();
        }
    }

    // ==========================================
    // 💾 저장 및 취소 버튼 로직
    // ==========================================
    private void OnSaveButtonClicked()
    {
        // 1. 혹시 테스트 중이었다면 끕니다.
        if (VoiceManager.Instance.isTesting) OnTestButtonClicked();

        // 2. 🌟 여기서 최종적으로 매니저들에게 영구 저장을 때립니다!
        VoiceManager.Instance.UpdateSettings(
            VoiceManager.Instance.micDeviceIndex, // 마이크 기기는 기존 것 유지 (추가 기획 시 드롭다운 연결)
            micVolumeSlider.value, 
            outputVolumeSlider.value
        );

        Debug.Log("[SettingUI] 설정 저장 완료 및 창 닫기");
        settingUIPanel.SetActive(false); // 패널 닫기
    }

    private void OnCancelButtonClicked()
    {
        // 1. 테스트 중이었다면 끕니다.
        if (VoiceManager.Instance.isTesting) OnTestButtonClicked();

        // 2. VoiceManager 내부의 임시 값을 원래 값(열 때 저장했던 값)으로 원상 복구합니다.
        VoiceManager.Instance.micVolumeMultiplier = originalMicVol;

        Debug.Log("[SettingUI] 설정 변경 취소 및 창 닫기");
        settingUIPanel.SetActive(false); // 패널 닫기
    }

    // 혹시 창이 비정상적으로 꺼졌을 때를 대비한 안전 장치
    private void OnDisable()
    {
        if (VoiceManager.Instance != null && VoiceManager.Instance.isTesting)
        {
            VoiceManager.Instance.StopMicTest();
            btn_test_text.text = "마이크 테스트";
        }
    }
}