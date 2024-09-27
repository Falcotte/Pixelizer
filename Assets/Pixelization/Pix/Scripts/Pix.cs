using AngryKoala.ObjectPool;
using NaughtyAttributes;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class Pix : MonoBehaviour, IPoolable
    {
        [HideInInspector] public Pixelizer Pixelizer;

        [SerializeField] private MeshFilter pixMeshFilter;
        public MeshFilter MeshFilter => pixMeshFilter;

        [SerializeField] private MeshRenderer pixMeshRenderer;
        public MeshRenderer MeshRenderer => pixMeshRenderer;

        public Vector2Int Position;

        [HideInInspector] public Color OriginalColor;

        [SerializeField][OnValueChanged("OnColorChanged")] private Color color;
        public Color Color => color;

        // Used with colorizer color groups
        [HideInInspector] public int ColorIndex;

        [SerializeField][OnValueChanged("OnHSVChanged")][Range(0f, 1f)] private float hue;

        [SerializeField][OnValueChanged("OnHSVChanged")][Range(0f, 1f)] private float saturation;

        [SerializeField][OnValueChanged("OnHSVChanged")][Range(0f, 1f)] private float value;

        public void Initialize()
        {

        }

        public void Terminate()
        {

        }
        
        public void SetColor(Color color)
        {
            this.color = color;

            Color.RGBToHSV(color, out hue, out saturation, out value);

            AdjustMaterial();
        }

        public void ResetColor()
        {
            color = OriginalColor;

            Color.RGBToHSV(color, out hue, out saturation, out value);

            AdjustMaterial();
        }

        public void ComplementColor()
        {
            float maxValue = 0f;
            float minValue = 1f;

            for(int i = 0; i < 3; i++)
            {
                if(color[i] >= maxValue)
                {
                    maxValue = color[i];
                }
                if(color[i] <= minValue)
                {
                    minValue = color[i];
                }
            }

            color = new Color(maxValue + minValue - color.r, maxValue + minValue - color.g, maxValue + minValue - color.b);

            AdjustMaterial();
        }

        public void InvertColor()
        {
            color = new Color(1 - color.r, 1 - color.g, 1 - color.b);

            AdjustMaterial();
        }

        public void SetMaterial()
        {
            if(Pixelizer.UsePerformanceMode)
            {
                pixMeshRenderer.sharedMaterial.shader = Shader.Find("Unlit/Texture");

                if(Pixelizer.PerformanceMode == PerformanceMode.Level1)
                {
                    SetUVs();
                }
            }
            else
            {
                pixMeshRenderer.material.shader = Shader.Find("Unlit/Color");
                pixMeshRenderer.material.color = color;
            }
        }

        public void SetUVs()
        {
            Mesh mesh = pixMeshFilter.mesh;

            Vector2[] uvs = new Vector2[4];

            uvs[0] = ConvertPixelsToUV(Position.x + .1f, Position.y + .9f, Pixelizer.CurrentWidth, Pixelizer.CurrentHeight);
            uvs[1] = ConvertPixelsToUV(Position.x + .9f, Position.y + .9f, Pixelizer.CurrentWidth, Pixelizer.CurrentHeight);
            uvs[2] = ConvertPixelsToUV(Position.x + .1f, Position.y + .1f, Pixelizer.CurrentWidth, Pixelizer.CurrentHeight);
            uvs[3] = ConvertPixelsToUV(Position.x + .9f, Position.y + .1f, Pixelizer.CurrentWidth, Pixelizer.CurrentHeight);

            mesh.uv = uvs;
        }

        private Vector2 ConvertPixelsToUV(float x, float y, int textureWidth, int textureHeight)
        {
            return new Vector2(x / textureWidth, y / textureHeight);
        }

        private void AdjustMaterial()
        {
#if UNITY_EDITOR
            if(UnityEditor.EditorApplication.isPlaying && !Pixelizer.UsePerformanceMode)
            {
                pixMeshRenderer.material.color = color;
            }
#endif
        }

        #region Validation

        private void OnColorChanged()
        {
            Color.RGBToHSV(color, out hue, out saturation, out value);
        }

        private void OnHSVChanged()
        {
            color = Color.HSVToRGB(hue, saturation, value);

            AdjustMaterial();
        }

        #endregion
        
    }
}