using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;

namespace AngryKoala.Pixelization
{
    public class Texturizer : MonoBehaviour
    {
        [SerializeField] private Pixelizer _pixelizer;

        private MeshRenderer _visual;

        private Texture2D _newTexture;

        public Texture2D TexturizedTexture { get; set; }

        [SerializeField, Min(1)] private int _pixSize;

        [SerializeField] private string _textureSavePath;

        // Persistent buffers
        private NativeArray<Color32> _sourceNativeArray;
        private NativeArray<Color32> _destinationNativeArray;

        // Cached sizes to prevent unnecessary reallocations
        private int _cachedSourceWidth = -1;
        private int _cachedSourceHeight = -1;
        private int _cachedPixSize = -1;
        private int _cachedDestinationWidth = -1;
        private int _cachedDestinationHeight = -1;

        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        public static UnityAction<float, float> VisualSizeUpdated;

        private void OnDisable() => DisposeBuffers();
        private void OnDestroy() => DisposeBuffers();

        private void CreateVisual()
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);

            visual.name = "Visual";
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;

            _visual = visual.GetComponent<MeshRenderer>();
            _visual.sharedMaterial = new Material(Shader.Find("Unlit/Texture"));
        }

        public void SetVisualSize(int width, int height)
        {
            if (_visual == null)
            {
                CreateVisual();
            }

            _visual.transform.localScale = new Vector3(width, height, 1f);

            VisualSizeUpdated?.Invoke(width, height);
        }

        public void SetVisualTexture()
        {
            _visual.sharedMaterial.SetTexture(MainTex, TexturizedTexture);
        }

        public void SaveTexture()
        {
            if (!AssetDatabase.IsValidFolder(_textureSavePath))
            {
                if (!CreateFolderAtSavePath(_textureSavePath))
                    return;
            }

            if (_newTexture == null)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

#if BENCHMARK
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif

            string path = AssetDatabase.GenerateUniqueAssetPath($"{_textureSavePath}/Texture_.png");

            byte[] bytes = _newTexture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

            importer.textureType = TextureImporterType.Default;

            TextureImporterSettings importerSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importerSettings);

            importerSettings.npotScale = TextureImporterNPOTScale.None;

            importer.SetTextureSettings(importerSettings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

#if BENCHMARK
            stopwatch.Stop();
            Debug.Log($"SaveTexture took {stopwatch.ElapsedMilliseconds} ms");
#endif
        }
        
        private bool CreateFolderAtSavePath(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                Debug.LogError("Save path is null or empty");
                return false;
            }

            if (!savePath.StartsWith("Assets"))
            {
                Debug.LogError("Path must start with 'Assets'");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(savePath))
            {
                string[] parts = savePath.Split('/');
                string current = parts[0];

                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }

                    current = next;
                }

                AssetDatabase.Refresh();
            }

            return true;
        }

        #region Texturization

        public void Texturize()
        {
            if (_pixelizer.PixCollection == null || _pixelizer.PixCollection.Length == 0)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

            if (_pixelizer.CurrentWidth * _pixSize > 16384 || _pixelizer.CurrentHeight * _pixSize > 16384)
            {
                Debug.LogWarning("Texture size exceeds maximum allowed size");
                return;
            }

#if BENCHMARK
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif

            if (_newTexture != null)
            {
                DestroyImmediate(_newTexture);
            }

            BuildTexture();

            TexturizedTexture = _newTexture;

#if BENCHMARK
            stopwatch.Stop();
            Debug.Log($"Texturization took {stopwatch.ElapsedMilliseconds} ms");
#endif
        }

        private void BuildTexture()
        {
            int sourceWidth = _pixelizer.CurrentWidth;
            int sourceHeight = _pixelizer.CurrentHeight;
            int pixSize = _pixSize;

            EnsureResources(sourceWidth, sourceHeight, pixSize);

            if (!_sourceNativeArray.IsCreated || !_destinationNativeArray.IsCreated || _newTexture == null)
            {
                return;
            }

            if (sourceWidth == 0 || sourceHeight == 0)
            {
                return;
            }

            FillSourceFromPixCollection();

            var buildJob = new BuildTextureJob
            {
                Source = _sourceNativeArray,
                Destination = _destinationNativeArray,
                SourceWidth = sourceWidth,
                SourceHeight = sourceHeight,
                DestinationWidth = _cachedDestinationWidth,
                PixSize = pixSize
            };

            JobHandle jobHandle = buildJob.Schedule(sourceWidth, 64);
            jobHandle.Complete();

            var rawTextureData = _newTexture.GetRawTextureData<Color32>();
            rawTextureData.CopyFrom(_destinationNativeArray);

            _newTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        private void EnsureResources(int sourceWidth, int sourceHeight, int pixSize)
        {
            if (pixSize < 1)
            {
                pixSize = 1;
            }

            int destinationWidth = sourceWidth * pixSize;
            int destinationHeight = sourceHeight * pixSize;

            bool sourceSizeChanged = (sourceWidth != _cachedSourceWidth) || (sourceHeight != _cachedSourceHeight);
            bool pixSizeChanged = (pixSize != _cachedPixSize);
            bool destinationSizeChanged = (destinationWidth != _cachedDestinationWidth) ||
                                          (destinationHeight != _cachedDestinationHeight);

            if (sourceSizeChanged || !_sourceNativeArray.IsCreated)
            {
                if (_sourceNativeArray.IsCreated)
                {
                    _sourceNativeArray.Dispose();
                }

                if (sourceWidth > 0 && sourceHeight > 0)
                {
                    _sourceNativeArray = new NativeArray<Color32>(sourceWidth * sourceHeight, Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                }
            }

            if (destinationSizeChanged || !_destinationNativeArray.IsCreated)
            {
                if (_destinationNativeArray.IsCreated)
                {
                    _destinationNativeArray.Dispose();
                }

                if (destinationWidth > 0 && destinationHeight > 0)
                {
                    _destinationNativeArray = new NativeArray<Color32>(destinationWidth * destinationHeight,
                        Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                }
            }

            if (_newTexture == null || _newTexture.width != destinationWidth ||
                _newTexture.height != destinationHeight || _newTexture.format != TextureFormat.RGBA32)
            {
                if (destinationWidth > 0 && destinationHeight > 0)
                {
                    if (_newTexture != null)
                    {
                        Destroy(_newTexture);
                    }

                    _newTexture = new Texture2D(destinationWidth, destinationHeight, TextureFormat.RGBA32,
                        mipChain: false, linear: false)
                    {
                        filterMode = FilterMode.Point
                    };
                }
            }

            _cachedSourceWidth = sourceWidth;
            _cachedSourceHeight = sourceHeight;
            _cachedPixSize = pixSize;
            _cachedDestinationWidth = destinationWidth;
            _cachedDestinationHeight = destinationHeight;
        }

        private void FillSourceFromPixCollection()
        {
            int totalSourcePixels = _cachedSourceWidth * _cachedSourceHeight;
            var pixelCollection = _pixelizer.PixCollection;

            int copyCount = Mathf.Min(totalSourcePixels, pixelCollection.Length);

            int linearIndex = 0;
            for (int i = 0; i < _cachedSourceWidth; i++)
            {
                for (int j = 0; j < _cachedSourceHeight; j++, linearIndex++)
                {
                    if (linearIndex < copyCount)
                    {
                        Color color = pixelCollection[linearIndex].Color;
                        _sourceNativeArray[linearIndex] = new Color32(
                            (byte)(Mathf.Clamp01(color.r) * 255f),
                            (byte)(Mathf.Clamp01(color.g) * 255f),
                            (byte)(Mathf.Clamp01(color.b) * 255f),
                            255);
                    }
                    else
                    {
                        _sourceNativeArray[linearIndex] = new Color32(0, 0, 0, 255);
                    }
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, CompileSynchronously = true)]
        private struct BuildTextureJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> Source;

            [NativeDisableParallelForRestriction] public NativeArray<Color32> Destination;

            public int SourceWidth;
            public int SourceHeight;
            public int DestinationWidth;
            public int PixSize;

            public void Execute(int sourceColumnIndex)
            {
                int blockStartX = sourceColumnIndex * PixSize;

                for (int sourceRowIndex = 0; sourceRowIndex < SourceHeight; sourceRowIndex++)
                {
                    int sourceIndex = (sourceRowIndex * SourceWidth) + sourceColumnIndex;
                    Color32 color = Source[sourceIndex];
                    int blockStartY = sourceRowIndex * PixSize;

                    for (int offsetY = 0; offsetY < PixSize; offsetY++)
                    {
                        int destinationRowStart = ((blockStartY + offsetY) * DestinationWidth) + blockStartX;

                        for (int offsetX = 0; offsetX < PixSize; offsetX++)
                        {
                            Destination[destinationRowStart + offsetX] = color;
                        }
                    }
                }
            }
        }

        private void DisposeBuffers()
        {
            if (_sourceNativeArray.IsCreated)
            {
                _sourceNativeArray.Dispose();
            }

            if (_destinationNativeArray.IsCreated)
            {
                _destinationNativeArray.Dispose();
            }
        }

        #endregion
    }
}