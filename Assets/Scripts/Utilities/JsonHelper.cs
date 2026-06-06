using UnityEngine;

namespace DefaultNamespace
{
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            // 서버 응답 배열을 Unity JsonUtility가 읽을 수 있도록 임시 객체로 감쌉니다.
            string wrapper = "{ \"Items\": " + json + "}";
            Wrapper<T> wrapperObj = JsonUtility.FromJson<Wrapper<T>>(wrapper);
            return wrapperObj.Items;
        }

        [System.Serializable]
        private class Wrapper<T>
        {
            public T[] Items;
        }
    }
}
