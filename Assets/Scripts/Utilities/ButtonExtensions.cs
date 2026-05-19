using System;
using System.Threading.Tasks;
using UnityEngine.UI;

public static class ButtonExtensions
{
    // 유니티 기본 Button 기능에 '비동기 리스너'를 강제로 추가해 주는 마법의 코드입니다.
    public static void AddAsyncListener(this Button button, Func<Task> action)
    {
        button.onClick.AddListener(async () =>
        {
            // 1. 비동기 작업이 시작되면 즉시 버튼을 비활성화하여 중복 클릭(따닥!) 방지
            button.interactable = false;

            try
            {
                // 2. 전달받은 비동기 함수가 끝날 때까지 완벽하게 기다림!
                await action();
            }
            finally
            {
                // 3. 작업이 무사히 끝나든, 에러가 나든 버튼을 다시 활성화
                button.interactable = true;
            }
        });
    }
}