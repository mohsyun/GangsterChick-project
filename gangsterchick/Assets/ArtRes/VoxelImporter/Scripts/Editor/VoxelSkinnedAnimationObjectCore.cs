using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace VoxelImporter
{
    public class VoxelSkinnedAnimationObjectCore : VoxelObjectCore
    {
        public VoxelSkinnedAnimationObject animationObject { get; protected set; }

        public VoxelSkinnedAnimationObjectCore(VoxelBase target) : base(target)
        {
            voxelObject = null;
            animationObject = target as VoxelSkinnedAnimationObject;
        }

        public override Mesh mesh { get { return animationObject.mesh; } set { animationObject.mesh = value; } }
        public override List<Material> materials { get { return animationObject.materials; } set { animationObject.materials = value; } }
        public override Texture2D atlasTexture { get { return animationObject.atlasTexture; } set { animationObject.atlasTexture = value; } }

        #region CreateMesh
        protected override bool IsCombineVoxelFace(IntVector3 basePos, IntVector3 combinePos, VoxelBase.Face face)
        {
            var baseWeights = GetBoneWeightTable(basePos);
            if (baseWeights == null)
            {
                return (GetBoneWeightTable(combinePos) == null) ? true : false;
            }
            else
            {
                var combineWeights = GetBoneWeightTable(combinePos);
                if (combineWeights == null)
                    return false;

                Assert.IsTrue(baseWeights.Length == (int)VoxelBase.VoxelVertexIndex.Total && combineWeights.Length == (int)VoxelBase.VoxelVertexIndex.Total);

                switch (face)
                {
                case VoxelBase.Face.forward:
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XYZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XYZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ])
                        return false;
                    break;
                case VoxelBase.Face.up:
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XYZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XYZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z])
                        return false;
                    break;
                case VoxelBase.Face.right:
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XYZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z])
                        return false;
                    break;
                case VoxelBase.Face.left:
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XYZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z])
                        return false;
                    break;
                case VoxelBase.Face.down:
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z])
                        return false;
                    break;
                case VoxelBase.Face.back:
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z] ||
                        baseWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z])
                        return false;
                    break;
                default:
                    break;
                }

                return true;
            }
        }
        protected override bool IsHiddenVoxelFace(IntVector3 basePos, VoxelBase.Face faceFlag)
        {
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

            var baseWeights = GetBoneWeightTable(basePos);
            var combineWeights = GetBoneWeightTable(combinePos);
            if (baseWeights == null && combineWeights == null)
                return true;
            if (baseWeights == null)
                baseWeights = BoneWeightTableDefault;
            if (combineWeights == null)
                combineWeights = BoneWeightTableDefault;

            switch (faceFlag)
            {
            case VoxelBase.Face.forward:
                {
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z]) return false;
                }
                break;
            case VoxelBase.Face.up:
                {
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z]) return false;
                }
                break;
            case VoxelBase.Face.right:
                {
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XYZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z]) return false;
                }
                break;
            case VoxelBase.Face.left:
                {
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._XYZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XYZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z]) return false;
                }
                break;
            case VoxelBase.Face.down:
                {
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XYZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XYZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z]) return false;
                }
                break;
            case VoxelBase.Face.back:
                {
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.XYZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._XY_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._XYZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex.X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex.X_YZ]) return false;
                    if (baseWeights[(int)VoxelBase.VoxelVertexIndex._X_Y_Z] != combineWeights[(int)VoxelBase.VoxelVertexIndex._X_YZ]) return false;
                }
                break;
            }

            return true;
        }
        protected override bool CreateMesh()
        {
            UpdateBoneWeight();
            var result = base.CreateMesh();
            if (result)
            {
                UpdateSkinnedMeshBounds();
                SetRendererCompornent();
            }
            return result;
        }
        public override void SetRendererCompornent()
        {
            base.SetRendererCompornent();

            {
                var renderer = animationObject.GetComponent<SkinnedMeshRenderer>();
                Undo.RecordObject(renderer, "Inspector");
                {//Unity Bug? force update.
                    renderer.enabled = !renderer.enabled;
                    renderer.enabled = !renderer.enabled;
                }
                if (animationObject.mesh != null && animationObject.mesh.vertexCount > 0)
                    renderer.sharedMesh = animationObject.mesh;
            }
            {
                var animator = voxelBase.GetComponent<Animator>();
                if (animator != null)
                {
                    Undo.RecordObject(animator, "Inspector");
                    animator.avatar = animationObject.avatar;
                }
            }
        }
        public void UpdateSkinnedMeshBounds()
        {
            var renderer = animationObject.GetComponent<SkinnedMeshRenderer>();
            if (animationObject.skinnedMeshBoundsUpdate)
            {
                Undo.RecordObject(renderer, "Update Skinned Mesh Bounds");
                var bounds = animationObject.mesh.bounds;
                if (animationObject.rootBone != null)
                    bounds.center = Matrix4x4.TRS(animationObject.rootBone.transform.localPosition, animationObject.rootBone.transform.localRotation, animationObject.rootBone.transform.localScale).inverse.MultiplyPoint3x4(bounds.center);
                bounds.size = Vector3.Scale(bounds.size, animationObject.skinnedMeshBoundsUpdateScale);
                if (float.IsNaN(bounds.center.x) || float.IsNaN(bounds.center.y) || float.IsNaN(bounds.center.z))
                    bounds.center = Vector3.zero;
                renderer.localBounds = bounds;
            }
        }
        #endregion

        #region BoneWeight
        private static readonly BoneWeight BoneWeightDefault = new BoneWeight() { boneIndex0 = 0, weight0 = 1f };
        private VoxelSkinnedAnimationObjectBoneCore[] bonesCore;
        protected override bool isHaveBoneWeight { get { return true; } }
        protected override Matrix4x4[] GetBindposes() { return animationObject.bindposes; }
        protected override BoneWeight GetBoneWeight(ref VoxelData.Voxel voxel, VoxelBase.VoxelVertexIndex index)
        {
            var boneWeights = GetBoneWeightTable(voxel.position);
            if (boneWeights == null || boneWeights[(int)index].weight0 <= 0f)
            {
                return BoneWeightDefault;
            }
            else
            {
                return boneWeights[(int)index];
            }
        }
        public void UpdateBoneWeight()
        {
            Undo.RecordObject(animationObject, "Update Bone Weight");

            UpdateBoneBindposes();

            #region Weight Update
            {
                CreateBoneWeightTable();
                for (int i = 0; i < animationObject.bones.Length; i++)
                {
                    bonesCore[i].UpdateBoneWeight(i);
                    animationObject.bones[i].weightData.AllAction((pos, weights) =>
                    {
                        var boneWeights = GetBoneWeightTable(pos);
                        if (boneWeights == null)
                            boneWeights = new BoneWeight[(int)VoxelBase.VoxelVertexIndex.Total];
                        for (int k = 0; k < (int)VoxelBase.VoxelVertexIndex.Total; k++)
                        {
                            var weight = weights.GetWeight((VoxelBase.VoxelVertexIndex)k);
                            if (weight == 0f) continue;
                            if (boneWeights[k].weight0 == 0f)
                            {
                                boneWeights[k].boneIndex0 = i;
                                boneWeights[k].weight0 = weight;
                            }
                            else if (boneWeights[k].weight1 == 0f)
                            {
                                boneWeights[k].boneIndex1 = i;
                                boneWeights[k].weight1 = weight;
                            }
                            else if (boneWeights[k].weight2 == 0f)
                            {
                                boneWeights[k].boneIndex2 = i;
                                boneWeights[k].weight2 = weight;
                            }
                            else if (boneWeights[k].weight3 == 0f)
                            {
                                boneWeights[k].boneIndex3 = i;
                                boneWeights[k].weight3 = weight;
                            }
                        }
                        SetBoneWeightTable(pos, boneWeights);
                    });
                }
                for (int i = 0; i < animationObject.bones.Length; i++)
                {
                    animationObject.bones[i].weightData.AllAction((pos, weights) =>
                    {
                        var boneWeights = GetBoneWeightTable(pos);
                        for (int k = 0; k < (int)VoxelBase.VoxelVertexIndex.Total; k++)
                        {
                            if (boneWeights[k].weight3 > 0f)
                            {
                                float power = boneWeights[k].weight0 + boneWeights[k].weight1 + boneWeights[k].weight2 + boneWeights[k].weight3;
                                boneWeights[k].weight0 = boneWeights[k].weight0 / power;
                                boneWeights[k].weight1 = boneWeights[k].weight1 / power;
                                boneWeights[k].weight2 = boneWeights[k].weight2 / power;
                                boneWeights[k].weight3 = boneWeights[k].weight3 / power;
                            }
                            else if (boneWeights[k].weight2 > 0f)
                            {
                                float power = boneWeights[k].weight0 + boneWeights[k].weight1 + boneWeights[k].weight2;
                                if (power >= 1f)
                                {
                                    boneWeights[k].weight0 = boneWeights[k].weight0 / power;
                                    boneWeights[k].weight1 = boneWeights[k].weight1 / power;
                                    boneWeights[k].weight2 = boneWeights[k].weight2 / power;
                                }
                                else
                                {
                                    boneWeights[k].boneIndex3 = 0;
                                    boneWeights[k].weight3 = 1f - power;
                                }
                            }
                            else if (boneWeights[k].weight1 > 0f)
                            {
                                float power = boneWeights[k].weight0 + boneWeights[k].weight1;
                                if (power >= 1f)
                                {
                                    boneWeights[k].weight0 = boneWeights[k].weight0 / power;
                                    boneWeights[k].weight1 = boneWeights[k].weight1 / power;
                                }
                                else
                                {
                                    boneWeights[k].boneIndex2 = 0;
                                    boneWeights[k].weight2 = 1f - power;
                                }
                            }
                            else if (boneWeights[k].weight0 > 0f)
                            {
                                float power = boneWeights[k].weight0;
                                if (power >= 1f)
                                {
                                    boneWeights[k].weight0 = 1f;
                                }
                                else
                                {
                                    boneWeights[k].boneIndex1 = 0;
                                    boneWeights[k].weight1 = 1f - power;
                                }
                            }
                            else
                            {
                                boneWeights[k] = BoneWeightDefault;
                            }
                            #region Sort
                            do
                            {
                                if (boneWeights[k].weight1 > 0 && boneWeights[k].weight0 < boneWeights[k].weight1)
                                {
                                    var boneIndex = boneWeights[k].boneIndex0;
                                    var weight = boneWeights[k].weight0;
                                    boneWeights[k].boneIndex0 = boneWeights[k].boneIndex1;
                                    boneWeights[k].weight0 = boneWeights[k].weight1;
                                    boneWeights[k].boneIndex1 = boneIndex;
                                    boneWeights[k].weight1 = weight;
                                    continue;
                                }
                                if (boneWeights[k].weight2 > 0 && boneWeights[k].weight1 < boneWeights[k].weight2)
                                {
                                    var boneIndex = boneWeights[k].boneIndex1;
                                    var weight = boneWeights[k].weight1;
                                    boneWeights[k].boneIndex1 = boneWeights[k].boneIndex2;
                                    boneWeights[k].weight1 = boneWeights[k].weight2;
                                    boneWeights[k].boneIndex2 = boneIndex;
                                    boneWeights[k].weight2 = weight;
                                    continue;
                                }
                                if (boneWeights[k].weight3 > 0 && boneWeights[k].weight2 < boneWeights[k].weight3)
                                {
                                    var boneIndex = boneWeights[k].boneIndex2;
                                    var weight = boneWeights[k].weight2;
                                    boneWeights[k].boneIndex2 = boneWeights[k].boneIndex3;
                                    boneWeights[k].weight2 = boneWeights[k].weight3;
                                    boneWeights[k].boneIndex3 = boneIndex;
                                    boneWeights[k].weight3 = weight;
                                    continue;
                                }
                            } while (false);
                            #endregion
                        }
                        SetBoneWeightTable(pos, boneWeights);
                    });
                }
            }
            #endregion

            UpdateAvatar();
        }
        public void UpdateBoneBindposes()
        {
            #region Bone
            animationObject.bones = null;
            {
                List<VoxelSkinnedAnimationObjectBone> list = new List<VoxelSkinnedAnimationObjectBone>();
                {
                    Action<Transform> GetChildrenBones = null;
                    GetChildrenBones = (trans) =>
                    {
                        var comp = trans.GetComponent<VoxelSkinnedAnimationObjectBone>();
                        if (comp != null)
                            list.Add(comp);
                        for (int i = 0; i < trans.childCount; i++)
                        {
                            GetChildrenBones(trans.GetChild(i));
                        }
                    };
                    {
                        var transformCache = animationObject.transform;
                        for (int i = 0; i < transformCache.childCount; i++)
                        {
                            GetChildrenBones(transformCache.GetChild(i));
                        }
                    }
                }
                animationObject.bones = list.ToArray();
                bonesCore = new VoxelSkinnedAnimationObjectBoneCore[animationObject.bones.Length];
                for (int i = 0; i < animationObject.bones.Length; i++)
                {
                    bonesCore[i] = new VoxelSkinnedAnimationObjectBoneCore(animationObject.bones[i]);
                }
            }
            #endregion

            #region MirrorBone
            for (int i = 0; i < animationObject.bones.Length; i++)
            {
                if (animationObject.bones[i].mirrorBone != null) continue;

                string mirrorName = null;
                if (animationObject.bones[i].name.IndexOf("Left") >= 0)
                    mirrorName = animationObject.bones[i].name.Replace("Left", "Right");
                else if (animationObject.bones[i].name.IndexOf("Right") >= 0)
                    mirrorName = animationObject.bones[i].name.Replace("Right", "Left");
                else
                    continue;

                for (int j = 0; j < animationObject.bones.Length; j++)
                {
                    if (i == j) continue;
                    if (animationObject.bones[j].name == mirrorName)
                    {
                        animationObject.bones[i].mirrorBone = animationObject.bones[j];
                        break;
                    }
                }
            }
            #endregion

            Transform[] boneTransforms = new Transform[animationObject.bones.Length];
            for (int i = 0; i < animationObject.bones.Length; i++)
            {
                boneTransforms[i] = animationObject.bones[i].transform;
            }

            #region UpdateBonePosition
            if (animationObject.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition)
            {
                Undo.RecordObject(animationObject, "Update Bone Positions");
                for (int i = 0; i < animationObject.bones.Length; i++)
                {
                    animationObject.bones[i].bonePosition = animationObject.bones[i].transform.localPosition;
                    animationObject.bones[i].bonePositionSave = true;
                }
            }
            else
            {
                Undo.RecordObject(animationObject, "Update Bone Positions");
                for (int i = 0; i < animationObject.bones.Length; i++)
                {
                    if (!animationObject.bones[i].bonePositionSave)
                    {
                        animationObject.bones[i].bonePosition = animationObject.bones[i].transform.localPosition;
                        animationObject.bones[i].bonePositionSave = true;
                    }
                }
            }
            #endregion

            {
                var renderer = animationObject.GetComponent<SkinnedMeshRenderer>();
                Undo.RecordObject(renderer, "Update Bone Bindposes");
                renderer.bones = boneTransforms;
                if (boneTransforms.Length > 0)
                    renderer.rootBone = boneTransforms[0];
                else
                    renderer.rootBone = null;
                Undo.RecordObject(animationObject, "Update Bone Bindposes");
                animationObject.bindposes = new Matrix4x4[boneTransforms.Length];
                {
                    var world = animationObject.transform.localToWorldMatrix;

                    List<Vector3> savePosition = new List<Vector3>(boneTransforms.Length);
                    List<Quaternion> saveRot = new List<Quaternion>(boneTransforms.Length);
                    List<Vector3> saveScale = new List<Vector3>(boneTransforms.Length);
                    for (int i = 0; i < boneTransforms.Length; i++)
                    {
                        savePosition.Add(boneTransforms[i].localPosition);
                        boneTransforms[i].localPosition = animationObject.bones[i].bonePosition;
                        saveRot.Add(boneTransforms[i].localRotation);
                        boneTransforms[i].localRotation = Quaternion.identity;
                        saveScale.Add(boneTransforms[i].localScale);
                        boneTransforms[i].localScale = Vector3.one;
                    }
                    for (int i = 0; i < boneTransforms.Length; i++)
                    {
                        animationObject.bindposes[i] = boneTransforms[i].worldToLocalMatrix * world;
                    }
                    for (int i = 0; i < boneTransforms.Length; i++)
                    {
                        boneTransforms[i].localPosition = savePosition[i];
                        boneTransforms[i].localRotation = saveRot[i];
                        boneTransforms[i].localScale = saveScale[i];
                    }
                }
            }
        }
        #endregion

        #region BoneWeightTable
        private static readonly BoneWeight[] BoneWeightTableDefault = new BoneWeight[(int)VoxelBase.VoxelVertexIndex.Total] { BoneWeightDefault, BoneWeightDefault, BoneWeightDefault, BoneWeightDefault, BoneWeightDefault, BoneWeightDefault, BoneWeightDefault, BoneWeightDefault };
        private BoneWeight[][][][] boneWeightTable;
        private void CreateBoneWeightTable()
        {
            if (animationObject.voxelData != null)
            {
                boneWeightTable = new BoneWeight[animationObject.voxelData.voxelSize.x][][][];
                for (int x = 0; x < animationObject.voxelData.voxelSize.x; x++)
                {
                    boneWeightTable[x] = new BoneWeight[animationObject.voxelData.voxelSize.y][][];
                    for (int y = 0; y < animationObject.voxelData.voxelSize.y; y++)
                    {
                        boneWeightTable[x][y] = new BoneWeight[animationObject.voxelData.voxelSize.z][];
                    }
                }
            }
        }
        private void SetBoneWeightTable(IntVector3 pos, BoneWeight[] boneWeight)
        {
            if (boneWeightTable == null)
                CreateBoneWeightTable();
            boneWeightTable[pos.x][pos.y][pos.z] = boneWeight;
        }
        private BoneWeight[] GetBoneWeightTable(IntVector3 pos)
        {
            if (boneWeightTable == null)
                return null;
            if (pos.x < 0 || pos.y < 0 || pos.z < 0 ||
                pos.x >= boneWeightTable.Length ||
                pos.y >= boneWeightTable[pos.x].Length ||
                pos.z >= boneWeightTable[pos.x][pos.y].Length)
                return null;
            else
                return boneWeightTable[pos.x][pos.y][pos.z];
        }
        #endregion

        #region Avatar
        protected void UpdateAvatar()
        {
            Undo.RecordObject(animationObject, "Update Avatar");

            string assetPath = "";
            if (animationObject.avatar != null &&
                AssetDatabase.Contains(animationObject.avatar))
            {
                assetPath = AssetDatabase.GetAssetPath(animationObject.avatar);
            }
            else if(!string.IsNullOrEmpty(animationObject.avatarPath))
            {
                assetPath = animationObject.avatarPath;
            }

            animationObject.avatar = null;
            animationObject.avatarPath = null;
            var parent = animationObject.transform.parent;
            var localPosition = animationObject.transform.localPosition;
            var localRotation = animationObject.transform.localRotation;
            var localScale = animationObject.transform.localScale;
            animationObject.transform.SetParent(null);
            animationObject.transform.localPosition = Vector3.zero;
            animationObject.transform.localRotation = Quaternion.identity;
            animationObject.transform.localScale = Vector3.one;
            switch (animationObject.rigAnimationType)
            {
            case VoxelSkinnedAnimationObject.RigAnimationType.Legacy:
                break;
            case VoxelSkinnedAnimationObject.RigAnimationType.Generic:
                if (animationObject.rootBone != null)
                {
                    Dictionary<Transform, Transform> saveList = new Dictionary<Transform, Transform>();
                    List<Transform> findList = new List<Transform>();
                    for (int j = 0; j < animationObject.transform.childCount; j++)
                    {
                        findList.Add(animationObject.transform.GetChild(j));
                    }
                    for (int i = 0; i < findList.Count; i++)
                    {
                        for (int j = 0; j < findList[i].childCount; j++)
                        {
                            findList.Add(findList[i].GetChild(j));
                        }
                        if (findList[i].GetComponent<VoxelSkinnedAnimationObjectBone>() == null)
                        {
                            saveList.Add(findList[i], findList[i].parent);
                            findList[i].SetParent(null);
                        }
                    }
                    animationObject.avatar = AvatarBuilder.BuildGenericAvatar(animationObject.gameObject, animationObject.rootBone.gameObject.name);
                    {
                        var enu = saveList.GetEnumerator();
                        while (enu.MoveNext())
                        {
                            enu.Current.Key.SetParent(enu.Current.Value);
                        }
                    }
                }
                break;
            case VoxelSkinnedAnimationObject.RigAnimationType.Humanoid:
                if (animationObject.rootBone != null)
                {
                    HumanDescription humanDescription = new HumanDescription()
                    {
                        upperArmTwist = animationObject.humanDescription.upperArmTwist,
                        lowerArmTwist = animationObject.humanDescription.lowerArmTwist,
                        upperLegTwist = animationObject.humanDescription.upperLegTwist,
                        lowerLegTwist = animationObject.humanDescription.lowerLegTwist,
                        armStretch = animationObject.humanDescription.armStretch,
                        legStretch = animationObject.humanDescription.legStretch,
                        feetSpacing = animationObject.humanDescription.feetSpacing,
                        hasTranslationDoF = animationObject.humanDescription.hasTranslationDoF,
                    };
                    #region CreateHumanAndSkeleton
                    {
                        List<HumanBone> humanBones = new List<HumanBone>();
                        List<SkeletonBone> skeletonBones = new List<SkeletonBone>();

                        if (!animationObject.humanDescription.firstAutomapDone)
                        {
                            AutomapHumanDescriptionHuman();
                            animationObject.humanDescription.firstAutomapDone = true;
                        }
                        for (int i = 0; i < VoxelSkinnedAnimationObject.HumanTraitBoneNameTable.Length; i++)
                        {
                            var index = VoxelSkinnedAnimationObject.HumanTraitBoneNameTable[i];
                            if (animationObject.humanDescription.bones[(int)index] == null) continue;

                            humanBones.Add(new HumanBone()
                            {
                                boneName = animationObject.humanDescription.bones[(int)index].name,
                                humanName = HumanTrait.BoneName[i],
                                limit = new HumanLimit() { useDefaultValues = true },
                            });
                        }

                        #region FindBones
                        {
                            skeletonBones.Add(new SkeletonBone()
                            {
                                name = animationObject.rootBone.parent.name,
                                position = animationObject.rootBone.parent.localPosition,
                                rotation = animationObject.rootBone.parent.localRotation,
                                scale = animationObject.rootBone.parent.localScale,
                            });
                            for (int i = 0; i < VoxelSkinnedAnimationObject.HumanTraitBoneNameTable.Length; i++)
                            {
                                var index = VoxelSkinnedAnimationObject.HumanTraitBoneNameTable[i];
                                if (animationObject.humanDescription.bones[(int)index] == null) continue;

                                skeletonBones.Add(new SkeletonBone()
                                {
                                    name = animationObject.humanDescription.bones[(int)index].name,
                                    position = animationObject.humanDescription.bones[(int)index].transform.localPosition,
                                    rotation = Quaternion.identity,
                                    scale = Vector3.one,
                                });
                            }
                        }
                        #endregion
                        humanDescription.human = humanBones.ToArray();
                        humanDescription.skeleton = skeletonBones.ToArray();
                    }
                    #endregion
                    animationObject.avatar = AvatarBuilder.BuildHumanAvatar(animationObject.gameObject, humanDescription);
                }
                break;
            }
            animationObject.transform.SetParent(parent);
            animationObject.transform.localPosition = localPosition;
            animationObject.transform.localRotation = localRotation;
            animationObject.transform.localScale = localScale;
            if (animationObject.avatar != null)
            {
                animationObject.avatar.name = animationObject.gameObject.ToString();

                if (!string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.CreateAsset(animationObject.avatar, assetPath);
                    animationObject.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(assetPath);
                    animationObject.avatarPath = assetPath;
                }
            }
        }
        public void ResetHumanDescriptionHuman()
        {
            Undo.RecordObject(animationObject, "Reset Human Description");

            for (int i = 0; i < animationObject.humanDescription.bones.Length; i++)
            {
                animationObject.humanDescription.bones[i] = null;
            }
        }
        public void AutomapHumanDescriptionHuman()
        {
            Undo.RecordObject(animationObject, "Reset Human Description");

            ResetHumanDescriptionHuman();

            Func<string, VoxelSkinnedAnimationObjectBone> FindBone = (name) =>
            {
                VoxelSkinnedAnimationObjectBone bone = null;
                string nameS = name.Replace(" ", "");
                for (int i = 0; i < animationObject.bones.Length; i++)
                {
                    if (animationObject.bones[i] == null) continue;

                    if (animationObject.bones[i].name.IndexOf(name) >= 0 ||
                        animationObject.bones[i].name.IndexOf(nameS) >= 0)
                    {
                        bone = animationObject.bones[i];
                        break;
                    }
                }
                return bone;
            };

            #region FindBones
            {
                var BoneName = HumanTrait.BoneName;
                for (int i = 0; i < BoneName.Length; i++)
                {
                    var bone = FindBone(BoneName[i]);
                    if (bone != null)
                    {
                        var index = VoxelSkinnedAnimationObject.HumanTraitBoneNameTable[i];
                        animationObject.humanDescription.bones[(int)index] = bone;
                    }
                }
            }
            #endregion
        }
        public void ResetBoneTransform()
        {
            if (animationObject.bones == null) return;
            for (int i = 0; i < animationObject.bones.Length; i++)
            {
                if (animationObject.bones[i] == null) continue;
                Undo.RecordObject(animationObject.bones[i].transform, "Reset Bone Transform");
                if (animationObject.bones[i].bonePositionSave)
                    animationObject.bones[i].transform.localPosition = animationObject.bones[i].bonePosition;
                animationObject.bones[i].transform.localRotation = Quaternion.identity;
                animationObject.bones[i].transform.localScale = Vector3.one;
            }
        }
        #endregion

        #region Undo
        protected override void RefreshCheckerCreate() { animationObject.refreshChecker = new VoxelSkinnedAnimationObject.RefreshCheckerSkinnedAnimation(animationObject); }
        #endregion
    }
}
