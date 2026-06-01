using System;
using Controllers.LobbyController;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace {
    public class Lobby_FullScreen : MonoBehaviour, UI_ILayerInfo {
        [SerializeField] private Button btn_GameStart;
        [SerializeField] private Button btn_Deck;
        [SerializeField] private Button btn_Tutorial;

        [SerializeField] private Button btn_credit;


        public EUILayer TargetLayer => EUILayer.FullScreen;


        private void Start() {
            if (LobbyController.Instance != null) {
                LobbyController.Instance.RegisterLobbyUI(this);
                BindEvents();
            }
        }

        private void BindEvents() {
            var cont = LobbyController.Instance;

            btn_GameStart.onClick.AddListener(() => cont.OnGameStartPressed());
            btn_Deck.onClick.AddListener(() => cont.OnDeckPressed());
            btn_Tutorial.onClick.AddListener(() => cont.OnTutorialPressed());
            btn_credit.onClick.AddListener(() => cont.OnCreditPressed());
        }
    }
}