using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VoxelImporter
{
    [DisallowMultipleComponent]
    public abstract class VoxelBase : MonoBehaviour
    {
#if UNITY_EDITOR        
        //Voxel
        public string voxelFilePath;
        public string voxelFileGUID;
        public enum FileType
        {
            vox,
            qb,
            png,
        }
        public FileType fileType;
        public enum ImportMode
        {
            LowTexture,
            LowPoly,
        }
        public ImportMode importMode = ImportMode.LowPoly;
        [Flags]
        public enum ImportFlag
        {
            FlipX = 1 << 0,
            FlipY = 1 << 1,
            FlipZ = 1 << 2,
        }
        public ImportFlag importFlags;
        public Vector3 importScale = Vector3.one;
        public Vector3 importOffset;
        public Vector3 localOffset;

        [NonSerialized]
        public VoxelData voxelData;

        [Flags]
        public enum Face
        {
            forward = 1 << 0,
            up = 1 << 1,
            right = 1 << 2,
            left = 1 << 3,
            down = 1 << 4,
            back = 1 << 5,
        }
        public const Face FaceAllFlags = (Face)(-1);
        public Face enableFaceFlags = FaceAllFlags;

        //Mesh
        public bool generateLightmapUVs;

        //Material
        public bool generateMipMaps = true;
        public List<MaterialData> materialData;
        public List<int> materialIndexes;

        //Voxel
        public enum VoxelVertexIndex
        {
            XYZ,
            _XYZ,
            X_YZ,
            XY_Z,
            _X_YZ,
            _XY_Z,
            X_Y_Z,
            _X_Y_Z,
            Total
        }
        [Flags]
        public enum VoxelVertexFlags
        {
            XYZ = (1 << 0),
            _XYZ = (1 << 1),
            X_YZ = (1 << 2),
            XY_Z = (1 << 3),
            _X_YZ = (1 << 4),
            _XY_Z = (1 << 5),
            X_Y_Z = (1 << 6),
            _X_Y_Z = (1 << 7),
        }
        public struct VoxelVertices
        {
            public Vector3 vertexXYZ;
            public Vector3 vertex_XYZ;
            public Vector3 vertexX_YZ;
            public Vector3 vertexXY_Z;
            public Vector3 vertex_X_YZ;
            public Vector3 vertex_XY_Z;
            public Vector3 vertexX_Y_Z;
            public Vector3 vertex_X_Y_Z;

            public void SetVertex(VoxelSkinnedAnimationObject.VoxelVertexIndex index, Vector3 vertex)
            {
                switch (index)
                {
                case VoxelBase.VoxelVertexIndex.XYZ: vertexXYZ = vertex; break;
                case VoxelBase.VoxelVertexIndex._XYZ: vertex_XYZ = vertex; break;
                case VoxelBase.VoxelVertexIndex.X_YZ: vertexX_YZ = vertex; break;
                case VoxelBase.VoxelVertexIndex.XY_Z: vertexXY_Z = vertex; break;
                case VoxelBase.VoxelVertexIndex._X_YZ: vertex_X_YZ = vertex; break;
                case VoxelBase.VoxelVertexIndex._XY_Z: vertex_XY_Z = vertex; break;
                case VoxelBase.VoxelVertexIndex.X_Y_Z: vertexX_Y_Z = vertex; break;
                case VoxelBase.VoxelVertexIndex._X_Y_Z: vertex_X_Y_Z = vertex; break;
                default: Assert.IsTrue(false); break;
                }
            }
            public Vector3 GetVertex(VoxelSkinnedAnimationObject.VoxelVertexIndex index)
            {
                switch (index)
                {
                case VoxelBase.VoxelVertexIndex.XYZ: return vertexXYZ;
                case VoxelBase.VoxelVertexIndex._XYZ: return vertex_XYZ;
                case VoxelBase.VoxelVertexIndex.X_YZ: return vertexX_YZ;
                case VoxelBase.VoxelVertexIndex.XY_Z: return vertexXY_Z;
                case VoxelBase.VoxelVertexIndex._X_YZ: return vertex_X_YZ;
                case VoxelBase.VoxelVertexIndex._XY_Z: return vertex_XY_Z;
                case VoxelBase.VoxelVertexIndex.X_Y_Z: return vertexX_Y_Z;
                case VoxelBase.VoxelVertexIndex._X_Y_Z: return vertex_X_Y_Z;
                default: Assert.IsTrue(false); return Vector3.zero;
                }
            }
        }

        #region Editor
        public virtual bool EditorInitialize(){ return false; }

        public bool edit_importFoldout = true;
        public bool edit_objectFoldout = true;

        [NonSerialized]
        public bool edit_afterRefresh = false;

        public enum Edit_configureMode
        {
            None,
            Material,
        }
        public Edit_configureMode edit_configureMode;
        public int edit_configureMaterialIndex;

        public enum Edit_MaterialMode
        {
            Add,
            Remove,
        }
        public Edit_MaterialMode edit_materialMode;

        public enum Edit_MaterialTypeMode
        {
            Voxel,
            Fill,
            Rect,
        }
        public Edit_MaterialTypeMode edit_materialTypeMode;

        public Mesh[] edit_enableMesh = null;
        public virtual void SaveEditTmpData() { }
        #endregion

        #region Undo
        public class RefreshChecker
        {
            public RefreshChecker(VoxelBase voxelBase)
            {
                controller = voxelBase;
            }

            public VoxelBase controller;

            public string voxelFilePath;
            public string voxelFileGUID;
            public VoxelBase.ImportMode importMode;
            public VoxelBase.ImportFlag importFlags;
            public Vector3 importScale;
            public Vector3 importOffset;
            public VoxelBase.Face enableFaceFlags;
            public bool generateLightmapUVs;
            public bool generateMipMaps;
            public MaterialData[] materialData;
            public int[] materialIndexes;

            public virtual void Save()
            {
                voxelFilePath = controller.voxelFilePath;
                voxelFileGUID = controller.voxelFileGUID;
                importMode = controller.importMode;
                importFlags = controller.importFlags;
                importScale = controller.importScale;
                importOffset = controller.importOffset;
                enableFaceFlags = controller.enableFaceFlags;
                generateLightmapUVs = controller.generateLightmapUVs;
                generateMipMaps = controller.generateMipMaps;
                if (controller.materialData != null)
                {
                    materialData = new MaterialData[controller.materialData.Count];
                    for (int i = 0; i < controller.materialData.Count; i++)
                        materialData[i] = controller.materialData[i].Clone();
                }
                else
                {
                    materialData = null;
                }
                materialIndexes = controller.materialIndexes != null ? controller.materialIndexes.ToArray() : null;
            }
            public virtual bool Check()
            {
                if (voxelFilePath != controller.voxelFilePath ||
                    voxelFileGUID != controller.voxelFileGUID ||
                    importMode != controller.importMode ||
                    importFlags != controller.importFlags ||
                    importScale != controller.importScale ||
                    importOffset != controller.importOffset ||
                    enableFaceFlags != controller.enableFaceFlags ||
                    generateLightmapUVs != controller.generateLightmapUVs ||
                    generateMipMaps != controller.generateMipMaps)
                    return true;
                #region materialData
                if (materialData != null && controller.materialData != null)
                {
                    if (materialData.Length != controller.materialData.Count)
                        return true;
                    for (int i = 0; i < materialData.Length; i++)
                    {
                        if (!materialData[i].IsEqual(controller.materialData[i]))
                            return true;
                    }
                }
                else if (materialData != null && controller.materialData == null)
                {
                    return true;
                }
                else if (materialData == null && controller.materialData != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
                #endregion
                #region materialIndexes
                if (materialIndexes != null && controller.materialIndexes != null)
                {
                    if (materialIndexes.Length != controller.materialIndexes.Count)
                        return true;
                    for (int i = 0; i < materialIndexes.Length; i++)
                    {
                        if (materialIndexes[i] != controller.materialIndexes[i])
                            return true;
                    }
                }
                else if (materialIndexes != null && controller.materialIndexes == null)
                {
                    return true;
                }
                else if (materialIndexes == null && controller.materialIndexes != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
                #endregion

                return false;
            }
        }
        [NonSerialized]
        public RefreshChecker refreshChecker;
        #endregion
#endif
    }
}
