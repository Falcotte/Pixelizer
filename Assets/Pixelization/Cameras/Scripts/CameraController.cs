using System;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;

        [SerializeField] private bool _autoAdjustCameraSize = true;

        private float _visualWidth;
        private float _visualHeight;

        private void OnEnable()
        {
            Texturizer.VisualSizeUpdated += OnVisualSizeUpdate;
        }

        private void OnDisable()
        {
            Texturizer.VisualSizeUpdated -= OnVisualSizeUpdate;
        }

        private void LateUpdate()
        {
            if(!_autoAdjustCameraSize)
                return;
            
            SetCameraSize(_visualWidth, _visualHeight);
        }

        private void OnVisualSizeUpdate(float width, float height)
        {
            _visualWidth = width;
            _visualHeight = height;
        }

        private void SetCameraSize(float width, float height)
        {
            if (width * height <= 0)
                return;
            
            if (width / height >= _mainCamera.aspect)
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