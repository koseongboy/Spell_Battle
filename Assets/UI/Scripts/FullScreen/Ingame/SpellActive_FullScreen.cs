using Cards.CardUIDatas;
using DG.Tweening;
using Models.CardDatabases;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace {
    public class SpellActive_FullScreen : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<(string, Property)> {
        public EUILayer TargetLayer => EUILayer.Top;

        public TextMeshProUGUI txt_Spell;
        public RawImage rawImage;
        
        [Header("파티클 동기화 설정")]
        [Tooltip("씬(Scene)에 배치해둔 마법 파티클 오브젝트의 정확한 이름을 적어주세요.")]
        public string particleObjectNameInScene = "Magic circle 2";

        private ParticleSystem sceneParticle;
        
        // 카메라 원위치 복구를 위한 변수 캐싱
        private Transform mainCamTransform;
        private Vector3 originalCameraPos;
        private bool isCamCached = false;
        
        public void ReceiveData((string, Property) data) {
            Color dbColor = CardDatabase.Instance.GetElementData(data.Item2).Color;
            dbColor.a = 1f; 
            
            // rawImage.color = dbColor;  // 좀 밤티임
            txt_Spell.text = data.Item1;
        }

        private void OnEnable() {
            PlaySceneParticle();
            StartContinuousCameraShake();
        }

        private void OnDisable() {
            StopCameraShakeAndReset();
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

            // 파티클 상태를 맨 처음으로 초기화한 뒤 재생
            sceneParticle.Simulate(0f, true, true);
            sceneParticle.Play();
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