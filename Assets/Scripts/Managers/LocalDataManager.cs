using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Managers.LocalDataManagers
{
    public class LocalDataManager : MonoBehaviour
    {
        public static LocalDataManager Instance;
        public static List<int> MyCustomDeck = new List<int>();
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
            
            
        }
    }
}
