using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VoxelImporter
{
    [AddComponentMenu("Voxel Importer/Voxel Object")]
    [RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer))]
    public class VoxelObject : VoxelBase
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
        
        public Mesh mesh;
        [SerializeField]
        protected Material material;        //ver1.021 old
        public List<Material> materials;    //ver1.0.3 new
        public Texture2D atlasTexture;
#endif
    }

}
