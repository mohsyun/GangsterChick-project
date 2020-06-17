using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace VoxelImporter
{
    public class VoxelChunksObjectCore : VoxelBaseCore
    {
        public VoxelChunksObjectCore(VoxelBase target) : base(target)
        {
            voxelObject = target as VoxelChunksObject;
        }

        public VoxelChunksObject voxelObject { get; protected set; }

        #region Chunk
        protected IntVector3 GetChunkPosition(IntVector3 position)
        {
            IntVector3 cpos = IntVector3.zero;
            cpos.x = position.x / voxelObject.chunkSize.x;
            cpos.y = position.y / voxelObject.chunkSize.y;
            cpos.z = position.z / voxelObject.chunkSize.z;
            return cpos;
        }

        protected class ChunkData
        {
            public IntVector3 position;
            public string name;
            public ChunkArea area;
            public List<int> voxels;
            public Color[] palettes;

            public VoxelChunksObjectChunk chunkObject;
            public VoxelData.FaceAreaTable faceAreaTable;

            public Rect[] atlasRects;
            public AtlasRectTable atlasRectTable;
        }
        protected List<ChunkData> chunkDataList;
        protected IntVector3[,,] voxelChunkPositionTable;

        protected override void CreateChunkData()
        {
            var chunkVoxels = new Dictionary<IntVector3, List<int>>();
            var chunkPalettes = new Dictionary<IntVector3, HashSet<Color>>();
            voxelChunkPositionTable = new IntVector3[voxelObject.voxelData.voxelSize.x, voxelObject.voxelData.voxelSize.y, voxelObject.voxelData.voxelSize.z];
            for (int i = 0; i < voxelObject.voxelData.voxels.Length; i++)
            {
                var chunkPosition = GetChunkPosition(voxelObject.voxelData.voxels[i].position);
                //voxel
                if (!chunkVoxels.ContainsKey(chunkPosition))
                    chunkVoxels.Add(chunkPosition, new List<int>());
                chunkVoxels[chunkPosition].Add(i);
                //palette
                if (!chunkPalettes.ContainsKey(chunkPosition))
                    chunkPalettes.Add(chunkPosition, new HashSet<Color>());
                chunkPalettes[chunkPosition].Add(voxelObject.voxelData.palettes[voxelObject.voxelData.voxels[i].palette]);
                //
                voxelChunkPositionTable[voxelObject.voxelData.voxels[i].x, voxelObject.voxelData.voxels[i].y, voxelObject.voxelData.voxels[i].z] = chunkPosition;
            }
            {
                chunkDataList = new List<ChunkData>(chunkVoxels.Count);
                var enu = chunkVoxels.GetEnumerator();
                while (enu.MoveNext())
                {
                    chunkDataList.Add(new ChunkData()
                    {
                        position = enu.Current.Key,
                        name = string.Format("Chunk({0}, {1}, {2})", enu.Current.Key.x, enu.Current.Key.y, enu.Current.Key.z),
                        area = new ChunkArea() { min = enu.Current.Key * voxelObject.chunkSize, max = (enu.Current.Key + IntVector3.one) * voxelObject.chunkSize - IntVector3.one },
                        voxels = enu.Current.Value,
                        palettes = chunkPalettes[enu.Current.Key].ToArray()
                    });
                }
                chunkDataList.Sort((a, b) => a.position.x != b.position.x ? a.position.x - b.position.x : a.position.y != b.position.y ? a.position.y - b.position.y : a.position.z - b.position.z );
            }
        }

        private void CreateVoxelChunkPositionTable()
        {
            if (voxelChunkPositionTable != null) return;
            voxelChunkPositionTable = new IntVector3[voxelObject.voxelData.voxelSize.x, voxelObject.voxelData.voxelSize.y, voxelObject.voxelData.voxelSize.z];
            for (int i = 0; i < voxelObject.voxelData.voxels.Length; i++)
            {
                var chunkPosition = GetChunkPosition(voxelObject.voxelData.voxels[i].position);
                voxelChunkPositionTable[voxelObject.voxelData.voxels[i].x, voxelObject.voxelData.voxels[i].y, voxelObject.voxelData.voxels[i].z] = chunkPosition;
            }
        }

        public VoxelChunksObjectChunk[] FindChunkComponents()
        {
            if (voxelObject == null)
                return new VoxelChunksObjectChunk[0];
            List<VoxelChunksObjectChunk> list = new List<VoxelChunksObjectChunk>();
            var all = Resources.FindObjectsOfTypeAll<VoxelChunksObjectChunk>();
            for (int i = 0; i < all.Length; i++)
            {
                if (voxelObject.transform == all[i].transform.parent)
                    list.Add(all[i]);
            }
            return list.ToArray();
        }

        public bool removeAllChunk;
        public void RemoveAllChunk()
        {
            removeAllChunk = true;
        }
        #endregion

        #region CreateVoxel
        protected override bool LoadVoxelDataFromQB(BinaryReader br)
        {
            if (voxelObject.splitMode == VoxelChunksObject.SplitMode.QubicleMatrix)
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

                //Chunk
                int chunkIndex = -1;
                List<int> chunkVoxelList = new List<int>();
                List<string> chunkNameList = new List<string>();

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

                            //Chunk
                            chunkVoxelList.Add(chunkIndex);
                        }
                    }
                };

                for (int i = 0; i < numMatrices; i++)
                {
                    var nameLength = br.ReadByte();
                    var name = new string(br.ReadChars(nameLength));
                    var sizeX = br.ReadUInt32();
                    var sizeY = br.ReadUInt32();
                    var sizeZ = br.ReadUInt32();
                    var posX = br.ReadInt32();
                    var posY = br.ReadInt32();
                    var posZ = br.ReadInt32();

                    //Chunk
                    chunkIndex = i;
                    chunkNameList.Add(name);

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
                #region CreateChunkData
                {
                    Dictionary<int, ChunkArea> chunkAreaList = new Dictionary<int, ChunkArea>();
                    for (int i = 0; i < voxelList.Count; i++)
                    {
                        if(!chunkAreaList.ContainsKey(chunkVoxelList[i]))
                        {
                            chunkAreaList.Add(chunkVoxelList[i], new ChunkArea() { min = new IntVector3(int.MaxValue, int.MaxValue, int.MaxValue), max = new IntVector3(int.MinValue, int.MinValue, int.MinValue) });
                        }
                        var chunkArea = chunkAreaList[chunkVoxelList[i]];
                        chunkArea.min = IntVector3.Min(chunkArea.min, voxelList[i].position);
                        chunkArea.max = IntVector3.Max(chunkArea.max, voxelList[i].position);
                        chunkAreaList[chunkVoxelList[i]] = chunkArea;
                    }

                    var chunkVoxels = new Dictionary<int, List<int>>();
                    var chunkPalettes = new Dictionary<int, HashSet<Color>>();
                    voxelChunkPositionTable = new IntVector3[voxelObject.voxelData.voxelSize.x, voxelObject.voxelData.voxelSize.y, voxelObject.voxelData.voxelSize.z];
                    for (int i = 0; i < voxelObject.voxelData.voxels.Length; i++)
                    {
                        chunkIndex = chunkVoxelList[i];
                        //voxel
                        if (!chunkVoxels.ContainsKey(chunkIndex))
                            chunkVoxels.Add(chunkIndex, new List<int>());
                        chunkVoxels[chunkIndex].Add(i);
                        //palette
                        if (!chunkPalettes.ContainsKey(chunkIndex))
                            chunkPalettes.Add(chunkIndex, new HashSet<Color>());
                        chunkPalettes[chunkIndex].Add(voxelObject.voxelData.palettes[voxelObject.voxelData.voxels[i].palette]);
                        //
                        voxelChunkPositionTable[voxelObject.voxelData.voxels[i].x, voxelObject.voxelData.voxels[i].y, voxelObject.voxelData.voxels[i].z] = new IntVector3(chunkIndex, 0, 0);
                    }
                    {
                        chunkDataList = new List<ChunkData>(chunkVoxels.Count);
                        var enu = chunkVoxels.GetEnumerator();
                        while (enu.MoveNext())
                        {
                            chunkDataList.Add(new ChunkData()
                            {
                                position = new IntVector3(enu.Current.Key, int.MinValue, int.MinValue),
                                name = string.Format("Chunk({0})", chunkNameList[enu.Current.Key]),
                                area = chunkAreaList[enu.Current.Key],
                                voxels = enu.Current.Value,
                                palettes = chunkPalettes[enu.Current.Key].ToArray()
                            });
                        }
                        chunkDataList.Sort((a, b) => string.Compare(a.name, b.name));
                    }
                }
                #endregion

                return true;
            }
            else
            {
                return base.LoadVoxelDataFromQB(br);
            }
        }
        public override string GetDefaultPath()
        {
            var path = base.GetDefaultPath();
            if (voxelObject != null)
            {
                if (voxelObject.materials != null)
                {
                    for (int i = 0; i < voxelObject.materials.Count; i++)
                    {
                        if (AssetDatabase.Contains(voxelObject.materials[i]))
                        {
                            var assetPath = AssetDatabase.GetAssetPath(voxelObject.materials[i]);
                            if (!string.IsNullOrEmpty(assetPath))
                            {
                                path = Path.GetDirectoryName(assetPath);
                            }
                        }
                    }
                }
                if (voxelObject.atlasTexture != null && AssetDatabase.Contains(voxelObject.atlasTexture))
                {
                    var assetPath = AssetDatabase.GetAssetPath(voxelObject.atlasTexture);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        path = Path.GetDirectoryName(assetPath);
                    }
                }
            }
            return path;
        }
        #endregion

        #region CreateMesh
        protected override bool IsCombineVoxelFace(IntVector3 basePos, IntVector3 combinePos, VoxelBase.Face face)
        {
            if (voxelChunkPositionTable == null)
                CreateVoxelChunkPositionTable();

            return voxelChunkPositionTable[basePos.x, basePos.y, basePos.z] == voxelChunkPositionTable[combinePos.x, combinePos.y, combinePos.z];
        }
        protected override bool IsHiddenVoxelFace(IntVector3 basePos, VoxelBase.Face faceFlag)
        {
            if (voxelObject.createContactChunkFaces)
            {
                if (voxelChunkPositionTable == null)
                    CreateVoxelChunkPositionTable();

                Assert.IsTrue(faceFlag == VoxelBase.Face.forward || faceFlag == VoxelBase.Face.up || faceFlag == VoxelBase.Face.right || faceFlag == VoxelBase.Face.left || faceFlag == VoxelBase.Face.down || faceFlag == VoxelBase.Face.back);
                IntVector3 combinePos = basePos;
                {
                    if (faceFlag == VoxelBase.Face.forward) combinePos.z++;
                    if (faceFlag == VoxelBase.Face.up) combinePos.y++;
                    if (faceFlag == VoxelBase.Face.right) combinePos.x++;
                    if (faceFlag == VoxelBase.Face.left) combinePos.x--;
                    if (faceFlag == VoxelBase.Face.down) combinePos.y--;
                    if (faceFlag == VoxelBase.Face.back) combinePos.z--;
                }
                return voxelChunkPositionTable[basePos.x, basePos.y, basePos.z] == voxelChunkPositionTable[combinePos.x, combinePos.y, combinePos.z];
            }
            else
            {
                return base.IsHiddenVoxelFace(basePos, faceFlag);
            }
        }
        protected override bool CreateMesh()
        {
            #region ProgressBar
            const float MaxProgressCount = 5f;
            float ProgressCount = 0;
            Action<string> DisplayProgressBar = (info) =>
            {
                if (!EditorApplication.isPlaying && voxelBase.voxelData.voxels.Length > 10000)
                    EditorUtility.DisplayProgressBar("Create Mesh...", string.Format("{0} / {1}", ProgressCount, MaxProgressCount), (ProgressCount++ / MaxProgressCount));
            };
            #endregion

            DisplayProgressBar("");

            #region Material
            {
                if (voxelObject.materialData == null)
                    voxelObject.materialData = new List<MaterialData>();
                if (voxelObject.materialData.Count == 0)
                    voxelObject.materialData.Add(null);
                for (int i = 0; i < voxelObject.materialData.Count; i++)
                {
                    if (voxelObject.materialData[i] == null)
                        voxelObject.materialData[i] = new MaterialData();
                }
                if (voxelObject.materials == null)
                    voxelObject.materials = new List<Material>();
                if (voxelObject.materials.Count < voxelObject.materialData.Count)
                {
                    for (int i = voxelObject.materials.Count; i < voxelObject.materialData.Count; i++)
                        voxelObject.materials.Add(null);
                }
                else if (voxelObject.materials.Count > voxelObject.materialData.Count)
                {
                    voxelObject.materials.RemoveRange(voxelObject.materialData.Count, voxelObject.materials.Count - voxelObject.materialData.Count);
                }
                #region Erase
                for (int i = 0; i < voxelObject.materialData.Count; i++)
                {
                    List<IntVector3> removeList = new List<IntVector3>();
                    voxelObject.materialData[i].AllAction((pos) =>
                    {
                        if (voxelObject.voxelData.VoxelTableContains(pos) < 0)
                        {
                            removeList.Add(pos);
                        }
                    });
                    for (int j = 0; j < removeList.Count; j++)
                    {
                        voxelObject.materialData[i].RemoveMaterial(removeList[j]);
                    }
                }
                #endregion
            }
            #endregion

            CalcDataCreate(voxelBase.voxelData.voxels);

            #region RemoveChunk
            var chunkObjects = FindChunkComponents();
            {
                bool chunkObjectsUpdate = false;
                bool[] enableTale = new bool[chunkObjects.Length];
                if (!removeAllChunk)
                {
                    for (int i = 0; i < chunkDataList.Count; i++)
                    {
                        for (int j = 0; j < chunkObjects.Length; j++)
                        {
                            if (chunkDataList[i].position == chunkObjects[j].position)
                            {
                                enableTale[j] = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    removeAllChunk = false;
                }
                for (int i = 0; i < enableTale.Length; i++)
                {
                    if (!enableTale[i])
                    {
                        var go = chunkObjects[i].gameObject;
                        while (go.transform.childCount > 0)
                        {
                            Undo.SetTransformParent(go.transform.GetChild(0), voxelObject.transform, "Remove Chunk");
                        }
                        Undo.DestroyObjectImmediate(go);
                        chunkObjectsUpdate = true;
                    }
                }
                if (chunkObjectsUpdate)
                    chunkObjects = FindChunkComponents();
            }
            #endregion

            #region AddChunk
            int chunkCount = 0;
            {
                bool chunkObjectsUpdate = false;
                for (int i = 0; i < chunkDataList.Count; i++)
                {
                    GameObject chunkObject = null;
                    for (int j = 0; j < chunkObjects.Length; j++)
                    {
                        if (chunkDataList[i].position == chunkObjects[j].position)
                        {
                            chunkObject = chunkObjects[j].gameObject;
                            break;
                        }
                    }
                    if (chunkObject == null)
                    {
                        chunkObject = new GameObject(chunkDataList[i].name);
                        Undo.RegisterCreatedObjectUndo(chunkObject, "Create Chunk");
                        Undo.SetTransformParent(chunkObject.transform, voxelObject.transform, "Create Chunk");
                        GameObjectUtility.SetStaticEditorFlags(chunkObject, GameObjectUtility.GetStaticEditorFlags(voxelObject.gameObject));
                        chunkObject.transform.localPosition = Vector3.Scale(voxelObject.localOffset + chunkDataList[i].area.centerf, voxelObject.importScale);
                        chunkObject.transform.localRotation = Quaternion.identity;
                        chunkObject.transform.localScale = Vector3.one;
                        chunkObject.layer = voxelObject.gameObject.layer;
                        chunkObject.tag = voxelObject.gameObject.tag;
                        chunkObjectsUpdate = true;
                    }
                    VoxelChunksObjectChunk controller = chunkObject.GetComponent<VoxelChunksObjectChunk>();
                    if (controller == null)
                        controller = Undo.AddComponent<VoxelChunksObjectChunk>(chunkObject);
                    controller.position = chunkDataList[i].position;
                    controller.chunkName = chunkDataList[i].name;
                    chunkCount++;
                }
                if (chunkObjectsUpdate)
                    chunkObjects = FindChunkComponents();
            }
            #endregion

            #region SortChunk
            {
                List<Transform> objList = new List<Transform>();
                var childCount = voxelObject.transform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    objList.Add(voxelObject.transform.GetChild(i));
                }
                objList.Sort((obj1, obj2) => string.Compare(obj1.name, obj2.name));
                for (int i = 0; i < objList.Count; i++)
                {
                    objList[i].SetSiblingIndex(childCount - 1);
                }
                chunkObjects = FindChunkComponents();
            }
            #endregion

            #region UpdateChunk
            for (int i = 0; i < chunkObjects.Length; i++)
            {
                for (int j = 0; j < chunkDataList.Count; j++)
                {
                    if (chunkObjects[i].position == chunkDataList[j].position)
                    {
                        chunkDataList[j].chunkObject = chunkObjects[i];
                        break;
                    }
                }
            }
            #endregion
            
            #region UpdateVoxelVisibleFlag
            {
                for (int i = 0; i < voxelBase.voxelData.voxels.Length; i++)
                {
                    int index;
                    VoxelBase.Face faceFlags = (VoxelBase.Face)0;
                    index = voxelBase.voxelData.VoxelTableContains(voxelBase.voxelData.voxels[i].x, voxelBase.voxelData.voxels[i].y, voxelBase.voxelData.voxels[i].z + 1);
                    if (index < 0)
                        faceFlags |= VoxelBase.Face.forward;
                    index = voxelBase.voxelData.VoxelTableContains(voxelBase.voxelData.voxels[i].x, voxelBase.voxelData.voxels[i].y + 1, voxelBase.voxelData.voxels[i].z);
                    if (index < 0)
                        faceFlags |= VoxelBase.Face.up;
                    index = voxelBase.voxelData.VoxelTableContains(voxelBase.voxelData.voxels[i].x + 1, voxelBase.voxelData.voxels[i].y, voxelBase.voxelData.voxels[i].z);
                    if (index < 0)
                        faceFlags |= VoxelBase.Face.right;
                    index = voxelBase.voxelData.VoxelTableContains(voxelBase.voxelData.voxels[i].x - 1, voxelBase.voxelData.voxels[i].y, voxelBase.voxelData.voxels[i].z);
                    if (index < 0)
                        faceFlags |= VoxelBase.Face.left;
                    index = voxelBase.voxelData.VoxelTableContains(voxelBase.voxelData.voxels[i].x, voxelBase.voxelData.voxels[i].y - 1, voxelBase.voxelData.voxels[i].z);
                    if (index < 0)
                        faceFlags |= VoxelBase.Face.down;
                    index = voxelBase.voxelData.VoxelTableContains(voxelBase.voxelData.voxels[i].x, voxelBase.voxelData.voxels[i].y, voxelBase.voxelData.voxels[i].z - 1);
                    if (index < 0)
                        faceFlags |= VoxelBase.Face.back;
                    voxelBase.voxelData.voxels[i].visible = faceFlags;
                }
            }
            #endregion

            #region CreateFaceAreaTable
            {
                for (int i = 0; i < chunkDataList.Count; i++)
                {
                    var voxels = new VoxelData.Voxel[chunkDataList[i].voxels.Count];
                    for (int j = 0; j < chunkDataList[i].voxels.Count; j++)
                    {
                        voxels[j] = voxelBase.voxelData.voxels[chunkDataList[i].voxels[j]];
                    }
                    chunkDataList[i].faceAreaTable = CreateFaceArea(voxels);
                    //
                    if (voxelObject.materialMode == VoxelChunksObject.MaterialMode.Individual)
                    {
                        var paletteTable = new int[voxelBase.voxelData.palettes.Length];
                        for (int j = 0; j < voxelBase.voxelData.palettes.Length; j++)
                        {
                            int newIndex = -1;
                            for (int k = 0; k < chunkDataList[i].palettes.Length; k++)
                            {
                                if (chunkDataList[i].palettes[k] == voxelBase.voxelData.palettes[j])
                                {
                                    newIndex = k;
                                    break;
                                }
                            }
                            paletteTable[j] = newIndex;
                        }
                        chunkDataList[i].faceAreaTable.ReplacePalette(paletteTable);
                    }
                }
            }
            #endregion

            #region CreateTexture
            if (voxelObject.materialMode == VoxelChunksObject.MaterialMode.Combine)
            {
                #region Combine
                var tmpFaceAreaTable = new VoxelData.FaceAreaTable();
                for (int i = 0; i < chunkDataList.Count; i++)
                {
                    tmpFaceAreaTable.Merge(chunkDataList[i].faceAreaTable);
                }
                {
                    var atlasTextureTmp = voxelObject.atlasTexture;
                    if (!CreateTexture(tmpFaceAreaTable, voxelBase.voxelData.palettes, ref chunkDataList[0].atlasRectTable, ref atlasTextureTmp, ref chunkDataList[0].atlasRects))
                    {
                        EditorUtility.ClearProgressBar();
                        return false;
                    }
                    voxelObject.atlasTexture = atlasTextureTmp;
                    {
                        if (voxelObject.materialData == null)
                            voxelObject.materialData = new List<MaterialData>();
                        if (voxelObject.materialData.Count == 0)
                            voxelObject.materialData.Add(null);
                        for (int i = 0; i < voxelObject.materialData.Count; i++)
                        {
                            if (voxelObject.materialData[i] == null)
                                voxelObject.materialData[i] = new MaterialData();
                        }
                        if (voxelObject.materials == null)
                            voxelObject.materials = new List<Material>();
                        if (voxelObject.materials.Count < voxelObject.materialData.Count)
                        {
                            for (int i = voxelObject.materials.Count; i < voxelObject.materialData.Count; i++)
                                voxelObject.materials.Add(null);
                        }
                        else if (voxelObject.materials.Count > voxelObject.materialData.Count)
                        {
                            voxelObject.materials.RemoveRange(voxelObject.materialData.Count, voxelObject.materials.Count - voxelObject.materialData.Count);
                        }
                        for (int i = 0; i < voxelObject.materials.Count; i++)
                        {
                            if (voxelObject.materials[i] == null)
                                voxelObject.materials[i] = new Material(Shader.Find("Standard"));
                            voxelObject.materials[i].mainTexture = voxelObject.atlasTexture;
                        }
                    }
                    for (int i = 0; i < chunkDataList.Count; i++)
                    {
                        chunkDataList[i].atlasRects = chunkDataList[0].atlasRects;
                        chunkDataList[i].atlasRectTable = chunkDataList[0].atlasRectTable;
                        chunkDataList[i].chunkObject.materials = null;
                        chunkDataList[i].chunkObject.atlasTexture = null;
                    }
                }
                #endregion
            }
            else if (voxelObject.materialMode == VoxelChunksObject.MaterialMode.Individual)
            {
                #region Individual
                if (voxelObject.materialData == null)
                    voxelObject.materialData = new List<MaterialData>();
                if (voxelObject.materialData.Count == 0)
                    voxelObject.materialData.Add(null);
                for (int i = 0; i < voxelObject.materialData.Count; i++)
                {
                    if (voxelObject.materialData[i] == null)
                        voxelObject.materialData[i] = new MaterialData();
                }
                voxelObject.materials = null;
                voxelObject.atlasTexture = null;
                for (int c = 0; c < chunkDataList.Count; c++)
                {
                    var atlasTextureTmp = chunkDataList[c].chunkObject.atlasTexture;
                    if (!CreateTexture(chunkDataList[c].faceAreaTable, chunkDataList[c].palettes, ref chunkDataList[c].atlasRectTable, ref atlasTextureTmp, ref chunkDataList[c].atlasRects))
                    {
                        EditorUtility.ClearProgressBar();
                        return false;
                    }
                    chunkDataList[c].chunkObject.atlasTexture = atlasTextureTmp;
                    {
                        if (chunkDataList[c].chunkObject.materials == null)
                            chunkDataList[c].chunkObject.materials = new List<Material>();
                        if (chunkDataList[c].chunkObject.materials.Count < voxelObject.materialData.Count)
                        {
                            for (int i = chunkDataList[c].chunkObject.materials.Count; i < voxelObject.materialData.Count; i++)
                                chunkDataList[c].chunkObject.materials.Add(null);
                        }
                        else if (chunkDataList[c].chunkObject.materials.Count > voxelObject.materialData.Count)
                        {
                            chunkDataList[c].chunkObject.materials.RemoveRange(voxelObject.materialData.Count, chunkDataList[c].chunkObject.materials.Count - voxelObject.materialData.Count);
                        }
                        for (int i = 0; i < chunkDataList[c].chunkObject.materials.Count; i++)
                        {
                            if (chunkDataList[c].chunkObject.materials[i] == null)
                                chunkDataList[c].chunkObject.materials[i] = new Material(Shader.Find("Standard"));
                            chunkDataList[c].chunkObject.materials[i].mainTexture = chunkDataList[c].chunkObject.atlasTexture;
                        }
                    }
                }
                #endregion
            }
            else
            {
                Assert.IsTrue(false);
            }
            #endregion

            #region CreateMesh
            DisplayProgressBar("");
            {
                if (voxelObject.materialMode == VoxelChunksObject.MaterialMode.Combine)
                {
                    #region Combine
                    if (voxelObject.importMode == VoxelBase.ImportMode.LowPoly)
                    {
                        int forward = 0;
                        int up = 0;
                        int right = 0;
                        int left = 0;
                        int down = 0;
                        int back = 0;
                        for (int i = 0; i < chunkDataList.Count; i++)
                        {
                            AtlasRectTable atlasRectTable = new AtlasRectTable();
                            {
                                atlasRectTable.forward = chunkDataList[i].atlasRectTable.forward.GetRange(forward, chunkDataList[i].faceAreaTable.forward.Count);
                                forward += chunkDataList[i].faceAreaTable.forward.Count;
                                atlasRectTable.up = chunkDataList[i].atlasRectTable.up.GetRange(up, chunkDataList[i].faceAreaTable.up.Count);
                                up += chunkDataList[i].faceAreaTable.up.Count;
                                atlasRectTable.right = chunkDataList[i].atlasRectTable.right.GetRange(right, chunkDataList[i].faceAreaTable.right.Count);
                                right += chunkDataList[i].faceAreaTable.right.Count;
                                atlasRectTable.left = chunkDataList[i].atlasRectTable.left.GetRange(left, chunkDataList[i].faceAreaTable.left.Count);
                                left += chunkDataList[i].faceAreaTable.left.Count;
                                atlasRectTable.down = chunkDataList[i].atlasRectTable.down.GetRange(down, chunkDataList[i].faceAreaTable.down.Count);
                                down += chunkDataList[i].faceAreaTable.down.Count;
                                atlasRectTable.back = chunkDataList[i].atlasRectTable.back.GetRange(back, chunkDataList[i].faceAreaTable.back.Count);
                                back += chunkDataList[i].faceAreaTable.back.Count;
                            }
                            chunkDataList[i].chunkObject.mesh = CreateMeshOnly(chunkDataList[i].chunkObject.mesh, chunkDataList[i].faceAreaTable, voxelObject.atlasTexture, chunkDataList[i].atlasRects, atlasRectTable, -(voxelObject.localOffset + chunkDataList[i].area.centerf), out chunkDataList[i].chunkObject.materialIndexes);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < chunkDataList.Count; i++)
                        {
                            var srcMesh = (chunkDataList[i].chunkObject.mesh != null && AssetDatabase.Contains(chunkDataList[i].chunkObject.mesh)) ? chunkDataList[i].chunkObject.mesh : null;
                            chunkDataList[i].chunkObject.mesh = CreateMeshOnly(srcMesh, chunkDataList[i].faceAreaTable, voxelObject.atlasTexture, chunkDataList[i].atlasRects, chunkDataList[i].atlasRectTable, -(voxelObject.localOffset + chunkDataList[i].area.centerf), out chunkDataList[i].chunkObject.materialIndexes);
                        }
                    }
                    #endregion
                }
                else if (voxelObject.materialMode == VoxelChunksObject.MaterialMode.Individual)
                {
                    #region Individual
                    for (int i = 0; i < chunkDataList.Count; i++)
                    {
                        chunkDataList[i].chunkObject.mesh = CreateMeshOnly(chunkDataList[i].chunkObject.mesh, chunkDataList[i].faceAreaTable, chunkDataList[i].chunkObject.atlasTexture, chunkDataList[i].atlasRects, chunkDataList[i].atlasRectTable, -(voxelObject.localOffset + chunkDataList[i].area.centerf), out chunkDataList[i].chunkObject.materialIndexes);
                    }
                    #endregion
                }
                else
                {
                    Assert.IsTrue(false);
                }
            }
            #endregion

            DisplayProgressBar("");
            if (voxelBase.generateLightmapUVs)
            {
                for (int i = 0; i < chunkDataList.Count; i++)
                {
                    if (chunkDataList[i].chunkObject.mesh.uv.Length > 0)
                        Unwrapping.GenerateSecondaryUVSet(chunkDataList[i].chunkObject.mesh);
                }
            }

            DisplayProgressBar("");

            SetRendererCompornent();
            
            CreateMeshAfterFree();

            RefreshCheckerSave();

            EditorUtility.ClearProgressBar();
            
            return true;
        }
        protected override void CreateMeshAfterFree()
        {
            base.CreateMeshAfterFree();

            chunkDataList = null;

            GC.Collect();
        }
        public override void SetRendererCompornent()
        {
            var chunkObjects = FindChunkComponents();
            if (voxelObject.materialMode == VoxelChunksObject.MaterialMode.Combine)
            {
                if (voxelObject.materials != null)
                {
                    for (int i = 0; i < voxelObject.materials.Count; i++)
                    {
                        Undo.RecordObject(voxelObject.materials[i], "Inspector");
                        voxelObject.materials[i].mainTexture = voxelObject.atlasTexture;
                    }
                }
                for (int i = 0; i < chunkObjects.Length; i++)
                {
                    var meshFilter = chunkObjects[i].GetComponent<MeshFilter>();
                    Undo.RecordObject(meshFilter, "Inspector");
                    meshFilter.sharedMesh = chunkObjects[i].mesh;
                    var renderer = chunkObjects[i].GetComponent<Renderer>();
                    Undo.RecordObject(renderer, "Inspector");
                    if (voxelObject.materials != null)
                    {
                        Material[] tmps = new Material[chunkObjects[i].materialIndexes.Count];
                        for (int j = 0; j < chunkObjects[i].materialIndexes.Count; j++)
                        {
                            tmps[j] = voxelObject.materials[chunkObjects[i].materialIndexes[j]];
                        }
                        renderer.sharedMaterials = tmps;
                    }
                    else
                    {
                        renderer.sharedMaterial = null;
                    }
                }
            }
            else if (voxelObject.materialMode == VoxelChunksObject.MaterialMode.Individual)
            {
                for (int i = 0; i < chunkObjects.Length; i++)
                {
                    if (chunkObjects[i].materials != null)
                    {
                        for (int j = 0; j < chunkObjects[i].materials.Count; j++)
                        {
                            Undo.RecordObject(chunkObjects[i].materials[j], "Inspector");
                            chunkObjects[i].materials[j].mainTexture = chunkObjects[i].atlasTexture;
                        }
                    }
                }
                for (int i = 0; i < chunkObjects.Length; i++)
                {
                    var meshFilter = chunkObjects[i].GetComponent<MeshFilter>();
                    Undo.RecordObject(meshFilter, "Inspector");
                    meshFilter.sharedMesh = chunkObjects[i].mesh;
                    var renderer = chunkObjects[i].GetComponent<Renderer>();
                    Undo.RecordObject(renderer, "Inspector");
                    if (chunkObjects[i].materials != null)
                    {
                        Material[] tmps = new Material[chunkObjects[i].materialIndexes.Count];
                        for (int j = 0; j < chunkObjects[i].materialIndexes.Count; j++)
                        {
                            tmps[j] = chunkObjects[i].materials[chunkObjects[i].materialIndexes[j]];
                        }
                        renderer.sharedMaterials = tmps;
                    }
                    else
                    {
                        renderer.sharedMaterial = null;
                    }
                }
            }
            else
            {
                Assert.IsTrue(false);
            }
        }
        public override Mesh[] Edit_CreateMesh(List<VoxelData.Voxel> voxels, List<Edit_VerticesInfo> dstList = null, bool combine = true)
        {
            return new Mesh[1] { Edit_CreateMeshOnly(voxels, null, dstList, combine) };
        }
        public Mesh[] Edit_CreateMesh(List<VoxelData.Voxel>[] chunkVoxels, List<Edit_VerticesInfo>[] chunkDstList = null, bool combine = true)
        {
            Assert.IsTrue(chunkVoxels.Length == chunkDataList.Count);
            var meshs = new Mesh[chunkVoxels.Length];
            for (int i = 0; i < chunkVoxels.Length; i++)
            {
                meshs[i] = Edit_CreateMeshOnly(chunkVoxels[i], chunkDataList[i].atlasRects, chunkDstList[i], combine);
            }
            return meshs;
        }
        protected void CreateChunksGameObject()
        {

        }
        #endregion

        #region Edit
        public override void SetSelectedWireframeHidden(bool hidden)
        {
            if (voxelObject == null) return;
            var chunkObjects = FindChunkComponents();
            for (int i = 0; i < chunkObjects.Length; i++)
            {
                var renderer = chunkObjects[i].GetComponent<Renderer>();
                if (renderer != null)
                    EditorUtility.SetSelectedWireframeHidden(renderer, hidden);
            }
        }
        #endregion

        #region Undo
        protected override void RefreshCheckerCreate() { voxelObject.refreshChecker = new VoxelChunksObject.RefreshCheckerChunk(voxelObject); }
        #endregion
    }
}
