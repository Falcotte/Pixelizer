using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;

        private void OnEnable()
        {
            Pixelizer.OnGridSizeUpdated += SetCameraSize;
        }

        private void OnDisable()
        {
            Pixelizer.OnGridSizeUpdated -= SetCameraSize;
        }

        public void SetCameraSize(float width, float height)
        {
            if(width / height >= _mainCamera.aspect)
            {
                _mainCamera.orthographicSize = width / (2 * _mainCamera.aspect);
            }
            else
            {
                _mainCamera.orthographicSize = height / 2f;
            }
        }
    }
}