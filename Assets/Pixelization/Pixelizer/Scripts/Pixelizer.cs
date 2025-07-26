using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

namespace AngryKoala.Pixelization
{
    public class Pixelizer : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _visual;

        [SerializeField] private Texturizer _texturizer;

        [SerializeField] [OnValueChanged("PreserveRatio")]
        private Texture2D _sourceTexture;

        public Texture2D SourceTexture
        {
            get => _sourceTexture;
            set => _sourceTexture = value;
        }

        public Texture2D TexturizedTexture { get; set; }

        private bool ShowOriginalDimensions => _sourceTexture != null;

        [SerializeField] [ReadOnly] [ShowIf("ShowOriginalDimensions")]
        private int _originalWidth;

        [SerializeField] [ReadOnly] [ShowIf("ShowOriginalDimensions")]
        private int _originalHeight;

        [SerializeField] [OnValueChanged("OnWidthChanged")]
        private int _width;

        [SerializeField] [HideInInspector] private int _currentWidth;
        public int CurrentWidth => _currentWidth;

        [SerializeField] [OnValueChanged("OnHeightChanged")]
        private int _height;

        [SerializeField] [HideInInspector] private int _currentHeight;
        public int CurrentHeight => _currentHeight;

        [Tooltip("Try to match the width/height ratio of the grid to the texture.")]
        [SerializeField]
        [OnValueChanged("PreserveRatio")]
        private bool _preserveRatio;

        [SerializeField] private float _pixSize;

        private Pix[] _pixCollection;
        public Pix[] PixCollection => _pixCollection;

        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        
        public static UnityAction<float, float> OnGridSizeUpdated;

        private void Start()
        {
            Pixelize();
        }
        
        private void LateUpdate()
        {
            OnGridSizeUpdated?.Invoke(_currentWidth * _pixSize, _currentHeight * _pixSize);
        }

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

            SetPixColors();

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                _texturizer.Texturize();
                SetPixTextures();
            }
#endif
            
            OnGridSizeUpdated?.Invoke(_currentWidth * _pixSize, _currentHeight * _pixSize);
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

                    pix.Position = new Vector2Int(i, j);

                    _pixCollection[pixIndex] = pix;
                    pixIndex++;
                }
            }

            _visual.transform.localScale = new Vector3(_pixSize * _width, _pixSize * _height, 1f);
        }

        private void SetPixColors()
        {
            float textureAreaX = (float)_sourceTexture.width / _width;
            float textureAreaY = (float)_sourceTexture.height / _height;

            for (int i = 0; i < _width * _height; i++)
            {
                Color color = GetAverageColor(_sourceTexture.GetPixels(Mathf.FloorToInt((i / _height) * textureAreaX),
                    Mathf.FloorToInt(i % _height * textureAreaY), Mathf.FloorToInt(textureAreaX),
                    Mathf.FloorToInt(textureAreaY)));

                _pixCollection[i].OriginalColor = color;
                _pixCollection[i].Color = color;
            }
        }

        private Color GetAverageColor(Color[] colors)
        {
            float r = 0f;
            float g = 0f;
            float b = 0f;

            for (int i = 0; i < colors.Length; i++)
            {
                r += colors[i].r;
                g += colors[i].g;
                b += colors[i].b;
            }

            r /= colors.Length;
            g /= colors.Length;
            b /= colors.Length;

            return new Color(r, g, b);
        }

        private void SetPixTextures()
        {
            _visual.sharedMaterial.SetTexture(MainTex, TexturizedTexture);
        }

        #region Validation

        private void OnValidate()
        {
            _pixSize = Mathf.Max(_pixSize, Mathf.Epsilon);

            _originalWidth = _sourceTexture != null ? _sourceTexture.width : 0;
            _originalHeight = _sourceTexture != null ? _sourceTexture.height : 0;
        }

        private void OnWidthChanged()
        {
            _width = Mathf.Max(_width, 1);

            if (_sourceTexture == null)
            {
                return;
            }

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
            {
                return;
            }

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