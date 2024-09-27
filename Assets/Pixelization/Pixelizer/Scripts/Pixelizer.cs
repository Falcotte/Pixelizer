using NaughtyAttributes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace AngryKoala.Pixelization
{
    public enum PerformanceMode
    {
        Level1,
        Level2
    }

    public class Pixelizer : MonoBehaviour
    {
        [SerializeField] private PixPool pixPool;

        [SerializeField] private Colorizer colorizer;
        public Colorizer Colorizer => colorizer;
        [SerializeField] private Texturizer texturizer;
        public Texturizer Texturizer => texturizer;

        [SerializeField] [OnValueChanged("PreserveRatio")]
        private Texture2D texture;

        public Texture2D Texture
        {
            get => texture;
            set => texture = value;
        }

        [HideInInspector] public Texture2D TexturizedTexture;

        private bool showOriginalDimensions => texture != null;

        [SerializeField] [ReadOnly] [ShowIf("showOriginalDimensions")]
        private int originalWidth;

        [SerializeField] [ReadOnly] [ShowIf("showOriginalDimensions")]
        private int originalHeight;

        [SerializeField] [OnValueChanged("OnWidthChanged")]
        private int width;

        [SerializeField] [HideInInspector] private int currentWidth;
        public int CurrentWidth => currentWidth;

        [SerializeField] [OnValueChanged("OnHeightChanged")]
        private int height;

        [SerializeField] [HideInInspector] private int currentHeight;
        public int CurrentHeight => currentHeight;


        [Tooltip("Try to match the width/height ratio of the grid to the texture.")]
        [SerializeField]
        [OnValueChanged("PreserveRatio")]
        private bool preserveRatio;

        [SerializeField] private float pixSize;

        [SerializeField] private Pix pixPrefab;
        [SerializeField] [HideInInspector] private Pix performantPix;

        [Tooltip(
            "Performance Mode Level 1 uses mesh UVs instead of material instances to display texturized image. Greatly reduces draw calls." +
            "Performance Mode Level 2 uses a single mesh to display texturized image. Greatly reduces tris count.")]
        [SerializeField]
        private bool usePerformanceMode;

        public bool UsePerformanceMode => usePerformanceMode;

        [SerializeField] [ShowIf("usePerformanceMode")]
        private PerformanceMode performanceMode;

        public PerformanceMode PerformanceMode => performanceMode;

#if UNITY_EDITOR
        private bool usePerformanceModeEnabled => !EditorApplication.isPlaying;
#endif

        [SerializeField] private bool usePixPool;

        [SerializeField] private Pix[] pixCollection;
        public Pix[] PixCollection => pixCollection;

        public static UnityAction<float, float> OnGridSizeUpdated;

        private void OnEnable()
        {
            Colorizer.OnColorize += SetPixTextures;
        }

        private void OnDisable()
        {
            Colorizer.OnColorize += SetPixTextures;
        }

        private void Start()
        {
            Pixelize();
        }

        private void Update()
        {
            OnGridSizeUpdated?.Invoke(currentWidth * pixSize, currentHeight * pixSize);
        }

        private void OnApplicationQuit()
        {
            SetPixTextures(null);
        }

        public void Pixelize()
        {
            if (width * height == 0)
                return;

            if (texture == null)
            {
                Debug.LogWarning("No texture found to pixelize");
                return;
            }
            
            CreateGrid();

            SetPixColors();

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                foreach (var pix in pixCollection)
                {
                    pix.SetMaterial();
                }

                if (usePerformanceMode)
                {
                    texturizer.Texturize();
                    SetPixTextures();
                }
            }
#endif
            
            OnGridSizeUpdated?.Invoke(currentWidth * pixSize, currentHeight * pixSize);
        }

        private void CreateGrid()
        {
            Clear();

            currentWidth = width;
            currentHeight = height;

            pixCollection = new Pix[width * height];
            int pixIndex = 0;

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    Pix pix = default;

                    if (usePixPool)
                    {
                        pix = pixPool.GetPooledObject(transform);
                    }
                    else
                    {
                        pix = Instantiate(pixPrefab, transform);
                    }

                    pix.Pixelizer = this;
                    pix.Position = new Vector2Int(i, j);

                    pix.gameObject.name = $"Pix[{i},{j}]";
                    pix.transform.localPosition = new Vector3(-width * pixSize / 2f + pixSize / 2f + i * pixSize, 0f,
                        -height * pixSize / 2f + pixSize / 2f + j * pixSize);
                    pix.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    pix.transform.localScale = new Vector3(pixSize, pixSize, 1f);

                    pix.MeshRenderer.enabled = !(usePerformanceMode && performanceMode == PerformanceMode.Level2);

                    pixCollection[pixIndex] = pix;
                    pixIndex++;
                }
            }

            if (usePixPool)
            {
                performantPix = pixPool.GetPooledObject(transform);
            }
            else
            {
                performantPix = Instantiate(pixPrefab, transform);
            }

            performantPix.Pixelizer = this;
            performantPix.Position = Vector2Int.zero;

            performantPix.gameObject.name = $"PerformantPix";
            performantPix.transform.localPosition = Vector3.zero;
            performantPix.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            performantPix.transform.localScale = new Vector3(pixSize * width, pixSize * height, 1f);

            performantPix.MeshRenderer.enabled = usePerformanceMode && performanceMode == PerformanceMode.Level2;
        }

        private void SetPixColors()
        {
            float textureAreaX = (float)texture.width / width;
            float textureAreaY = (float)texture.height / height;

            for (int i = 0; i < width * height; i++)
            {
                Color color = GetAverageColor(texture.GetPixels(Mathf.FloorToInt((i / height) * textureAreaX),
                    Mathf.FloorToInt(i % height * textureAreaY), Mathf.FloorToInt(textureAreaX),
                    Mathf.FloorToInt(textureAreaY)));

                pixCollection[i].OriginalColor = color;
                pixCollection[i].SetColor(color);
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

        public void Clear()
        {
            if (pixCollection != null)
            {
                for (int i = 0; i < pixCollection.Length; i++)
                {
                    if (usePixPool)
                    {
                        pixPool.ReturnToPool(pixCollection[i]);
                    }
                    else
                    {
                        DestroyImmediate(pixCollection[i].gameObject);
                    }
                }
            }

            pixCollection = null;

            if (performantPix != null)
            {
                if (usePixPool)
                {
                    pixPool.ReturnToPool(performantPix);
                }
                else
                {
                    DestroyImmediate(performantPix.gameObject);
                }
            }
        }

        private void SetPixTextures()
        {
            if (!usePerformanceMode)
                return;

            if (performanceMode == PerformanceMode.Level1)
            {
                for (int i = 0; i < pixCollection.Length; i++)
                {
                    pixCollection[i].MeshRenderer.sharedMaterial.SetTexture("_MainTex", TexturizedTexture);
                }
            }
            else
            {
                performantPix.MeshRenderer.sharedMaterial.SetTexture("_MainTex", TexturizedTexture);
            }
        }

        private void SetPixTextures(Texture2D texture)
        {
            if (!usePerformanceMode)
                return;

            if (performanceMode == PerformanceMode.Level1)
            {
                if (pixCollection == null)
                    return;

                for (int i = 0; i < pixCollection.Length; i++)
                {
                    pixCollection[i].MeshRenderer.sharedMaterial.SetTexture("_MainTex", texture);
                }
            }
            else
            {
                if (performantPix == null)
                    return;

                performantPix.MeshRenderer.sharedMaterial.SetTexture("_MainTex", texture);
            }
        }

        #region Validation

        private void OnValidate()
        {
            pixSize = Mathf.Max(pixSize, Mathf.Epsilon);

            originalWidth = texture != null ? texture.width : 0;
            originalHeight = texture != null ? texture.height : 0;
        }

        private void OnWidthChanged()
        {
            width = Mathf.Max(width, 1);

            if (texture == null)
            {
                return;
            }

            if (preserveRatio)
            {
                float ratio = (float)texture.width / texture.height;

                height = Mathf.FloorToInt(width * (1f / ratio));
                height = Mathf.Max(height, 1);
            }
        }

        private void OnHeightChanged()
        {
            height = Mathf.Max(height, 1);

            if (texture == null)
            {
                return;
            }

            if (preserveRatio)
            {
                float ratio = (float)texture.width / texture.height;

                width = Mathf.FloorToInt(height * ratio);
                width = Mathf.Max(width, 1);
            }
        }

        private void PreserveRatio()
        {
            if (width >= height)
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