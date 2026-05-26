using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// UILoader로부터 타입 안정성을 보장받으며 데이터를 전달받기 위한 공통 인터페이스입니다.
    /// 다중 데이터의 경우 ValueTuple 형태(예: IUIDataReceiver<(StructA, ClassB)>)로 구현이 가능합니다.
    /// </summary>
    public interface UI_IDataReceiver<in T>
    {
        void ReceiveData(T data);
    }
}
