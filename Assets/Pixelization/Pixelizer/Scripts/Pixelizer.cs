using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class Pixelizer : MonoBehaviour
    {
        [SerializeField] private PixPool _pixPool;
        
        [SerializeField] private Colorizer _colorizer;
        public Colorizer Colorizer => _colorizer;

        [SerializeField] private Texturizer _texturizer;
        public Texturizer Texturizer => _texturizer;

        [SerializeField] [OnValueChanged("PreserveRatio")]
        private Texture2D _sourceTexture;

        public Texture2D SourceTexture
        {
            get => _sourceTexture;
            set => _sourceTexture = value;
        }

        private bool ShowOriginalDimensions => _sourceTexture != null;

        [SerializeField] [ReadOnly] [ShowIf("ShowOriginalDimensions")]
        private int _originalWidth;

        [SerializeField] [ReadOnly] [ShowIf("ShowOriginalDimensions")]
        private int _originalHeight;

        [SerializeField] [OnValueChanged("OnWidthChanged")]
        private int _width;

        private int _currentWidth;
        public int CurrentWidth => _currentWidth;

        [SerializeField] [OnValueChanged("OnHeightChanged")]
        private int _height;

        private int _currentHeight;
        public int CurrentHeight => _currentHeight;

        [Tooltip("Try to match the width/height ratio of the grid to the texture.")]
        [SerializeField]
        [OnValueChanged("PreserveRatio")]
        private bool _preserveRatio;

        private List<Pix> _pixCollection = new();
        public List<Pix> PixCollection => _pixCollection;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
        }
        
        public void Pixelize()
        {
            if (_sourceTexture == null)
            {
                Debug.LogWarning("No texture found to pixelize");
                return;
            }
            
            if (_width * _texturizer.PixSize > 16384 || _height * _texturizer.PixSize > 16384)
            {
                Debug.LogWarning("Texture size exceeds maximum allowed size");
                return;
            }

#if BENCHMARK
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif

            CreateGrid();

            _colorizer.SetPixColors(_sourceTexture, _currentWidth, _currentHeight);

            _texturizer.Texturize();
            _texturizer.SetVisualTexture();

#if BENCHMARK
            stopwatch.Stop();
            Debug.Log($"Pixelization took {stopwatch.ElapsedMilliseconds} ms");
#endif
        }

        private void CreateGrid()
        {
#if BENCHMARK
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif

            if(_pixCollection is { Count: > 0 } && _pixCollection.Count != _width * _height)
            {
                foreach (Pix pix in _pixCollection)
                {
                    _pixPool.Return(pix);
                }
                
                _pixCollection.Clear();
                
                if (_pixCollection.Capacity < _width * _height)
                {
                    _pixCollection.Capacity = _width * _height;
                }
            }
            
            _currentWidth = _width;
            _currentHeight = _height;

            if (_pixCollection.Count != _width * _height)
            {
                for (int j = 0; j < _height; j++)
                {
                    for (int i = 0; i < _width; i++)
                    {
                        Pix pix = _pixPool.Get();

                        _pixCollection.Add(pix);
                    }
                }
                
                _texturizer.SetVisualSize(_width, _height);
            }

#if BENCHMARK
            stopwatch.Stop();
            Debug.Log($"CreateGrid took {stopwatch.ElapsedMilliseconds} ms");
#endif
        }

        #region Validation

        private void OnValidate()
        {
            _originalWidth = _sourceTexture != null ? _sourceTexture.width : 0;
            _originalHeight = _sourceTexture != null ? _sourceTexture.height : 0;

            if (_sourceTexture != null)
            {
                _width = Mathf.Clamp(_width, 1, _originalWidth <= 16384 ? _originalWidth : 16384);
                _height = Mathf.Clamp(_height, 1, _originalHeight <= 16384 ? _originalHeight : 16384);
            }
            else
            {
                if (_width < 1)
                    _width = 1;
                if (_height < 1)
                    _height = 1;
            }
        }

        private void OnWidthChanged()
        {
            _width = Mathf.Max(_width, 1);

            if (_sourceTexture == null)
                return;

            if (_preserveRatio)
            {
                float ratio = (float)_sourceTexture.width / _sourceTexture.height;

                _height = Mathf.FloorToInt(_width * (1f / ratio));
                _height = Mathf.Max(_height, 1);
            }
        }

        private void OnHeightChanged()
        {
            _height = Mathf.Max(_height, 1);

            if (_sourceTexture == null)
                return;

            if (_preserveRatio)
            {
                float ratio = (float)_sourceTexture.width / _sourceTexture.height;

                _width = Mathf.FloorToInt(_height * ratio);
                _width = Mathf.Max(_width, 1);
            }
        }

        private void PreserveRatio()
        {
            if (_width >= _height)
            {
                OnWidthChanged();
            }
            else
            {
                OnHeightChanged();
            }
        }

        #endregion
    }
}