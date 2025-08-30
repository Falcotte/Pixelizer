using System.Collections.Generic;
using System.IO;
using System.Linq;
using NaughtyAttributes;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
            if (_pixelizer.PixCollection == null || _pixelizer.PixCollection.Count == 0)
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

            int pixCount = _pixelizer.PixCollection.Count;

            NativeArray<float3> pixColors = new NativeArray<float3>(pixCount, Allocator.TempJob);

            for (int i = 0; i < pixCount; i++)
            {
                Color pixColor = _pixelizer.PixCollection[i].Color;

                pixColors[i] = new float3(pixColor.r, pixColor.g, pixColor.b);
            }

            NativeArray<float3> colorPaletteColors =
                new NativeArray<float3>(_colorPalette.Colors.Count, Allocator.TempJob);

            for (int i = 0; i < _colorPalette.Colors.Count; i++)
            {
                Color colorPaletteColor = _colorPalette.Colors[i];

                colorPaletteColors[i] = new float3(colorPaletteColor.r, colorPaletteColor.g, colorPaletteColor.b);
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

            for (int i = 0; i < _pixelizer.PixCollection.Count; i++)
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
                float3 pixOKColor = LinearRgbToOKLab(pixColor);

                int closestIndex = 0;
                float closestDifference = float.MaxValue;

                for (int j = 0; j < ColorPaletteColors.Length; j++)
                {
                    float3 colorPaletteColor = ColorPaletteColors[j];
                    float3 colorPaletteOKColor = LinearRgbToOKLab(colorPaletteColor);

                    float difference = GetColorDifference(pixOKColor, colorPaletteOKColor);

                    if (difference < closestDifference)
                    {
                        closestDifference = difference;
                        closestIndex = j;
                    }
                }

                ClosestColorIndices[index] = closestIndex;
            }

            /// <summary>
            /// OKLab-based perceptual difference
            /// </summary>
            private float GetColorDifference(float3 color1, float3 color2)
            {
                return math.lengthsq(color1 - color2);
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

            int pixCount = _pixelizer.PixCollection.Count;

            var pixColors =
                new NativeArray<float3>(pixCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < pixCount; i++)
            {
                var color = _pixelizer.PixCollection[i].Color;
                pixColors[i] = LinearRgbToOKLab(new float3(color.r, color.g, color.b));
            }

            var centroids =
                new NativeArray<float3>(colorCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < colorCount; i++)
            {
                var pixColor = _pixelizer.PixCollection[Random.Range(0, pixCount)].Color;
                centroids[i] = LinearRgbToOKLab(new float3(pixColor.r, pixColor.g, pixColor.b));
            }

            var assignments = new NativeArray<int>(pixCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            int workerCount = math.max(1, Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount);
            int lanes = workerCount + 1;
            int laneStride = colorCount;

            var partialSums =
                new NativeArray<float3>(lanes * laneStride, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            var partialCounts =
                new NativeArray<int>(lanes * laneStride, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            var reducedSums = new NativeArray<float3>(colorCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            var reducedCounts = new NativeArray<int>(colorCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < iterationCount; i++)
            {
                var assignJob = new AssignNearestCentroidJob
                {
                    PixColors = pixColors,
                    Centroids = centroids,
                    Assignments = assignments
                };

                JobHandle assignHandle = assignJob.Schedule(pixCount, 128);

                var clearJob = new ClearPartialsJob
                {
                    PartialSums = partialSums,
                    PartialCounts = partialCounts
                };
                JobHandle clearHandle = clearJob.Schedule(lanes * laneStride, 128, assignHandle);

                var accumulateJob = new AccumulatePerThreadJob
                {
                    PixColors = pixColors,
                    Assignments = assignments,
                    LaneStride = laneStride,
                    PartialSums = partialSums,
                    PartialCounts = partialCounts
                };
                JobHandle accumulateHandle = accumulateJob.Schedule(pixCount, 128, clearHandle);

                var reduceJob = new ReduceCentroidsJob
                {
                    Lanes = lanes,
                    LaneStride = laneStride,
                    PartialSums = partialSums,
                    PartialCounts = partialCounts,
                    OutSums = reducedSums,
                    OutCounts = reducedCounts
                };
                JobHandle reduceHandle = reduceJob.Schedule(colorCount, 64, accumulateHandle);

                var updateJob = new UpdateCentroidsJob
                {
                    Sums = reducedSums,
                    Counts = reducedCounts,
                    Centroids = centroids
                };
                JobHandle updateHandle = updateJob.Schedule(colorCount, 64, reduceHandle);

                updateHandle.Complete();
            }

            var colorPalette = new List<Color>(colorCount);

            for (int i = 0; i < colorCount; i++)
            {
                var color = OKLabToLinearRgb(centroids[i]);
                colorPalette.Add(new Color(color.x, color.y, color.z, 1f));
            }

            pixColors.Dispose();
            centroids.Dispose();
            assignments.Dispose();
            partialSums.Dispose();
            partialCounts.Dispose();
            reducedSums.Dispose();
            reducedCounts.Dispose();

#if BENCHMARK
            stopwatch.Stop();
            Debug.Log($"GetColorPalette took {stopwatch.ElapsedMilliseconds} ms");
#endif

            return colorPalette;
        }

        [BurstCompile]
        private struct AssignNearestCentroidJob : IJobParallelFor
        {
            [Unity.Collections.ReadOnly] public NativeArray<float3> PixColors;
            [Unity.Collections.ReadOnly] public NativeArray<float3> Centroids;

            [WriteOnly] public NativeArray<int> Assignments;

            public void Execute(int index)
            {
                float3 pixColor = PixColors[index];

                float bestDistance = float.MaxValue;
                int bestIndex = 0;

                // Euclidean in RGB
                for (int i = 0; i < Centroids.Length; i++)
                {
                    float3 difference = pixColor - Centroids[i];

                    float distance = math.lengthsq(difference);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = i;
                    }
                }

                Assignments[index] = bestIndex;
            }
        }

        [BurstCompile]
        private struct ClearPartialsJob : IJobParallelFor
        {
            public NativeArray<float3> PartialSums;
            public NativeArray<int> PartialCounts;

            public void Execute(int index)
            {
                PartialSums[index] = float3.zero;
                PartialCounts[index] = 0;
            }
        }

        [BurstCompile]
        private struct AccumulatePerThreadJob : IJobParallelFor
        {
            [Unity.Collections.ReadOnly] public NativeArray<float3> PixColors;
            [Unity.Collections.ReadOnly] public NativeArray<int> Assignments;

            public int LaneStride;

            [NativeDisableParallelForRestriction] public NativeArray<float3> PartialSums;
            [NativeDisableParallelForRestriction] public NativeArray<int> PartialCounts;

            [NativeSetThreadIndex] private int _threadIndex;

            public void Execute(int index)
            {
                int assignment = Assignments[index];
                float3 pixColor = PixColors[index];

                int lane = (_threadIndex <= 0) ? 0 : (_threadIndex - 1);
                int baseIdx = lane * LaneStride + assignment;

                PartialSums[baseIdx] += pixColor;
                PartialCounts[baseIdx] += 1;
            }
        }

        [BurstCompile]
        private struct ReduceCentroidsJob : IJobParallelFor
        {
            [Unity.Collections.ReadOnly] public int Lanes;
            [Unity.Collections.ReadOnly] public int LaneStride;

            [Unity.Collections.ReadOnly] public NativeArray<float3> PartialSums;
            [Unity.Collections.ReadOnly] public NativeArray<int> PartialCounts;

            [WriteOnly] public NativeArray<float3> OutSums;
            [WriteOnly] public NativeArray<int> OutCounts;

            public void Execute(int k)
            {
                float3 sum = float3.zero;

                int count = 0;
                for (int lane = 0; lane < Lanes; lane++)
                {
                    int index = lane * LaneStride + k;
                    sum += PartialSums[index];
                    count += PartialCounts[index];
                }

                OutSums[k] = sum;
                OutCounts[k] = count;
            }
        }

        [BurstCompile]
        private struct UpdateCentroidsJob : IJobParallelFor
        {
            [Unity.Collections.ReadOnly] public NativeArray<float3> Sums;
            [Unity.Collections.ReadOnly] public NativeArray<int> Counts;

            public NativeArray<float3> Centroids;

            public void Execute(int k)
            {
                int count = Counts[k];
                if (count > 0)
                {
                    Centroids[k] = Sums[k] / math.max(1, count);
                }
            }
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

        private static float3 LinearRgbToOKLab(float3 color)
        {
            float l = 0.4122214708f * color.x + 0.5363325363f * color.y + 0.0514459929f * color.z;
            float m = 0.2119034982f * color.x + 0.6806995451f * color.y + 0.1073969566f * color.z;
            float s = 0.0883024619f * color.x + 0.2817188376f * color.y + 0.6299787005f * color.z;

            float l_ = math.pow(math.max(0f, l), 1f / 3f);
            float m_ = math.pow(math.max(0f, m), 1f / 3f);
            float s_ = math.pow(math.max(0f, s), 1f / 3f);

            float L = 0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_;
            float a = 1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_;
            float b = 0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_;

            return new float3(L, a, b);
        }

        private static float3 OKLabToLinearRgb(float3 color)
        {
            float l_ = color.x + 0.3963377774f * color.y + 0.2158037573f * color.z;
            float m_ = color.x - 0.1055613458f * color.y - 0.0638541728f * color.z;
            float s_ = color.x - 0.0894841775f * color.y - 1.2914855379f * color.z;

            float l = math.pow(math.max(0f, l_), 3f);
            float m = math.pow(math.max(0f, m_), 3f);
            float s = math.pow(math.max(0f, s_), 3f);

            float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
            float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
            float b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;

            return new float3(r, g, b);
        }

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