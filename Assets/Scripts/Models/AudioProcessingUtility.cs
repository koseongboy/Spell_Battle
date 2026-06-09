using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class AudioProcessingUtility
{
    // 🌟 핵심: 유니티 메인 스레드 멈춤 방지를 위한 비동기 WAV 변환
    public static async Task<byte[]> ConvertAudioClipToWavAsync(AudioClip clip)
    {
        // 1. [메인 스레드] AudioClip의 데이터를 float 배열로 아주 빠르게 추출합니다.
        // (GetData는 유니티 API라 무조건 메인 스레드에서만 호출해야 함)
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        
        int channels = clip.channels;
        int frequency = clip.frequency;

        // 2. [백그라운드 스레드] 무거운 바이트 변환 연산을 메인 스레드에서 분리합니다.
        return await Task.Run(() =>
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // WAV 헤더 공간 확보 (44바이트)
                byte[] header = new byte[44];
                memoryStream.Write(header, 0, 44);

                // float 샘플(-1f ~ 1f)을 16bit PCM(short) 바이트로 변환
                // 연산량이 매우 많지만 백그라운드라 게임은 절대 멈추지 않음!
                byte[] sampleBytes = new byte[2];
                foreach (float sample in samples)
                {
                    short intSample = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767);
                    sampleBytes = BitConverter.GetBytes(intSample);
                    memoryStream.Write(sampleBytes, 0, 2);
                }

                // 완성된 파일 크기로 WAV 헤더 작성
                WriteWavHeader(memoryStream, channels, frequency);

                return memoryStream.ToArray();
            }
        });
    }

    // WAV 포맷의 필수 규격 헤더 작성 로직
    private static void WriteWavHeader(MemoryStream stream, int channels, int frequency)
    {
        stream.Seek(0, SeekOrigin.Begin);

        byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
        stream.Write(riff, 0, 4);

        byte[] chunkSize = BitConverter.GetBytes((int)stream.Length - 8);
        stream.Write(chunkSize, 0, 4);

        byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
        stream.Write(wave, 0, 4);

        byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
        stream.Write(fmt, 0, 4);

        byte[] subChunk1Size = BitConverter.GetBytes(16);
        stream.Write(subChunk1Size, 0, 4);

        ushort audioFormat = 1;
        stream.Write(BitConverter.GetBytes(audioFormat), 0, 2);
        stream.Write(BitConverter.GetBytes((ushort)channels), 0, 2);
        stream.Write(BitConverter.GetBytes(frequency), 0, 4);
        stream.Write(BitConverter.GetBytes(frequency * channels * 2), 0, 4);
        stream.Write(BitConverter.GetBytes((ushort)(channels * 2)), 0, 2);
        stream.Write(BitConverter.GetBytes((ushort)16), 0, 2);

        byte[] data = System.Text.Encoding.UTF8.GetBytes("data");
        stream.Write(data, 0, 4);

        byte[] subChunk2Size = BitConverter.GetBytes((int)stream.Length - 44);
        stream.Write(subChunk2Size, 0, 4);
    }

    // (선택 사항) 실제 녹음된 길이만큼만 클립을 잘라내는 함수
    public static AudioClip TrimAudioClip(AudioClip originalClip, int recordedSamples)
    {
        if (recordedSamples <= 0) return originalClip;

        float[] data = new float[recordedSamples * originalClip.channels];
        originalClip.GetData(data, 0);

        AudioClip trimmedClip = AudioClip.Create("TrimmedVoice", recordedSamples, originalClip.channels, originalClip.frequency, false);
        trimmedClip.SetData(data, 0);

        return trimmedClip;
    }
}