using System;
using System.Collections.Generic;
using Cards.EffectInfos;
using Controllers.SpellControllers;
using Managers.VoiceManagers;
using Microsoft.Unity.VisualStudio.Editor;
using Models.SpellPayloads;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

namespace DefaultNamespace
{
    public class Spell_FullScreen : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<SpellPayload>
    {
        public EUILayer TargetLayer => EUILayer.FullScreen;
        
        [SerializeField] private TextMeshProUGUI txt_concept;
        [SerializeField] private TextMeshProUGUI txt_prefix;
        [SerializeField] private Transform wordPanel;
        [SerializeField] private SpellWordPiece wordPiecePrefab;
        [SerializeField] private UnityEngine.UI.Image gaugeBar;

        private IObjectPool<SpellWordPiece> wordPiecePool;
        private List<SpellWordPiece> activeWords = new List<SpellWordPiece>();
        private bool isRecording = false;
        
        private void OnEnable() {

            if (VoiceManager.Instance != null)
            {
                VoiceManager.Instance.OnMicVolumeChanged += UpdateGauge;
            }

        }

        private void UpdateGauge(float volumeValue)
        {
            gaugeBar.fillAmount = volumeValue;
        }

        private void Awake() {
            // 오브젝트 풀 초기화
            wordPiecePool = new ObjectPool<SpellWordPiece>(
                createFunc: () => 
                {
                    SpellWordPiece obj = Instantiate(wordPiecePrefab, wordPanel);
                    return obj.GetComponent<SpellWordPiece>();
                },
                actionOnGet: (card) => card.gameObject.SetActive(true),
                actionOnRelease: (card) => 
                {
                    card.gameObject.SetActive(false);
                    card.transform.SetParent(wordPanel); // 반납 시 부모 원상복구
                },
                actionOnDestroy: (card) => Destroy(card.gameObject),
                collectionCheck: false,
                defaultCapacity: 3,
                maxSize: 10
            );
        }
        
        private void OnDisable()
        {
            ReleaseAllPieces();
            if (VoiceManager.Instance != null)
            {
                VoiceManager.Instance.OnMicVolumeChanged -= UpdateGauge;
            }
        }
        
        // 데이터 받아서 화면 구성하는 함수.
        public void ReceiveData(SpellPayload payload) {
            isRecording = false;
            
            txt_concept.text = payload.GetConcept();
            txt_prefix.text = payload.GetPrefix();
            
            ReleaseAllPieces();
            var cardList = payload.GetCards();
            for (int i = 0; i < cardList.Count; i++) {
                var piece = wordPiecePool.Get();
                piece.UpdateUI(cardList[i]);
                
                activeWords.Add(piece);
            }
        }

        public void Toggle_Record() {
            if (!isRecording) {
                StartRecording();
            }
            else {
                StopRecording();
            }
            isRecording = !isRecording;
        }

        public void StartRecording() {
            SpellController.Instance.StartRecording();
        }

        public async void StopRecording() {
            await SpellController.Instance.EndRecording();
        }
        
        // 풀 piece 모두 반납하는 함수. 초기화용.
        private void ReleaseAllPieces()
        {
            foreach (var piece in activeWords)
            {
                wordPiecePool.Release(piece);
            }
            wordPiecePool.Clear();
        }
        
    }
}
