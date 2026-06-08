using System;
using System.Collections.Generic;
using Controllers.TurnControllers;
using Models.PlayerModels;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Pool;

namespace DefaultNamespace
{
    public class StatusDetail_Popup : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.Popup;
        
        [Header("Status List")]
        public UI_StatusPiece statusPrefab;
        public Transform poolContainer;
        public Transform playerStatusContainer;
        public Transform enemyStatusContainer;
        private IObjectPool<UI_StatusPiece> statusPool;
        
        private List<UI_StatusPiece> activeStatusPieces = new List<UI_StatusPiece>();

        private void Awake() {
            statusPool = new ObjectPool<UI_StatusPiece>(
                createFunc: () => {
                    UI_StatusPiece obj = Instantiate(statusPrefab, poolContainer);
                    return obj.GetComponent<UI_StatusPiece>();
                },
                actionOnGet: (status) => status.gameObject.SetActive(true),
                actionOnRelease: (status) => {
                    status.gameObject.SetActive(false);
                    status.transform.SetParent(poolContainer);
                },
                actionOnDestroy: (status) => Destroy(status.gameObject),
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 50
            );
        }

        public void OnEnable() {
            OpenAction();
            
            NetworkList<StatusData> playerStatusList = SpellController.Instance.MyPlayer.ActiveStatuses;
            NetworkList<StatusData> enemyStatusList = SpellController.Instance.EnemyPlayer.ActiveStatuses;    
            
            ReleaseAllPiece();
            
            UpdateStatusUI( playerStatusContainer, playerStatusList );
            UpdateStatusUI( enemyStatusContainer, enemyStatusList );
        }

        private void UpdateStatusUI(Transform container, NetworkList<StatusData> statusList) {
            
            foreach (var statusData in statusList) {
                var piece = statusPool.Get();
                piece.transform.SetParent(container);
                
                piece.UpdateUI( statusData );
            }
        }

        private void ReleaseAllPiece() {
            foreach (var card in activeStatusPieces) {
                statusPool.Release(card);
            }
        }

        public void OpenAction() {
            // TODO : DOTween
        }

        public void CloseUI() {
            UILoader.Instance.HideUI("StatusDetail_Popup");
        }
    }
}
