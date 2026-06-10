using UnityEngine;
using Managers.LocalDataManagers;
using System;
using System.Collections; // 코루틴을 위해 추가

namespace Managers.VoiceManagers
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("하위 모듈 (Voice)")]
        public MicRecorder recorder;
        public VoicePlayer player;

        [Header("하위 모듈 (BGM)")]
        public AudioSource bgmSource; // 🌟 BGM을 담당할 오디오 소스 추가
        private Coroutine bgmFadeCoroutine;

        [Header("음성 및 사운드 설정값")]
        public int micDeviceIndex = 0;
        public float micVolumeMultiplier = 1.0f;
        public float outputVolume = 1.0f; // 🌟 이제 상대방 음성 + BGM 마스터 볼륨으로 쓰입니다.

        [Header("마이크 테스트 (Loopback)")]
        public AudioSource testAudioSource; 
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

                // 깜빡하고 BGM용 AudioSource를 안 붙였을 경우 자동 생성
                if (bgmSource == null)
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                }
            }
            else
            {
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

                // 시작할 때 BGM 볼륨도 초기 세팅값으로 맞춰줍니다.
                if (bgmSource != null) bgmSource.volume = outputVolume; 
            }
        }

        private void Update()
        {
            if (isRecording && OnMicVolumeChanged != null)
            {
                OnMicVolumeChanged.Invoke(GetMicVolumeGauge());
            }
        }

        // ==========================================
        // 🎵 BGM 통합 제어 모듈
        // ==========================================
        public void PlayBGM(AudioClip clip, float fadeDuration = 1.0f)
        {
            if (clip == null || bgmSource.clip == clip) return;

            if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = StartCoroutine(FadeToNextBGMRoutine(clip, fadeDuration));
        }

        public void StopBGM(float fadeDuration = 1.0f)
        {
            if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = StartCoroutine(FadeOutBGMRoutine(fadeDuration));
        }

        private IEnumerator FadeToNextBGMRoutine(AudioClip nextClip, float duration)
        {
            // 1. 기존 음악 페이드 아웃
            if (bgmSource.isPlaying)
            {
                while (bgmSource.volume > 0)
                {
                    bgmSource.volume -= Time.deltaTime / duration;
                    yield return null;
                }
            }

            // 2. 새 음악 교체 및 재생
            bgmSource.clip = nextClip;
            bgmSource.loop = true;
            bgmSource.Play();

            // 3. 새 음악 페이드 인 (🌟 목표 볼륨은 사용자가 설정한 outputVolume!)
            float targetVolume = outputVolume; 
            while (bgmSource.volume < targetVolume)
            {
                bgmSource.volume += (Time.deltaTime / duration) * targetVolume;
                yield return null;
            }
            bgmSource.volume = targetVolume;
        }

        private IEnumerator FadeOutBGMRoutine(float duration)
        {
            while (bgmSource.volume > 0)
            {
                bgmSource.volume -= Time.deltaTime / duration;
                yield return null;
            }
            bgmSource.Stop();
        }


        // ==========================================
        // ⚙️ 설정 업데이트 동기화 (볼륨 실시간 적용)
        // ==========================================
        public void UpdateSettings(int deviceIndex, float micVol, float outVol)
        {
            this.micDeviceIndex = deviceIndex;
            this.micVolumeMultiplier = micVol;
            this.outputVolume = outVol;

            // 🌟 설정창에서 볼륨 슬라이더를 움직이면, 재생 중인 브금 볼륨도 즉시 바뀝니다!
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.volume = outVol;
            }

            if (LocalDataManager.Instance != null)
            {
                LocalDataManager.Instance.UpdateMicSetting(deviceIndex, micVol, outVol);
                Debug.Log($"[VoiceManager] 마이크/사운드 설정 저장! (기기: {deviceIndex}, 마이크: {micVol}, 마스터 볼륨: {outVol})");
            }
        }

        // ==========================================
        // 🎙️ 마이크 녹음 & 재생 모듈 (기존 동일)
        // ==========================================
        public void StartMicTest()
        {
            if (Microphone.devices.Length == 0) return;
            testDeviceName = Microphone.devices[micDeviceIndex];
            testAudioSource.clip = Microphone.Start(testDeviceName, true, 1, 44100);
            testAudioSource.loop = true;
            testAudioSource.volume = micVolumeMultiplier; 
            while (!(Microphone.GetPosition(testDeviceName) > 0)) { }
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
        }

        public float GetMicVolumeGauge()
        {
            if (!isRecording || recorder.recordingClip == null) return 0f;
            string deviceName = recorder.currentDeviceName;
            int micPosition = Microphone.GetPosition(deviceName);
            if (micPosition <= 0) return 0f;

            int sampleCount = 256;
            float[] samples = new float[sampleCount];
            int startPosition = micPosition - sampleCount;
            
            if (startPosition < 0 || startPosition + sampleCount > recorder.recordingClip.samples) return 0f;

            try { recorder.recordingClip.GetData(samples, startPosition); }
            catch (System.Exception) { return 0f; }

            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            float rmsValue = Mathf.Sqrt(sum / samples.Length);
            float sensitivity = 40f; 

            return Mathf.Clamp01(rmsValue * sensitivity * micVolumeMultiplier);
        }

        public void StartRecording(bool needPlayBack = false)
        {
            recorder.StartRecord(micDeviceIndex);
            isRecording = true; 
            if(needPlayBack)
            {
                testAudioSource.clip = recorder.recordingClip;
                testAudioSource.loop = false;
                testAudioSource.volume = micVolumeMultiplier; 
                while (!(Microphone.GetPosition(testDeviceName) > 0)) { }
                testAudioSource.Play();
            }
        }

        public byte[] StopRecording()
        {
            if (testAudioSource != null && testAudioSource.isPlaying) testAudioSource.Stop();
            isRecording = false; 
            OnMicVolumeChanged?.Invoke(0f); 
            return recorder.StopAndGetWav();
        }
        
    }
}