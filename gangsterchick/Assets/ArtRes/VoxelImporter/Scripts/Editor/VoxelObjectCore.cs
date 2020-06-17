using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace VoxelImporter
{
    public class VoxelObjectCore : VoxelBaseCore
    {
        public VoxelObjectCore(VoxelBase target) : base(target)
        {
            voxelObject = target as VoxelObject;
        }

        public VoxelObject voxelObject { get; protected set; }

        public virtual Mesh mesh { get { return voxelObject.mesh; } set { voxelObject.mesh = value; } }
        public virtual List<Material> materials { get { return voxelObject.materials; } set { voxelObject.materials = value; } }
        public virtual Texture2D atlasTexture { get { return voxelObject.atlasTexture; } set { voxelObject.atlasTexture = value; } }

        #region AtlasRects
        protected Rect[] atlasRects;
        protected AtlasRectTable atlasRectTable;
        #endregion

        #region FaceArea
        protected VoxelData.FaceAreaTable faceAreaTable;
        #endregion

        #region CreateVoxel
        public override string GetDefaultPath()
        {
            var path = base.GetDefaultPath();
            if (mesh != null && AssetDatabase.Contains(mesh))
            {
                var assetPath = AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    path = Path.GetDirectoryName(assetPath);
                }
            }
            if (materials != null)
            {
                for (int i = 0; i < materials.Count; i++)
                {
                    if (AssetDatabase.Contains(materials[i]))
                    {
                        var assetPath = AssetDatabase.GetAssetPath(materials[i]);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            path = Path.GetDirectoryName(assetPath);
                        }
                    }
                }
            }
            if (atlasTexture != null && AssetDatabase.Contains(atlasTexture))
            {
                var assetPath = AssetDatabase.GetAssetPath(atlasTexture);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    path = Path.GetDirectoryName(assetPath);
                }
            }
            return path;
        }
        #endregion

        #region CreateMesh
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
                if (voxelBase.materialData == null)
                    voxelBase.materialData = new List<MaterialData>();
                if (voxelBase.materialData.Count == 0)
                    voxelBase.materialData.Add(null);
                for (int i = 0; i < voxelBase.materialData.Count; i++)
                {
                    if (voxelBase.materialData[i] == null)
                        voxelBase.materialData[i] = new MaterialData();
                }
                if (materials == null)
                    materials = new List<Material>();
                if (materials.Count < voxelBase.materialData.Count)
                {
                    for (int i = materials.Count; i < voxelBase.materialData.Count; i++)
                        materials.Add(null);
                }
                else if (materials.Count > voxelBase.materialData.Count)
                {
                    materials.RemoveRange(voxelBase.materialData.Count, materials.Count - voxelBase.materialData.Count);
                }
                #region Erase
                for (int i = 0; i < voxelBase.materialData.Count; i++)
                {
                    List<IntVector3> removeList = new List<IntVector3>();
                    voxelBase.materialData[i].AllAction((pos) =>
                    {
                        if (voxelBase.voxelData.VoxelTableContains(pos) < 0)
                        {
                            removeList.Add(pos);
                        }
                    });
                    for (int j = 0; j < removeList.Count; j++)
                    {
                        voxelBase.materialData[i].RemoveMaterial(removeList[j]);
                    }
                }
                #endregion
            }
            #endregion

            CalcDataCreate(voxelBase.voxelData.voxels);

            faceAreaTable = CreateFaceArea(voxelBase.voxelData.voxels);

            DisplayProgressBar("");
            {
                var atlasTextureTmp = atlasTexture;
                if (!CreateTexture(faceAreaTable, voxelBase.voxelData.palettes, ref atlasRectTable, ref atlasTextureTmp, ref atlasRects))
                {
                    EditorUtility.ClearProgressBar();
                    return false;
                }
                atlasTexture = atlasTextureTmp;
                for (int i = 0; i < materials.Count; i++)
                {
                    if (materials[i] == null)
                        materials[i] = new Material(Shader.Find("Standard"));
                    materials[i].mainTexture = atlasTexture;
                }
            }

            DisplayProgressBar("");
            {
                var srcMesh = (mesh != null && AssetDatabase.Contains(mesh)) ? mesh : null;
                mesh = CreateMeshOnly(srcMesh, faceAreaTable, atlasTexture, atlasRects, atlasRectTable, Vector3.zero, out voxelBase.materialIndexes);
            }

            DisplayProgressBar("");
            if (voxelBase.generateLightmapUVs)
            {
                if (mesh.uv.Length > 0)
                    Unwrapping.GenerateSecondaryUVSet(mesh);
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

            atlasRects = null;
            atlasRectTable = null;
            faceAreaTable = null;

            GC.Collect();
        }
        public override void SetRendererCompornent()
        {
            {
                var meshFilter = voxelBase.GetComponent<MeshFilter>();
                Undo.RecordObject(meshFilter, "Inspector");
                meshFilter.sharedMesh = mesh;
            }
            if (materials != null)
            {
                for (int i = 0; i < materials.Count; i++)
                {
                    if (materials[i] != null)
                    {
                        Undo.RecordObject(materials[i], "Inspector");
                        materials[i].mainTexture = atlasTexture;
                    }
                }
            }
            {
                var renderer = voxelBase.GetComponent<Renderer>();
                Undo.RecordObject(renderer, "Inspector");
                if (materials != null)
                {
                    Material[] tmps = new Material[voxelBase.materialIndexes.Count];
                    for (int i = 0; i < voxelBase.materialIndexes.Count; i++)
                    {
                        tmps[i] = materials[voxelBase.materialIndexes[i]];
                    }
                    renderer.sharedMaterials = tmps;
                }
                else
                {
                    renderer.sharedMaterial = null;
                }
            }
        }
        public override Mesh[] Edit_CreateMesh(List<VoxelData.Voxel> voxels, List<Edit_VerticesInfo> dstList = null, bool combine = true)
        {
            return new Mesh[1] { Edit_CreateMeshOnly(voxels, atlasRects, dstList, combine) };
        }
        #endregion
    }
}
