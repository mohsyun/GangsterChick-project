using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VoxelImporter
{
	public class VoxelSkinnedAnimationObjectBoneCore
    {
        public VoxelSkinnedAnimationObjectBone voxelBone { get; protected set; }
        public VoxelSkinnedAnimationObjectCore objectCore { get; protected set; }

        private VoxelSkinnedAnimationObjectBoneCore _mirrorBoneCore;
        public VoxelSkinnedAnimationObjectBoneCore mirrorBoneCore
        {
            get
            {
                if(_mirrorBoneCore == null)
                    _mirrorBoneCore = new VoxelSkinnedAnimationObjectBoneCore(voxelBone.mirrorBone);
                return _mirrorBoneCore;
            }
        }

        public VoxelSkinnedAnimationObjectBoneCore(VoxelSkinnedAnimationObjectBone target)
        {
            voxelBone = target;
            objectCore = new VoxelSkinnedAnimationObjectCore(voxelBone.voxelObject);
        }

        public void Initialize()
        {
            voxelBone.EditorInitialize();
        }

        public void UpdateBoneWeight(int boneIndex = -1)
        {
            Undo.RecordObject(voxelBone, "Update Bone Weight");

            if (boneIndex >= 0)
                voxelBone.boneIndex = boneIndex;

            if (voxelBone.weightData == null)
            {
                voxelBone.weightData = new WeightData();
            }

            #region Erase
            {
                List<IntVector3> removeList = new List<IntVector3>();
                voxelBone.weightData.AllAction((pos, weights) =>
                {
                    if (voxelBone.voxelObject.voxelData.VoxelTableContains(pos) < 0 ||
                        !weights.HasValue())
                    {
                        removeList.Add(pos);
                    }
                });
                for (int i = 0; i < removeList.Count; i++)
                {
                    voxelBone.weightData.RemoveWeight(removeList[i]);
                }
            }
            #endregion
        }

        public bool IsHaveEraseDisablePositionAnimation()
        {
            var animator = voxelBone.voxelObject.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        if (bindings[i].path != fullPathBoneName)
                            continue;
                        if (bindings[i].propertyName.StartsWith("m_LocalPosition."))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        public bool IsHaveEraseDisableRotationAnimation()
        {
            var animator = voxelBone.voxelObject.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        if (bindings[i].path != fullPathBoneName)
                            continue;
                        if (bindings[i].propertyName.StartsWith("localEulerAnglesRaw."))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        public bool IsHaveEraseDisableScaleAnimation()
        {
            var animator = voxelBone.voxelObject.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        if (bindings[i].path != fullPathBoneName)
                            continue;
                        if (bindings[i].propertyName.StartsWith("m_LocalScale."))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void EraseDisableAnimation()
        {
            if (voxelBone.edit_disablePositionAnimation || voxelBone.edit_disableRotationAnimation || voxelBone.edit_disableScaleAnimation)
            {
                var animator = voxelBone.voxelObject.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    {
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        for (int i = 0; i < bindings.Length; i++)
                        {
                            if (bindings[i].path != fullPathBoneName)
                                continue;
                            if ((voxelBone.edit_disablePositionAnimation && bindings[i].propertyName.StartsWith("m_LocalPosition.")) ||
                                (voxelBone.edit_disableRotationAnimation && bindings[i].propertyName.StartsWith("localEulerAnglesRaw.")) ||
                                (voxelBone.edit_disableScaleAnimation && bindings[i].propertyName.StartsWith("m_LocalScale.")))
                            {
                                AnimationUtility.SetEditorCurve(clip, bindings[i], null);
                            }
                        }
                    }
                }
            }
        }

        public void MirroringAnimation()
        {
            if (voxelBone.edit_mirrorSetBoneAnimation && voxelBone.mirrorBone != null)
            {
                var animator = voxelBone.voxelObject.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    Func<Keyframe, Keyframe, bool> IsKeyFrameEqual = (keyA, keyB) =>
                    {
                        return (keyA.inTangent == keyB.inTangent && keyA.outTangent == keyB.outTangent && keyA.tangentMode == keyB.tangentMode && keyA.time == keyB.time && keyA.value == keyA.value);
                    };
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    {
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        for (int i = 0; i < bindings.Length; i++)
                        {
                            if (bindings[i].path != fullPathBoneName)
                                continue;
                            var curve = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                            if ((!voxelBone.edit_disablePositionAnimation &&
                                ((voxelBone.voxelObject.edit_mirrorPosition[0] != VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None && bindings[i].propertyName.StartsWith("m_LocalPosition.x")) ||
                                (voxelBone.voxelObject.edit_mirrorPosition[1] != VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None && bindings[i].propertyName.StartsWith("m_LocalPosition.y")) ||
                                (voxelBone.voxelObject.edit_mirrorPosition[2] != VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None && bindings[i].propertyName.StartsWith("m_LocalPosition.z")))) ||
                                (!voxelBone.edit_disableRotationAnimation &&
                                ((voxelBone.voxelObject.edit_mirrorRotation[0] != VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None && bindings[i].propertyName.StartsWith("localEulerAnglesRaw.x")) ||
                                (voxelBone.voxelObject.edit_mirrorRotation[1] != VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None && bindings[i].propertyName.StartsWith("localEulerAnglesRaw.y")) ||
                                (voxelBone.voxelObject.edit_mirrorRotation[2] != VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None && bindings[i].propertyName.StartsWith("localEulerAnglesRaw.z")))) ||
                                (!voxelBone.edit_disableScaleAnimation &&
                                ((voxelBone.voxelObject.edit_mirrorScale[0] != VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None && bindings[i].propertyName.StartsWith("m_LocalScale.x")) ||
                                (voxelBone.voxelObject.edit_mirrorScale[1] != VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None && bindings[i].propertyName.StartsWith("m_LocalScale.y")) ||
                                (voxelBone.voxelObject.edit_mirrorScale[2] != VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None && bindings[i].propertyName.StartsWith("m_LocalScale.z")))))
                            {
                                EditorCurveBinding? mirrorBinding = null;
                                AnimationCurve mirrorCurve = null;
                                for (int j = 0; j < bindings.Length; j++)
                                {
                                    if (bindings[j].path == mirrorBoneCore.fullPathBoneName && bindings[j].propertyName == bindings[i].propertyName)
                                    {
                                        mirrorBinding = bindings[j];
                                        mirrorCurve = AnimationUtility.GetEditorCurve(clip, bindings[j]);
                                        break;
                                    }
                                }
                                Func<Keyframe[], Keyframe[]> Mirroring = (keys) =>
                                {
                                    if ((voxelBone.voxelObject.edit_mirrorPosition[0] == VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative && bindings[i].propertyName.StartsWith("m_LocalPosition.x")) ||
                                        (voxelBone.voxelObject.edit_mirrorPosition[1] == VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative && bindings[i].propertyName.StartsWith("m_LocalPosition.y")) ||
                                        (voxelBone.voxelObject.edit_mirrorPosition[2] == VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative && bindings[i].propertyName.StartsWith("m_LocalPosition.z")) ||
                                        (voxelBone.voxelObject.edit_mirrorRotation[0] == VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative && bindings[i].propertyName.StartsWith("localEulerAnglesRaw.x")) ||
                                        (voxelBone.voxelObject.edit_mirrorRotation[1] == VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative && bindings[i].propertyName.StartsWith("localEulerAnglesRaw.y")) ||
                                        (voxelBone.voxelObject.edit_mirrorRotation[2] == VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative && bindings[i].propertyName.StartsWith("localEulerAnglesRaw.z")) ||
                                        (voxelBone.voxelObject.edit_mirrorScale[0] == VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative && bindings[i].propertyName.StartsWith("m_LocalScale.x")) ||
                                        (voxelBone.voxelObject.edit_mirrorScale[1] == VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative && bindings[i].propertyName.StartsWith("m_LocalScale.y")) ||
                                        (voxelBone.voxelObject.edit_mirrorScale[2] == VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative && bindings[i].propertyName.StartsWith("m_LocalScale.z")))
                                    {
                                        for (int k = 0; k < keys.Length; k++)
                                        {
                                            keys[k].inTangent = -keys[k].inTangent;
                                            keys[k].outTangent = -keys[k].outTangent;
                                            keys[k].value = -keys[k].value;
                                        }
                                    }
                                    return keys;
                                };
                                bool updateCurve = false;
                                if (!mirrorBinding.HasValue)
                                {
                                    EditorCurveBinding newBinding = new EditorCurveBinding();
                                    newBinding.path = mirrorBoneCore.fullPathBoneName;
                                    newBinding.type = bindings[i].type;
                                    newBinding.propertyName = bindings[i].propertyName;
                                    mirrorBinding = newBinding;
                                    mirrorCurve = new AnimationCurve(Mirroring(curve.keys));
                                    updateCurve = true;
                                }
                                else
                                {
                                    mirrorCurve = AnimationUtility.GetEditorCurve(clip, mirrorBinding.Value);
                                    if (curve.keys.Length != mirrorCurve.keys.Length)
                                    {
                                        mirrorCurve = new AnimationCurve(Mirroring(curve.keys));
                                        updateCurve = true;
                                    }
                                    else
                                    {
                                        var keysA = mirrorCurve.keys;
                                        var keysB = Mirroring(curve.keys);
                                        for (int k = 0; k < keysA.Length; k++)
                                        {
                                            if (!IsKeyFrameEqual(keysA[k], keysB[k]))
                                            {
                                                mirrorCurve = new AnimationCurve(keysB);
                                                updateCurve = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                if (updateCurve)
                                {
                                    AnimationUtility.SetEditorCurve(clip, mirrorBinding.Value, mirrorCurve);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void MirrorBoneAnimation()
        {
            if (voxelBone.mirrorBone == null) return;
            if (!voxelBone.edit_mirrorSetBoneAnimation) return;

            #region Position
            {
                var tmp = voxelBone.transform.localPosition;
                switch (voxelBone.mirrorBone.voxelObject.edit_mirrorPosition[0])
                {
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None: tmp.x = voxelBone.mirrorBone.transform.localPosition.x; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Positive: tmp.x = voxelBone.transform.localPosition.x; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative: tmp.x = -voxelBone.transform.localPosition.x; break;
                }
                switch (voxelBone.mirrorBone.voxelObject.edit_mirrorPosition[1])
                {
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None: tmp.y = voxelBone.mirrorBone.transform.localPosition.y; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Positive: tmp.y = voxelBone.transform.localPosition.y; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative: tmp.y = -voxelBone.transform.localPosition.y; break;
                }
                switch (voxelBone.mirrorBone.voxelObject.edit_mirrorPosition[2])
                {
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None: tmp.z = voxelBone.mirrorBone.transform.localPosition.z; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Positive: tmp.z = voxelBone.transform.localPosition.z; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative: tmp.z = -voxelBone.transform.localPosition.z; break;
                }
                if (voxelBone.mirrorBone.transform.localPosition != tmp)
                    voxelBone.mirrorBone.transform.localPosition = tmp;
            }
            #endregion
            #region Rotation
            {
                var tmp = voxelBone.transform.localEulerAngles;
                switch (voxelBone.mirrorBone.voxelObject.edit_mirrorRotation[0])
                {
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None: tmp.x = voxelBone.mirrorBone.transform.localEulerAngles.x; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Positive: tmp.x = voxelBone.transform.localEulerAngles.x; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative: tmp.x = -voxelBone.transform.localEulerAngles.x; break;
                }
                switch (voxelBone.mirrorBone.voxelObject.edit_mirrorRotation[1])
                {
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None: tmp.y = voxelBone.mirrorBone.transform.localEulerAngles.y; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Positive: tmp.y = voxelBone.transform.localEulerAngles.y; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative: tmp.y = -voxelBone.transform.localEulerAngles.y; break;
                }
                switch (voxelBone.mirrorBone.voxelObject.edit_mirrorRotation[2])
                {
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None: tmp.z = voxelBone.mirrorBone.transform.localEulerAngles.z; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Positive: tmp.z = voxelBone.transform.localEulerAngles.z; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative: tmp.z = -voxelBone.transform.localEulerAngles.z; break;
                }
                if (voxelBone.mirrorBone.transform.localEulerAngles != tmp)
                    voxelBone.mirrorBone.transform.localEulerAngles = tmp;
            }
            #endregion
            #region Scale
            {
                var tmp = voxelBone.transform.localScale;
                switch (voxelBone.mirrorBone.voxelObject.edit_mirrorScale[0])
                {
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None: tmp.x = voxelBone.mirrorBone.transform.localScale.x; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Positive: tmp.x = voxelBone.transform.localScale.x; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative: tmp.x = -voxelBone.transform.localScale.x; break;
                }
                switch (voxelBone.mirrorBone.voxelObject.edit_mirrorScale[1])
                {
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None: tmp.y = voxelBone.mirrorBone.transform.localScale.y; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Positive: tmp.y = voxelBone.transform.localScale.y; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative: tmp.y = -voxelBone.transform.localScale.y; break;
                }
                switch (voxelBone.mirrorBone.voxelObject.edit_mirrorScale[2])
                {
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.None: tmp.z = voxelBone.mirrorBone.transform.localScale.z; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Positive: tmp.z = voxelBone.transform.localScale.z; break;
                case VoxelSkinnedAnimationObject.Edit_MirrorSetMode.Negative: tmp.z = -voxelBone.transform.localScale.z; break;
                }
                if (voxelBone.mirrorBone.transform.localScale != tmp)
                    voxelBone.mirrorBone.transform.localScale = tmp;
            }
            #endregion
        }
        public void MirrorBonePosition()
        {
            if (voxelBone.mirrorBone == null) return;
            if (!voxelBone.edit_mirrorSetBonePosition) return;

            var tmp = voxelBone.transform.localPosition;
            tmp.x = -voxelBone.transform.localPosition.x;
            voxelBone.mirrorBone.transform.localPosition = tmp;
        }
        public void MirrorBoneWeight()
        {
            if (voxelBone.mirrorBone == null) return;
            if (!voxelBone.edit_mirrorSetBoneWeight) return;

            voxelBone.mirrorBone.transform.localPosition = new Vector3(-voxelBone.transform.localPosition.x, voxelBone.transform.localPosition.y, voxelBone.transform.localPosition.z);

            #region Weight
            {
                voxelBone.mirrorBone.weightData.ClearWeight();
                voxelBone.weightData.AllAction((pos, weights) =>
                {
                    var newPos = GetMirrorVoxelPosition(pos);
                    var newWeights = new WeightData.VoxelWeight();
                    newWeights.weightXYZ = weights.weight_XYZ;
                    newWeights.weight_XYZ = weights.weightXYZ;
                    newWeights.weightX_YZ = weights.weight_X_YZ;
                    newWeights.weightXY_Z = weights.weight_XY_Z;
                    newWeights.weight_X_YZ = weights.weightX_YZ;
                    newWeights.weight_XY_Z = weights.weightXY_Z;
                    newWeights.weightX_Y_Z = weights.weight_X_Y_Z;
                    newWeights.weight_X_Y_Z = weights.weightX_Y_Z;
                    voxelBone.mirrorBone.weightData.SetWeight(newPos, newWeights);
                });
            }
            #endregion
        }
        public IntVector3 GetMirrorVoxelPosition(IntVector3 pos)
        {
            Assert.IsNotNull(voxelBone.mirrorBone);

            var srcVoxelPosition = objectCore.GetVoxelPosition(voxelBone.voxelObject.bindposes[voxelBone.boneIndex].inverse.GetColumn(3));
            var voxelPosition = objectCore.GetVoxelPosition(voxelBone.mirrorBone.voxelObject.bindposes[voxelBone.mirrorBone.boneIndex].inverse.GetColumn(3));

            IntVector3 newPos;
            {
                var offset = new Vector3(pos.x, pos.y, pos.z) - srcVoxelPosition;
                var mirror = voxelPosition + new Vector3(-offset.x - 1, offset.y, offset.z);
                newPos = new IntVector3(Mathf.FloorToInt(mirror.x), Mathf.FloorToInt(mirror.y), Mathf.FloorToInt(mirror.z));
            }

            return newPos;
        }

        private string _fullPathBoneName;
        public string fullPathBoneName
        {
            get
            {
                if (string.IsNullOrEmpty(_fullPathBoneName))
                    _fullPathBoneName = AnimationUtility.CalculateTransformPath(voxelBone.transform, voxelBone.voxelObject.transform);
                return _fullPathBoneName;
            }
        }
    }
}
