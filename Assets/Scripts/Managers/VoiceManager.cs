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

        [Header("마이크 테스트 (Loopback)")]
        public AudioSource testAudioSource; // 🌟 내 목소리를 들려줄 테스트용 스피커
        public bool isTesting { get; private set; } = false;
        private string testDeviceName;

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

        // ==========================================
        // 🎙️ 마이크 테스트 로직 (Loopback)
        // ==========================================
        public void StartMicTest()
        {
            if (Microphone.devices.Length == 0) return;

            testDeviceName = Microphone.devices[micDeviceIndex];

            // 1. 1초짜리 루프(Loop) 클립을 만들어 마이크 입력을 담습니다.
            testAudioSource.clip = Microphone.Start(testDeviceName, true, 1, 44100);
            testAudioSource.loop = true;
            testAudioSource.volume = micVolumeMultiplier; // 현재 볼륨 적용

            // 2. 딜레이와 지직거림을 막기 위해 마이크 버퍼가 조금 찰 때까지 대기
            while (!(Microphone.GetPosition(testDeviceName) > 0)) { }

            // 3. 내 스피커로 마이크 소리를 바로 송출!
            testAudioSource.Play();
            isTesting = true;
            Debug.Log($"[VoiceManager] 마이크 테스트 시작: {testDeviceName}");
        }

        public void StopMicTest()
        {
            if (!isTesting) return;

            testAudioSource.Stop();
            Microphone.End(testDeviceName);
            isTesting = false;
            Debug.Log("[VoiceManager] 마이크 테스트 종료");
        }

        // ==========================================
        // 📊 UI 게이지 바를 위한 실시간 음량 계산
        // ==========================================
        public float GetMicVolumeGauge()
        {
            if (!isTesting) return 0f;

            // 현재 스피커에서 나고 있는 소리의 파형 샘플 256개를 추출합니다.
            float[] samples = new float[256];
            testAudioSource.GetOutputData(samples, 0);

            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                // 파형의 절대값을 모두 더합니다.
                sum += Mathf.Abs(samples[i]);
            }

            // 평균을 구합니다. (순수 평균값은 수치가 너무 작습니다)
            float averageVolume = sum / samples.Length;

            // 🌟 UI 게이지(0.0 ~ 1.0)에 맞게 시각적 보정치(예: 50f)를 곱해줍니다.
            // 테스트해보시고 게이지가 너무 안 차면 50f를 100f 등으로 늘리시면 됩니다.
            return Mathf.Clamp01(averageVolume * micVolumeMultiplier * 50f);
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
