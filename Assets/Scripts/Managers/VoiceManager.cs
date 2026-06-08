using UnityEngine;
using Managers.LocalDataManagers;
using System;

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
        public bool isRecording { get; private set; } = false;
        [Obsolete("이 변수는 isRecording으로 통합되었습니다. 앞으로는 isRecording을 사용해 주세요.")]
        public bool isTesting { get => isRecording; private set => isRecording = value; }
        private string testDeviceName;

        public Action<float> OnMicVolumeChanged;

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
        // 📊 매 프레임 UI로 게이지 값을 쏴주는 로직
        // ==========================================
        private void Update()
        {
            // 녹음 중이고, 누군가(UI) 이 방송을 구독하고 있다면 쏴줍니다.
            if (isRecording && OnMicVolumeChanged != null)
            {
                OnMicVolumeChanged.Invoke(GetMicVolumeGauge());
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
            isRecording = true;
            Debug.Log($"[VoiceManager] 마이크 테스트 시작: {testDeviceName}");
        }

        public void StopMicTest()
        {
            if (!isRecording) return;

            testAudioSource.Stop();
            Microphone.End(testDeviceName);
            isRecording = false;
            Debug.Log("[VoiceManager] 마이크 테스트 종료");
        }

        // ==========================================
        // 📊 UI 게이지 바를 위한 실시간 음량 계산
        // ==========================================
        public float GetMicVolumeGauge()
        {
            // 1차 방어: 녹음 중이 아니거나 클립이 없으면 0 반환
            if (!isRecording || recorder.recordingClip == null) return 0f;

            string deviceName = recorder.currentDeviceName;
            int micPosition = Microphone.GetPosition(deviceName);
            
            // 2차 방어: 마이크가 아직 켜지는 중이라 데이터가 없으면 0 반환
            if (micPosition <= 0) return 0f;

            int sampleCount = 256;
            float[] samples = new float[sampleCount];

            int startPosition = micPosition - sampleCount;
            
            // 3차 방어: 아직 256개의 샘플이 모이지 않았을 극초반 프레임 무시
            if (startPosition < 0) return 0f;

            // 🌟 4차 방어 (핵심 원인 해결): 읽으려는 범위가 클립의 총 길이를 벗어나면 무시
            if (startPosition + sampleCount > recorder.recordingClip.samples) return 0f;

            // 여기까지 통과했으면 100% 안전한 상태!
            try 
            {
                recorder.recordingClip.GetData(samples, startPosition);
            }
            catch (System.Exception)
            {
                // 혹시라도 알 수 없는 FMOD 내부 충돌이 발생하면 게임이 터지지 않게 무시
                return 0f; 
            }

            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            float rmsValue = Mathf.Sqrt(sum / samples.Length);

            // 게이지 민감도 (필요에 따라 30~100 사이로 조절)
            float sensitivity = 40f; 

            return Mathf.Clamp01(rmsValue * sensitivity * micVolumeMultiplier);
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
        public void StartRecording(bool needPlayBack = false)
        {
            recorder.StartRecord(micDeviceIndex);
            isRecording = true; // 플래그 On!
            if(needPlayBack)
            {
                testAudioSource.clip = recorder.recordingClip;
                testAudioSource.loop = false;
                testAudioSource.volume = micVolumeMultiplier; // 현재 볼륨 적용

                // 2. 딜레이와 지직거림을 막기 위해 마이크 버퍼가 조금 찰 때까지 대기
                while (!(Microphone.GetPosition(testDeviceName) > 0)) { }

                // 3. 내 스피커로 마이크 소리를 바로 송출!
                testAudioSource.Play();
            }
            Debug.Log("[VoiceManager] 녹음이 시작되어 게이지 바 연동이 활성화됩니다.");
        }
        public byte[] StopRecording()
        {
            if (testAudioSource != null && testAudioSource.isPlaying) testAudioSource.Stop();
            isRecording = false; // 플래그 Off!
            OnMicVolumeChanged?.Invoke(0f); 

            return recorder.StopAndGetWav();
        }
        
        public void PlayOpponentVoice(string url) => player.PlayFromUrl(url, outputVolume);
    }
}
