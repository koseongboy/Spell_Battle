using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Managers.LocalDataManagers;
using Managers.VoiceManagers;
using Models.Networks;
using UnityEngine;
using UnityEngine.Networking;

namespace DefaultNamespace
{
    public class PitchAnalyzer : MonoBehaviour
    {
        public static PitchAnalyzer Instance { get; private set; }
        
        [Header("UI")]
        [SerializeField] private PitchSetting_FullScreen ui;

        [Header("녹음 설정")]
        [SerializeField] private int sampleRate = 44100;
        [SerializeField] private int maxRecordTime = 5; // 최대 녹음 시간 (초)
        
        [Header("피치 분석 알고리즘 설정")]
        [SerializeField] private float volumeThreshold = 0.02f; // 무음 제외를 위한 최소 진폭(RMS)
        [SerializeField] private int windowSize = 2048;          // 분석 단위 샘플 수 (~46ms)
        [SerializeField] private int hopSize = 1024;             // 분석 간격 (오버랩)
        private AudioClip _recordingClip;
        private string _micDevice;
        private bool _isRecording = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // 사용 가능한 첫 번째 마이크 장치 선택
            if (Microphone.devices.Length > 0)
            {
                _micDevice = Microphone.devices[0];
            }
            else
            {
                Debug.LogError("[PitchAnalyzer] 연결된 마이크 장치를 찾을 수 없습니다.");
            }
        }

        public async void ToggleRecording() {
            if (!_isRecording) {
                Debug.Log("녹음 시작 진입");
                StartRecording();
            }
            else {
                Debug.Log("녹음 종료 진입");
                var resultPitch = StopRecordingAndAnalyze();
                if (Mathf.Approximately(resultPitch, -1f)) {
                    CommonUIController.Instance.ShowRedAlert("유효한 목소리 주파수를 감지하지 못했습니다.");
                }
                else {
                    bool isServerSuccess = await SendServerPitch(resultPitch);
                    if (isServerSuccess) {
                        ui.UpdateResultUI(resultPitch);
                    }
                    else {
                        CommonUIController.Instance.ShowRedAlert("서버 통신에 실패했습니다. 다시 진행해주세요.");
                    }
                }
            }
        }


        /// <summary>
        /// 🎙️ 예시 문장 읽기 녹음 시작
        /// </summary>
        private void StartRecording()
        {
            if (SoundManager.Instance == null)
            {
                Debug.LogError("[PitchAnalyzer] VoiceManager 인스턴스를 찾을 수 없습니다.");
                return;
            }

            if (_isRecording) return;

            _isRecording = true;
            SoundManager.Instance.StartRecording(true);
        }

        /// <summary>
        /// ⏹️ 녹음 중지 및 평균 피치 계산 실행
        /// </summary>
        /// <returns>측정된 평균 피치 Hz (실패 시 -1f)</returns>
        private float StopRecordingAndAnalyze()
        {
            if (!_isRecording || SoundManager.Instance == null) return -1f;

            _isRecording = false;
            
            // 1. VoiceManager에서 WAV 형태의 byte 배열 추출
            byte[] wavData = SoundManager.Instance.StopRecording();

            if (wavData == null || wavData.Length <= 44)
            {
                Debug.LogWarning("[PitchAnalyzer] 녹음된 데이터가 없거나 너무 짧습니다.");
                return -1f;
            }

            // 2. WAV 바이트 배열을 float 배열로 변환 (16-bit PCM 기준)
            float[] totalSamples = ConvertWavToFloats(wavData);

            // 3. 데이터를 윈도우 단위로 쪼개어 피치 측정
            List<float> validPitches = new List<float>();

            for (int start = 0; start + windowSize <= totalSamples.Length; start += hopSize)
            {
                float rms = CalculateRMS(totalSamples, start, windowSize);
                if (rms < volumeThreshold) continue; // 소리가 너무 작으면 무시

                float pitch = AnalyzeWindowPitch(totalSamples, start, windowSize);
                
                // 인간의 발성 범위(60Hz ~ 400Hz) 내부의 유효 데이터만 수집
                if (pitch >= 60f && pitch <= 400f)
                {
                    validPitches.Add(pitch);
                }
            }

            // 4. 평균 산출
            if (validPitches.Count == 0)
            {
                Debug.LogWarning("[PitchAnalyzer] 유효한 목소리 주파수를 감지하지 못했습니다.");
                return -1f;
            }

            float sum = 0f;
            for (int i = 0; i < validPitches.Count; i++) sum += validPitches[i];
            float averagePitch = sum / validPitches.Count;

            Debug.Log($"[PitchAnalyzer] 분석 완료! 유저 평균 피치: {averagePitch:F2} Hz");
            return averagePitch;
        }

        public async Task<bool> SendServerPitch(float pitch) {
            CommonUIController.Instance.ShowLoading();
            string myUserId = LocalDataManager.Instance.userId;

            if (string.IsNullOrEmpty(myUserId)) 
            {
                Debug.LogError("[SendServerPitch] 유저 ID가 비어있어 서버에 피치를 전송할 수 없습니다.");
                return false;
            }

            bool isSuccess = await WebServerModel.Instance.SetDefaultPitchAsync(myUserId, pitch);

            CommonUIController.Instance.DoneLoading();
            return isSuccess;
        }

        // 진폭 제곱평균제곱근(RMS) 계산 함수 (소리 크기 측정)
        private float CalculateRMS(float[] samples, int startIndex, int length)
        {
            float sum = 0f;
            for (int i = 0; i < length; i++)
            {
                sum += samples[startIndex + i] * samples[startIndex + i];
            }
            return Mathf.Sqrt(sum / length);
        }

        // 시간 도메인 자기상관함수 알고리즘 (Autocorrelation)
        private float AnalyzeWindowPitch(float[] samples, int startIndex, int length)
        {
            int minLag = Mathf.FloorToInt((float)sampleRate / 400f);
            int maxLag = Mathf.FloorToInt((float)sampleRate / 60f);

            float bestCorrelation = -1f;
            int bestLag = -1;

            for (int lag = minLag; lag <= maxLag; lag++)
            {
                float correlation = 0f;
                for (int i = 0; i < length - lag; i++)
                {
                    correlation += samples[startIndex + i] * samples[startIndex + i + lag];
                }

                if (correlation > bestCorrelation)
                {
                    bestCorrelation = correlation;
                    bestLag = lag;
                }
            }

            return bestLag > -1 ? (float)sampleRate / bestLag : -1f;
        }
        
        
        /// <summary>
        /// WAV 바이트 배열(16-bit PCM)을 float 배열(-1.0 ~ 1.0)로 디코딩합니다.
        /// </summary>
        private float[] ConvertWavToFloats(byte[] wavData)
        {
            // WAV 헤더는 통상 44바이트입니다.
            int headerSize = 44;
            // 2바이트(16비트)가 1개의 오디오 샘플을 구성합니다.
            int sampleCount = (wavData.Length - headerSize) / 2;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                int byteIndex = headerSize + (i * 2);
                // 2바이트를 읽어 16비트 정수(short)로 변환
                short sample16 = System.BitConverter.ToInt16(wavData, byteIndex);

                // float 범위로 정규화 (16비트 최대치 32768)
                samples[i] = sample16 / 32768f;
            }

            return samples;
        }
    }
    
    [System.Serializable]
    public class PitchRequestData
    {
        public string userId;
        public float defaultPitch;
    }

    [System.Serializable]
    public class PitchResponseData
    {
        public string message;
    }
}