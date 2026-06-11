    using System;
    using System.IO;
    using UnityEngine;


namespace Managers.VoiceManagers
{

    public class MicRecorder : MonoBehaviour
    {
        public AudioClip recordingClip;
        public string currentDeviceName { get; private set; }

        // 🎙️ 1. 녹음 시작
        public void StartRecord(int deviceIndex)
        {
            if (Microphone.devices.Length == 0) return;

            // 🌟 현재 기기 이름을 변수에 저장해 둡니다.
            currentDeviceName = Microphone.devices[deviceIndex];
            
            recordingClip = Microphone.Start(currentDeviceName, false, 10, 44100);
            Debug.Log($"[MicRecorder] 녹음 시작: {currentDeviceName}");
        }
        // 🛑 2. 녹음 종료 및 WAV 바이트 배열 반환
        public byte[] StopAndGetWav()
        {

            // 1. 녹음 종료
            Microphone.End(null);
            Debug.Log("[MicRecorder] 녹음 종료. WAV 변환을 시작합니다.");
            CleanAudioData(recordingClip);

            // 2. AudioClip을 WAV 바이트 배열로 변환해서 반환
            return ConvertClipToWav(recordingClip);
        }

        // ==========================================
        // ⚙️ AudioClip -> WAV (byte[]) 변환 핵심 로직
        // ==========================================
        private byte[] ConvertClipToWav(AudioClip clip)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // 1. 빈 헤더 공간 확보 (44바이트)
                writer.Write(new byte[44]);

                // 2. 오디오 데이터(PCM) 추출 및 작성
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                Int16[] intData = new Int16[samples.Length];
                // float(-1.0f ~ 1.0f) 데이터를 16bit 정수로 변환
                for (int i = 0; i < samples.Length; i++)
                {
                    intData[i] = (short)(samples[i] * 32767f);
                    writer.Write(intData[i]); // 데이터 기록
                }

                // 3. 다시 맨 앞으로 돌아가서 WAV 헤더 작성
                writer.Seek(0, SeekOrigin.Begin);

                uint sampleRate = (uint)clip.frequency;
                ushort channels = (ushort)clip.channels;
                ushort bitsPerSample = 16;
                uint byteRate = (uint)(sampleRate * channels * (bitsPerSample / 8));
                ushort blockAlign = (ushort)(channels * (bitsPerSample / 8));
                uint subChunk2Size = (uint)(samples.Length * (bitsPerSample / 8));
                uint chunkSize = 36 + subChunk2Size;

                writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(chunkSize);
                writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16u); // Subchunk1Size (16 for PCM)
                writer.Write((ushort)1); // AudioFormat (1 for PCM)
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);
                writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
                writer.Write(subChunk2Size);

                // 4. 최종 완성된 메모리 스트림을 byte 배열로 반환
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 오디오 클립의 원본 데이터에서 백그라운드 잡음을 제거(무음 처리)합니다.
        /// </summary>
        /// <param name="recordedClip">마이크로 녹음된 원본 클립</param>
        /// <param name="noiseThreshold">이 수치보다 작은 소리는 잡음으로 간주 (0.01f ~ 0.05f 추천)</param>
        public void CleanAudioData(AudioClip recordedClip, float noiseThreshold = 0.02f)
        {
            if (recordedClip == null) return;

            // 1. 오디오 클립에서 전체 파형 데이터를 뽑아옵니다.
            float[] samples = new float[recordedClip.samples * recordedClip.channels];
            recordedClip.GetData(samples, 0);

            // 2. 파형을 쭉 검사하면서 잡음(threshold 미만)을 완전히 0(무음)으로 밀어버립니다.
            for (int i = 0; i < samples.Length; i++)
            {
                if (Mathf.Abs(samples[i]) < noiseThreshold)
                {
                    samples[i] = 0f;
                }
            }

            // 3. 깨끗해진 데이터를 다시 오디오 클립에 덮어씌웁니다.
            recordedClip.SetData(samples, 0);
            
            Debug.Log("[Sound] 원본 녹음 데이터의 백그라운드 잡음 제거가 완료되었습니다.");
        }

    }
}
