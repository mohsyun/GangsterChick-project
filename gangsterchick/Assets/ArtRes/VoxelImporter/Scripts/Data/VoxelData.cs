using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR

namespace VoxelImporter
{
    public class VoxelData
    {
        [System.Diagnostics.DebuggerDisplay("\"Position({x}, {y}, {z}\"), Palette({palette})")]
        public struct Voxel
        {
            public Voxel(int x, int y, int z, int palette)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.palette = palette;
                this.visible = VoxelBase.Face.forward | VoxelBase.Face.up | VoxelBase.Face.right | VoxelBase.Face.left | VoxelBase.Face.down | VoxelBase.Face.back;
            }

            public IntVector3 position { get { return new IntVector3(x, y, z); } set { x = value.x; y = value.y; z = value.z; } }

            public int x;
            public int y;
            public int z;
            public int palette;
            public VoxelBase.Face visible;
        }

        #region FaceArea
        public struct FaceArea
        {
            public IntVector3 min;
            public IntVector3 max;
            public int palette;
            public int material;

            public IntVector3 size { get { return max - min + IntVector3.one; } }
            public Vector3 minf { get { return new Vector3(min.x, min.y, min.z); } }
            public Vector3 maxf { get { return new Vector3(max.x, max.y, max.z); } }

            public IntVector3 Get(VoxelBase.VoxelVertexIndex index)
            {
                switch (index)
                {
                case VoxelBase.VoxelVertexIndex.XYZ: return new IntVector3(max.x, max.y, max.z);
                case VoxelBase.VoxelVertexIndex._XYZ: return new IntVector3(min.x, max.y, max.z);
                case VoxelBase.VoxelVertexIndex.X_YZ: return new IntVector3(max.x, min.y, max.z);
                case VoxelBase.VoxelVertexIndex.XY_Z: return new IntVector3(max.x, max.y, min.z);
                case VoxelBase.VoxelVertexIndex._X_YZ: return new IntVector3(min.x, min.y, max.z);
                case VoxelBase.VoxelVertexIndex._XY_Z: return new IntVector3(min.x, max.y, min.z);
                case VoxelBase.VoxelVertexIndex.X_Y_Z: return new IntVector3(max.x, min.y, min.z);
                case VoxelBase.VoxelVertexIndex._X_Y_Z: return new IntVector3(min.x, min.y, min.z);
                default: Assert.IsFalse(false); return IntVector3.zero;
                }
            }
        }
        public class FaceAreaTable
        {
            public List<FaceArea> forward = new List<FaceArea>();
            public List<FaceArea> up = new List<FaceArea>();
            public List<FaceArea> right = new List<FaceArea>();
            public List<FaceArea> left = new List<FaceArea>();
            public List<FaceArea> down = new List<FaceArea>();
            public List<FaceArea> back = new List<FaceArea>();

            public void Merge(FaceAreaTable src)
            {
                forward.AddRange(src.forward);
                up.AddRange(src.up);
                right.AddRange(src.right);
                left.AddRange(src.left);
                down.AddRange(src.down);
                back.AddRange(src.back);
            }

            public void ReplacePalette(int[] paletteTable)
            {
                for (int i = 0; i < forward.Count; i++)
                {
                    var faceArea = forward[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    forward[i] = faceArea;
                }
                for (int i = 0; i < up.Count; i++)
                {
                    var faceArea = up[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    up[i] = faceArea;
                }
                for (int i = 0; i < right.Count; i++)
                {
                    var faceArea = right[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    right[i] = faceArea;
                }
                for (int i = 0; i < left.Count; i++)
                {
                    var faceArea = left[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    left[i] = faceArea;
                }
                for (int i = 0; i < down.Count; i++)
                {
                    var faceArea = down[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    down[i] = faceArea;
                }
                for (int i = 0; i < back.Count; i++)
                {
                    var faceArea = back[i];
                    faceArea.palette = paletteTable[faceArea.palette];
                    back[i] = faceArea;
                }
            }
        }
        #endregion

        #region VoxelTable
        private DataTable3<int> voxelTable;
        public List<IntVector3> vertexList;

        public void CreateVoxelTable()
        {
            #region voxelTable
            {
                voxelTable = new DataTable3<int>(voxelSize.x, voxelSize.y, voxelSize.z);
                if (voxels != null)
                {
                    for (int i = 0; i < voxels.Length; i++)
                    {
                        voxelTable.Set(voxels[i].position, i);
                    }
                }
            }
            #endregion
            #region vertexList 
            {
                vertexList = new List<IntVector3>();
                bool[,,] doneTable = new bool[voxelSize.x + 1, voxelSize.y + 1, voxelSize.z + 1];
                Action<IntVector3> AddPoint = (pos) =>
                {
                   if (pos.x < 0 || pos.y < 0 || pos.z < 0) return;
                   if (!doneTable[pos.x, pos.y, pos.z])
                   {
                       doneTable[pos.x, pos.y, pos.z] = true;
                       vertexList.Add(pos);
                   }
                };
                if (voxels != null)
                {
                    for (int i = 0; i < voxels.Length; i++)
                    {
                        AddPoint(new IntVector3(voxels[i].x, voxels[i].y, voxels[i].z));
                        AddPoint(new IntVector3(voxels[i].x + 1, voxels[i].y, voxels[i].z));
                        AddPoint(new IntVector3(voxels[i].x, voxels[i].y + 1, voxels[i].z));
                        AddPoint(new IntVector3(voxels[i].x, voxels[i].y, voxels[i].z + 1));
                        AddPoint(new IntVector3(voxels[i].x + 1, voxels[i].y + 1, voxels[i].z));
                        AddPoint(new IntVector3(voxels[i].x + 1, voxels[i].y, voxels[i].z + 1));
                        AddPoint(new IntVector3(voxels[i].x, voxels[i].y + 1, voxels[i].z + 1));
                        AddPoint(new IntVector3(voxels[i].x + 1, voxels[i].y + 1, voxels[i].z + 1));
                    }
                }
            }
            #endregion
        }
        public int VoxelTableContains(IntVector3 pos)
        {
            if (!voxelTable.Contains(pos))
                return -1;
            else
                return voxelTable.Get(pos);
        }
        public int VoxelTableContains(int x, int y, int z)
        {
            if (!voxelTable.Contains(x, y, z))
                return -1;
            else
                return voxelTable.Get(x, y, z);
        }
        protected void SetVoxelTable(IntVector3 pos, int index)
        {
            voxelTable.Set(pos, index);
        }
        protected void SetVoxelTable(int x, int y, int z, int index)
        {
            voxelTable.Set(x, y, z, index);
        }
        #endregion

        public Voxel[] voxels;
        public Color[] palettes;
        public IntVector3 voxelSize;
    }
}

#endif
