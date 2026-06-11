using UnityEngine;

public class LogManager : MonoBehaviour
{
    void Awake()
    {
        // #if !UNITY_EDITOR
        // Debug.unityLogger.logEnabled = false;
        // #endif
    }
}