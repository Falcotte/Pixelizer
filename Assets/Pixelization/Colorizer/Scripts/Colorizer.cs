using System.Collections.Generic;
using System.IO;
using System.Linq;
using NaughtyAttributes;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AngryKoala.Pixelization
{
    public class Colorizer : MonoBehaviour
    {
        [SerializeField] private Pixelizer _pixelizer;

        [SerializeField] private ColorPalette _colorPalette;
        public ColorPalette ColorPalette => _colorPalette;

        [SerializeField] private bool _createNewColorPaletteOnColorize;

        [SerializeField] [OnValueChanged("OnColorPaletteColorCountChanged")] [Range(1, 10)]
        private int _newColorPaletteColorCount = 1;

        private List<Color> _newColorPaletteColors = new();

        private enum ColorizationStyle
        {
            Replace,
            ReplaceWithOriginalSaturationAndValue
        }

        [SerializeField] private ColorizationStyle _colorizationStyle;

        [SerializeField] private bool _useValueRamp;

        [SerializeField] [ShowIf("_useValueRamp")] [OnValueChanged("AdjustValueRampCurve")] [Range(2, 10)]
        private int _stepCount = 2;

        [SerializeField] [ShowIf("_useValueRamp")]
        private AnimationCurve _valueRampCurve;

        private NativeArray<Color> _sourceNativeArray;
        private NativeArray<Color> _blockColorsNativeArray;

        private int _cachedSourceWidth = -1;
        private int _cachedSourceHeight = -1;
        private int _cachedBlockWidth = -1;
        private int _cachedBlockHeight = -1;

        private void OnDisable() => DisposeBuffers();
        private void OnDestroy() => DisposeBuffers();

        public void SetPixColors(Texture2D sourceTexture, int blockWidth, int blockHeight)
        {
#if BENCHMARK
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif

            int sourceWidth = sourceTexture.width;
            int sourceHeight = sourceTexture.height;

            EnsureBuffers(sourceWidth, sourceHeight, blockWidth, blockHeight);

            if (!_sourceNativeArray.IsCreated || !_blockColorsNativeArray.IsCreated)
            {
                return;
            }

            Color[] sourcePixelsManaged = sourceTexture.GetPixels();
            _sourceNativeArray.CopyFrom(sourcePixelsManaged);

            var job = new AverageColorJob
            {
                SourcePixels = _sourceNativeArray,
                BlockColors = _blockColorsNativeArray,
                SourceWidth = sourceWidth,
                SourceHeight = sourceHeight,
                BlockWidth = blockWidth,
                BlockHeight = blockHeight
            };

            JobHandle jobHandle = job.Schedule(blockWidth * blockHeight, 64);
            jobHandle.Complete();

            for (int i = 0; i < _blockColorsNativeArray.Length; i++)
            {
                Color color = _blockColorsNativeArray[i];
                _pixelizer.PixCollection[i].OriginalColor = color;
                _pixelizer.PixCollection[i].Color = color;
            }

#if BENCHMARK
            stopwatch.Stop();
            Debug.Log($"SetPixColors took {stopwatch.ElapsedMilliseconds} ms");
#endif
        }

        private void EnsureBuffers(int sourceWidth, int sourceHeight, int blockWidth, int blockHeight)
        {
            bool sourceChanged = (sourceWidth != _cachedSourceWidth) || (sourceHeight != _cachedSourceHeight);
            bool blockChanged = (blockWidth != _cachedBlockWidth) || (blockHeight != _cachedBlockHeight);

            if (sourceChanged || !_sourceNativeArray.IsCreated)
            {
                if (_sourceNativeArray.IsCreated)
                {
                    _sourceNativeArray.Dispose();
                }

                if (sourceWidth > 0 && sourceHeight > 0)
                {
                    _sourceNativeArray = new NativeArray<Color>(sourceWidth * sourceHeight, Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                }
            }

            if (blockChanged || !_blockColorsNativeArray.IsCreated)
            {
                if (_blockColorsNativeArray.IsCreated)
                {
                    _blockColorsNativeArray.Dispose();
                }

                if (blockWidth > 0 && blockHeight > 0)
                {
                    _blockColorsNativeArray = new NativeArray<Color>(blockWidth * blockHeight, Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                }
            }

            _cachedSourceWidth = sourceWidth;
            _cachedSourceHeight = sourceHeight;
            _cachedBlockWidth = blockWidth;
            _cachedBlockHeight = blockHeight;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, CompileSynchronously = true)]
        private struct AverageColorJob : IJobParallelFor
        {
            [Unity.Collections.ReadOnly] public NativeArray<Color> SourcePixels;

            [NativeDisableParallelForRestriction] public NativeArray<Color> BlockColors;

            public int SourceWidth;
            public int SourceHeight;
            public int BlockWidth;
            public int BlockHeight;

            public void Execute(int index)
            {
                int blockX = index % BlockWidth;
                int blockY = index / BlockWidth;

                int startX = Mathf.FloorToInt(blockX * (float)SourceWidth / BlockWidth);
                int startY = Mathf.FloorToInt(blockY * (float)SourceHeight / BlockHeight);
                int sizeX = Mathf.FloorToInt((float)SourceWidth / BlockWidth);
                int sizeY = Mathf.FloorToInt((float)SourceHeight / BlockHeight);

                float r = 0f, g = 0f, b = 0f;
                int count = 0;

                for (int y = 0; y < sizeY; y++)
                {
                    int rowStart = (startY + y) * SourceWidth + startX;

                    for (int x = 0; x < sizeX; x++)
                    {
                        Color c = SourcePixels[rowStart + x];
                        r += c.r;
                        g += c.g;
                        b += c.b;
                        count++;
                    }
                }

                BlockColors[index] = new Color(r / count, g / count, b / count);
            }
        }

        private void DisposeBuffers()
        {
            if (_sourceNativeArray.IsCreated)
            {
                _sourceNativeArray.Dispose();
            }

            if (_blockColorsNativeArray.IsCreated)
            {
                _blockColorsNativeArray.Dispose();
            }
        }

        #region Colorization

        public void Colorize()
        {
            if (_pixelizer.PixCollection == null || _pixelizer.PixCollection.Length == 0)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

#if BENCHMARK
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif

            if (_createNewColorPaletteOnColorize)
            {
                ColorPalette newColorPalette = ScriptableObject.CreateInstance<ColorPalette>();

                _newColorPaletteColors = GetColorPalette(_newColorPaletteColorCount);

                foreach (var color in _newColorPaletteColors)
                {
                    newColorPalette.Colors.Add(color);
                }

                _colorPalette = newColorPalette;
            }

            if (_colorPalette == null)
            {
                Debug.LogWarning("Color palette is not assigned");

#if BENCHMARK
                stopwatch.Stop();
#endif
                return;
            }

            if (_colorPalette.Colors.Count == 0)
            {
                Debug.LogWarning("No colors selected");

#if BENCHMARK
                stopwatch.Stop();
#endif
                return;
            }

            int pixCount = _pixelizer.PixCollection.Length;

            NativeArray<float3> pixColors = new NativeArray<float3>(pixCount, Allocator.TempJob);

            for (int i = 0; i < pixCount; i++)
            {
                Color pixColor = _pixelizer.PixCollection[i].Color;
                Color.RGBToHSV(pixColor, out float hue, out float saturation, out float value);

                pixColors[i] = new float3(hue, saturation, value);
            }

            NativeArray<float3> colorPaletteColors =
                new NativeArray<float3>(_colorPalette.Colors.Count, Allocator.TempJob);

            for (int i = 0; i < _colorPalette.Colors.Count; i++)
            {
                Color color = _colorPalette.Colors[i];
                Color.RGBToHSV(color, out float hue, out float saturation, out float value);

                colorPaletteColors[i] = new float3(hue, saturation, value);
            }

            NativeArray<int> closestColorIndices = new NativeArray<int>(pixCount, Allocator.TempJob);

            var getClosestColorJob = new GetClosestColorJob
            {
                PixColors = pixColors,
                ColorPaletteColors = colorPaletteColors,
                ClosestColorIndices = closestColorIndices
            };

            JobHandle jobHandle = getClosestColorJob.Schedule(pixCount, 64);
            jobHandle.Complete();

            for (int i = 0; i < _pixelizer.PixCollection.Length; i++)
            {
                float rampValue = 0f;

                if (_useValueRamp)
                {
                    rampValue = _valueRampCurve.Evaluate(_pixelizer.PixCollection[i].Color.Value());
                }

                switch (_colorizationStyle)
                {
                    case ColorizationStyle.Replace:
                    {
                        int colorIndex = closestColorIndices[i];
                        Color closestColor = _colorPalette.Colors[colorIndex];

                        if (_useValueRamp)
                        {
                            Color.RGBToHSV(closestColor, out float hue, out float saturation, out float value);

                            closestColor = Color.HSVToRGB(hue, saturation, rampValue);
                        }

                        _pixelizer.PixCollection[i].Color = closestColor;
                    }
                        break;

                    case ColorizationStyle.ReplaceWithOriginalSaturationAndValue:
                    {
                        Color originalColor = _pixelizer.PixCollection[i].Color;

                        int colorIndex = closestColorIndices[i];
                        Color adjustedColor = _colorPalette.Colors[colorIndex];

                        if (_useValueRamp)
                        {
                            _pixelizer.PixCollection[i].Color = Color.HSVToRGB(adjustedColor.Hue(),
                                originalColor.Saturation(),
                                rampValue);
                        }
                        else
                        {
                            _pixelizer.PixCollection[i].Color = Color.HSVToRGB(adjustedColor.Hue(),
                                originalColor.Saturation(),
                                originalColor.Value());
                        }
                    }
                        break;
                }
            }

            pixColors.Dispose();
            colorPaletteColors.Dispose();
            closestColorIndices.Dispose();

#if BENCHMARK
            stopwatch.Stop();
            Debug.Log($"Colorization took {stopwatch.ElapsedMilliseconds} ms");
#endif

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        [BurstCompile(FloatMode = FloatMode.Fast, CompileSynchronously = true)]
        private struct GetClosestColorJob : IJobParallelFor
        {
            [Unity.Collections.ReadOnly] public NativeArray<float3> PixColors;
            [Unity.Collections.ReadOnly] public NativeArray<float3> ColorPaletteColors;

            [WriteOnly] public NativeArray<int> ClosestColorIndices;

            public void Execute(int index)
            {
                float3 pixColor = PixColors[index];

                int closestIndex = 0;
                float closestDifference = float.MaxValue;

                for (int j = 0; j < ColorPaletteColors.Length; j++)
                {
                    float3 colorPaletteColor = ColorPaletteColors[j];
                    float difference = GetColorDifference(pixColor.x, pixColor.y, pixColor.z, colorPaletteColor.x,
                        colorPaletteColor.y,
                        colorPaletteColor.z);

                    if (difference < closestDifference)
                    {
                        closestDifference = difference;
                        closestIndex = j;
                    }
                }

                ClosestColorIndices[index] = closestIndex;
            }

            /// <summary>
            /// Computes a perceptual difference between two colors in HSV space, normalized to [0,1].
            /// The difference is calculated using weighted hue, saturation, and value distances,
            /// where weights adapt dynamically. Hue influence increases for vivid mid-bright colors and decreases for 
            /// dark or desaturated colors, while value influence increases in darker ranges.
            /// </summary>
            /// <param name="color1Hue">Hue of the first color in [0,1] range.</param>
            /// <param name="color1Saturation">Saturation of the first color in [0,1] range.</param>
            /// <param name="color1Value">Value of the first color in [0,1] range.</param>
            /// <param name="color2Hue">Hue of the second color in [0,1] range.</param>
            /// <param name="color2Saturation">Saturation of the second color in [0,1] range.</param>
            /// <param name="color2Value">Value of the second color in [0,1] range.</param>
            /// <returns>Returns 0 for identical colors and 1 for maximally different colors given these perceptual rules.</returns>
            private float GetColorDifference(float color1Hue, float color1Saturation, float color1Value,
                float color2Hue, float color2Saturation, float color2Value)
            {
                float hueDifference = math.abs(color1Hue - color2Hue);
                hueDifference = math.min(hueDifference, 1f - hueDifference) * 2f;
                float saturationDifference = math.abs(color1Saturation - color2Saturation);
                float valueDifference = math.abs(color1Value - color2Value);

                float minSaturation = math.min(color1Saturation, color2Saturation);
                float minValue = math.min(color1Value, color2Value);

                float darkness = math.smoothstep(0f, 1f, 1f - minValue);

                float midSaturation = SmoothRamp(minSaturation, 0.15f, 0.30f);
                float midValue = SmoothRamp(minValue, 0.15f, 0.30f);

                float vividness = minSaturation * minValue;

                float hueCurve = SmoothRamp(vividness, 0.35f, 0.85f);

                float hueDrive = math.saturate(0.8f * (midSaturation * midValue) + 0.4f * hueCurve);

                float hueWeight = math.lerp(0f, 4.0f, hueDrive);
                float saturationWeight = math.lerp(0.3f, 1.0f, minValue);
                float valueWeight = math.lerp(1.0f, 0.4f, minSaturation) * Mathf.Lerp(0.4f, 1.0f, minValue);

                float hueBrightnessBoost = SmoothRamp(vividness, 0.40f, 0.90f);
                hueWeight *= (1f + 2f * hueBrightnessBoost);

                hueWeight *= (1f - darkness);
                saturationWeight *= (1f - 0.8f * darkness);
                valueWeight = math.lerp(valueWeight, 3.0f, darkness);

                float activeHueRange = hueDifference > 1e-6f ? 1f : 0f;
                float activeSaturationRange = saturationDifference > 1e-6f ? 1f : 0f;
                float activeValueRange = valueDifference > 1e-6f ? 1f : 0f;

                float numerator = hueWeight * hueDifference + saturationWeight * saturationDifference +
                                  valueWeight * valueDifference;
                float denominator = hueWeight * activeHueRange + saturationWeight * activeSaturationRange +
                                    valueWeight * activeValueRange;

                if (denominator <= 1e-6f)
                    return 0f;

                return math.saturate(numerator / denominator);
            }

            private float SmoothRamp(float value, float edge0, float edge1)
            {
                if (edge1 <= edge0)
                    return value >= edge1 ? 1f : 0f;

                float step = math.saturate((value - edge0) / (edge1 - edge0));

                return step * step * (3f - 2f * step);
            }
        }

        #endregion

        #region Color Palette

        /// <summary>
        /// Generates a representative color palette of the specified size from the pixel data
        /// in <c>PixCollection</c> using a k-means–like clustering algorithm.
        /// </summary>
        /// <param name="colorCount">The number of colors to include in the generated palette.</param>
        /// <returns>A list of <see cref="Color"/> objects representing the final palette.</returns>
        private List<Color> GetColorPalette(int colorCount)
        {
#if BENCHMARK
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif

            int iterationCount = 10;

            List<Color> pixels = new List<Color>();
            foreach (var pix in _pixelizer.PixCollection)
            {
                pixels.Add(pix.Color);
            }

            int pixelCount = pixels.Count;

            Color[] centroids = new Color[colorCount];
            for (int i = 0; i < colorCount; i++)
            {
                centroids[i] = pixels[Random.Range(0, pixelCount)];
            }

            for (int i = 0; i < iterationCount; i++)
            {
                int[] nearestCentroidIndices = new int[pixelCount];

                for (int j = 0; j < pixelCount; j++)
                {
                    float nearestDistance = float.MaxValue;
                    int nearestCentroidIndex = 0;
                    for (int k = 0; k < colorCount; k++)
                    {
                        Vector3 colorA = new Vector3(pixels[j].r, pixels[j].g, pixels[j].b);
                        Vector3 colorB = new Vector3(centroids[k].r, centroids[k].g, centroids[k].b);

                        float distance = Vector3.Distance(colorA, colorB);
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestCentroidIndex = k;
                        }
                    }

                    nearestCentroidIndices[j] = nearestCentroidIndex;
                }

                for (int j = 0; j < colorCount; j++)
                {
                    var pixelsInCluster =
                        from pixelIndex in Enumerable.Range(0, pixelCount)
                        where nearestCentroidIndices[pixelIndex] == j
                        select pixels[pixelIndex];

                    if (pixelsInCluster.Any())
                    {
                        centroids[j] = new Color(pixelsInCluster.Average(c => c.r), pixelsInCluster.Average(c => c.g),
                            pixelsInCluster.Average(c => c.b));
                    }
                }
            }

#if BENCHMARK
            stopwatch.Stop();
            Debug.Log($"GetColorPalette took {stopwatch.ElapsedMilliseconds} ms");
#endif

            return centroids.ToList();
        }

        public void CreateNewColorPalette()
        {
#if UNITY_EDITOR
            if (_pixelizer.PixCollection == null)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

            if (_newColorPaletteColorCount <= 0)
            {
                Debug.LogWarning("Color palette color count must be greater than 0");
                return;
            }

            List<Color> centroids = GetColorPalette(_newColorPaletteColorCount);

            ColorPalette newColorPalette = ScriptableObject.CreateInstance<ColorPalette>();

            foreach (var centroid in centroids)
            {
                newColorPalette.Colors.Add(centroid);
            }

            _colorPalette = newColorPalette;
#endif
        }

        public void SaveColorPalette()
        {
#if UNITY_EDITOR
            if (_colorPalette == null)
            {
                Debug.LogWarning("Color palette is not assigned");
                return;
            }

            string path =
                UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                    "Assets/Pixelization/Colorizer/ScriptableObjects/ColorPalette_.asset");

            ColorPalette colorPalette = UnityEditor.AssetDatabase.Contains(_colorPalette)
                ? Instantiate(_colorPalette)
                : _colorPalette;

            colorPalette.name = Path.GetFileNameWithoutExtension(path);

            UnityEditor.AssetDatabase.CreateAsset(colorPalette, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate);

            Debug.Log($"Color palette saved as {colorPalette.name}");

            UnityEditor.EditorGUIUtility.PingObject(colorPalette);
#endif
        }

        #endregion

        #region Color Operations

        public void ComplementColors()
        {
            if (_pixelizer.PixCollection == null)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.ComplementColor();
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        public void InvertColors()
        {
            if (_pixelizer.PixCollection == null)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.InvertColor();
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        public bool ResetColors(bool texturize = false)
        {
            if (_pixelizer.PixCollection == null)
            {
                Debug.LogWarning("Pixelize a texture first");
                return false;
            }

            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.ResetColor();
            }

            if (texturize)
            {
                _pixelizer.Texturizer.Texturize();
                _pixelizer.Texturizer.SetVisualTexture();
            }

            return true;
        }

        #endregion

        #region Validation

        private void OnColorPaletteColorCountChanged()
        {
            _newColorPaletteColorCount = Mathf.Max(_newColorPaletteColorCount, 1);
        }

        private void AdjustValueRampCurve()
        {
            var keys = new Keyframe[_stepCount + 1];

            for (int i = 0; i < _stepCount; i++)
            {
                float time = i / (float)_stepCount;
                float value = i / (_stepCount - 1f);
                keys[i] = new Keyframe(time, value, 0f, 0f);
            }

            keys[_stepCount] = new Keyframe(1f, 1f, 0f, 0f);

            _valueRampCurve.keys = keys;
            _valueRampCurve.preWrapMode = WrapMode.ClampForever;
            _valueRampCurve.postWrapMode = WrapMode.ClampForever;

#if UNITY_EDITOR
            for (int i = 0; i < _valueRampCurve.length; i++)
            {
                var keyframe = _valueRampCurve[i];
                keyframe.inTangent = 0f;
                keyframe.outTangent = 0f;
                _valueRampCurve.MoveKey(i, keyframe);

                UnityEditor.AnimationUtility.SetKeyBroken(_valueRampCurve, i, true);
                UnityEditor.AnimationUtility.SetKeyLeftTangentMode(_valueRampCurve, i,
                    UnityEditor.AnimationUtility.TangentMode.Constant);
                UnityEditor.AnimationUtility.SetKeyRightTangentMode(_valueRampCurve, i,
                    UnityEditor.AnimationUtility.TangentMode.Constant);
            }
#endif
        }

        #endregion
    }
}