using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace VoxelImporter
{
    public abstract class VoxelBaseCore
    {
        public VoxelBaseCore(VoxelBase target)
        {
            voxelBase = target;
        }

        public VoxelBase voxelBase { get; protected set; }

        public virtual void Initialize()
        {
            voxelBase.EditorInitialize();
            voxelBase.SaveEditTmpData();

            if (!ReadyVoxelData())
                voxelBase.edit_configureMode = VoxelBase.Edit_configureMode.None;

            AutoSetSelectedWireframeHidden();

            RefreshCheckerClear();
            RefreshCheckerSave();
        }

        #region AtlasRects
        protected class TextureBoundArea
        {
            public TextureBoundArea()
            {
                textureIndex = -1;
                min = new IntVector2(int.MaxValue, int.MaxValue);
                max = new IntVector2(int.MinValue, int.MinValue);
            }
            public void Set(IntVector2 pos)
            {
                min = IntVector2.Min(min, pos);
                max = IntVector2.Max(max, pos);
            }
            public IntVector2 Size { get { return max - min + IntVector2.one; } }
            public int textureIndex;
            public IntVector2 min;
            public IntVector2 max;
        }
        protected class AtlasRectTable
        {
            public List<TextureBoundArea> forward = new List<TextureBoundArea>();
            public List<TextureBoundArea> up = new List<TextureBoundArea>();
            public List<TextureBoundArea> right = new List<TextureBoundArea>();
            public List<TextureBoundArea> left = new List<TextureBoundArea>();
            public List<TextureBoundArea> down = new List<TextureBoundArea>();
            public List<TextureBoundArea> back = new List<TextureBoundArea>();
        }
        #endregion

        #region Chunk
        protected struct ChunkArea
        {
            public IntVector3 min;
            public IntVector3 max;

            public Vector3 minf { get { return new Vector3(min.x, min.y, min.z); } }
            public Vector3 maxf { get { return new Vector3(max.x, max.y, max.z); } }
            public Vector3 centerf { get { return Vector3.Lerp(minf, maxf, 0.5f); } }
        }
        protected virtual void CreateChunkData() { }
        #endregion

        #region CreateVoxel
        public bool Create(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogErrorFormat("<color=green>[VoxelCharacteImporter]</color> Voxel file not found. <color=red>{0}</color>", path);
                return false;
            }

            if (PrefabUtility.GetPrefabType(voxelBase) == PrefabType.PrefabInstance)
            {
                PrefabUtility.DisconnectPrefabInstance(voxelBase);
            }

            var undoGroupID = Undo.GetCurrentGroup();

            voxelBase.voxelFilePath = path;
            voxelBase.voxelFileGUID = "";
            if (voxelBase.voxelFilePath.IndexOf(Application.dataPath) >= 0)
            {
                var assetPath = voxelBase.voxelFilePath.Replace(Application.dataPath, "Assets");
                var assets = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(voxelBase.voxelFilePath));
                for (int i = 0; i < assets.Length; i++)
                {
                    var tmpPath = AssetDatabase.GUIDToAssetPath(assets[i]);
                    if (tmpPath == assetPath)
                    {
                        voxelBase.voxelFileGUID = assets[i];
                        break;
                    }
                }
            }

            bool result = LoadVoxelData();
            if (result)
            {
                result = CreateMesh();
            }

            Undo.CollapseUndoOperations(undoGroupID);

            return result;
        }
        public bool ReCreate()
        {
            #region GUID
            if (!string.IsNullOrEmpty(voxelBase.voxelFileGUID))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(voxelBase.voxelFileGUID);
                if(!string.IsNullOrEmpty(assetPath))
                {
                    var fullPath = Application.dataPath + assetPath.Remove(0, "Assets".Length);
                    voxelBase.voxelFilePath = fullPath;
                }
            }
            #endregion
            return Create(voxelBase.voxelFilePath);
        }
        public bool IsVoxelFileExists()
        {
            var fileExists = false;
            if (!string.IsNullOrEmpty(voxelBase.voxelFileGUID))
            {
                var path = AssetDatabase.GUIDToAssetPath(voxelBase.voxelFileGUID);
                if (!string.IsNullOrEmpty(path))
                    fileExists = File.Exists(path);
            }
            else if(!string.IsNullOrEmpty(voxelBase.voxelFilePath))
            {
                fileExists = File.Exists(voxelBase.voxelFilePath);
            }
            return fileExists;
        }
        public bool ReadyVoxelData()
        {
            if (voxelBase.voxelData == null)
            {
                return LoadVoxelData();
            }
            return true;
        }
        protected bool LoadVoxelData()
        {
            bool result = false;
            #region GUID
            if (!string.IsNullOrEmpty(voxelBase.voxelFileGUID))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(voxelBase.voxelFileGUID);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var fullPath = Application.dataPath + assetPath.Remove(0, "Assets".Length);
                    voxelBase.voxelFilePath = fullPath;
                }
            }
            #endregion
            if (!string.IsNullOrEmpty(voxelBase.voxelFilePath) &&
                File.Exists(voxelBase.voxelFilePath))
            {
                using (BinaryReader br = new BinaryReader(File.Open(voxelBase.voxelFilePath, FileMode.Open)))
                {
                    var ext = Path.GetExtension(voxelBase.voxelFilePath);
                    if (ext == ".vox")
                        result = LoadVoxelDataFromVOX(br);
                    else if (ext == ".qb")
                        result = LoadVoxelDataFromQB(br);
                    else if (ext == ".png")
                        result = LoadVoxelDataFromPNG(br);
                    else
                        result = false;
                }
            }
            return result;
        }
        protected virtual bool LoadVoxelDataFromVOX(BinaryReader br)
        {
            Func<string, bool> CheckChunk = (name) =>
            {
                if (br.BaseStream.Length - br.BaseStream.Position < 4)
                    return false;
                string data = new string(br.ReadChars(4));
                return name == data;
            };
            if (!CheckChunk("VOX "))
            {
                Debug.LogError("[VoxelImporter] vox file error.");
                return false;
            }
            br.BaseStream.Seek(4, SeekOrigin.Current);  //version
            if (!CheckChunk("MAIN"))
            {
                Debug.LogError("[VoxelImporter] vox chunk error.");
                return false;
            }
            br.BaseStream.Seek(8, SeekOrigin.Current);
            if (!CheckChunk("SIZE"))
            {
                Debug.LogError("[VoxelImporter] vox chunk error.");
                return false;
            }
            br.BaseStream.Seek(8, SeekOrigin.Current);
            IntVector3 voxelSize;
            {
                var x = (int)br.ReadUInt32();
                var z = (int)br.ReadUInt32();   //swapYZ
                var y = (int)br.ReadUInt32();
                voxelSize = new IntVector3(x, y, z);
            }
            if (!CheckChunk("XYZI"))
            {
                Debug.LogError("[VoxelImporter] vox chunk error.");
                return false;
            }
            br.BaseStream.Seek(8, SeekOrigin.Current);
            var numVoxels = br.ReadUInt32();
            var voxels = new VoxelData.Voxel[numVoxels];
            for (int i = 0; i < numVoxels; i++)
            {
                var x = voxelSize.x - 1 - br.ReadByte();  //invert
                var z = voxelSize.z - 1 - br.ReadByte();   //swapYZ  //invert
                var y = br.ReadByte();
                var index = br.ReadByte();
                voxels[i] = new VoxelData.Voxel(x, y, z, index - 1);
            }
            Color[] palettes = null;
            if (CheckChunk("RGBA"))
            {
                #region PaletteChunk
                br.BaseStream.Seek(8, SeekOrigin.Current);
                palettes = new Color[256];
                for (int i = 0; i < 256; i++)
                {
                    var r = br.ReadByte();
                    var g = br.ReadByte();
                    var b = br.ReadByte();
                    var a = br.ReadByte();
                    a = 0xff;
                    palettes[i] = new Color32(r, g, b, a);
                }
                #endregion
            }
            else
            {
                #region default palette
                Color[] MagicaVoxelDefaultPalette =
                {
                    new Color(0.988f, 0.988f, 0.988f, 1.000f),
                    new Color(0.988f, 0.988f, 0.800f, 1.000f),
                    new Color(0.988f, 0.988f, 0.596f, 1.000f),
                    new Color(0.988f, 0.988f, 0.392f, 1.000f),
                    new Color(0.988f, 0.988f, 0.188f, 1.000f),
                    new Color(0.988f, 0.988f, 0.000f, 1.000f),
                    new Color(0.988f, 0.800f, 0.988f, 1.000f),
                    new Color(0.988f, 0.800f, 0.800f, 1.000f),
                    new Color(0.988f, 0.800f, 0.596f, 1.000f),
                    new Color(0.988f, 0.800f, 0.392f, 1.000f),
                    new Color(0.988f, 0.800f, 0.188f, 1.000f),
                    new Color(0.988f, 0.800f, 0.000f, 1.000f),
                    new Color(0.988f, 0.596f, 0.988f, 1.000f),
                    new Color(0.988f, 0.596f, 0.800f, 1.000f),
                    new Color(0.988f, 0.596f, 0.596f, 1.000f),
                    new Color(0.988f, 0.596f, 0.392f, 1.000f),
                    new Color(0.988f, 0.596f, 0.188f, 1.000f),
                    new Color(0.988f, 0.596f, 0.000f, 1.000f),
                    new Color(0.988f, 0.392f, 0.988f, 1.000f),
                    new Color(0.988f, 0.392f, 0.800f, 1.000f),
                    new Color(0.988f, 0.392f, 0.596f, 1.000f),
                    new Color(0.988f, 0.392f, 0.392f, 1.000f),
                    new Color(0.988f, 0.392f, 0.188f, 1.000f),
                    new Color(0.988f, 0.392f, 0.000f, 1.000f),
                    new Color(0.988f, 0.188f, 0.988f, 1.000f),
                    new Color(0.988f, 0.188f, 0.800f, 1.000f),
                    new Color(0.988f, 0.188f, 0.596f, 1.000f),
                    new Color(0.988f, 0.188f, 0.392f, 1.000f),
                    new Color(0.988f, 0.188f, 0.188f, 1.000f),
                    new Color(0.988f, 0.188f, 0.000f, 1.000f),
                    new Color(0.988f, 0.000f, 0.988f, 1.000f),
                    new Color(0.988f, 0.000f, 0.800f, 1.000f),
                    new Color(0.988f, 0.000f, 0.596f, 1.000f),
                    new Color(0.988f, 0.000f, 0.392f, 1.000f),
                    new Color(0.988f, 0.000f, 0.188f, 1.000f),
                    new Color(0.988f, 0.000f, 0.000f, 1.000f),
                    new Color(0.800f, 0.988f, 0.988f, 1.000f),
                    new Color(0.800f, 0.988f, 0.800f, 1.000f),
                    new Color(0.800f, 0.988f, 0.596f, 1.000f),
                    new Color(0.800f, 0.988f, 0.392f, 1.000f),
                    new Color(0.800f, 0.988f, 0.188f, 1.000f),
                    new Color(0.800f, 0.988f, 0.000f, 1.000f),
                    new Color(0.800f, 0.800f, 0.988f, 1.000f),
                    new Color(0.800f, 0.800f, 0.800f, 1.000f),
                    new Color(0.800f, 0.800f, 0.596f, 1.000f),
                    new Color(0.800f, 0.800f, 0.392f, 1.000f),
                    new Color(0.800f, 0.800f, 0.188f, 1.000f),
                    new Color(0.800f, 0.800f, 0.000f, 1.000f),
                    new Color(0.800f, 0.596f, 0.988f, 1.000f),
                    new Color(0.800f, 0.596f, 0.800f, 1.000f),
                    new Color(0.800f, 0.596f, 0.596f, 1.000f),
                    new Color(0.800f, 0.596f, 0.392f, 1.000f),
                    new Color(0.800f, 0.596f, 0.188f, 1.000f),
                    new Color(0.800f, 0.596f, 0.000f, 1.000f),
                    new Color(0.800f, 0.392f, 0.988f, 1.000f),
                    new Color(0.800f, 0.392f, 0.800f, 1.000f),
                    new Color(0.800f, 0.392f, 0.596f, 1.000f),
                    new Color(0.800f, 0.392f, 0.392f, 1.000f),
                    new Color(0.800f, 0.392f, 0.188f, 1.000f),
                    new Color(0.800f, 0.392f, 0.000f, 1.000f),
                    new Color(0.800f, 0.188f, 0.988f, 1.000f),
                    new Color(0.800f, 0.188f, 0.800f, 1.000f),
                    new Color(0.800f, 0.188f, 0.596f, 1.000f),
                    new Color(0.800f, 0.188f, 0.392f, 1.000f),
                    new Color(0.800f, 0.188f, 0.188f, 1.000f),
                    new Color(0.800f, 0.188f, 0.000f, 1.000f),
                    new Color(0.800f, 0.000f, 0.988f, 1.000f),
                    new Color(0.800f, 0.000f, 0.800f, 1.000f),
                    new Color(0.800f, 0.000f, 0.596f, 1.000f),
                    new Color(0.800f, 0.000f, 0.392f, 1.000f),
                    new Color(0.800f, 0.000f, 0.188f, 1.000f),
                    new Color(0.800f, 0.000f, 0.000f, 1.000f),
                    new Color(0.596f, 0.988f, 0.988f, 1.000f),
                    new Color(0.596f, 0.988f, 0.800f, 1.000f),
                    new Color(0.596f, 0.988f, 0.596f, 1.000f),
                    new Color(0.596f, 0.988f, 0.392f, 1.000f),
                    new Color(0.596f, 0.988f, 0.188f, 1.000f),
                    new Color(0.596f, 0.988f, 0.000f, 1.000f),
                    new Color(0.596f, 0.800f, 0.988f, 1.000f),
                    new Color(0.596f, 0.800f, 0.800f, 1.000f),
                    new Color(0.596f, 0.800f, 0.596f, 1.000f),
                    new Color(0.596f, 0.800f, 0.392f, 1.000f),
                    new Color(0.596f, 0.800f, 0.188f, 1.000f),
                    new Color(0.596f, 0.800f, 0.000f, 1.000f),
                    new Color(0.596f, 0.596f, 0.988f, 1.000f),
                    new Color(0.596f, 0.596f, 0.800f, 1.000f),
                    new Color(0.596f, 0.596f, 0.596f, 1.000f),
                    new Color(0.596f, 0.596f, 0.392f, 1.000f),
                    new Color(0.596f, 0.596f, 0.188f, 1.000f),
                    new Color(0.596f, 0.596f, 0.000f, 1.000f),
                    new Color(0.596f, 0.392f, 0.988f, 1.000f),
                    new Color(0.596f, 0.392f, 0.800f, 1.000f),
                    new Color(0.596f, 0.392f, 0.596f, 1.000f),
                    new Color(0.596f, 0.392f, 0.392f, 1.000f),
                    new Color(0.596f, 0.392f, 0.188f, 1.000f),
                    new Color(0.596f, 0.392f, 0.000f, 1.000f),
                    new Color(0.596f, 0.188f, 0.988f, 1.000f),
                    new Color(0.596f, 0.188f, 0.800f, 1.000f),
                    new Color(0.596f, 0.188f, 0.596f, 1.000f),
                    new Color(0.596f, 0.188f, 0.392f, 1.000f),
                    new Color(0.596f, 0.188f, 0.188f, 1.000f),
                    new Color(0.596f, 0.188f, 0.000f, 1.000f),
                    new Color(0.596f, 0.000f, 0.988f, 1.000f),
                    new Color(0.596f, 0.000f, 0.800f, 1.000f),
                    new Color(0.596f, 0.000f, 0.596f, 1.000f),
                    new Color(0.596f, 0.000f, 0.392f, 1.000f),
                    new Color(0.596f, 0.000f, 0.188f, 1.000f),
                    new Color(0.596f, 0.000f, 0.000f, 1.000f),
                    new Color(0.392f, 0.988f, 0.988f, 1.000f),
                    new Color(0.392f, 0.988f, 0.800f, 1.000f),
                    new Color(0.392f, 0.988f, 0.596f, 1.000f),
                    new Color(0.392f, 0.988f, 0.392f, 1.000f),
                    new Color(0.392f, 0.988f, 0.188f, 1.000f),
                    new Color(0.392f, 0.988f, 0.000f, 1.000f),
                    new Color(0.392f, 0.800f, 0.988f, 1.000f),
                    new Color(0.392f, 0.800f, 0.800f, 1.000f),
                    new Color(0.392f, 0.800f, 0.596f, 1.000f),
                    new Color(0.392f, 0.800f, 0.392f, 1.000f),
                    new Color(0.392f, 0.800f, 0.188f, 1.000f),
                    new Color(0.392f, 0.800f, 0.000f, 1.000f),
                    new Color(0.392f, 0.596f, 0.988f, 1.000f),
                    new Color(0.392f, 0.596f, 0.800f, 1.000f),
                    new Color(0.392f, 0.596f, 0.596f, 1.000f),
                    new Color(0.392f, 0.596f, 0.392f, 1.000f),
                    new Color(0.392f, 0.596f, 0.188f, 1.000f),
                    new Color(0.392f, 0.596f, 0.000f, 1.000f),
                    new Color(0.392f, 0.392f, 0.988f, 1.000f),
                    new Color(0.392f, 0.392f, 0.800f, 1.000f),
                    new Color(0.392f, 0.392f, 0.596f, 1.000f),
                    new Color(0.392f, 0.392f, 0.392f, 1.000f),
                    new Color(0.392f, 0.392f, 0.188f, 1.000f),
                    new Color(0.392f, 0.392f, 0.000f, 1.000f),
                    new Color(0.392f, 0.188f, 0.988f, 1.000f),
                    new Color(0.392f, 0.188f, 0.800f, 1.000f),
                    new Color(0.392f, 0.188f, 0.596f, 1.000f),
                    new Color(0.392f, 0.188f, 0.392f, 1.000f),
                    new Color(0.392f, 0.188f, 0.188f, 1.000f),
                    new Color(0.392f, 0.188f, 0.000f, 1.000f),
                    new Color(0.392f, 0.000f, 0.988f, 1.000f),
                    new Color(0.392f, 0.000f, 0.800f, 1.000f),
                    new Color(0.392f, 0.000f, 0.596f, 1.000f),
                    new Color(0.392f, 0.000f, 0.392f, 1.000f),
                    new Color(0.392f, 0.000f, 0.188f, 1.000f),
                    new Color(0.392f, 0.000f, 0.000f, 1.000f),
                    new Color(0.188f, 0.988f, 0.988f, 1.000f),
                    new Color(0.188f, 0.988f, 0.800f, 1.000f),
                    new Color(0.188f, 0.988f, 0.596f, 1.000f),
                    new Color(0.188f, 0.988f, 0.392f, 1.000f),
                    new Color(0.188f, 0.988f, 0.188f, 1.000f),
                    new Color(0.188f, 0.988f, 0.000f, 1.000f),
                    new Color(0.188f, 0.800f, 0.988f, 1.000f),
                    new Color(0.188f, 0.800f, 0.800f, 1.000f),
                    new Color(0.188f, 0.800f, 0.596f, 1.000f),
                    new Color(0.188f, 0.800f, 0.392f, 1.000f),
                    new Color(0.188f, 0.800f, 0.188f, 1.000f),
                    new Color(0.188f, 0.800f, 0.000f, 1.000f),
                    new Color(0.188f, 0.596f, 0.988f, 1.000f),
                    new Color(0.188f, 0.596f, 0.800f, 1.000f),
                    new Color(0.188f, 0.596f, 0.596f, 1.000f),
                    new Color(0.188f, 0.596f, 0.392f, 1.000f),
                    new Color(0.188f, 0.596f, 0.188f, 1.000f),
                    new Color(0.188f, 0.596f, 0.000f, 1.000f),
                    new Color(0.188f, 0.392f, 0.988f, 1.000f),
                    new Color(0.188f, 0.392f, 0.800f, 1.000f),
                    new Color(0.188f, 0.392f, 0.596f, 1.000f),
                    new Color(0.188f, 0.392f, 0.392f, 1.000f),
                    new Color(0.188f, 0.392f, 0.188f, 1.000f),
                    new Color(0.188f, 0.392f, 0.000f, 1.000f),
                    new Color(0.188f, 0.188f, 0.988f, 1.000f),
                    new Color(0.188f, 0.188f, 0.800f, 1.000f),
                    new Color(0.188f, 0.188f, 0.596f, 1.000f),
                    new Color(0.188f, 0.188f, 0.392f, 1.000f),
                    new Color(0.188f, 0.188f, 0.188f, 1.000f),
                    new Color(0.188f, 0.188f, 0.000f, 1.000f),
                    new Color(0.188f, 0.000f, 0.988f, 1.000f),
                    new Color(0.188f, 0.000f, 0.800f, 1.000f),
                    new Color(0.188f, 0.000f, 0.596f, 1.000f),
                    new Color(0.188f, 0.000f, 0.392f, 1.000f),
                    new Color(0.188f, 0.000f, 0.188f, 1.000f),
                    new Color(0.188f, 0.000f, 0.000f, 1.000f),
                    new Color(0.000f, 0.988f, 0.988f, 1.000f),
                    new Color(0.000f, 0.988f, 0.800f, 1.000f),
                    new Color(0.000f, 0.988f, 0.596f, 1.000f),
                    new Color(0.000f, 0.988f, 0.392f, 1.000f),
                    new Color(0.000f, 0.988f, 0.188f, 1.000f),
                    new Color(0.000f, 0.988f, 0.000f, 1.000f),
                    new Color(0.000f, 0.800f, 0.988f, 1.000f),
                    new Color(0.000f, 0.800f, 0.800f, 1.000f),
                    new Color(0.000f, 0.800f, 0.596f, 1.000f),
                    new Color(0.000f, 0.800f, 0.392f, 1.000f),
                    new Color(0.000f, 0.800f, 0.188f, 1.000f),
                    new Color(0.000f, 0.800f, 0.000f, 1.000f),
                    new Color(0.000f, 0.596f, 0.988f, 1.000f),
                    new Color(0.000f, 0.596f, 0.800f, 1.000f),
                    new Color(0.000f, 0.596f, 0.596f, 1.000f),
                    new Color(0.000f, 0.596f, 0.392f, 1.000f),
                    new Color(0.000f, 0.596f, 0.188f, 1.000f),
                    new Color(0.000f, 0.596f, 0.000f, 1.000f),
                    new Color(0.000f, 0.392f, 0.988f, 1.000f),
                    new Color(0.000f, 0.392f, 0.800f, 1.000f),
                    new Color(0.000f, 0.392f, 0.596f, 1.000f),
                    new Color(0.000f, 0.392f, 0.392f, 1.000f),
                    new Color(0.000f, 0.392f, 0.188f, 1.000f),
                    new Color(0.000f, 0.392f, 0.000f, 1.000f),
                    new Color(0.000f, 0.188f, 0.988f, 1.000f),
                    new Color(0.000f, 0.188f, 0.800f, 1.000f),
                    new Color(0.000f, 0.188f, 0.596f, 1.000f),
                    new Color(0.000f, 0.188f, 0.392f, 1.000f),
                    new Color(0.000f, 0.188f, 0.188f, 1.000f),
                    new Color(0.000f, 0.188f, 0.000f, 1.000f),
                    new Color(0.000f, 0.000f, 0.988f, 1.000f),
                    new Color(0.000f, 0.000f, 0.800f, 1.000f),
                    new Color(0.000f, 0.000f, 0.596f, 1.000f),
                    new Color(0.000f, 0.000f, 0.392f, 1.000f),
                    new Color(0.000f, 0.000f, 0.188f, 1.000f),
                    new Color(0.925f, 0.000f, 0.000f, 1.000f),
                    new Color(0.863f, 0.000f, 0.000f, 1.000f),
                    new Color(0.722f, 0.000f, 0.000f, 1.000f),
                    new Color(0.659f, 0.000f, 0.000f, 1.000f),
                    new Color(0.533f, 0.000f, 0.000f, 1.000f),
                    new Color(0.455f, 0.000f, 0.000f, 1.000f),
                    new Color(0.329f, 0.000f, 0.000f, 1.000f),
                    new Color(0.267f, 0.000f, 0.000f, 1.000f),
                    new Color(0.125f, 0.000f, 0.000f, 1.000f),
                    new Color(0.063f, 0.000f, 0.000f, 1.000f),
                    new Color(0.000f, 0.925f, 0.000f, 1.000f),
                    new Color(0.000f, 0.863f, 0.000f, 1.000f),
                    new Color(0.000f, 0.722f, 0.000f, 1.000f),
                    new Color(0.000f, 0.659f, 0.000f, 1.000f),
                    new Color(0.000f, 0.533f, 0.000f, 1.000f),
                    new Color(0.000f, 0.455f, 0.000f, 1.000f),
                    new Color(0.000f, 0.329f, 0.000f, 1.000f),
                    new Color(0.000f, 0.267f, 0.000f, 1.000f),
                    new Color(0.000f, 0.125f, 0.000f, 1.000f),
                    new Color(0.000f, 0.063f, 0.000f, 1.000f),
                    new Color(0.000f, 0.000f, 0.925f, 1.000f),
                    new Color(0.000f, 0.000f, 0.863f, 1.000f),
                    new Color(0.000f, 0.000f, 0.722f, 1.000f),
                    new Color(0.000f, 0.000f, 0.659f, 1.000f),
                    new Color(0.000f, 0.000f, 0.533f, 1.000f),
                    new Color(0.000f, 0.000f, 0.455f, 1.000f),
                    new Color(0.000f, 0.000f, 0.329f, 1.000f),
                    new Color(0.000f, 0.000f, 0.267f, 1.000f),
                    new Color(0.000f, 0.000f, 0.125f, 1.000f),
                    new Color(0.000f, 0.000f, 0.063f, 1.000f),
                    new Color(0.925f, 0.925f, 0.925f, 1.000f),
                    new Color(0.863f, 0.863f, 0.863f, 1.000f),
                    new Color(0.722f, 0.722f, 0.722f, 1.000f),
                    new Color(0.659f, 0.659f, 0.659f, 1.000f),
                    new Color(0.533f, 0.533f, 0.533f, 1.000f),
                    new Color(0.455f, 0.455f, 0.455f, 1.000f),
                    new Color(0.329f, 0.329f, 0.329f, 1.000f),
                    new Color(0.267f, 0.267f, 0.267f, 1.000f),
                    new Color(0.125f, 0.125f, 0.125f, 1.000f),
                    new Color(0.063f, 0.063f, 0.063f, 1.000f),
                    new Color(0.000f, 0.000f, 0.000f, 1.000f),
                };
                palettes = new Color[256];
                for (int i = 0; i < 256; i++)
                {
                    palettes[i] = MagicaVoxelDefaultPalette[i];
                }
                #endregion
            }

            #region compress palette
            {
                int[] paletteCount = new int[palettes.Length];
                for (int i = 0; i < voxels.Length; i++)
                {
                    paletteCount[voxels[i].palette]++;
                }
                int[] removeCount = new int[palettes.Length];
                for (int i = 0; i < paletteCount.Length; i++)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (paletteCount[j] == 0)
                            removeCount[i]++;
                    }
                }
                for (int i = 0; i < voxels.Length; i++)
                {
                    voxels[i].palette -= removeCount[voxels[i].palette];
                }
                List<Color> paletteList = new List<Color>(palettes.Length);
                for (int i = 0; i < paletteCount.Length; i++)
                {
                    if (paletteCount[i] > 0)
                        paletteList.Add(palettes[i]);
                }
                palettes = paletteList.ToArray();
            }
            #endregion

            voxelBase.localOffset = new Vector3(-((float)voxelSize.x / 2), 0f, -((float)voxelSize.z / 2));

            voxelBase.fileType = VoxelBase.FileType.vox;

            voxelBase.voxelData = new VoxelData();
            voxelBase.voxelData.voxels = voxels;
            voxelBase.voxelData.palettes = palettes;
            voxelBase.voxelData.voxelSize = voxelSize;

            ApplyImportFlags();
            voxelBase.voxelData.CreateVoxelTable();
            UpdateVisibleFlags();
            CreateChunkData();

            return true;
        }
        protected virtual bool LoadVoxelDataFromQB(BinaryReader br)
        {
            br.BaseStream.Seek(4, SeekOrigin.Current);  //version
            var colorFormat = br.ReadUInt32();
            var zAxisOrientation = br.ReadUInt32();
            var compressed = br.ReadUInt32();
            br.BaseStream.Seek(4, SeekOrigin.Current);  //visibilityMaskEncoded
            var numMatrices = br.ReadUInt32();

            List<VoxelData.Voxel> voxelList = new List<VoxelData.Voxel>();
            Dictionary<Color, int> paletteList = new Dictionary<Color, int>();
            Dictionary<int, Dictionary<int, HashSet<int>>> doneTable = new Dictionary<int, Dictionary<int, HashSet<int>>>();

            Action<int, int, int, UInt32> AddVoxel = (x, y, z, data) =>
            {
                Color color;
                if (colorFormat == 0)
                {
                    var a = (byte)((data & 0xff000000) >> 24);
                    var b = (byte)((data & 0x00ff0000) >> 16);
                    var g = (byte)((data & 0x0000ff00) >> 8);
                    var r = (byte)((data & 0x000000ff));
                    color = new Color32(r, g, b, a);
                }
                else
                {
                    var a = (byte)((data & 0xff000000) >> 24);
                    var r = (byte)((data & 0x00ff0000) >> 16);
                    var g = (byte)((data & 0x0000ff00) >> 8);
                    var b = (byte)((data & 0x000000ff));
                    color = new Color32(r, g, b, a);
                }
                if (color.a > 0f)
                {
                    if (!doneTable.ContainsKey(x) ||
                        !doneTable[x].ContainsKey(y) ||
                        !doneTable[x][y].Contains(z))
                    {
                        if (!doneTable.ContainsKey(x))
                            doneTable.Add(x, new Dictionary<int, HashSet<int>>());
                        if (!doneTable[x].ContainsKey(y))
                            doneTable[x].Add(y, new HashSet<int>());
                        doneTable[x][y].Add(z);

                        color.a = 1f;
                        int palette;
                        if (paletteList.ContainsKey(color))
                        {
                            palette = paletteList[color];
                        }
                        else
                        {
                            palette = paletteList.Count;
                            paletteList.Add(color, palette);
                        }

                        voxelList.Add(new VoxelData.Voxel(x, y, z, palette));
                    }
                }
            };

            for (int i = 0; i < numMatrices; i++)
            {
                var nameLength = br.ReadByte();
                br.BaseStream.Seek(nameLength, SeekOrigin.Current); //name
                var sizeX = br.ReadUInt32();
                var sizeY = br.ReadUInt32();
                var sizeZ = br.ReadUInt32();
                var posX = br.ReadInt32();
                var posY = br.ReadInt32();
                var posZ = br.ReadInt32();

                if (compressed == 0)
                {
                    for (int zi = 0; zi < sizeZ; zi++)
                    {
                        for (int yi = 0; yi < sizeY; yi++)
                        {
                            for (int xi = 0; xi < sizeX; xi++)
                            {
                                var x = (posX + xi);
                                var y = (posY + yi);
                                var z = (zAxisOrientation == 0 ? -(posZ + zi + 1) : (posZ + zi));
                                AddVoxel(x, y, z, br.ReadUInt32());
                            }
                        }
                    }
                }
                else
                {
                    const UInt32 CODEFLAG = 2;
                    const UInt32 NEXTSLICEFLAG = 6;
                    int zi = 0;
                    while (zi < sizeZ)
                    {
                        zi++;
                        int index = 0;
                        while (true)
                        {
                            var data = br.ReadUInt32();
                            if (data == NEXTSLICEFLAG)
                                break;
                            else if (data == CODEFLAG)
                            {
                                var count = br.ReadUInt32();
                                data = br.ReadUInt32();

                                for (int j = 0; j < count; j++)
                                {
                                    int xi = (int)(index % sizeX) + 1;
                                    int yi = (int)(index / sizeX) + 1;
                                    index++;

                                    var x = (posX + xi) - 1;
                                    var y = (posY + yi) - 1;
                                    var z = (zAxisOrientation == 0 ? -(posZ + zi) : (posZ + zi) - 1);
                                    AddVoxel(x, y, z, data);
                                }
                            }
                            else
                            {
                                int xi = (int)(index % sizeX) + 1;
                                int yi = (int)(index / sizeX) + 1;
                                index++;

                                var x = (posX + xi) - 1;
                                var y = (posY + yi) - 1;
                                var z = (zAxisOrientation == 0 ? -(posZ + zi) : (posZ + zi) - 1);
                                AddVoxel(x, y, z, data);
                            }
                        }
                    }
                }
            }

            IntVector3 voxelSize;
            {
                IntVector3 min = new IntVector3(int.MaxValue, int.MaxValue, int.MaxValue);
                IntVector3 max = new IntVector3(int.MinValue, int.MinValue, int.MinValue);
                for (int i = 0; i < voxelList.Count; i++)
                {
                    //invert
                    {
                        var voxel = voxelList[i];
                        voxel.x = -voxel.x - 1;
                        voxel.z = -voxel.z - 1;
                        voxelList[i] = voxel;
                    }

                    min = IntVector3.Min(min, voxelList[i].position);
                    max = IntVector3.Max(max, voxelList[i].position);
                }

                voxelSize = max - min + IntVector3.one;
                for (int i = 0; i < voxelList.Count; i++)
                {
                    var v = voxelList[i];
                    v.position -= min;
                    voxelList[i] = v;
                }
                voxelBase.localOffset = new Vector3(min.x, min.y, min.z);
            }

            var voxels = voxelList.ToArray();
            var palettes = new Color[paletteList.Count];
            foreach (var pair in paletteList)
            {
                palettes[pair.Value] = pair.Key;
            }

            voxelBase.fileType = VoxelBase.FileType.qb;

            voxelBase.voxelData = new VoxelData();
            voxelBase.voxelData.voxels = voxels;
            voxelBase.voxelData.palettes = palettes;
            voxelBase.voxelData.voxelSize = voxelSize;

            ApplyImportFlags();
            voxelBase.voxelData.CreateVoxelTable();
            UpdateVisibleFlags();
            CreateChunkData();

            return true;
        }
        protected virtual bool LoadVoxelDataFromPNG(BinaryReader br)
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
            tex.hideFlags = HideFlags.DontSave;
            if (!tex.LoadImage(br.ReadBytes((int)br.BaseStream.Length)))
                return false;
            IntVector3 voxelSize = new IntVector3(tex.width, tex.height, 1);
            VoxelData.Voxel[] voxels;
            Color[] palettes;
            {
                List<VoxelData.Voxel> voxelList = new List<VoxelData.Voxel>(tex.width * tex.height);
                Dictionary<Color, int> paletteList = new Dictionary<Color, int>();
                for (int x = 0; x < tex.width; x++)
                {
                    for (int y = 0; y < tex.height; y++)
                    {
                        var color = tex.GetPixel(x, y);
                        if (color.a <= 0f) continue;
                        color.a = 1f;
                        int index;
                        if (paletteList.ContainsKey(color))
                        {
                            index = paletteList[color];
                        }
                        else
                        {
                            index = paletteList.Count;
                            paletteList.Add(color, index);
                        }
                        voxelList.Add(new VoxelData.Voxel(x, y, 0, index));
                    }
                }
                voxels = voxelList.ToArray();
                palettes = new Color[paletteList.Count];
                foreach (var pair in paletteList)
                {
                    palettes[pair.Value] = pair.Key;
                }
            }

            voxelBase.localOffset = new Vector3(-((float)voxelSize.x / 2), 0f, -((float)voxelSize.z / 2));

            voxelBase.fileType = VoxelBase.FileType.png;

            voxelBase.voxelData = new VoxelData();
            voxelBase.voxelData.voxels = voxels;
            voxelBase.voxelData.palettes = palettes;
            voxelBase.voxelData.voxelSize = voxelSize;

            ApplyImportFlags();
            voxelBase.voxelData.CreateVoxelTable();
            UpdateVisibleFlags();
            CreateChunkData();

            return true;
        }
        protected void ApplyImportFlags()
        {
            VoxelData.Voxel[] vs = voxelBase.voxelData.voxels;
            if ((voxelBase.importFlags & (VoxelBase.ImportFlag.FlipX | VoxelBase.ImportFlag.FlipY | VoxelBase.ImportFlag.FlipZ)) != 0)
            {
                vs = new VoxelData.Voxel[voxelBase.voxelData.voxels.Length];
                for (int i = 0; i < vs.Length; i++)
                {
                    vs[i] = voxelBase.voxelData.voxels[i];
                    if ((voxelBase.importFlags & VoxelBase.ImportFlag.FlipX) != 0) vs[i].x = voxelBase.voxelData.voxelSize.x - 1 - vs[i].x;
                    if ((voxelBase.importFlags & VoxelBase.ImportFlag.FlipY) != 0) vs[i].y = voxelBase.voxelData.voxelSize.y - 1 - vs[i].y;
                    if ((voxelBase.importFlags & VoxelBase.ImportFlag.FlipZ) != 0) vs[i].z = voxelBase.voxelData.voxelSize.z - 1 - vs[i].z;
                }
            }
            voxelBase.voxelData.voxels = vs;
        }
        protected void UpdateVisibleFlags()
        {
            VoxelData.Voxel[] vs = voxelBase.voxelData.voxels;
            for (int i = 0; i < vs.Length; i++)
            {
                vs[i].visible = 0;
                if (voxelBase.voxelData.VoxelTableContains(vs[i].x, vs[i].y, vs[i].z + 1) < 0)
                    vs[i].visible |= VoxelBase.Face.forward;
                if (voxelBase.voxelData.VoxelTableContains(vs[i].x, vs[i].y + 1, vs[i].z) < 0)
                    vs[i].visible |= VoxelBase.Face.up;
                if (voxelBase.voxelData.VoxelTableContains(vs[i].x + 1, vs[i].y, vs[i].z) < 0)
                    vs[i].visible |= VoxelBase.Face.right;
                if (voxelBase.voxelData.VoxelTableContains(vs[i].x - 1, vs[i].y, vs[i].z) < 0)
                    vs[i].visible |= VoxelBase.Face.left;
                if (voxelBase.voxelData.VoxelTableContains(vs[i].x, vs[i].y - 1, vs[i].z) < 0)
                    vs[i].visible |= VoxelBase.Face.down;
                if (voxelBase.voxelData.VoxelTableContains(vs[i].x, vs[i].y, vs[i].z - 1) < 0)
                    vs[i].visible |= VoxelBase.Face.back;
            }
        }
        public virtual string GetDefaultPath()
        {
            var path = Application.dataPath;
            var prefabType = PrefabUtility.GetPrefabType(voxelBase.gameObject);
            if (prefabType == PrefabType.Prefab)
            {
                var prefabObject = PrefabUtility.GetPrefabObject(voxelBase.gameObject);
                if (prefabObject != null)
                {
                    var assetPath = AssetDatabase.GetAssetPath(prefabObject);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        path = Path.GetDirectoryName(assetPath);
                    }
                }
            }
            else if (prefabType == PrefabType.PrefabInstance || prefabType == PrefabType.DisconnectedPrefabInstance)
            {
                var prefabParent = PrefabUtility.GetPrefabParent(voxelBase.gameObject);
                if (prefabParent != null)
                {
                    var prefabObject = PrefabUtility.GetPrefabObject(prefabParent);
                    if (prefabObject != null)
                    {
                        var assetPath = AssetDatabase.GetAssetPath(prefabObject);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            path = Path.GetDirectoryName(assetPath);
                        }
                    }
                }
            }
            return path;
        }
        #endregion

        #region CalcData
        protected int[][][] materialIndexTable;
        protected VoxelBase.Face[] voxelDoneFaces;
        protected void CalcDataCreate(VoxelData.Voxel[] voxels)
        {
            #region materialIndexTable 
            materialIndexTable = new int[voxelBase.voxelData.voxelSize.x][][];
            for (int x = 0; x < voxelBase.voxelData.voxelSize.x; x++)
            {
                materialIndexTable[x] = new int[voxelBase.voxelData.voxelSize.y][];
                for (int y = 0; y < voxelBase.voxelData.voxelSize.y; y++)
                {
                    materialIndexTable[x][y] = new int[voxelBase.voxelData.voxelSize.z];
                }
            }
            for (int i = 1; i < voxelBase.materialData.Count; i++)
            {
                voxelBase.materialData[i].AllAction((pos) =>
                {
                    materialIndexTable[pos.x][pos.y][pos.z] = i;
                });
            }
            #endregion
            #region voxelDoneFaces
            voxelDoneFaces = new VoxelBase.Face[voxelBase.voxelData.voxels.Length];
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(voxels[i].position);
                    var material = GetVoxelMaterialIndex(voxels[i].position);
                    if ((voxels[i].visible & VoxelBase.Face.forward) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.forward))
                    {
                        var nearMaterial = GetVoxelMaterialIndex(new IntVector3(voxels[i].position.x, voxels[i].position.y, voxels[i].position.z + 1));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.forward;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.up) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.up))
                    {
                        var nearMaterial = GetVoxelMaterialIndex(new IntVector3(voxels[i].position.x, voxels[i].position.y + 1, voxels[i].position.z));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.up;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.right) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.right))
                    {
                        var nearMaterial = GetVoxelMaterialIndex(new IntVector3(voxels[i].position.x + 1, voxels[i].position.y, voxels[i].position.z));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.right;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.left) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.left))
                    {
                        var nearMaterial = GetVoxelMaterialIndex(new IntVector3(voxels[i].position.x - 1, voxels[i].position.y, voxels[i].position.z));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.left;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.down) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.down))
                    {
                        var nearMaterial = GetVoxelMaterialIndex(new IntVector3(voxels[i].position.x, voxels[i].position.y - 1, voxels[i].position.z));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.down;
                        }
                    }
                    if ((voxels[i].visible & VoxelBase.Face.back) == 0 && IsHiddenVoxelFace(voxels[i].position, VoxelBase.Face.back))
                    {
                        var nearMaterial = GetVoxelMaterialIndex(new IntVector3(voxels[i].position.x, voxels[i].position.y, voxels[i].position.z - 1));
                        if (material == nearMaterial || (nearMaterial >= 0 && !voxelBase.materialData[nearMaterial].transparent))
                        {
                            voxelDoneFaces[index] |= VoxelBase.Face.back;
                        }
                    }
                }
            }
            #endregion
        }
        protected void CalcDataRelease()
        {
            voxelDoneFaces = null;
            materialIndexTable = null;
        }
        protected void SetDoneFacesFlag(VoxelData.FaceArea faceArea, VoxelBase.Face flag)
        {
            for (int x = faceArea.min.x; x <= faceArea.max.x; x++)
            {
                for (int y = faceArea.min.y; y <= faceArea.max.y; y++)
                {
                    for (int z = faceArea.min.z; z <= faceArea.max.z; z++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(x, y, z);
                        Assert.IsTrue(index >= 0);
                        voxelDoneFaces[index] |= flag;
                    }
                }
            }
        }
        protected int GetVoxelMaterialIndex(IntVector3 pos)
        {
            Assert.IsNotNull(materialIndexTable);

            if (pos.x < 0 || pos.x >= voxelBase.voxelData.voxelSize.x ||
                pos.y < 0 || pos.y >= voxelBase.voxelData.voxelSize.y ||
                pos.z < 0 || pos.z >= voxelBase.voxelData.voxelSize.z)
                return -1;

            return materialIndexTable[pos.x][pos.y][pos.z];
        }
        protected int[] GetVoxelIndexTable(VoxelData.Voxel[] voxels)
        {
            var voxelIndexTable = new int[voxels.Length];
            for (int i = 0; i < voxels.Length; i++)
            {
                var index = voxelBase.voxelData.VoxelTableContains(voxels[i].position);
                voxelIndexTable[i] = index;
            }
            return voxelIndexTable;
        }
        #endregion

        #region CreateTexture
        protected bool CreateTexture(VoxelData.FaceAreaTable faceAreaTable, Color[] palettes, ref AtlasRectTable atlasRectTable, ref Texture2D atlasTexture, ref Rect[] atlasRects)
        {
            if (voxelBase.importMode == VoxelBase.ImportMode.LowTexture)
                return CreateTexture_LowTexture(palettes, ref atlasRectTable, ref atlasTexture, ref atlasRects);
            else if (voxelBase.importMode == VoxelBase.ImportMode.LowPoly)
                return CreateTexture_LowPoly(faceAreaTable, ref atlasRectTable, ref atlasTexture, ref atlasRects);
            else
                return false;
        }
        protected bool CreateTexture_LowTexture(Color[] palettes, ref AtlasRectTable atlasRectTable, ref Texture2D atlasTexture, ref Rect[] atlasRects)
        {
            if (voxelBase.voxelData == null) return false;

            var textures = new Texture2D[palettes.Length];
            for (int i = 0; i < palettes.Length; i++)
            {
                textures[i] = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                textures[i].hideFlags = HideFlags.DontSave;
                textures[i].name = palettes[i].ToString();
                for (int x = 0; x < textures[i].width; x++)
                {
                    for (int y = 0; y < textures[i].height; y++)
                    {
                        textures[i].SetPixel(x, y, palettes[i]);
                    }
                }
                textures[i].Apply();
            }

            //Texture
            {
                var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                atlasRects = tex.PackTextures(textures, 0, 8192);
                #region Fill
                {
                    var pixels = tex.GetPixels();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i].a = 1f;
                    }
                    tex.SetPixels(pixels);
                    tex.Apply();
                }
                #endregion
                #region Mipmap
                if (voxelBase.generateMipMaps)
                {
                    var newTex = new Texture2D(tex.width, tex.height, tex.format, true);
                    newTex.filterMode = tex.filterMode;
                    newTex.wrapMode = tex.wrapMode;
                    newTex.SetPixels(tex.GetPixels(), 0);
                    newTex.Apply(true);
                    MonoBehaviour.DestroyImmediate(tex);
                    tex = newTex;
                }
                #endregion
                for (int i = 0; i < atlasRects.Length; i++)
                {
                    atlasRects[i].center += atlasRects[i].size / 2;
                    atlasRects[i].size = Vector2.zero;
                }
                if (atlasTexture != null && AssetDatabase.Contains(atlasTexture))
                {
                    var path = AssetDatabase.GetAssetPath(atlasTexture);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    AssetDatabase.ImportAsset(path);
                    MonoBehaviour.DestroyImmediate(tex);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                atlasTexture = tex;
            }
            for (int i = 0; i < textures.Length; i++)
            {
                MonoBehaviour.DestroyImmediate(textures[i]);
            }
            textures = null;
            
            return true;
        }
        protected bool CreateTexture_LowPoly(VoxelData.FaceAreaTable faceAreaTable, ref AtlasRectTable atlasRectTable, ref Texture2D atlasTexture, ref Rect[] atlasRects)
        {
            if (voxelBase.voxelData == null) return false;

            Assert.IsNotNull(faceAreaTable);

            Func<Color[,], TextureBoundArea, Texture2D> CompressTexture = (tex, bound) =>
            {
                var size = bound.Size;
                Texture2D newTex = new Texture2D(size.x, size.y, TextureFormat.ARGB32, false);
                for (int x = 0; x < size.x; x++)
                {
                    for (int y = 0; y < size.y; y++)
                    {
                        newTex.SetPixel(x, y, tex[bound.min.x + x, bound.min.y + y]);
                    }
                }
                newTex.Apply();
                return newTex;
            };

            List<Texture2D> textures = new List<Texture2D>();
            atlasRectTable = new AtlasRectTable();
            #region forward 
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.forward) != 0)
            {
                Color[,] tex = new Color[voxelBase.voxelData.voxelSize.x, voxelBase.voxelData.voxelSize.y];
                for (int i = 0; i < faceAreaTable.forward.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.forward[i].min.z == faceAreaTable.forward[i].max.z);
                    int z = faceAreaTable.forward[i].min.z;
                    TextureBoundArea bound = null;
                    for (int x = faceAreaTable.forward[i].min.x; x <= faceAreaTable.forward[i].max.x; x++)
                    {
                        for (int y = faceAreaTable.forward[i].min.y; y <= faceAreaTable.forward[i].max.y; y++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.forward))
                            {
                                var index = voxelBase.voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                tex[x, y] = voxelBase.voxelData.palettes[voxelBase.voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(x, y));
                            }
                            else
                            {
                                tex[x, y] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = textures.Count;
                        textures.Add(CompressTexture(tex, bound));
                    }
                    atlasRectTable.forward.Add(bound);
                }
            }
            #endregion
            #region up 
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.up) != 0)
            {
                Color[,] tex = new Color[voxelBase.voxelData.voxelSize.x, voxelBase.voxelData.voxelSize.z];
                for (int i = 0; i < faceAreaTable.up.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.up[i].min.y == faceAreaTable.up[i].max.y);
                    int y = faceAreaTable.up[i].min.y;
                    TextureBoundArea bound = null;
                    for (int x = faceAreaTable.up[i].min.x; x <= faceAreaTable.up[i].max.x; x++)
                    {
                        for (int z = faceAreaTable.up[i].min.z; z <= faceAreaTable.up[i].max.z; z++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.up))
                            {
                                var index = voxelBase.voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                tex[x, z] = voxelBase.voxelData.palettes[voxelBase.voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(x, z));
                            }
                            else
                            {
                                tex[x, z] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = textures.Count;
                        textures.Add(CompressTexture(tex, bound));
                    }
                    atlasRectTable.up.Add(bound);
                }
            }
            #endregion
            #region right 
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.right) != 0)
            {
                Color[,] tex = new Color[voxelBase.voxelData.voxelSize.y, voxelBase.voxelData.voxelSize.z];
                for (int i = 0; i < faceAreaTable.right.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.right[i].min.x == faceAreaTable.right[i].max.x);
                    int x = faceAreaTable.right[i].min.x;
                    TextureBoundArea bound = null;
                    for (int y = faceAreaTable.right[i].min.y; y <= faceAreaTable.right[i].max.y; y++)
                    {
                        for (int z = faceAreaTable.right[i].min.z; z <= faceAreaTable.right[i].max.z; z++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.right))
                            {
                                var index = voxelBase.voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                tex[y, z] = voxelBase.voxelData.palettes[voxelBase.voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(y, z));
                            }
                            else
                            {
                                tex[y, z] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = textures.Count;
                        textures.Add(CompressTexture(tex, bound));
                    }
                    atlasRectTable.right.Add(bound);
                }
            }
            #endregion
            #region left 
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.left) != 0)
            {
                Color[,] tex = new Color[voxelBase.voxelData.voxelSize.y, voxelBase.voxelData.voxelSize.z];
                for (int i = 0; i < faceAreaTable.left.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.left[i].min.x == faceAreaTable.left[i].max.x);
                    int x = faceAreaTable.left[i].min.x;
                    TextureBoundArea bound = null;
                    for (int y = faceAreaTable.left[i].min.y; y <= faceAreaTable.left[i].max.y; y++)
                    {
                        for (int z = faceAreaTable.left[i].min.z; z <= faceAreaTable.left[i].max.z; z++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.left))
                            {
                                var index = voxelBase.voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                tex[y, z] = voxelBase.voxelData.palettes[voxelBase.voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(y, z));
                            }
                            else
                            {
                                tex[y, z] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = textures.Count;
                        textures.Add(CompressTexture(tex, bound));
                    }
                    atlasRectTable.left.Add(bound);
                }
            }
            #endregion
            #region down 
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.down) != 0)
            {
                Color[,] tex = new Color[voxelBase.voxelData.voxelSize.x, voxelBase.voxelData.voxelSize.z];
                for (int i = 0; i < faceAreaTable.down.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.down[i].min.y == faceAreaTable.down[i].max.y);
                    int y = faceAreaTable.down[i].min.y;
                    TextureBoundArea bound = null;
                    for (int x = faceAreaTable.down[i].min.x; x <= faceAreaTable.down[i].max.x; x++)
                    {
                        for (int z = faceAreaTable.down[i].min.z; z <= faceAreaTable.down[i].max.z; z++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.down))
                            {
                                var index = voxelBase.voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                tex[x, z] = voxelBase.voxelData.palettes[voxelBase.voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(x, z));
                            }
                            else
                            {
                                tex[x, z] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = textures.Count;
                        textures.Add(CompressTexture(tex, bound));
                    }
                    atlasRectTable.down.Add(bound);
                }
            }
            #endregion
            #region back 
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.back) != 0)
            {
                Color[,] tex = new Color[voxelBase.voxelData.voxelSize.x, voxelBase.voxelData.voxelSize.y];
                for (int i = 0; i < faceAreaTable.back.Count; i++)
                {
                    Assert.IsTrue(faceAreaTable.back[i].min.z == faceAreaTable.back[i].max.z);
                    int z = faceAreaTable.back[i].min.z;
                    TextureBoundArea bound = null;
                    for (int x = faceAreaTable.back[i].min.x; x <= faceAreaTable.back[i].max.x; x++)
                    {
                        for (int y = faceAreaTable.back[i].min.y; y <= faceAreaTable.back[i].max.y; y++)
                        {
                            if (IsShowVoxelFace(new IntVector3(x, y, z), VoxelBase.Face.back))
                            {
                                var index = voxelBase.voxelData.VoxelTableContains(new IntVector3(x, y, z));
                                tex[x, y] = voxelBase.voxelData.palettes[voxelBase.voxelData.voxels[index].palette];
                                if (bound == null) bound = new TextureBoundArea();
                                bound.Set(new IntVector2(x, y));
                            }
                            else
                            {
                                tex[x, y] = Color.clear;
                            }
                        }
                    }
                    if (bound != null)
                    {
                        bound.textureIndex = textures.Count;
                        textures.Add(CompressTexture(tex, bound));
                    }
                    atlasRectTable.back.Add(bound);
                }
            }
            #endregion

            //Texture
            {
                var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                atlasRects = tex.PackTextures(textures.ToArray(), 2, 8192);
                #region Bordering
                {
                    Color color = Color.clear;
                    int count = 0;
                    Action<int, int> AddPixel = (xx, yy) =>
                    {
                        var c = tex.GetPixel(xx, yy);
                        if (c.a > 0f)
                        {
                            color += c;
                            count++;
                        }
                    };

                    var pixels = tex.GetPixels();
                    for (int x = 0; x < tex.width; x++)
                    {
                        for (int y = 0; y < tex.height; y++)
                        {
                            color = tex.GetPixel(x, y);
                            if (color.a <= 0f)
                            {
                                color = Color.clear;
                                count = 0;
                                if (x > 0) AddPixel(x - 1, y);
                                if (x < tex.width - 1) AddPixel(x + 1, y);
                                if (y > 0) AddPixel(x, y - 1);
                                if (y < tex.height - 1) AddPixel(x, y + 1);
                                if (count == 0)
                                {
                                    if (x > 0 && y > 0) AddPixel(x - 1, y - 1);
                                    if (x < tex.width - 1 && y > 0) AddPixel(x + 1, y - 1);
                                    if (x > 0 && y < tex.height - 1) AddPixel(x - 1, y + 1);
                                    if (x < tex.width - 1 && y < tex.height - 1) AddPixel(x + 1, y + 1);
                                }
                                if (count > 0)
                                    color = color / (float)count;
                                color.a = 1f;
                                pixels[x + y * tex.width] = color;
                            }
                            else
                            {
                                color.a = 1f;
                                pixels[x + y * tex.width] = color;
                            }
                        }
                    }
                    tex.SetPixels(pixels);
                    tex.Apply();
                }
                #endregion
                #region Mipmap
                if (voxelBase.generateMipMaps)
                {
                    var newTex = new Texture2D(tex.width, tex.height, tex.format, true);
                    newTex.filterMode = tex.filterMode;
                    newTex.wrapMode = tex.wrapMode;
                    newTex.SetPixels(tex.GetPixels(), 0);
                    newTex.Apply(true);
                    MonoBehaviour.DestroyImmediate(tex);
                    tex = newTex;
                }
                #endregion
                if (atlasTexture != null && AssetDatabase.Contains(atlasTexture))
                {
                    var path = AssetDatabase.GetAssetPath(atlasTexture);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    AssetDatabase.ImportAsset(path);
                    MonoBehaviour.DestroyImmediate(tex);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                atlasTexture = tex;
            }
            for (int i = 0; i < textures.Count; i++)
            {
                MonoBehaviour.DestroyImmediate(textures[i]);
            }
            textures = null;
            
            return true;
        }
        #endregion

        #region CreateMesh
        protected virtual bool IsCombineVoxelFace(IntVector3 pos, IntVector3 combinePos, VoxelBase.Face face)
        {
            return true;
        }
        protected virtual bool IsHiddenVoxelFace(IntVector3 pos, VoxelBase.Face faceFlag)
        {
            return true;
        }
        protected virtual bool IsShowVoxelFace(IntVector3 pos, VoxelBase.Face faceFlag)
        {
            var index = voxelBase.voxelData.VoxelTableContains(pos);
            if (index < 0) return false;
            Assert.IsTrue(faceFlag == VoxelBase.Face.forward || faceFlag == VoxelBase.Face.up || faceFlag == VoxelBase.Face.right || faceFlag == VoxelBase.Face.left || faceFlag == VoxelBase.Face.down || faceFlag == VoxelBase.Face.back);
            IntVector3 combinePos = pos;
            {
                if (faceFlag == VoxelBase.Face.forward) combinePos.z++;
                if (faceFlag == VoxelBase.Face.up) combinePos.y++;
                if (faceFlag == VoxelBase.Face.right) combinePos.x++;
                if (faceFlag == VoxelBase.Face.left) combinePos.x--;
                if (faceFlag == VoxelBase.Face.down) combinePos.y--;
                if (faceFlag == VoxelBase.Face.back) combinePos.z--;
            }
            index = voxelBase.voxelData.VoxelTableContains(combinePos);
            if (index < 0)
                return true;
            {
                var combineMaterial = GetVoxelMaterialIndex(combinePos);
                if (combineMaterial >= 0)
                {
                    if (voxelBase.materialData[combineMaterial].transparent)
                        return true;
                }
            }
            return !IsHiddenVoxelFace(pos, faceFlag);
        }
        protected abstract bool CreateMesh();
        protected virtual void CreateMeshAfterFree()
        {
            CalcDataRelease();
        }
        protected Mesh CreateMeshOnly(Mesh result, VoxelData.FaceAreaTable faceAreaTable, Texture2D atlasTexture, Rect[] atlasRects, AtlasRectTable atlasRectTable, Vector3 extraOffset, out List<int> materialIndexes)
        {
            if (voxelBase.importMode == VoxelBase.ImportMode.LowTexture)
                return CreateMeshOnly_LowTexture(result, faceAreaTable, atlasTexture, atlasRects, atlasRectTable, extraOffset, out materialIndexes);
            else if (voxelBase.importMode == VoxelBase.ImportMode.LowPoly)
                return CreateMeshOnly_LowPoly(result, faceAreaTable, atlasTexture, atlasRects, atlasRectTable, extraOffset, out materialIndexes);
            else
            {
                materialIndexes = new List<int>();
                return null;
            }
        }
        protected Mesh CreateMeshOnly_LowTexture(Mesh result, VoxelData.FaceAreaTable faceAreaTable, Texture2D atlasTexture, Rect[] atlasRects, AtlasRectTable atlasRectTable, Vector3 extraOffset, out List<int> materialIndexes)
        {
            Assert.IsNotNull(faceAreaTable);

            materialIndexes = new List<int>();

            if (result == null)
            {
                result = new Mesh();
            }
            else
            {
                result.ClearBlendShapes();
                result.Clear(false);
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<BoneWeight> boneWeights = isHaveBoneWeight ? new List<BoneWeight>() : null;
            List<int>[] triangles = new List<int>[voxelBase.materialData.Count];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new List<int>();
            }

            #region Create
            {
                var offsetPosition = voxelBase.localOffset + voxelBase.importOffset + extraOffset;
                #region forward
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.forward) != 0)
                {
                    for (int i = 0; i < faceAreaTable.forward.Count; i++)
                    {
                        var faceArea = faceAreaTable.forward[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, sizeY, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(0, 0, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX, sizeY, voxelBase.importScale.z) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.forward);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))], VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))], VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))], VoxelBase.VoxelVertexIndex.X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))], VoxelBase.VoxelVertexIndex.XYZ));
                        }
                    }
                }
                #endregion
                #region up
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.up) != 0)
                {
                    for (int i = 0; i < faceAreaTable.up.Count; i++)
                    {
                        var faceArea = faceAreaTable.up[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, voxelBase.importScale.y, 0) + pOffset);
                        vertices.Add(new Vector3(0, voxelBase.importScale.y, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, voxelBase.importScale.y, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, voxelBase.importScale.y, 0) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.up);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))], VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))], VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))], VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))], VoxelBase.VoxelVertexIndex.XY_Z));
                        }
                    }
                }
                #endregion
                #region right
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.right) != 0)
                {
                    for (int i = 0; i < faceAreaTable.right.Count; i++)
                    {
                        var faceArea = faceAreaTable.right[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(voxelBase.importScale.x, 0, 0) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY, sizeZ) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, 0, sizeZ) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.right);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))], VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))], VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))], VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))], VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                    }
                }
                #endregion
                #region left
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.left) != 0)
                {
                    for (int i = 0; i < faceAreaTable.left.Count; i++)
                    {
                        var faceArea = faceAreaTable.left[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, 0, sizeZ) + pOffset);
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, sizeZ) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.left);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))], VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))], VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))], VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))], VoxelBase.VoxelVertexIndex._XYZ));
                        }
                    }
                }
                #endregion
                #region down
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.down) != 0)
                {
                    for (int i = 0; i < faceAreaTable.down.Count; i++)
                    {
                        var faceArea = faceAreaTable.down[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(sizeX, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, 0, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, sizeZ) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.down);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))], VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))], VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))], VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))], VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                    }
                }
                #endregion
                #region back
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.back) != 0)
                {
                    for (int i = 0; i < faceAreaTable.back.Count; i++)
                    {
                        var faceArea = faceAreaTable.back[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, 0) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.back);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))], VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))], VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))], VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))], VoxelBase.VoxelVertexIndex._XY_Z));
                        }
                    }
                }
                #endregion
            }
            #endregion

            #region Mesh
            {
                if (vertices.Count > 65000)
                {
                    const int Sepalate = 64999;
                    Debug.LogWarningFormat("<color=green>[VoxelCharacteImporter]</color> Mesh.vertices is too large. A mesh may not have more than 65000 vertices. <color=red>{0} / 65000</color>", vertices.Count);
                    vertices.RemoveRange(Sepalate, vertices.Count - Sepalate);
                    if (uv.Count > Sepalate)
                        uv.RemoveRange(Sepalate, uv.Count - Sepalate);
                    if (normals.Count > Sepalate)
                        normals.RemoveRange(Sepalate, normals.Count - Sepalate);
                    if (isHaveBoneWeight)
                    {
                        if (boneWeights.Count > Sepalate)
                            boneWeights.RemoveRange(Sepalate, boneWeights.Count - Sepalate);
                    }
                    for (int j = 0; j < triangles.Length; j++)
                    {
                        for (int i = triangles[j].Count - 1; i >= 0; i--)
                        {
                            if (triangles[j][i] < Sepalate)
                            {
                                int index = ((i / 3) * 3);
                                triangles[j].RemoveRange(index, triangles[j].Count - index);
                                break;
                            }
                        }
                    }
                }
                result.vertices = vertices.ToArray();
                result.uv = uv.ToArray();
                result.normals = normals.ToArray();
                if (isHaveBoneWeight)
                {
                    result.boneWeights = boneWeights.ToArray();
                    result.bindposes = GetBindposes();
                }
                {
                    int materialCount = 0;
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        if (triangles[i].Count > 0)
                            materialCount++;
                    }
                    result.subMeshCount = materialCount;
                    int submesh = 0;
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        if (triangles[i].Count > 0)
                        {
                            materialIndexes.Add(i);
                            result.SetTriangles(triangles[i].ToArray(), submesh++);
                        }
                    }
                }
                result.RecalculateBounds();
                result.Optimize();
            }
            #endregion

            return result;
        }
        protected Mesh CreateMeshOnly_LowPoly(Mesh result, VoxelData.FaceAreaTable faceAreaTable, Texture2D atlasTexture, Rect[] atlasRects, AtlasRectTable atlasRectTable, Vector3 extraOffset, out List<int> materialIndexes)
        {
            Assert.IsNotNull(faceAreaTable);

            materialIndexes = new List<int>();

            if (result == null)
            {
                result = new Mesh();
            }
            else
            {
                result.ClearBlendShapes();
                result.Clear(false);
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<BoneWeight> boneWeights = isHaveBoneWeight ? new List<BoneWeight>() : null;
            List<int>[] triangles = new List<int>[voxelBase.materialData.Count];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new List<int>();
            }

            #region Create
            {
                var offsetPosition = voxelBase.localOffset + voxelBase.importOffset + extraOffset;
                Vector2 uvone = new Vector2(1f / atlasTexture.width, 1f / atlasTexture.height);
                #region forward
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.forward) != 0)
                {
                    for (int i = 0; i < faceAreaTable.forward.Count; i++)
                    {
                        var faceArea = faceAreaTable.forward[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, sizeY, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(0, 0, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX, sizeY, voxelBase.importScale.z) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.forward);
                        }
                        if (faceArea.palette >= 0)
                        {
                            var bound = atlasRectTable.forward[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.y - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.y - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.y) * uvone.y));
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))], VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))], VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))], VoxelBase.VoxelVertexIndex.X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))], VoxelBase.VoxelVertexIndex.XYZ));
                        }
                    }
                }
                #endregion
                #region up
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.up) != 0)
                {
                    for (int i = 0; i < faceAreaTable.up.Count; i++)
                    {
                        var faceArea = faceAreaTable.up[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, voxelBase.importScale.y, 0) + pOffset);
                        vertices.Add(new Vector3(0, voxelBase.importScale.y, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, voxelBase.importScale.y, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, voxelBase.importScale.y, 0) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.up);
                        }
                        if (faceArea.palette >= 0)
                        {
                            var bound = atlasRectTable.up[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y));
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))], VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))], VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))], VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))], VoxelBase.VoxelVertexIndex.XY_Z));
                        }
                    }
                }
                #endregion
                #region right
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.right) != 0)
                {
                    for (int i = 0; i < faceAreaTable.right.Count; i++)
                    {
                        var faceArea = faceAreaTable.right[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(voxelBase.importScale.x, 0, 0) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY, sizeZ) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, 0, sizeZ) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 2);
                        triangles[faceArea.material].Add(vOffset + 0); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.right);
                        }
                        if (faceArea.palette >= 0)
                        {
                            var bound = atlasRectTable.right[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.y - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.y) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.y) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.y - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y));
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))], VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))], VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))], VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))], VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                    }
                }
                #endregion
                #region left
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.left) != 0)
                {
                    for (int i = 0; i < faceAreaTable.left.Count; i++)
                    {
                        var faceArea = faceAreaTable.left[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, 0, sizeZ) + pOffset);
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, sizeZ) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.left);
                        }
                        if (faceArea.palette >= 0)
                        {
                            var bound = atlasRectTable.left[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.y - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.y - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.y) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.y) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y));
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))], VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))], VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))], VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))], VoxelBase.VoxelVertexIndex._XYZ));
                        }
                    }
                }
                #endregion
                #region down
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.down) != 0)
                {
                    for (int i = 0; i < faceAreaTable.down.Count; i++)
                    {
                        var faceArea = faceAreaTable.down[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(sizeX, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, 0, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, sizeZ) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.down);
                        }
                        if (faceArea.palette >= 0)
                        {
                            var bound = atlasRectTable.down[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.z - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.z) * uvone.y));
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))], VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))], VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))], VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))], VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                    }
                }
                #endregion
                #region back
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.back) != 0)
                {
                    for (int i = 0; i < faceAreaTable.back.Count; i++)
                    {
                        var faceArea = faceAreaTable.back[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, 0) + pOffset);
                        triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 1); triangles[faceArea.material].Add(vOffset + 0);
                        triangles[faceArea.material].Add(vOffset + 3); triangles[faceArea.material].Add(vOffset + 2); triangles[faceArea.material].Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            normals.Add(Vector3.back);
                        }
                        if (faceArea.palette >= 0)
                        {
                            var bound = atlasRectTable.back[i];
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.y - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x, atlasRects[bound.textureIndex].position.y + (faceArea.min.y - bound.min.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + atlasRects[bound.textureIndex].size.x - (bound.max.x - faceArea.max.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.y) * uvone.y));
                            uv.Add(new Vector2(atlasRects[bound.textureIndex].position.x + (faceArea.min.x - bound.min.x) * uvone.x, atlasRects[bound.textureIndex].position.y + atlasRects[bound.textureIndex].size.y - (bound.max.y - faceArea.max.y) * uvone.y));
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))], VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))], VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))], VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))], VoxelBase.VoxelVertexIndex._XY_Z));
                        }
                    }
                }
                #endregion
            }
            #endregion

            #region Mesh
            {
                if (vertices.Count > 65000)
                {
                    const int Sepalate = 64999;
                    Debug.LogWarningFormat("<color=green>[VoxelCharacteImporter]</color> Mesh.vertices is too large. A mesh may not have more than 65000 vertices. <color=red>{0} / 65000</color>", vertices.Count);
                    vertices.RemoveRange(Sepalate, vertices.Count - Sepalate);
                    if (uv.Count > Sepalate)
                        uv.RemoveRange(Sepalate, uv.Count - Sepalate);
                    if (normals.Count > Sepalate)
                        normals.RemoveRange(Sepalate, normals.Count - Sepalate);
                    if (isHaveBoneWeight)
                    {
                        if (boneWeights.Count > Sepalate)
                            boneWeights.RemoveRange(Sepalate, boneWeights.Count - Sepalate);
                    }
                    for (int j = 0; j < triangles.Length; j++)
                    {
                        for (int i = triangles[j].Count - 1; i >= 0; i--)
                        {
                            if (triangles[j][i] < Sepalate)
                            {
                                int index = ((i / 3) * 3);
                                triangles[j].RemoveRange(index, triangles[j].Count - index);
                                break;
                            }
                        }
                    }
                }
                result.vertices = vertices.ToArray();
                result.uv = uv.ToArray();
                result.normals = normals.ToArray();
                if (isHaveBoneWeight)
                {
                    result.boneWeights = boneWeights.ToArray();
                    result.bindposes = GetBindposes();
                }
                {
                    int materialCount = 0;
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        if (triangles[i].Count > 0)
                            materialCount++;
                    }
                    result.subMeshCount = materialCount;
                    int submesh = 0;
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        if (triangles[i].Count > 0)
                        {
                            materialIndexes.Add(i);
                            result.SetTriangles(triangles[i].ToArray(), submesh++);
                        }
                    }
                }
                result.RecalculateBounds();
                result.Optimize();
            }
            #endregion

            return result;
        }
        public virtual void SetRendererCompornent() { }
        #region Edit
        public struct Edit_VerticesInfo
        {
            public IntVector3 position;
            public VoxelBase.VoxelVertexIndex vertexIndex;
        }
        public abstract Mesh[] Edit_CreateMesh(List<VoxelData.Voxel> voxels, List<Edit_VerticesInfo> dstList = null, bool combine = true);
        public Mesh Edit_CreateMeshOnly(List<VoxelData.Voxel> voxels, Rect[] atlasRects, List<Edit_VerticesInfo> dstList = null, bool combine = true)
        {
            var tmpFaceAreaTable = Edit_CreateMeshOnly_FaceArea(voxels, combine);

            var result = Edit_CreateMeshOnly_Mesh(tmpFaceAreaTable, atlasRects, dstList);

            return result;
        }
        public VoxelData.FaceAreaTable Edit_CreateMeshOnly_FaceArea(List<VoxelData.Voxel> voxels, bool combine = true)
        {
            #region VoxelTable
            DataTable3<int> tmpVoxelTable;
            {
                tmpVoxelTable = new DataTable3<int>(voxelBase.voxelData.voxelSize.x, voxelBase.voxelData.voxelSize.y, voxelBase.voxelData.voxelSize.z);
                for (int i = 0; i < voxels.Count; i++)
                {
                    tmpVoxelTable.Set(voxels[i].position, i);
                }
            }
            Func<int, int, int, int> TmpVoxelTableContains = (x, y, z) =>
            {
                if (!tmpVoxelTable.Contains(x, y, z))
                    return -1;
                else
                    return tmpVoxelTable.Get(x, y, z);
            };
            #endregion

            #region FaceArea
            VoxelData.FaceAreaTable tmpFaceAreaTable;
            {
                VoxelBase.Face[] voxelDoneFaces = new VoxelBase.Face[voxels.Count];
                {
                    int index;
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        index = TmpVoxelTableContains(voxels[i].x, voxels[i].y, voxels[i].z + 1);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.forward;
                        index = TmpVoxelTableContains(voxels[i].x, voxels[i].y + 1, voxels[i].z);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.up;
                        index = TmpVoxelTableContains(voxels[i].x + 1, voxels[i].y, voxels[i].z);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.right;
                        index = TmpVoxelTableContains(voxels[i].x - 1, voxels[i].y, voxels[i].z);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.left;
                        index = TmpVoxelTableContains(voxels[i].x, voxels[i].y - 1, voxels[i].z);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.down;
                        index = TmpVoxelTableContains(voxels[i].x, voxels[i].y, voxels[i].z - 1);
                        if (index >= 0)
                            voxelDoneFaces[i] |= VoxelBase.Face.back;
                    }
                }
                Action<VoxelData.FaceArea, VoxelBase.Face> SetDoneFacesFlag = (faceArea, flag) =>
                {
                    for (int x = faceArea.min.x; x <= faceArea.max.x; x++)
                    {
                        for (int y = faceArea.min.y; y <= faceArea.max.y; y++)
                        {
                            for (int z = faceArea.min.z; z <= faceArea.max.z; z++)
                            {
                                var index = TmpVoxelTableContains(x, y, z);
                                Assert.IsTrue(index >= 0);
                                voxelDoneFaces[index] |= flag;
                            }
                        }
                    }
                };

                Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceX = (baseIndex, flag) =>
                {
                    var palette = voxels[baseIndex].palette;
                    var x = voxels[baseIndex].x;
                    var y = voxels[baseIndex].y;
                    var z = voxels[baseIndex].z;
                    var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = voxels[baseIndex].palette };
                    if (combine)
                    {
                        //back
                        for (int i = z - 1; ; i--)
                        {
                            var index = TmpVoxelTableContains(x, y, i);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.min.z = i;
                        }
                        //forward
                        for (int i = z + 1; ; i++)
                        {
                            var index = TmpVoxelTableContains(x, y, i);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.max.z = i;
                        }
                        //down
                        for (int i = y - 1; ; i--)
                        {
                            bool r = true;
                            for (int j = area.min.z; j <= area.max.z; j++)
                            {
                                var index = TmpVoxelTableContains(x, i, j);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.min.y = i;
                        }
                        //up
                        for (int i = y + 1; ; i++)
                        {
                            bool r = true;
                            for (int j = area.min.z; j <= area.max.z; j++)
                            {
                                var index = TmpVoxelTableContains(x, i, j);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.max.y = i;
                        }
                    }
                    return area;
                };
                Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceY = (baseIndex, flag) =>
                {
                    var palette = voxels[baseIndex].palette;
                    var x = voxels[baseIndex].x;
                    var y = voxels[baseIndex].y;
                    var z = voxels[baseIndex].z;
                    var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = voxels[baseIndex].palette };
                    if (combine)
                    {
                        //back
                        for (int i = z - 1; ; i--)
                        {
                            var index = TmpVoxelTableContains(x, y, i);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.min.z = i;
                        }
                        //forward
                        for (int i = z + 1; ; i++)
                        {
                            var index = TmpVoxelTableContains(x, y, i);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.max.z = i;
                        }
                        //left
                        for (int i = x - 1; ; i--)
                        {
                            bool r = true;
                            for (int j = area.min.z; j <= area.max.z; j++)
                            {
                                var index = TmpVoxelTableContains(i, y, j);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.min.x = i;
                        }
                        //right
                        for (int i = x + 1; ; i++)
                        {
                            bool r = true;
                            for (int j = area.min.z; j <= area.max.z; j++)
                            {
                                var index = TmpVoxelTableContains(i, y, j);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.max.x = i;
                        }
                    }
                    return area;
                };
                Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceZ = (baseIndex, flag) =>
                {
                    var palette = voxels[baseIndex].palette;
                    var x = voxels[baseIndex].x;
                    var y = voxels[baseIndex].y;
                    var z = voxels[baseIndex].z;
                    var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = voxels[baseIndex].palette };
                    if (combine)
                    {
                        //up
                        for (int i = y - 1; ; i--)
                        {
                            var index = TmpVoxelTableContains(x, i, z);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.min.y = i;
                        }
                        //down
                        for (int i = y + 1; ; i++)
                        {
                            var index = TmpVoxelTableContains(x, i, z);
                            if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) break;
                            area.max.y = i;
                        }
                        //left
                        for (int i = x - 1; ; i--)
                        {
                            bool r = true;
                            for (int j = area.min.y; j <= area.max.y; j++)
                            {
                                var index = TmpVoxelTableContains(i, j, z);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.min.x = i;
                        }
                        //right
                        for (int i = x + 1; ; i++)
                        {
                            bool r = true;
                            for (int j = area.min.y; j <= area.max.y; j++)
                            {
                                var index = TmpVoxelTableContains(i, j, z);
                                if (index < 0 || voxels[index].palette != palette || (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxels[index].position, flag)) { r = false; break; }
                            }
                            if (!r) break;
                            area.max.x = i;
                        }
                    }
                    return area;
                };

                tmpFaceAreaTable = new VoxelData.FaceAreaTable();

                #region forward
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.forward) != 0) continue;
                        var faceArea = CalcFaceZ(i, VoxelBase.Face.forward);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.forward);
                        tmpFaceAreaTable.forward.Add(faceArea);
                    }
                }
                #endregion
                #region up
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.up) != 0) continue;
                        var faceArea = CalcFaceY(i, VoxelBase.Face.up);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.up);
                        tmpFaceAreaTable.up.Add(faceArea);
                    }
                }
                #endregion
                #region right
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.right) != 0) continue;
                        var faceArea = CalcFaceX(i, VoxelBase.Face.right);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.right);
                        tmpFaceAreaTable.right.Add(faceArea);
                    }
                }
                #endregion
                #region left
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.left) != 0) continue;
                        var faceArea = CalcFaceX(i, VoxelBase.Face.left);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.left);
                        tmpFaceAreaTable.left.Add(faceArea);
                    }
                }
                #endregion
                #region down
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.down) != 0) continue;
                        var faceArea = CalcFaceY(i, VoxelBase.Face.down);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.down);
                        tmpFaceAreaTable.down.Add(faceArea);
                    }
                }
                #endregion
                #region back
                {
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        if ((voxelDoneFaces[i] & VoxelBase.Face.back) != 0) continue;
                        var faceArea = CalcFaceZ(i, VoxelBase.Face.back);
                        SetDoneFacesFlag(faceArea, VoxelBase.Face.back);
                        tmpFaceAreaTable.back.Add(faceArea);
                    }
                }
                #endregion
            }
            #endregion

            return tmpFaceAreaTable;
        }
        public Mesh Edit_CreateMeshOnly_Mesh(VoxelData.FaceAreaTable tmpFaceAreaTable, Rect[] atlasRects, List<Edit_VerticesInfo> dstList = null)
        {
            #region CreateMesh
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<BoneWeight> boneWeights = isHaveBoneWeight ? new List<BoneWeight>() : null;
            List<int> triangles = new List<int>();

            #region Create
            {
                var offsetPosition = voxelBase.localOffset + voxelBase.importOffset;
                #region forward
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.forward) != 0)
                {
                    for (int i = 0; i < tmpFaceAreaTable.forward.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.forward[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, sizeY, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(0, 0, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, voxelBase.importScale.z) + pOffset);
                        vertices.Add(new Vector3(sizeX, sizeY, voxelBase.importScale.z) + pOffset);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 1); triangles.Add(vOffset + 2);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 2); triangles.Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.forward);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))], VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))], VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))], VoxelBase.VoxelVertexIndex.X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))], VoxelBase.VoxelVertexIndex.XYZ));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ), vertexIndex = VoxelBase.VoxelVertexIndex._XYZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex._X_YZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex.X_YZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ), vertexIndex = VoxelBase.VoxelVertexIndex.XYZ });
                        }
                    }
                }
                #endregion
                #region up
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.up) != 0)
                {
                    for (int i = 0; i < tmpFaceAreaTable.up.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.up[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, voxelBase.importScale.y, 0) + pOffset);
                        vertices.Add(new Vector3(0, voxelBase.importScale.y, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, voxelBase.importScale.y, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, voxelBase.importScale.y, 0) + pOffset);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 1); triangles.Add(vOffset + 2);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 2); triangles.Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.up);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))], VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))], VoxelBase.VoxelVertexIndex._XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))], VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))], VoxelBase.VoxelVertexIndex.XY_Z));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex._XY_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ), vertexIndex = VoxelBase.VoxelVertexIndex._XYZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ), vertexIndex = VoxelBase.VoxelVertexIndex.XYZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex.XY_Z });
                        }
                    }
                }
                #endregion
                #region right
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.right) != 0)
                {
                    for (int i = 0; i < tmpFaceAreaTable.right.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.right[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(voxelBase.importScale.x, 0, 0) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, sizeY, sizeZ) + pOffset);
                        vertices.Add(new Vector3(voxelBase.importScale.x, 0, sizeZ) + pOffset);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 1); triangles.Add(vOffset + 2);
                        triangles.Add(vOffset + 0); triangles.Add(vOffset + 2); triangles.Add(vOffset + 3);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.right);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))], VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))], VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ))], VoxelBase.VoxelVertexIndex.XYZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))], VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex.X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex.XY_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XYZ), vertexIndex = VoxelBase.VoxelVertexIndex.XYZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex.X_YZ });
                        }
                    }
                }
                #endregion
                #region left
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.left) != 0)
                {
                    for (int i = 0; i < tmpFaceAreaTable.left.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.left[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, 0, sizeZ) + pOffset);
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, sizeZ) + pOffset);
                        triangles.Add(vOffset + 2); triangles.Add(vOffset + 1); triangles.Add(vOffset + 0);
                        triangles.Add(vOffset + 3); triangles.Add(vOffset + 2); triangles.Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.left);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))], VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))], VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))], VoxelBase.VoxelVertexIndex._XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ))], VoxelBase.VoxelVertexIndex._XYZ));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex._X_YZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex._X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex._XY_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XYZ), vertexIndex = VoxelBase.VoxelVertexIndex._XYZ });
                        }
                    }
                }
                #endregion
                #region down
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.down) != 0)
                {
                    for (int i = 0; i < tmpFaceAreaTable.down.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.down[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeZ = faceArea.size.z * voxelBase.importScale.z;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(sizeX, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(0, 0, sizeZ) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, sizeZ) + pOffset);
                        triangles.Add(vOffset + 2); triangles.Add(vOffset + 1); triangles.Add(vOffset + 0);
                        triangles.Add(vOffset + 3); triangles.Add(vOffset + 2); triangles.Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.down);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))], VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))], VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ))], VoxelBase.VoxelVertexIndex._X_YZ));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ))], VoxelBase.VoxelVertexIndex.X_YZ));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex.X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex._X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex._X_YZ });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_YZ), vertexIndex = VoxelBase.VoxelVertexIndex.X_YZ });
                        }
                    }
                }
                #endregion
                #region back
                if ((voxelBase.enableFaceFlags & VoxelBase.Face.back) != 0)
                {
                    for (int i = 0; i < tmpFaceAreaTable.back.Count; i++)
                    {
                        var faceArea = tmpFaceAreaTable.back[i];
                        var pOffset = Vector3.Scale(voxelBase.importScale, faceArea.minf + offsetPosition);
                        var sizeX = faceArea.size.x * voxelBase.importScale.x;
                        var sizeY = faceArea.size.y * voxelBase.importScale.y;
                        var vOffset = vertices.Count;
                        vertices.Add(new Vector3(0, 0, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX, 0, 0) + pOffset);
                        vertices.Add(new Vector3(sizeX, sizeY, 0) + pOffset);
                        vertices.Add(new Vector3(0, sizeY, 0) + pOffset);
                        triangles.Add(vOffset + 2); triangles.Add(vOffset + 1); triangles.Add(vOffset + 0);
                        triangles.Add(vOffset + 3); triangles.Add(vOffset + 2); triangles.Add(vOffset + 0);
                        for (int j = 0; j < 4; j++)
                        {
                            if (faceArea.palette >= 0)
                                uv.Add(atlasRects[faceArea.palette].position);
                            normals.Add(Vector3.back);
                        }
                        if (isHaveBoneWeight)
                        {
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z))], VoxelBase.VoxelVertexIndex._X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z))], VoxelBase.VoxelVertexIndex.X_Y_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z))], VoxelBase.VoxelVertexIndex.XY_Z));
                            boneWeights.Add(GetBoneWeight(ref voxelBase.voxelData.voxels[voxelBase.voxelData.VoxelTableContains(faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z))], VoxelBase.VoxelVertexIndex._XY_Z));
                        }
                        if (dstList != null)
                        {
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex._X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.X_Y_Z), vertexIndex = VoxelBase.VoxelVertexIndex.X_Y_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex.XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex.XY_Z });
                            dstList.Add(new Edit_VerticesInfo() { position = faceArea.Get(VoxelBase.VoxelVertexIndex._XY_Z), vertexIndex = VoxelBase.VoxelVertexIndex._XY_Z });
                        }
                    }
                }
                #endregion
            }
            #endregion

            #region Mesh
            var result = new Mesh();
            {
                if (vertices.Count > 65000)
                {
                    const int Sepalate = 64999;
                    //Debug.LogWarningFormat("<color=green>[VoxelCharacteImporter]</color> Mesh.vertices is too large. A mesh may not have more than 65000 vertices. <color=red>{0} / 65000</color>", vertices.Count);
                    vertices.RemoveRange(Sepalate, vertices.Count - Sepalate);
                    if (uv.Count > Sepalate)
                        uv.RemoveRange(Sepalate, uv.Count - Sepalate);
                    if (normals.Count > Sepalate)
                        normals.RemoveRange(Sepalate, normals.Count - Sepalate);
                    if (isHaveBoneWeight)
                    {
                        if (boneWeights.Count > Sepalate)
                            boneWeights.RemoveRange(Sepalate, boneWeights.Count - Sepalate);
                    }
                    for (int i = triangles.Count - 1; i >= 0; i--)
                    {
                        if (triangles[i] < Sepalate)
                        {
                            int index = ((i / 3) * 3);
                            triangles.RemoveRange(index, triangles.Count - index);
                            break;
                        }
                    }
                    if (dstList != null)
                    {
                        dstList.RemoveRange(Sepalate, dstList.Count - Sepalate);
                    }
                }
                result.vertices = vertices.ToArray();
                result.uv = uv.ToArray();
                result.normals = normals.ToArray();
                if (isHaveBoneWeight)
                {
                    result.boneWeights = boneWeights.ToArray();
                    result.bindposes = GetBindposes();
                }
                result.triangles = triangles.ToArray();
                result.RecalculateBounds();
            }
            #endregion
            #endregion

            result.hideFlags = HideFlags.DontSave;

            return result;
        }
        #endregion
        #endregion

        #region CreateFaceArea
        protected VoxelData.FaceAreaTable CreateFaceArea(VoxelData.Voxel[] voxels)
        {
            VoxelData.FaceAreaTable result;
            if (voxelBase.importMode == VoxelBase.ImportMode.LowTexture)
                result = CreateFaceArea_LowTexture(voxels);
            else if (voxelBase.importMode == VoxelBase.ImportMode.LowPoly)
                result = CreateFaceData_LowPoly(voxels);
            else
            {
                Assert.IsFalse(false);
                result = new VoxelData.FaceAreaTable();
            }

            return result;
        }
        protected VoxelData.FaceAreaTable CreateFaceArea_LowTexture(VoxelData.Voxel[] voxels)
        {
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceX = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = materialIndexTable[x][y][z];
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //back
                for (int i = z - 1; ; i--)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[x][y][i] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.min.z = i;
                }
                //forward
                for (int i = z + 1; ; i++)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[x][y][i] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.max.z = i;
                }
                //down
                for (int i = y - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(x, i, j);
                        if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[x][i][j] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.y = i;
                }
                //up
                for (int i = y + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(x, i, j);
                        if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[x][i][j] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.y = i;
                }
                return area;
            };
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceY = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = materialIndexTable[x][y][z];
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //back
                for (int i = z - 1; ; i--)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[x][y][i] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.min.z = i;
                }
                //forward
                for (int i = z + 1; ; i++)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[x][y][i] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.max.z = i;
                }
                //left
                for (int i = x - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(i, y, j);
                        if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[i][y][j] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.x = i;
                }
                //right
                for (int i = x + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(i, y, j);
                        if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[i][y][j] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.x = i;
                }
                return area;
            };
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceZ = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = materialIndexTable[x][y][z];
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //up
                for (int i = y - 1; ; i--)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, i, z);
                    if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[x][i][z] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.min.y = i;
                }
                //down
                for (int i = y + 1; ; i++)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, i, z);
                    if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[x][i][z] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.max.y = i;
                }
                //left
                for (int i = x - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.y; j <= area.max.y; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(i, j, z);
                        if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[i][j][z] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.x = i;
                }
                //right
                for (int i = x + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.y; j <= area.max.y; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(i, j, z);
                        if (index < 0 || voxelBase.voxelData.voxels[index].palette != palette || materialIndexTable[i][j][z] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.x = i;
                }
                return area;
            };

            var result = new VoxelData.FaceAreaTable();
            var voxelIndexTable = GetVoxelIndexTable(voxels);

            #region forward
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.forward) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.forward) != 0) continue;
                    var faceArea = CalcFaceZ(i, VoxelBase.Face.forward);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.forward);
                    result.forward.Add(faceArea);
                }
            }
            #endregion
            #region up
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.up) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.up) != 0) continue;
                    var faceArea = CalcFaceY(i, VoxelBase.Face.up);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.up);
                    result.up.Add(faceArea);
                }
            }
            #endregion
            #region right
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.right) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.right) != 0) continue;
                    var faceArea = CalcFaceX(i, VoxelBase.Face.right);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.right);
                    result.right.Add(faceArea);
                }
            }
            #endregion
            #region left
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.left) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.left) != 0) continue;
                    var faceArea = CalcFaceX(i, VoxelBase.Face.left);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.left);
                    result.left.Add(faceArea);
                }
            }
            #endregion
            #region down
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.down) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.down) != 0) continue;
                    var faceArea = CalcFaceY(i, VoxelBase.Face.down);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.down);
                    result.down.Add(faceArea);
                }
            }
            #endregion
            #region back
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.back) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.back) != 0) continue;
                    var faceArea = CalcFaceZ(i, VoxelBase.Face.back);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.back);
                    result.back.Add(faceArea);
                }
            }
            #endregion

            return result;
        }
        protected VoxelData.FaceAreaTable CreateFaceData_LowPoly(VoxelData.Voxel[] voxels)
        {
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceX = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = materialIndexTable[x][y][z];
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //back
                for (int i = z - 1; ; i--)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || materialIndexTable[x][y][i] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.min.z = i;
                }
                //forward
                for (int i = z + 1; ; i++)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || materialIndexTable[x][y][i] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.max.z = i;
                }
                //down
                for (int i = y - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(x, i, j);
                        if (index < 0 || materialIndexTable[x][i][j] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.y = i;
                }
                //up
                for (int i = y + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(x, i, j);
                        if (index < 0 || materialIndexTable[x][i][j] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.y = i;
                }
                return area;
            };
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceY = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = materialIndexTable[x][y][z];
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //back
                for (int i = z - 1; ; i--)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || materialIndexTable[x][y][i] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.min.z = i;
                }
                //forward
                for (int i = z + 1; ; i++)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, y, i);
                    if (index < 0 || materialIndexTable[x][y][i] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.max.z = i;
                }
                //left
                for (int i = x - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(i, y, j);
                        if (index < 0 || materialIndexTable[i][y][j] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.x = i;
                }
                //right
                for (int i = x + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.z; j <= area.max.z; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(i, y, j);
                        if (index < 0 || materialIndexTable[i][y][j] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.x = i;
                }
                return area;
            };
            Func<int, VoxelBase.Face, VoxelData.FaceArea> CalcFaceZ = (baseIndex, flag) =>
            {
                var x = voxels[baseIndex].x;
                var y = voxels[baseIndex].y;
                var z = voxels[baseIndex].z;
                var palette = voxels[baseIndex].palette;
                var material = materialIndexTable[x][y][z];
                var area = new VoxelData.FaceArea() { min = new IntVector3(x, y, z), max = new IntVector3(x, y, z), palette = palette, material = material };
                //up
                for (int i = y - 1; ; i--)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, i, z);
                    if (index < 0 || materialIndexTable[x][i][z] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.min.y = i;
                }
                //down
                for (int i = y + 1; ; i++)
                {
                    var index = voxelBase.voxelData.VoxelTableContains(x, i, z);
                    if (index < 0 || materialIndexTable[x][i][z] != material ||
                        (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) break;
                    area.max.y = i;
                }
                //left
                for (int i = x - 1; ; i--)
                {
                    bool r = true;
                    for (int j = area.min.y; j <= area.max.y; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(i, j, z);
                        if (index < 0 || materialIndexTable[i][j][z] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.min.x = i;
                }
                //right
                for (int i = x + 1; ; i++)
                {
                    bool r = true;
                    for (int j = area.min.y; j <= area.max.y; j++)
                    {
                        var index = voxelBase.voxelData.VoxelTableContains(i, j, z);
                        if (index < 0 || materialIndexTable[i][j][z] != material ||
                            (voxelDoneFaces[index] & flag) != 0 || !IsCombineVoxelFace(voxels[baseIndex].position, voxelBase.voxelData.voxels[index].position, flag)) { r = false; break; }
                    }
                    if (!r) break;
                    area.max.x = i;
                }
                return area;
            };

            var result = new VoxelData.FaceAreaTable();
            var voxelIndexTable = GetVoxelIndexTable(voxels);

            #region forward
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.forward) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.forward) != 0) continue;
                    var faceArea = CalcFaceZ(i, VoxelBase.Face.forward);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.forward);
                    result.forward.Add(faceArea);
                }
            }
            #endregion
            #region up
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.up) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.up) != 0) continue;
                    var faceArea = CalcFaceY(i, VoxelBase.Face.up);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.up);
                    result.up.Add(faceArea);
                }
            }
            #endregion
            #region right
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.right) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.right) != 0) continue;
                    var faceArea = CalcFaceX(i, VoxelBase.Face.right);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.right);
                    result.right.Add(faceArea);
                }
            }
            #endregion
            #region left
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.left) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.left) != 0) continue;
                    var faceArea = CalcFaceX(i, VoxelBase.Face.left);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.left);
                    result.left.Add(faceArea);
                }
            }
            #endregion
            #region down
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.down) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.down) != 0) continue;
                    var faceArea = CalcFaceY(i, VoxelBase.Face.down);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.down);
                    result.down.Add(faceArea);
                }
            }
            #endregion
            #region back
            if ((voxelBase.enableFaceFlags & VoxelBase.Face.back) != 0)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if ((voxelDoneFaces[voxelIndexTable[i]] & VoxelBase.Face.back) != 0) continue;
                    var faceArea = CalcFaceZ(i, VoxelBase.Face.back);
                    SetDoneFacesFlag(faceArea, VoxelBase.Face.back);
                    result.back.Add(faceArea);
                }
            }
            #endregion

            return result;
        }
        #endregion

        #region BoneWeight
        protected virtual bool isHaveBoneWeight { get { return false; } }
        protected virtual Matrix4x4[] GetBindposes() { return null; }
        protected virtual BoneWeight GetBoneWeight(ref VoxelData.Voxel voxel, VoxelBase.VoxelVertexIndex index) { return new BoneWeight(); }
        #endregion

        #region Voxel
        public Vector3 GetVoxelRatePosition(IntVector3 pos, Vector3 rate)
        {
            Vector3 posV3 = new Vector3(pos.x, pos.y, pos.z);
            return Vector3.Scale(voxelBase.localOffset + voxelBase.importOffset + rate + posV3, voxelBase.importScale);
        }
        public Vector3 GetVoxelCenterPosition(IntVector3 pos)
        {
            return GetVoxelRatePosition(pos, new Vector3(0.5f, 0.5f, 0.5f));
        }
        public VoxelBase.VoxelVertices GetVoxelVertices(IntVector3 pos)
        {
            var min = GetVoxelRatePosition(pos, Vector3.zero);
            var max = GetVoxelRatePosition(pos, Vector3.one);
            VoxelBase.VoxelVertices vertices = new VoxelBase.VoxelVertices();
            vertices.vertexXYZ = new Vector3(max.x, max.y, max.z); //XYZ,
            vertices.vertex_XYZ = new Vector3(min.x, max.y, max.z); //_XYZ,
            vertices.vertexX_YZ = new Vector3(max.x, min.y, max.z); //X_YZ,
            vertices.vertexXY_Z = new Vector3(max.x, max.y, min.z); //XY_Z,
            vertices.vertex_X_YZ = new Vector3(min.x, min.y, max.z); //_X_YZ,
            vertices.vertex_XY_Z = new Vector3(min.x, max.y, min.z); //_XY_Z,
            vertices.vertexX_Y_Z = new Vector3(max.x, min.y, min.z); //X_Y_Z,
            vertices.vertex_X_Y_Z = new Vector3(min.x, min.y, min.z); //_X_Y_Z,
            return vertices;
        }
        public Bounds GetVoxelBounds(IntVector3 pos)
        {
            Bounds bounds = new Bounds();
            bounds.SetMinMax(GetVoxelRatePosition(pos, Vector3.zero), GetVoxelRatePosition(pos, Vector3.one));
            return bounds;
        }
        public BoundingSphere GetVoxelBoundingSphere(IntVector3 pos)
        {
            var min = GetVoxelRatePosition(pos, Vector3.zero);
            var max = GetVoxelRatePosition(pos, Vector3.one);
            return new BoundingSphere(Vector3.Lerp(min, max, 0.5f), (max - min).magnitude * 0.5f);
        }
        public Vector3 GetVoxelPosition(Vector3 localPosition)
        {
            return new Vector3(localPosition.x / voxelBase.importScale.x, localPosition.y / voxelBase.importScale.y, localPosition.z / voxelBase.importScale.z) - (voxelBase.localOffset + voxelBase.importOffset);
        }
        #endregion

        #region Edit
        public void AutoSetSelectedWireframeHidden()
        {
            SetSelectedWireframeHidden(voxelBase.edit_configureMode != VoxelBase.Edit_configureMode.None);
        }
        public virtual void SetSelectedWireframeHidden(bool hidden)
        {
            if (voxelBase != null)
            {
                var renderer = voxelBase.GetComponent<Renderer>();
                if (renderer != null)
                    EditorUtility.SetSelectedWireframeHidden(renderer, hidden);
            }
        }
        #endregion

        #region Undo
        protected virtual void RefreshCheckerCreate() { voxelBase.refreshChecker = new VoxelBase.RefreshChecker(voxelBase); }
        public void RefreshCheckerClear() { voxelBase.refreshChecker = null; }
        public void RefreshCheckerSave() { if (voxelBase.refreshChecker == null) { RefreshCheckerCreate(); } voxelBase.refreshChecker.Save(); }
        public bool RefreshCheckerCheck() { return voxelBase.refreshChecker != null ? voxelBase.refreshChecker.Check() : false; }
        #endregion
    }
}
