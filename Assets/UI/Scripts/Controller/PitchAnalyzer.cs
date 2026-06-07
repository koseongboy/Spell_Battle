using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Managers.LocalDataManagers;
using Managers.VoiceManagers;
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

        public void ToggleRecording() {
            if (!_isRecording) {
                Debug.Log("녹음 시작 진입");
                StartRecording();
                ui.StartGaugeCoroutine();
            }
            else {
                Debug.Log("녹음 종료 진입");

                ui.StopGaugeCoroutine();
                var resultPitch = StopRecordingAndAnalyze();
                if (Mathf.Approximately(resultPitch, -1f)) {
                    CommonUIController.Instance.ShowRedAlert("유효한 목소리 주파수를 감지하지 못했습니다.");
                }
                else {
                    ui.UpdateResultUI(resultPitch);
                }
            }
        }


        /// <summary>
        /// 🎙️ 예시 문장 읽기 녹음 시작
        /// </summary>
        private void StartRecording()
        {
            if (VoiceManager.Instance == null)
            {
                Debug.LogError("[PitchAnalyzer] VoiceManager 인스턴스를 찾을 수 없습니다.");
                return;
            }

            if (_isRecording) return;

            _isRecording = true;
            VoiceManager.Instance.StartRecording();
        }

        /// <summary>
        /// ⏹️ 녹음 중지 및 평균 피치 계산 실행
        /// </summary>
        /// <returns>측정된 평균 피치 Hz (실패 시 -1f)</returns>
        private float StopRecordingAndAnalyze()
        {
            if (!_isRecording || VoiceManager.Instance == null) return -1f;

            _isRecording = false;
            
            // 1. VoiceManager에서 WAV 형태의 byte 배열 추출
            byte[] wavData = VoiceManager.Instance.StopRecording();

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
            // TODO : 이거 서버랑 통신하는 거 어디 한데 모으는게 좋지 않으려나?
            string url = "http://3.107.201.71:3000/set-default-pitch";

            // 2. 전송할 JSON 데이터 조립
            string myUserId = LocalDataManager.Instance.userId;
    
            PitchRequestData requestData = new PitchRequestData 
            {
                userId = myUserId,
                defaultPitch = pitch
            };
    
            // C# 객체를 JSON 문자열로 변환
            string jsonData = JsonUtility.ToJson(requestData);

            // 3. UnityWebRequest 객체를 POST 모드로 수동 세팅 (매우 중요)
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                // JSON 문자열을 UTF-8 바이트 배열로 인코딩하여 Body에 탑재
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
        
                // 백엔드 서버가 이 데이터가 JSON임을 알 수 있도록 명시
                request.SetRequestHeader("Content-Type", "application/json");

                // 4. 요청 보내기 및 응답 대기 (메인 스레드 멈춤 방지)
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                // 5. 서버 응답 결과 판별
                if (request.result == UnityWebRequest.Result.Success)
                {
                    // 성공: 서버가 보내준 JSON 응답 파싱
                    PitchResponseData response = JsonUtility.FromJson<PitchResponseData>(request.downloadHandler.text);
                    Debug.Log($"[서버 응답] {response.message}");

                    return true;
                }
                else
                {
                    // 실패: 네트워크 단절 또는 400/500 에러 발생
                    Debug.LogError($"[통신 실패] 코드: {request.responseCode}, 메시지: {request.error}");
                    Debug.LogError($"[상세 로그] {request.downloadHandler.text}");
                    
                    return false;
                }
            }
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