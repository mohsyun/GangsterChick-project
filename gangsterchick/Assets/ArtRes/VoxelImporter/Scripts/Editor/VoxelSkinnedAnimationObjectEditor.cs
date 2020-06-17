using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace VoxelImporter
{
    [CustomEditor(typeof(VoxelSkinnedAnimationObject))]
    public class VoxelSkinnedAnimationObjectEditor : VoxelObjectEditor
    {
        public VoxelSkinnedAnimationObject animationTarget { get; private set; }
        public VoxelSkinnedAnimationObjectCore animationCore { get; protected set; }

        public override Mesh mesh { get { return animationTarget.mesh; } set { animationTarget.mesh = value; } }
        public override List<Material> materials { get { return animationTarget.materials; } set { animationTarget.materials = value; } }
        public override Texture2D atlasTexture { get { return animationTarget.atlasTexture; } set { animationTarget.atlasTexture = value; } }

        protected override void OnEnable()
        {
            base.OnEnable();

            animationTarget = target as VoxelSkinnedAnimationObject;
            if (animationTarget == null) return;
            baseCore = objectCore = animationCore = new VoxelSkinnedAnimationObjectCore(animationTarget);

            baseCore.Initialize();

            UpdateMaterialList(materials);
            if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
                UpdateMaterialEnableMesh();
        }

        protected override void InspectorGUI()
        {
            if (animationTarget == null) return;

            base.InspectorGUI();

            var prefabType = PrefabUtility.GetPrefabType(baseTarget.gameObject);
            var prefabEnable = prefabType == PrefabType.Prefab || prefabType == PrefabType.PrefabInstance || prefabType == PrefabType.DisconnectedPrefabInstance;

            Action<UnityEngine.Object, string> TypeTitle = (o, title) =>
            {
                if (o == null)
                    EditorGUILayout.LabelField(title, guiStyleMagentaBold);
                else
                    EditorGUILayout.LabelField(title, guiStyleBold);
            };

            #region Animation
            {
                animationTarget.edit_animationFoldout = EditorGUILayout.Foldout(animationTarget.edit_animationFoldout, "Animation", guiStyleFoldoutBold);
                if (animationTarget.edit_animationFoldout)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    {
                        EditorGUILayout.BeginHorizontal();
                        TypeTitle(animationTarget.rootBone, "Bone");
                        {
                            EditorGUI.BeginDisabledGroup(animationTarget.rootBone == null);
                            if (GUILayout.Button("Save as template", GUILayout.Width(128)))
                            {
                                #region Save as template
                                string BoneTemplatesPath = Application.dataPath + "/VoxelImporter/Scripts/Editor/BoneTemplates";
                                string path = EditorUtility.SaveFilePanel("Save as template", BoneTemplatesPath, string.Format("{0}.asset", baseTarget.gameObject.name), "asset");
                                if (!string.IsNullOrEmpty(path))
                                {
                                    if (path.IndexOf(Application.dataPath) < 0)
                                    {
                                        SaveInsideAssetsFolderDisplayDialog();
                                    }
                                    else
                                    {
                                        path = path.Replace(Application.dataPath, "Assets");
                                        var boneTemplate = ScriptableObject.CreateInstance<BoneTemplate>();
                                        boneTemplate.Set(animationTarget.rootBone);
                                        AssetDatabase.CreateAsset(boneTemplate, path);
                                    }
                                }
                                #endregion
                            }
                            EditorGUI.EndDisabledGroup();
                            if (GUILayout.Button("Create", guiStyleDropDown, GUILayout.Width(64)))
                            {
                                #region Create
                                VoxelHumanoidConfigreAvatar.Destroy();

                                Dictionary<string, BoneTemplate> boneTemplates = new Dictionary<string, BoneTemplate>();
                                {
                                    {
                                        var boneTemplate = ScriptableObject.CreateInstance<BoneTemplate>();
                                        boneTemplate.boneInitializeData.Add(new BoneTemplate.BoneInitializeData() { name = "Root" });
                                        boneTemplate.boneInitializeData.Add(new BoneTemplate.BoneInitializeData() { name = "Bone", parentName = "Root", position = new Vector3(0f, 2f, 0f) });
                                        boneTemplates.Add("Default", boneTemplate);
                                    }
                                    {
                                        var guids = AssetDatabase.FindAssets("t:bonetemplate");
                                        for (int i = 0; i < guids.Length; i++)
                                        {
                                            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                                            var boneTemplate = AssetDatabase.LoadAssetAtPath<BoneTemplate>(path);
                                            if (boneTemplate == null) continue;
                                            var name = path.Replace("Assets/", "");
                                            boneTemplates.Add(name, boneTemplate);
                                        }
                                    }
                                }

                                Action<BoneTemplate> MenuCallback = (boneTemplate) =>
                                {
                                    GameObject goRoot = baseTarget.gameObject;
                                    VoxelBase clRoot = baseTarget;

                                    if (prefabType == PrefabType.Prefab)
                                    {
                                        goRoot = (GameObject)PrefabUtility.InstantiatePrefab(baseTarget.gameObject);
                                        clRoot = goRoot.GetComponent<VoxelBase>();
                                    }

                                    {
                                        var bones = clRoot.GetComponentsInChildren<VoxelSkinnedAnimationObjectBone>();
                                        for (int i = 0; i < bones.Length; i++)
                                        {
                                            for (int j = 0; j < bones[i].transform.childCount; j++)
                                            {
                                                var child = bones[i].transform.GetChild(j);
                                                if (child.GetComponent<VoxelSkinnedAnimationObjectBone>() == null)
                                                {
                                                    Undo.SetTransformParent(child, animationTarget.transform, "Create Bone");
                                                    i--;
                                                }
                                            }
                                        }
                                        for (int i = 0; i < bones.Length; i++)
                                        {
                                            if (bones[i] == null || bones[i].gameObject == null) continue;
                                            Undo.DestroyObjectImmediate(bones[i].gameObject);
                                        }
                                    }

                                    {
                                        List<GameObject> createList = new List<GameObject>();
                                        for (int i = 0; i < boneTemplate.boneInitializeData.Count; i++)
                                        {
                                            var tp = boneTemplate.boneInitializeData[i];
                                            GameObject go = new GameObject(tp.name);
                                            Undo.RegisterCreatedObjectUndo(go, "Create Bone");
                                            var bone = Undo.AddComponent<VoxelSkinnedAnimationObjectBone>(go);
                                            {
                                                bone.edit_disablePositionAnimation = tp.disablePositionAnimation;
                                                bone.edit_disableRotationAnimation = tp.disableRotationAnimation;
                                                bone.edit_disableScaleAnimation = tp.disableScaleAnimation;
                                                bone.edit_mirrorSetBoneAnimation = tp.mirrorSetBoneAnimation;
                                                bone.edit_mirrorSetBonePosition = tp.mirrorSetBonePosition;
                                                bone.edit_mirrorSetBoneWeight = tp.mirrorSetBoneWeight;
                                            }
                                            if (string.IsNullOrEmpty(tp.parentName))
                                            {
                                                Undo.SetTransformParent(go.transform, goRoot.transform, "Create Bone");
                                            }
                                            else
                                            {
                                                int parentIndex = createList.FindIndex(a => a.name == tp.parentName);
                                                Debug.Assert(parentIndex >= 0);
                                                GameObject parent = createList[parentIndex];
                                                Assert.IsNotNull(parent);
                                                Undo.SetTransformParent(go.transform, parent.transform, "Create Bone");
                                            }
                                            go.transform.localPosition = tp.position;
                                            go.transform.localRotation = Quaternion.identity;
                                            go.transform.localScale = Vector3.one;
                                            createList.Add(go);
                                        }
                                    }
                                    animationTarget.humanDescription.firstAutomapDone = false;
                                    Refresh();

                                    if (prefabType == PrefabType.Prefab)
                                    {
                                        PrefabUtility.ReplacePrefab(goRoot, PrefabUtility.GetPrefabParent(goRoot), ReplacePrefabOptions.ConnectToPrefab);
                                        DestroyImmediate(goRoot);
                                    }
                                };
                                GenericMenu menu = new GenericMenu();
                                {
                                    var enu = boneTemplates.GetEnumerator();
                                    while (enu.MoveNext())
                                    {
                                        var value = enu.Current.Value;
                                        menu.AddItem(new GUIContent(enu.Current.Key), false, () => { MenuCallback(value); });
                                    }
                                }
                                menu.ShowAsContext();
                                #endregion
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                        {
                            EditorGUI.indentLevel++;
                            {
                                EditorGUILayout.LabelField("Count", animationTarget.rootBone != null ? animationTarget.bones.Length.ToString() : "");
                            }
                            #region Reset
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Reset");
                                {
                                    if (GUILayout.Button("All"))
                                    {
                                        for (int i = 0; i < animationTarget.bones.Length; i++)
                                        {
                                            Undo.RecordObject(animationTarget.bones[i].transform, "Reset Bone Transform");
                                            if (animationTarget.bones[i].bonePositionSave)
                                                animationTarget.bones[i].transform.localPosition = animationTarget.bones[i].bonePosition;
                                            animationTarget.bones[i].transform.localRotation = Quaternion.identity;
                                            animationTarget.bones[i].transform.localScale = Vector3.one;
                                        }
                                    }
                                    if (GUILayout.Button("Position"))
                                    {
                                        for (int i = 0; i < animationTarget.bones.Length; i++)
                                        {
                                            Undo.RecordObject(animationTarget.bones[i].transform, "Reset Bone Position");
                                            if (animationTarget.bones[i].bonePositionSave)
                                                animationTarget.bones[i].transform.localPosition = animationTarget.bones[i].bonePosition;
                                        }
                                    }
                                    if (GUILayout.Button("Rotation"))
                                    {
                                        for (int i = 0; i < animationTarget.bones.Length; i++)
                                        {
                                            Undo.RecordObject(animationTarget.bones[i].transform, "Reset Bone Rotation");
                                            animationTarget.bones[i].transform.localRotation = Quaternion.identity;
                                        }
                                    }
                                    if (GUILayout.Button("Scale"))
                                    {
                                        for (int i = 0; i < animationTarget.bones.Length; i++)
                                        {
                                            Undo.RecordObject(animationTarget.bones[i].transform, "Reset Bone Scale");
                                            animationTarget.bones[i].transform.localScale = Vector3.one;
                                        }
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            #endregion
                            if (animationTarget.mesh != null)
                            {
                                if (animationTarget.rootBone == null)
                                {
                                    EditorGUILayout.HelpBox("Bone not found. Please create bone.", MessageType.Error);
                                }
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    {
                        EditorGUILayout.LabelField("Rig", guiStyleBold);
                        {
                            EditorGUI.indentLevel++;
                            {
                                #region AnimationType
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var rigAnimationType = (VoxelSkinnedAnimationObject.RigAnimationType)EditorGUILayout.EnumPopup("Animation Type", animationTarget.rigAnimationType);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        UndoRecordObject("Inspector");
                                        VoxelHumanoidConfigreAvatar.Destroy();
                                        animationTarget.rigAnimationType = rigAnimationType;
                                        animationTarget.humanDescription.firstAutomapDone = false;
                                        Refresh();
                                    }
                                }
                                #endregion
                                #region Avatar
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    {
                                        EditorGUI.BeginDisabledGroup(true);
                                        EditorGUILayout.ObjectField("Avatar", animationTarget.avatar, typeof(Avatar), false);
                                        EditorGUI.EndDisabledGroup();
                                    }
                                    if (animationTarget.avatar != null)
                                    {
                                        if (!AssetDatabase.Contains(animationTarget.avatar))
                                        {
                                            if (GUILayout.Button("Save", GUILayout.Width(64)))
                                            {
                                                #region Create Avatar
                                                string path = EditorUtility.SaveFilePanel("Save avatar", objectCore.GetDefaultPath(), string.Format("{0}_avatar.asset", baseTarget.gameObject.name), "asset");
                                                if (!string.IsNullOrEmpty(path))
                                                {
                                                    if (path.IndexOf(Application.dataPath) < 0)
                                                    {
                                                        EditorUtility.DisplayDialog("Error!", "Please save a lower than \"Assets\"", "ok");
                                                    }
                                                    else
                                                    {
                                                        UndoRecordObject("Save Avatar");
                                                        path = path.Replace(Application.dataPath, "Assets");
                                                        AssetDatabase.CreateAsset(Avatar.Instantiate(animationTarget.avatar), path);
                                                        animationTarget.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                                                        animationTarget.avatarPath = path;
                                                        objectCore.SetRendererCompornent();
                                                    }
                                                }
                                                #endregion
                                            }
                                        }
                                        else
                                        {
                                            if (GUILayout.Button("Reset", GUILayout.Width(64)))
                                            {
                                                #region Reset Avatar
                                                UndoRecordObject("Reset Avatar");
                                                animationTarget.avatar = null;
                                                animationTarget.avatarPath = null;
                                                animationCore.UpdateBoneWeight();
                                                #endregion
                                            }
                                        }
                                    }
                                    EditorGUILayout.EndHorizontal();
                                    EditorGUI.indentLevel++;
                                    if (animationTarget.avatar != null && !animationTarget.avatar.isValid)
                                    {
                                        EditorGUILayout.HelpBox("Invalid mecanim avatar.\nCheck the bone please.", MessageType.Error);
                                    }
                                    #region HelpBox
                                    if (prefabEnable)
                                    {
                                        if (animationTarget.rigAnimationType != VoxelSkinnedAnimationObject.RigAnimationType.Legacy)
                                        {
                                            if (animationTarget.avatar == null || !AssetDatabase.Contains(animationTarget.avatar))
                                            {
                                                EditorGUILayout.HelpBox("Prefab is need save file.\nPlease save Avatar.", MessageType.Error);
                                            }
                                        }
                                    }
                                    #endregion
                                    EditorGUI.indentLevel--;
                                }
                                #endregion
                                #region Configre Avatar
                                if (animationTarget.rigAnimationType == VoxelSkinnedAnimationObject.RigAnimationType.Humanoid)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.Space();
                                    if (GUILayout.Button("Configure Avatar", VoxelHumanoidConfigreAvatar.instance == null ? GUI.skin.button : guiStyleBoldActiveButton))
                                    {
                                        if (VoxelHumanoidConfigreAvatar.instance == null)
                                            VoxelHumanoidConfigreAvatar.Create(animationTarget);
                                        else
                                            VoxelHumanoidConfigreAvatar.instance.Close();
                                    }
                                    EditorGUILayout.Space();
                                    EditorGUILayout.EndHorizontal();
                                }
                                #endregion
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    {
                        TypeTitle(animationTarget.mesh, "Mesh");
                        {
                            EditorGUI.indentLevel++;
                            {
                                #region skinnedMeshBoundsUpdate
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var skinnedMeshBoundsUpdate = EditorGUILayout.ToggleLeft("Skinned Mesh Renderer Bounds Update", animationTarget.skinnedMeshBoundsUpdate);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        UndoRecordObject("Inspector");
                                        animationTarget.skinnedMeshBoundsUpdate = skinnedMeshBoundsUpdate;
                                        animationCore.UpdateSkinnedMeshBounds();
                                    }
                                }
                                #endregion
                                #region skinnedMeshBoundsUpdateScale
                                if (animationTarget.skinnedMeshBoundsUpdate)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUI.BeginChangeCheck();
                                    var skinnedMeshBoundsUpdateScale = EditorGUILayout.Vector3Field("Scale", animationTarget.skinnedMeshBoundsUpdateScale);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        UndoRecordObject("Inspector");
                                        animationTarget.skinnedMeshBoundsUpdateScale = skinnedMeshBoundsUpdateScale;
                                        animationCore.UpdateSkinnedMeshBounds();
                                    }
                                    EditorGUI.indentLevel--;
                                }
                                #endregion
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
            }
            #endregion

            base.InspectorGUI_Refresh();
        }
        protected override void InspectorGUI_ImportOpenBefore()
        {
            base.InspectorGUI_ImportOpenBefore();

            VoxelHumanoidConfigreAvatar.Destroy();
        }
        protected override void InspectorGUI_Refresh() { }
    }
}
