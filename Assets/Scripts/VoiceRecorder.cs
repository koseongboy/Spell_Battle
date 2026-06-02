using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class VoiceRecorder : MonoBehaviour
{
    // Node.js 메인 서버의 음성 업로드 API 주소 (3000번 포트)
    private string serverUrl = "http://localhost:3000/upload-voice"; 
    
    private AudioClip recordingClip;
    private string deviceName;
    private bool isRecording = false;

    void Start()
    {
        // 연결된 마이크 장치가 있는지 확인하고 첫 번째 장치를 선택
        if (Microphone.devices.Length > 0)
        {
            deviceName = Microphone.devices[0];
        }
        else
        {
            Debug.LogError("연결된 마이크 장치를 찾을 수 없습니다!");
        }
    }

    // UI 버튼 등에 연결할 함수: 녹음 시작/종료 토글
    public void ToggleRecording()
    {
        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopRecordingAndSend();
        }
    }

    private void StartRecording()
    {
        if (deviceName == null) return;

        isRecording = true;
        Debug.Log("🎙️ 녹음 시작...");
        // 최대 녹음 시간 10초, 샘플 레이트 16000Hz (Wav2Vec2 모델 최적화 사양)
        recordingClip = Microphone.Start(deviceName, false, 10, 16000);
    }

    private void StopRecordingAndSend()
    {
        if (!isRecording) return;

        isRecording = false;
        int lastTimeSample = Microphone.GetPosition(deviceName);
        Microphone.End(deviceName);
        Debug.Log("⏹️ 녹음 완료. 서버 전송 준비 중...");

        // 실제 녹음된 길이만큼 오디오 데이터를 자르기
        AudioClip trimmedClip = TrimAudioClip(recordingClip, lastTimeSample);

        if (trimmedClip != null)
        {
            // .wav 바이너리로 변환 후 코루틴으로 서버 전송
            byte[] wavData = ConvertToWav(trimmedClip);
            StartCoroutine(UploadVoiceCoroutine(wavData));
        }
    }

    // 유저가 10초를 채우지 않고 중간에 끊었을 때, 공백을 잘라내는 함수
    private AudioClip TrimAudioClip(AudioClip clip, int lastSample)
    {
        if (lastSample <= 0) return null;

        float[] samples = new float[lastSample * clip.channels];
        clip.GetData(samples, 0);

        AudioClip trimmed = AudioClip.Create(clip.name, lastSample, clip.channels, clip.frequency, false);
        trimmed.SetData(samples, 0);
        return trimmed;
    }

    // --- 2. AudioClip을 .wav 파일 포맷(바이너리)으로 변환하는 로직 ---
    private byte[] ConvertToWav(AudioClip clip)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                var samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                // WAV 헤더 작성
                writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + samples.Length * 2);
                writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((ushort)1); // PCM 변환
                writer.Write((ushort)clip.channels);
                writer.Write(clip.frequency);
                writer.Write(clip.frequency * clip.channels * 2);
                writer.Write((ushort)(clip.channels * 2));
                writer.Write((ushort)16); // 16비트
                writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                writer.Write(samples.Length * 2);

                // 오디오 데이터 작성 (Float -> Short 변환)
                foreach (var sample in samples)
                {
                    writer.Write((short)(sample * short.MaxValue));
                }
            }
            return stream.ToArray();
        }
    }

    // --- 3. Node.js 서버로 멀티파트 폼 전송 코루틴 ---
    private IEnumerator UploadVoiceCoroutine(byte[] wavBytes)
    {
        WWWForm form = new WWWForm();
        // Node.js의 multer가 인식할 필드명 'file', 파일명 'voice.wav'
        form.AddBinaryData("file", wavBytes, "voice.wav", "audio/wav");
        
        // 추가 데이터 전송이 필요하다면 여기에 덧붙임, 나중에 수정 (예: 방 ID, 유저 ID 등)
        form.AddField("userId", "Player_1");
        form.AddField("roomId", "Room_101");

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ 서버 전송 실패: {www.error}");
            }
            else
            {
                Debug.Log("✅ 음성 전송 완료!");
                Debug.Log($"서버 응답 결과: {www.downloadHandler.text}");
                // 여기서 서버가 보내준 LLM 채점 결과 JSON을 파싱해서 UI에
            }
        }
    }
}