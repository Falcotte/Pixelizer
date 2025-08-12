using NaughtyAttributes;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class Pixelizer : MonoBehaviour
    {
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

        private Pix[] _pixCollection;
        public Pix[] PixCollection => _pixCollection;

        public void Pixelize()
        {
            if (_width * _height == 0)
                return;

            if (_sourceTexture == null)
            {
                Debug.LogWarning("No texture found to pixelize");
                return;
            }

            CreateGrid();

            _colorizer.SetPixColors(_sourceTexture, _currentWidth, _currentHeight);

            _texturizer.Texturize();
            _texturizer.SetVisualTexture();
        }

        private void CreateGrid()
        {
            _currentWidth = _width;
            _currentHeight = _height;

            _pixCollection = new Pix[_width * _height];
            int pixIndex = 0;

            for (int i = 0; i < _width; i++)
            {
                for (int j = 0; j < _height; j++)
                {
                    Pix pix = new();

                    pix.Pixelizer = this;
                    pix.Position = new Vector2Int(i, j);

                    _pixCollection[pixIndex] = pix;
                    pixIndex++;
                }
            }

            _texturizer.SetVisualSize(_width, _height);
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