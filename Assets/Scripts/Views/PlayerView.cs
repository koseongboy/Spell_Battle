using UnityEngine;
using TMPro;
using Unity.Netcode;
using Models.PlayerModel;

namespace Views.PlayerView
{
    public abstract class PlayerView : MonoBehaviour
    {
        public abstract void UpdateHealth(int currentHp); //채력이 바뀌었을 때 표현

        public abstract void UpdateMana(int currentMana); //마나가 바뀌었을 때 표현

        public abstract void UpdateStatuses(NetworkList<StatusData> statuses); //상태이상이 바뀌었을 때 표현
    }
}
