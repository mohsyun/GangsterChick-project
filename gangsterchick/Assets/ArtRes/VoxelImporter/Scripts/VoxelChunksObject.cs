using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VoxelImporter
{
    [AddComponentMenu("Voxel Importer/Voxel Chunks Object")]
    public class VoxelChunksObject : VoxelBase
    {
#if !UNITY_EDITOR   
        void Awake()
        {
            Destroy(this);
        }
#else
        public override bool EditorInitialize()
        {
            var result = base.EditorInitialize();

            //ver1.021 -> ver1.0.3
            if (material != null)
            {
                materials = new List<Material>();
                materials.Add(material);
                materialData = new List<MaterialData>();
                materialData.Add(new MaterialData());
                materialIndexes = new List<int>();
                materialIndexes.Add(0);
                material = null;
                result = true;
            }

            return result;
        }

        [SerializeField]
        protected Material material;        //ver1.021 old
        public List<Material> materials;    //ver1.0.3 new
        public Texture2D atlasTexture;

        public enum SplitMode
        {
            ChunkSize,
            QubicleMatrix = 100,
        }

        public SplitMode splitMode;
        public IntVector3 chunkSize = new IntVector3(16, 16, 16);
        public bool createContactChunkFaces;

        public enum MaterialMode
        {
            Combine,
            Individual,
        }
        public MaterialMode materialMode;

        #region Editor
        public IntVector3 edit_chunkSize;

        public override void SaveEditTmpData()
        {
            base.SaveEditTmpData();

            edit_chunkSize = chunkSize;
        }
        #endregion

        #region Undo
        public class RefreshCheckerChunk : RefreshChecker
        {
            public RefreshCheckerChunk(VoxelChunksObject voxelObject) : base(voxelObject)
            {
                controllerChunk = voxelObject;
            }

            public VoxelChunksObject controllerChunk;

            public VoxelChunksObject.SplitMode splitMode;
            public IntVector3 chunkSize;
            public bool createContactChunkFaces;
            public VoxelChunksObject.MaterialMode materialMode;

            public override void Save()
            {
                base.Save();

                splitMode = controllerChunk.splitMode;
                chunkSize = controllerChunk.chunkSize;
                createContactChunkFaces = controllerChunk.createContactChunkFaces;
                materialMode = controllerChunk.materialMode;
            }
            public override bool Check()
            {
                if (base.Check())
                    return true;

                return splitMode != controllerChunk.splitMode ||
                        chunkSize != controllerChunk.chunkSize ||
                        createContactChunkFaces != controllerChunk.createContactChunkFaces ||
                        materialMode != controllerChunk.materialMode;
            }
        }
        #endregion
#endif
    }
}
