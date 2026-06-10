using System;
using System.Collections.Generic;
using Cards.EffectInfos;
using Controllers.SpellControllers;
using Managers.VoiceManagers;
using Microsoft.Unity.VisualStudio.Editor;
using DG.Tweening;
using Models.SpellPayloads;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

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

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.OnMicVolumeChanged += UpdateGauge;
            }

        }

        private void UpdateGauge(float volumeValue)
        {
            gaugeBar.fillAmount = volumeValue;
        }

        public RawImage rawImage;
        [Header("파티클 동기화 설정")]
        [Tooltip("씬(Scene)에 배치해둔 마법 파티클 오브젝트의 정확한 이름을 적어주세요.")]
        public string particleObjectNameInScene = "Magic circle";
        private ParticleSystem sceneParticle;
        
        // 카메라 원위치 복구를 위한 변수 캐싱
        private Transform mainCamTransform;
        private Vector3 originalCameraPos;
        private bool isCamCached = false;
        
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
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.OnMicVolumeChanged -= UpdateGauge;
            }
        }
        
        // 데이터 받아서 화면 구성하는 함수.
        public void ReceiveData(SpellPayload payload) {
            isRecording = false;
            rawImage.gameObject.SetActive(false);
            
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
                PlaySceneParticle();
                StartContinuousCameraShake();
            }
            else {
                StopRecording();
                StopSceneParticle();
                StopCameraShakeAndReset();
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
        
        
        private void PlaySceneParticle() {
            if (sceneParticle == null) {
                GameObject obj = GameObject.Find(particleObjectNameInScene);
                if (obj != null) {
                    sceneParticle = obj.GetComponent<ParticleSystem>();
                } else {
                    Debug.LogWarning($"[Spell UI] 씬에서 '{particleObjectNameInScene}' 이름의 파티클 오브젝트를 찾을 수 없습니다!");
                    return;
                }
            }

            rawImage.gameObject.SetActive(true);
            // 파티클 상태를 맨 처음으로 초기화한 뒤 재생
            sceneParticle.Simulate(0f, true, true);
            sceneParticle.Play();
        }
        
        private void StopSceneParticle() {
            sceneParticle.Stop();
            rawImage.gameObject.SetActive(false);
        }

        private void StartContinuousCameraShake() {
            if (Camera.main != null) {
                mainCamTransform = Camera.main.transform;
                
                originalCameraPos = mainCamTransform.position;
                isCamCached = true;

                mainCamTransform.DOKill();

                mainCamTransform.DOShakePosition(9999f, 0.1f, 14, 90f, false, false)
                    .SetUpdate(true);
            }
        }

        private void StopCameraShakeAndReset() {
            if (isCamCached && mainCamTransform != null) {
                mainCamTransform.DOKill();
                
                mainCamTransform.position = originalCameraPos;
                isCamCached = false;
            }
        }
        
    }
}
