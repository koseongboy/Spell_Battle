using System;
using UnityEngine;

namespace DefaultNamespace
{
    public enum EUILayer
    {
        FullScreen,
        Popup,
        Top // 필요하다면 더 확장 가능
    }
    
    public interface UI_ILayerInfo
    {
        EUILayer TargetLayer { get; }
    }

    public interface UI_Popup {
        public void OpenAction();
        public void CloseAction( Action onAnimationComplete );
    }
}
