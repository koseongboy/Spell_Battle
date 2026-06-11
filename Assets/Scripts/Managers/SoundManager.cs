using UnityEngine;
using Managers.LocalDataManagers;
using System;
using System.Collections;
using Models.PlayerModels;
using Cards.EffectInfos; // 코루틴을 위해 추가

namespace Managers.VoiceManagers
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("하위 모듈 (Voice)")]
        public MicRecorder recorder;
        public VoicePlayer player;

        [Header("하위 모듈 (BGM)")]
        public AudioSource bgmSource;

        [Header("하위 모듈 (SFX)")]
        public AudioSource sfxSource;
        public AudioClip defaultButtonSFX; 

        [Header("음성 및 사운드 설정값")]
        public int micDeviceIndex = 0;
        public float micVolumeMultiplier = 1.0f;
        public float outputVolume = 1.0f;

        [Header("마이크 테스트 (Loopback)")]
        public AudioSource testAudioSource;
        public bool isRecording { get; private set; } = false;

        [Header("Combat Actions (Damage & Heal)")]
        [Tooltip("기본 데미지 (DamageCommand)")]
        [SerializeField] private AudioClip defaultDamageSFX;
        
        [Tooltip("체력 회복 (HealCommand)")]
        [SerializeField] private AudioClip healSFX;
        
        [Tooltip("보호막 생성 (ShieldCommand)")]
        [SerializeField] private AudioClip shieldSFX;
        [System.Serializable]
        public struct StatusSFXMapping {
            public StatusType statusType;
            public AudioClip sfxClip;
        }
         [Tooltip("상태이상 부여")]
        [SerializeField] private StatusSFXMapping[] specificStatusSFXs;

        [Header("Mana & System")]
        [Tooltip("마나 회복 (ManaCommand - 양수)")]
        [SerializeField] private AudioClip manaGainSFX;
        
        [Tooltip("마나 감소/소모 (ManaCommand - 음수)")]
        [SerializeField] private AudioClip manaLossSFX;



        
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
        public void SetBgmAudioClip(AudioClip source)
        {
            bgmSource.Pause();
            bgmSource.clip = null;
            bgmSource.clip = source;
            bgmSource.Play();
        }
        public void ToggleBGM()
        {
            // 오디오 소스가 없거나 할당된 음악이 없다면 무시합니다.
            if (bgmSource == null || bgmSource.clip == null) return;

            if (bgmSource.isPlaying)
            {
                // 재생 중이면 일시정지합니다.
                bgmSource.Pause();
                Debug.Log("[VoiceManager] BGM 일시정지됨");
            }
            else
            {
                // 멈춰있으면 멈춘 구간부터 다시 재생합니다.
                bgmSource.UnPause();
                Debug.Log("[VoiceManager] BGM 재생 재개됨");
            }
        }

        // ==========================================
        // 🎵 SFX 통합 제어 모듈
        // ==========================================

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;
            
            // PlayOnShot은 기존에 나고 있던 효과음을 끊지 않고 겹쳐서 예쁘게 재생해줍니다.
            sfxSource.PlayOneShot(clip, outputVolume); 
        }

        public void PlayDefaultButtonSFX()
        {
            PlaySFX(defaultButtonSFX);
        }
        public void PlaySkillSFX(Models.EffectCommands.VFXType vfxType, StatusType statusType, EffectType cardMovement = EffectType.None)
        {
            // 명찰이 None이거나 타겟이 없으면 그냥 넘어갑니다.
            if (vfxType == Models.EffectCommands.VFXType.None) return;

            AudioClip sfx = null;

            // 🌟 넘어온 명찰(enum)에 맞는 프리팹을 인스펙터 필드에서 꺼냅니다.
            switch (vfxType)
            {
                case Models.EffectCommands.VFXType.Damage: sfx = defaultDamageSFX; break;
                case Models.EffectCommands.VFXType.Heal: sfx = healSFX; break;
                case Models.EffectCommands.VFXType.Shield: sfx = shieldSFX; break;
                case Models.EffectCommands.VFXType.AddStatus: 
                    sfx = GetSpecificStatusSFX(statusType);
                    break;
                case Models.EffectCommands.VFXType.ManaGain: sfx = manaGainSFX; break;
                case Models.EffectCommands.VFXType.ManaLoss: sfx = manaLossSFX; break;
                default: sfx = defaultDamageSFX; break;
            }
            if(sfx != null) PlaySFX(sfx);
        }
        private AudioClip GetSpecificStatusSFX(StatusType targetStatus)
        {
            if (specificStatusSFXs == null) return defaultDamageSFX; // 방어 코드

            foreach (var mapping in specificStatusSFXs)
            {
                if (mapping.statusType == targetStatus && mapping.sfxClip != null)
                {
                    return mapping.sfxClip; // 매핑된 전용 이펙트 반환!
                }
            }
            // 매핑을 못 찾았다면(아직 프리팹 안 넣은 경우 등) 기본 상태이상 이펙트를 띄웁니다.
            return defaultDamageSFX; 
        }

        // ==========================================
        // ⚙️ 설정 업데이트 동기화 (볼륨 실시간 적용)
        // ==========================================
        public void UpdateSettings()
        {

            if (LocalDataManager.Instance != null)
            {
                LocalDataManager.Instance.UpdateMicSetting(micDeviceIndex, micVolumeMultiplier, outputVolume);
                Debug.Log($"[VoiceManager] 마이크/사운드 설정 저장!)");
            }
        }

        // ==========================================
        // 🎚️ UI 슬라이더 실시간 연동용 함수
        // ==========================================
        public void SetOutputVolume(float volume)
        {
            // 1. 매니저의 기준 볼륨 값을 업데이트
            outputVolume = volume;

            // 2. 현재 재생 중인 브금 오디오 소스에 즉시 적용
            if (bgmSource != null)
            {
                bgmSource.volume = outputVolume;
            }
        }


        // ==========================================
        // 🎙️ 마이크 녹음 & 재생 모듈 (기존 동일)
        // ==========================================
        public void StartMicTest()
        {
            ToggleBGM();
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
            ToggleBGM();
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
            ToggleBGM();
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
            ToggleBGM();
            if (testAudioSource != null && testAudioSource.isPlaying) testAudioSource.Stop();
            isRecording = false; 
            OnMicVolumeChanged?.Invoke(0f); 
            return recorder.StopAndGetWav();
        }
        
    }
}