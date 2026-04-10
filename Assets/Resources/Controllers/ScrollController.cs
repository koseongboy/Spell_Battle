using ScriptableObjects.ScrollData;
using UnityEngine;

namespace Controller.ScrollController
{
    public class ScrollController : MonoBehaviour
    {
        public ScrollData myData;
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        public void Setup(ScrollData data)
        {
            myData = data;
            gameObject.name = data.word + "_Scroll";
        }

        public void FlyTo(Vector3 pos, Quaternion rot)
        {
            targetPosition = pos;
            targetRotation = rot;
        }

        void Awake()
        {
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }

        // Update is called once per frame
        void Update()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
}
