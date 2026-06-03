using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

// 서버가 보내주는 JSON과 일치하는 구조체 정의
[System.Serializable]
public class SpellResultData
{
    public string userId;
    public int score;
    public string feedback;
    public string audioUrl;
}

public class RoomAudioManager : MonoBehaviour
{
    public AudioSource remoteAudioSource; // 오디오를 재생할 컴포넌트

    // Socket.io 이벤트를 받았을 때 호출할 함수 (소켓 라이브러리 연동부에서 실행)
    public void OnReceiveSpellResult(string jsonFromServer)
    {
        SpellResultData data = JsonUtility.FromJson<SpellResultData>(jsonFromServer);
        
        Debug.Log($"유저 {data.userId}의 영창 점수: {data.score}점 / 피드백: {data.feedback}");
        
        // 화면 UI에 점수와 피드백을 뿌리는 코드 추가 가능 (Text 컴포넌트 연동)

        // 상대방 및 나의 녹음본 다운로드 후 실시간 재생 시작
        StartCoroutine(DownloadAndPlayAudio(data.audioUrl));
    }

    IEnumerator DownloadAndPlayAudio(string url)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                remoteAudioSource.clip = clip;
                remoteAudioSource.Play(); // 나와 상대방 화면에서 동시에 재생됨!
            }
            else
            {
                Debug.LogError("오디오 다운로드 실패: " + www.error);
            }
        }
    }
}