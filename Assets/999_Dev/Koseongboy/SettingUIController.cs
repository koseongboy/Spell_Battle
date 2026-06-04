using UnityEngine;
using UnityEngine.UI;
using Managers.VoiceManagers;

public class SettingUIController : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    public Slider micVolSlider;        // 마이크 볼륨 조절 슬라이더
    public Image micGaugeBar;          // 음량 게이지 바 (Image 타입, Fill Method = Horizontal)
    public Button testButton;          // 테스트 시작/종료 버튼
    public Text testButtonText;        // 버튼 안의 텍스트 ("테스트" / "중지")

    private void Start()
    {
        // 1. 슬라이더를 초기 설정값으로 세팅
        micVolSlider.value = VoiceManager.Instance.micVolumeMultiplier;

        // 2. 슬라이더 값이 변할 때마다 VoiceManager에 쏴주기
        micVolSlider.onValueChanged.AddListener((val) => 
        {
            VoiceManager.Instance.UpdateSettings(VoiceManager.Instance.micDeviceIndex, val, VoiceManager.Instance.outputVolume);
        });

        // 3. 테스트 버튼 클릭 이벤트
        testButton.onClick.AddListener(OnTestButtonClicked);
    }

    private void OnTestButtonClicked()
    {
        if (VoiceManager.Instance.isTesting)
        {
            VoiceManager.Instance.StopMicTest();
            testButtonText.text = "마이크 테스트";
            micGaugeBar.fillAmount = 0f; // 게이지 초기화
        }
        else
        {
            VoiceManager.Instance.StartMicTest();
            testButtonText.text = "테스트 중지";
        }
    }

    private void Update()
    {
        // 🌟 마이크 테스트 중일 때 매 프레임마다 게이지 바의 길이를 갱신!
        if (VoiceManager.Instance.isTesting)
        {
            micGaugeBar.fillAmount = VoiceManager.Instance.GetMicVolumeGauge();
        }
    }

    // 설정 창이 꺼질 때 깜빡하고 테스트를 안 껐다면 안전하게 종료
    private void OnDisable()
    {
        if (VoiceManager.Instance != null && VoiceManager.Instance.isTesting)
        {
            VoiceManager.Instance.StopMicTest();
            testButtonText.text = "마이크 테스트";
        }
    }
}