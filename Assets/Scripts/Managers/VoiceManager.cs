using UnityEngine;
using Managers.LocalDataManagers;

namespace Managers.VoiceManagers
{
    public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }

    [Header("하위 모듈")]
    public MicRecorder recorder;
    public VoicePlayer player;

    [Header("음성 설정값")]
    public int micDeviceIndex = 0;
    public float micVolumeMultiplier = 1.0f;
    public float outputVolume = 1.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);

        }
        else
        {
            // 이미 매니저가 존재하는데 또 씬이 로드되면서 생성되려고 하면 파괴
            Destroy(this.gameObject); 
        }
    }

        private void Start()
        {
            if (LocalDataManager.Instance != null)
            {
                var settings = LocalDataManager.Instance.GetMicSettings();
                micDeviceIndex = settings.deviceIdx;
                micVolumeMultiplier = settings.micV;
                outputVolume = settings.outV;
            }
        }



        // 설정이 변경되었을 때 LocalDataManager로 쏴주는 로직
        public void UpdateSettings(int deviceIndex, float micVol, float outVol)
    {
        // 1. VoiceManager 자신의 현재 상태 업데이트
        this.micDeviceIndex = deviceIndex;
        this.micVolumeMultiplier = micVol;
        this.outputVolume = outVol;

        // 2. 🌟 LocalDataManager에 구현해둔 함수를 호출하여 전역 데이터 동기화!
        if (LocalDataManager.Instance != null)
        {
            LocalDataManager.Instance.UpdateMicSetting(deviceIndex, micVol, outVol);
            Debug.Log($"[VoiceManager] 마이크 설정 저장 완료! (기기: {deviceIndex}, 마이크 볼륨: {micVol}, 출력 볼륨: {outVol})");
        }
        else
        {
            Debug.LogWarning("[VoiceManager] LocalDataManager 인스턴스를 찾을 수 없어 설정을 저장하지 못했습니다.");
        }
    }

    // TurnController 등에서 호출할 인터페이스
    public void StartRecording() => recorder.StartRecord(micDeviceIndex);
    public byte[] StopRecording() => recorder.StopAndGetWav();
    
    public void PlayOpponentVoice(string url) => player.PlayFromUrl(url, outputVolume);
}
}
