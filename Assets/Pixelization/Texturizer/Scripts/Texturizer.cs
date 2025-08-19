using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        public int PixSize => _pixSize;

        [SerializeField] private string _textureSavePath;

        private NativeArray<Color32> _sourceNativeArray;

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

            if (!_sourceNativeArray.IsCreated || _newTexture == null || sourceWidth == 0 || sourceHeight == 0)
            {
                return;
            }

            FillSourceFromPixCollection();

            var rawTextureData = _newTexture.GetRawTextureData<Color32>();

            var buildTextureJob = new BuildTextureUnsafeJob
            {
                Source = _sourceNativeArray,
                Destination = rawTextureData,
                SourceWidth = sourceWidth,
                SourceHeight = sourceHeight,
                DestinationWidth = _cachedDestinationWidth,
                PixSize = pixSize
            };

            JobHandle jobHandle = buildTextureJob.Schedule(sourceHeight, 1);
            jobHandle.Complete();

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
            
            if (_newTexture == null || _newTexture.width != destinationWidth ||
                _newTexture.height != destinationHeight || _newTexture.format != TextureFormat.RGBA32)
            {
                if (destinationWidth > 0 && destinationHeight > 0)
                {
                    if (_newTexture == null)
                    {
                        _newTexture = new Texture2D(destinationWidth, destinationHeight, TextureFormat.RGBA32,
                            false, false);
                        _newTexture.filterMode = FilterMode.Point;
                    }
                    else if (_newTexture.width != destinationWidth ||
                             _newTexture.height != destinationHeight ||
                             _newTexture.format != TextureFormat.RGBA32)
                    {
                        _newTexture.Reinitialize(destinationWidth, destinationHeight, TextureFormat.RGBA32, false);
                        _newTexture.filterMode = FilterMode.Point;
                    }
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

            for (int i = 0; i < copyCount; i++)
            {
                Color32 color = pixelCollection[i].Color;
                color.a = 255;

                _sourceNativeArray[i] = color;
            }

            for (int i = copyCount; i < totalSourcePixels; i++)
            {
                _sourceNativeArray[i] = new Color32(0, 0, 0, 255);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private unsafe struct BuildTextureUnsafeJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> Source;
            [NativeDisableParallelForRestriction] public NativeArray<Color32> Destination;

            public int SourceWidth;
            public int SourceHeight;
            public int DestinationWidth;
            public int PixSize;

            public void Execute(int sourceRowIndex)
            {
                byte* destinationBase = (byte*)Destination.GetUnsafePtr();
                Color32* sourceBase = (Color32*)Source.GetUnsafeReadOnlyPtr();

                int bytesPerPixel = UnsafeUtility.SizeOf<Color32>();
                long destinationRowStrideBytes = (long)DestinationWidth * bytesPerPixel;
                
                int destinationRowTop = (sourceRowIndex * PixSize) * DestinationWidth;

                for (int sourceColumnIndex = 0; sourceColumnIndex < SourceWidth; sourceColumnIndex++)
                {
                    int sourceIndex = sourceRowIndex * SourceWidth + sourceColumnIndex;
                    Color32 color = sourceBase[sourceIndex];

                    int destinationColumnStart = sourceColumnIndex * PixSize;
                    int runStart = destinationRowTop + destinationColumnStart;
                    
                    byte* firstRowPtr = destinationBase + (long)runStart * bytesPerPixel;
                    
                    UnsafeUtility.MemCpyReplicate(
                        destination: firstRowPtr,
                        source: &color,
                        size: bytesPerPixel,
                        count: PixSize
                    );
                    
                    long rowBytes = (long)PixSize * bytesPerPixel;
                    for (int y = 1; y < PixSize; y++)
                    {
                        byte* destinationRowPtr = firstRowPtr + y * destinationRowStrideBytes;
                        UnsafeUtility.MemCpy(destinationRowPtr, firstRowPtr, rowBytes);
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
        }

        #endregion

        public void SaveTexture()
        {
#if UNITY_EDITOR
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
#else
            Debug.LogWarning("SaveTexture is editor-only.");
#endif
        }

        private bool CreateFolderAtSavePath(string savePath)
        {
#if UNITY_EDITOR
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
#else
            Debug.LogWarning("CreateFolderAtSavePath is editor-only.");
            return false;
#endif
        }
    }
}
