using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;

namespace VoxelImporter
{
	[CustomEditor(typeof(VoxelSkinnedAnimationObjectBone))]
	public class VoxelSkinnedAnimationObjectBoneEditor : Editor
	{
		public VoxelSkinnedAnimationObjectBone boneTarget { get; private set; }
        public VoxelSkinnedAnimationObject objectTarget { get; private set; }
        public VoxelSkinnedAnimationObjectBone rootTarget { get; private set; }

        public VoxelSkinnedAnimationObjectBoneCore boneCore { get; protected set; }
        public VoxelSkinnedAnimationObjectCore animationCore { get; protected set; }

        private VoxelEditorCommon editorCommon;

        private class EditWeight
        {
            public EditWeight()
            {
                this.flags = (VoxelBase.VoxelVertexFlags)(-1);
                this.power = new float[(int)VoxelBase.VoxelVertexIndex.Total];
                for (int i = 0; i < this.power.Length; i++)
                {
                    this.power[i] = 1f;
                }
            }
            public EditWeight(VoxelBase.VoxelVertexFlags flags, float power = 1f)
            {
                this.flags = flags;
                this.power = new float[(int)VoxelBase.VoxelVertexIndex.Total];
                for (int i = 0; i < this.power.Length; i++)
                {
                    this.power[i] = power;
                }
            }

            public VoxelBase.VoxelVertexFlags flags;
            public float[] power = new float[(int)VoxelBase.VoxelVertexIndex.Total];
        }
        private DataTable3<EditWeight> editWeightList = new DataTable3<EditWeight>();
        
        //Editor
        private bool updateEnableVoxel;
        private bool drawEditorMesh = true;

        //GUIStyle
        private GUIStyle guiStyleBoldButton;
        private GUIStyle guiStyleBoldActiveButton;
        private GUIStyle guiStyleFoldoutBold;

        #region strings
        public static readonly string[] Edit_VoxelModeString =
        {
            VoxelSkinnedAnimationObject.Edit_VoxelMode.Voxel.ToString(),
            VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex.ToString(),
        };
        public static readonly string[] Edit_VoxelWeightModeString =
        {
            VoxelSkinnedAnimationObject.Edit_VoxelWeightMode.Voxel.ToString(),
            VoxelSkinnedAnimationObject.Edit_VoxelWeightMode.Fill.ToString(),
            VoxelSkinnedAnimationObject.Edit_VoxelWeightMode.Rect.ToString(),
        };
        public static readonly string[] Edit_VertexWeightModeString =
        {
            VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Brush.ToString(),
            VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Rect.ToString(),
        };
        public static readonly GUIContent[] Edit_BlendModeString =
        {
            new GUIContent("=", VoxelSkinnedAnimationObject.Edit_BlendMode.Replace.ToString()),
            new GUIContent("+", VoxelSkinnedAnimationObject.Edit_BlendMode.Add.ToString()),
            new GUIContent("-", VoxelSkinnedAnimationObject.Edit_BlendMode.Subtract.ToString()),
        };
        public static readonly string[] Edit_MirrorSetModeString =
        {
            " ",
            "+",
            "-",
        };
        #endregion

        void OnEnable()
        {
            boneTarget = target as VoxelSkinnedAnimationObjectBone;
            if (boneTarget == null) return;
            boneCore = new VoxelSkinnedAnimationObjectBoneCore(boneTarget);
            objectTarget = boneTarget.voxelObject;
            if (objectTarget == null) return;
            animationCore = new VoxelSkinnedAnimationObjectCore(objectTarget);

            boneCore.Initialize();

            if (!animationCore.ReadyVoxelData())
                return;

            #region rootTarget
            rootTarget = objectTarget.GetComponentInChildren<VoxelSkinnedAnimationObjectBone>();
            {
                var trans = objectTarget.transform;
                for (int i = 0; i < trans.childCount; i++)
                {
                    rootTarget = trans.GetChild(i).GetComponent<VoxelSkinnedAnimationObjectBone>();
                    if (rootTarget != null) break;
                }
            }
            #endregion

            editorCommon = new VoxelEditorCommon(objectTarget);

            if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition)
            {
                animationCore.ResetBoneTransform();
            }
            else if(objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
            {
                UpdateEnableVoxel(false);
                updateEnableVoxel = false;
            }

            #region DisableAnimation
            if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneAnimation)
            {
                if (boneTarget.edit_disablePositionAnimation && boneCore.IsHaveEraseDisablePositionAnimation())
                    boneTarget.edit_disablePositionAnimation = false;
                if (boneTarget.edit_disableRotationAnimation && boneCore.IsHaveEraseDisableRotationAnimation())
                    boneTarget.edit_disableRotationAnimation = false;
                if (boneTarget.edit_disableScaleAnimation && boneCore.IsHaveEraseDisableScaleAnimation())
                    boneTarget.edit_disableScaleAnimation = false;
            }
            #endregion

            AnimationUtility.onCurveWasModified -= EditorOnCurveWasModified;
            AnimationUtility.onCurveWasModified += EditorOnCurveWasModified;

            Undo.undoRedoPerformed -= EditorUndoRedoPerformed;
            Undo.undoRedoPerformed += EditorUndoRedoPerformed;
        }
        void OnDisable()
        {
            if (boneTarget == null || objectTarget == null) return;

            {
                if (updateEnableVoxel)
                {
                    UpdateEnableVoxel();
                }
                else
                {
                    animationCore.SetRendererCompornent();
                }
            }

            if (boneTarget.edit_weightMesh != null)
            {
                for (int i = 0; i < boneTarget.edit_weightMesh.Length; i++)
                {
                    MonoBehaviour.DestroyImmediate(boneTarget.edit_weightMesh[i]);
                }
                boneTarget.edit_weightMesh = null;
            }
            boneTarget.edit_weightColorTexture = null;

            Tools.current = VoxelEditorCommon.lastTool;

            AnimationUtility.onCurveWasModified -= EditorOnCurveWasModified;

            Undo.undoRedoPerformed -= EditorUndoRedoPerformed;
        }
        void OnDestroy()
        {
            OnDisable();
        }

        public override void OnInspectorGUI()
        {
            if (boneTarget == null || objectTarget == null)
			{
				DrawDefaultInspector();
				return;
            }

            if (objectTarget.voxelData == null)
            {
                EditorGUILayout.HelpBox("Voxel file not found. Please open file.", MessageType.Error);
                return;
            }

            serializedObject.Update();

            #region GuiStyle
            if (guiStyleBoldButton == null)
                guiStyleBoldButton = new GUIStyle(GUI.skin.button);
            guiStyleBoldButton.fontStyle = FontStyle.Bold;
            if (guiStyleBoldActiveButton == null)
                guiStyleBoldActiveButton = new GUIStyle(GUI.skin.button);
            guiStyleBoldActiveButton.fontStyle = FontStyle.Bold;
            guiStyleBoldActiveButton.normal = guiStyleBoldActiveButton.active;
            if (guiStyleFoldoutBold == null)
                guiStyleFoldoutBold = new GUIStyle(EditorStyles.foldout);
            guiStyleFoldoutBold.fontStyle = FontStyle.Bold;
            editorCommon.GUIStyleReady();
            #endregion

            var prefabType = PrefabUtility.GetPrefabType(objectTarget.gameObject);

            EditorGUI.BeginDisabledGroup(prefabType == PrefabType.Prefab);

            //Edit
            {
                #region BoneAnimation
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();
                {
                    if (GUILayout.Button("Edit Bone Animation", objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneAnimation ? guiStyleBoldActiveButton : guiStyleBoldButton, GUILayout.Height(32)))
                    {
                        Undo.RecordObject(objectTarget, "Inspector");
                        Undo.RecordObject(boneTarget, "Inspector");
                        if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneAnimation)
                        {
                            objectTarget.editLastMode = objectTarget.editMode;
                            objectTarget.editMode = VoxelSkinnedAnimationObject.Edit_Mode.None;
                            Tools.current = VoxelEditorCommon.lastTool;
                        }
                        else
                        {
                            objectTarget.editLastMode = VoxelSkinnedAnimationObject.Edit_Mode.None;
                            objectTarget.editMode = VoxelSkinnedAnimationObject.Edit_Mode.BoneAnimation;
                            Tools.current = VoxelEditorCommon.lastTool;
                            UpdateEnableVoxel();
                            #region DisableAnimation
                            {
                                if (boneTarget.edit_disablePositionAnimation && boneCore.IsHaveEraseDisablePositionAnimation())
                                    boneTarget.edit_disablePositionAnimation = false;
                                if (boneTarget.edit_disableRotationAnimation && boneCore.IsHaveEraseDisableRotationAnimation())
                                    boneTarget.edit_disableRotationAnimation = false;
                                if (boneTarget.edit_disableScaleAnimation && boneCore.IsHaveEraseDisableScaleAnimation())
                                    boneTarget.edit_disableScaleAnimation = false;
                            }
                            #endregion
                        }
                    }
                }
                EditorGUILayout.Space();
                EditorGUILayout.EndHorizontal();
                #endregion
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
				EditorGUILayout.Space();
                #region BonePosition
                {
                    EditorGUI.BeginDisabledGroup(AnimationMode.InAnimationMode());
                    if (GUILayout.Button("Edit Bone Position", objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition ? guiStyleBoldActiveButton : guiStyleBoldButton, GUILayout.Height(24)))
					{
						Undo.RecordObject(objectTarget, "Inspector");
                        if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition)
                        {
                            objectTarget.editLastMode = objectTarget.editMode;
                            objectTarget.editMode = VoxelSkinnedAnimationObject.Edit_Mode.None;
                            Tools.current = VoxelEditorCommon.lastTool;
                            UpdateEnableVoxel();
                        }
                        else
                        {
                            objectTarget.editLastMode = VoxelSkinnedAnimationObject.Edit_Mode.None;
                            objectTarget.editMode = VoxelSkinnedAnimationObject.Edit_Mode.BonePosition;
                            Tools.current = Tool.None;
                            animationCore.ResetBoneTransform();
                            UpdateEnableVoxel();
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }
                #endregion
                EditorGUILayout.Space();
                #region BoneWeight
                {
                    EditorGUI.BeginDisabledGroup(AnimationMode.InAnimationMode());
                    if (GUILayout.Button("Edit Bone Weight", objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight ? guiStyleBoldActiveButton : guiStyleBoldButton, GUILayout.Height(24)))
					{
                        Undo.RecordObject(objectTarget, "Inspector");
                        if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
                        {
                            objectTarget.editLastMode = objectTarget.editMode;
                            objectTarget.editMode = VoxelSkinnedAnimationObject.Edit_Mode.None;
                            Tools.current = VoxelEditorCommon.lastTool;
                            UpdateEnableVoxel();
                        }
                        else
                        {
                            objectTarget.editLastMode = VoxelSkinnedAnimationObject.Edit_Mode.None;
                            objectTarget.editMode = VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight;
                            Tools.current = Tool.None;
                            UpdateEnableVoxel();
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }
                #endregion
                EditorGUILayout.Space();
				EditorGUILayout.EndHorizontal();

                #region AnimationModeRecordReset
                if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition ||
                    objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
                {
                    if (AnimationMode.InAnimationMode())
                    {
                        objectTarget.editLastMode = objectTarget.editMode;
                        objectTarget.editMode = VoxelSkinnedAnimationObject.Edit_Mode.BoneAnimation;
                        Tools.current = VoxelEditorCommon.lastTool;
                    }
                }
                #endregion
            }
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            {
                var rect = EditorGUILayout.GetControlRect();
                rect.height = 2;
                GUI.Box(rect, "");
                GUILayout.Space(-rect.height);
            }

            #region Add Child Bone
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();
                {
                    if (GUILayout.Button("Add Child Bone", GUILayout.Height(24)))
                    {
                        GameObject go = new GameObject("Bone");
                        Undo.RegisterCreatedObjectUndo(go, "Create Object");
                        Undo.SetTransformParent(go.transform, boneTarget.transform, "Create Object");
                        Undo.AddComponent<VoxelSkinnedAnimationObjectBone>(go);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                        go.transform.localScale = Vector3.one;
                        UpdateEnableVoxel();
                        //
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(Selection.activeGameObject);
                    }
                }
                EditorGUILayout.Space();
                EditorGUILayout.EndHorizontal();
            }
            #endregion

            EditorGUILayout.Separator();

            //MirrorBone
            boneTarget.edit_objectFoldout = EditorGUILayout.Foldout(boneTarget.edit_objectFoldout, "Object", guiStyleFoldoutBold);
            if (boneTarget.edit_objectFoldout)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                {
                    EditorGUI.BeginChangeCheck();
                    var mirrorBone = (VoxelSkinnedAnimationObjectBone)EditorGUILayout.ObjectField("Mirror Bone", boneTarget.mirrorBone, typeof(VoxelSkinnedAnimationObjectBone), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (mirrorBone == null || boneTarget.voxelObject == mirrorBone.voxelObject)
                        {
                            Undo.RecordObject(boneTarget, "Disable Animation");
                            boneTarget.mirrorBone = mirrorBone;
                        }
                    }
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Refresh"))
            {
                Undo.RecordObject(objectTarget, "Refresh");
                UpdateEnableVoxel();
            }

            EditorGUI.EndDisabledGroup();

            #region Mirror
            {
                switch (objectTarget.editMode)
                {
                case VoxelSkinnedAnimationObject.Edit_Mode.BoneAnimation:
                    boneCore.MirrorBoneAnimation();
                    break;
                case VoxelSkinnedAnimationObject.Edit_Mode.BonePosition:
                    boneCore.MirrorBonePosition();
                    break;
                case VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight:
                    //boneCore.MirrorBoneWeight();
                    break;
                }
            }
            #endregion

            #region Changed
            if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition)
            {
                if (boneTarget.transform.hasChanged || (boneTarget.mirrorBone != null && boneTarget.mirrorBone.transform.hasChanged))
                {
                    animationCore.UpdateBoneBindposes();
                    boneTarget.transform.hasChanged = false;
                    if (boneTarget.mirrorBone != null)
                        boneTarget.mirrorBone.transform.hasChanged = false;
                }
            }
            else if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
            {
                if (boneTarget.transform.hasChanged || (boneTarget.mirrorBone != null && boneTarget.mirrorBone.transform.hasChanged))
                {
                    UpdateEnableVoxel(false);
                    boneTarget.transform.hasChanged = false;
                    if (boneTarget.mirrorBone != null)
                        boneTarget.mirrorBone.transform.hasChanged = false;
                }
            }
            #endregion

            serializedObject.ApplyModifiedProperties();
		}
        
        void OnSceneGUI()
        {
            if (boneTarget == null || objectTarget == null || rootTarget == null) return;
            if (objectTarget.voxelData == null) return;

            editorCommon.GUIStyleReady();

            Event e = Event.current;
			bool repaint = false;
            
            #region Event
            if (SceneView.currentDrawingSceneView == SceneView.lastActiveSceneView)
            {
                if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneAnimation)
                {
                    #region BoneAnimation
                    drawEditorMesh = true;
                    VoxelEditorCommon.lastTool = Tools.current;
                    switch (e.type)
                    {
                    case EventType.KeyDown:
                        if (!e.alt)
                        {
                            #region Refresh
                            if(e.keyCode == KeyCode.F5)
                            {
                                UpdateEnableVoxel();
                            }
                            #endregion
                        }
                        break;
                    case EventType.Layout:
                        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                        break;
                    }
                    #endregion
                }
                else if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition)
                {
                    #region BonePosition
                    Tools.current = Tool.None;
                    switch (e.type)
                    {
                    case EventType.KeyDown:
                        if (!e.alt)
                        {
                            if (e.keyCode == KeyCode.F5)
                            {
                                UpdateEnableVoxel();
                            }
                            else if (e.keyCode == KeyCode.Space)
                            {
                                drawEditorMesh = false;
                            }
                        }
                        break;
                    case EventType.KeyUp:
                        {
                            if (e.keyCode == KeyCode.Space)
                            {
                                drawEditorMesh = true;
                            }
                        }
                        break;
                    case EventType.Layout:
                        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                        break;
                    }
                    #endregion
                }
                else if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
                {
                    #region BoneWeight
                    Tools.current = Tool.None;
                    if (boneTarget == rootTarget)
                    {
                        switch (e.type)
                        {
                        case EventType.Layout:
                            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                            break;
                        }
                        switch (e.type)
                        {
                        case EventType.KeyDown:
                            if (!e.alt)
                            {
                                if (e.keyCode == KeyCode.F5)
                                {
                                    UpdateEnableVoxel();
                                }
                                else if (e.keyCode == KeyCode.Space)
                                {
                                    drawEditorMesh = false;
                                }
                            }
                            break;
                        case EventType.KeyUp:
                            {
                                if (e.keyCode == KeyCode.Space)
                                {
                                    drawEditorMesh = true;
                                }
                            }
                            break;
                        }
                    }
                    else
                    {
                        switch (e.type)
                        {
                        case EventType.MouseMove:
                            editWeightList.Clear();
                            editorCommon.selectionRect.Reset();
                            editorCommon.ClearPreviewMesh();
                            UpdateCursorMesh();
                            break;
                        case EventType.MouseDown:
                            if (editorCommon.CheckMousePositionEditorRects())
                            {
                                if (!e.alt && e.button == 0)
                                {
                                    editorCommon.ClearCursorMesh();
                                    EventMouseDrag(true);
                                }
                                else if (!e.alt && e.button == 1)
                                {
                                    ClearMakeAddData();
                                }
                            }
                            break;
                        case EventType.MouseDrag:
                            {
                                if (!e.alt && e.button == 0)
                                {
                                    EventMouseDrag(false);
                                }
                            }
                            break;
                        case EventType.MouseUp:
                            if (!e.alt && e.button == 0)
                            {
                                EventMouseApply();
                            }
                            ClearMakeAddData();
                            UpdateCursorMesh();
                            repaint = true;
                            break;
                        case EventType.Layout:
                            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                            break;
                        }
                        switch (e.type)
                        {
                        case EventType.KeyDown:
                            if (!e.alt)
                            {
                                if (e.keyCode == KeyCode.F5)
                                {
                                    UpdateEnableVoxel();
                                }
                                else if (e.keyCode == KeyCode.Space)
                                {
                                    drawEditorMesh = false;
                                }
                            }
                            break;
                        case EventType.KeyUp:
                            {
                                if (e.keyCode == KeyCode.Space)
                                {
                                    drawEditorMesh = true;
                                }
                            }
                            break;
                        }
                    }
                    #endregion
                }
                else
                {
                    #region None
                    drawEditorMesh = true;
                    VoxelEditorCommon.lastTool = Tools.current;
                    switch (e.type)
                    {
                    case EventType.KeyDown:
                        if (!e.alt)
                        {
                            #region Refresh
                            if (e.keyCode == KeyCode.F5)
                            {
                                UpdateEnableVoxel();
                            }
                            #endregion
                        }
                        break;
                    }
                    #endregion
                }
            }
            #endregion

            if(drawEditorMesh)
            {
                #region DrawBaseMesh
                if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition)
                {
                    if (objectTarget.mesh != null && objectTarget.atlasTexture != null)
                    {
                        editorCommon.unlitTextureMaterial.mainTexture = objectTarget.atlasTexture;
                        editorCommon.unlitTextureMaterial.color = new Color(1, 1, 1, 0.5f);
                        editorCommon.unlitTextureMaterial.SetPass(0);
                        Graphics.DrawMeshNow(objectTarget.mesh, objectTarget.transform.localToWorldMatrix);
                    }
                }
                else if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
                {
                    if (objectTarget.mesh != null)
                    {
                        editorCommon.unlitColorMaterial.color = new Color(0, 0, 0, 1f);
                        editorCommon.unlitColorMaterial.SetPass(0);
                        Graphics.DrawMeshNow(objectTarget.mesh, objectTarget.transform.localToWorldMatrix);
                    }
                }
                #endregion

                if (boneTarget != rootTarget &&
                    objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
                {
                    #region WeightMesh
                    if (boneTarget.edit_weightMesh != null)
                    {
                        for (int i = 0; i < boneTarget.edit_weightMesh.Length; i++)
                        {
                            if (boneTarget.edit_weightMesh[i] == null) continue;
                            editorCommon.vertexColorMaterial.color = new Color(1, 1, 1, 1);
                            editorCommon.vertexColorMaterial.SetPass(0);
                            Graphics.DrawMeshNow(boneTarget.edit_weightMesh[i], objectTarget.transform.localToWorldMatrix);
                        }
                    }
                    #endregion
                }

                #region DrawArrow
                if (objectTarget.editMode != VoxelSkinnedAnimationObject.Edit_Mode.None)
                {
                    DrawBoneArrow(rootTarget.transform);
                }
                #endregion
            }

            if (SceneView.currentDrawingSceneView == SceneView.lastActiveSceneView)
            {
                editorCommon.editorRectList.Clear();

                if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneAnimation)
                {
                    int mirrorCount = 0;
                    if (boneTarget.edit_mirrorSetBoneAnimation && boneTarget.mirrorBone != null)
                    {
                        if (!boneTarget.edit_disablePositionAnimation) mirrorCount++;
                        if (!boneTarget.edit_disableRotationAnimation) mirrorCount++;
                        if (!boneTarget.edit_disableScaleAnimation) mirrorCount++;
                    }
                    #region Tool
                    {
                        Handles.BeginGUI();
                        float x, y;
                        {
                            var editorBoxRect = new Rect(2, 2, 204, 96 + 55 * mirrorCount);
                            GUI.Box(editorBoxRect, "Bone Animation Editor", editorCommon.guiStyleAlphaBox);
                            editorCommon.editorRectList.Add(editorBoxRect);
                            x = editorBoxRect.x + 2;
                            y = editorBoxRect.y + 18;
                        }
                        #region Disable
                        {
                            EditorGUI.BeginDisabledGroup(objectTarget.GetComponent<Animator>() == null);
                            {
                                EditorGUI.BeginChangeCheck();
                                var edit_disablePositionAnimation = GUI.Toggle(new Rect(x, y, 200, 16), boneTarget.edit_disablePositionAnimation, "Disable Position Animation", editorCommon.guiStyleToggleLeft);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    bool enable = false;
                                    if (edit_disablePositionAnimation && boneCore.IsHaveEraseDisablePositionAnimation())
                                        enable = EditorUtility.DisplayDialog("Warning", "All position animation curve will be deleted.\nAre you sure?", "ok", "cancel");
                                    else
                                        enable = true;
                                    if (enable)
                                    {
                                        Undo.RecordObject(boneTarget, "Disable Animation");
                                        {
                                            var animator = objectTarget.GetComponent<Animator>();
                                            if (animator != null && animator.runtimeAnimatorController != null)
                                                Undo.RecordObjects(animator.runtimeAnimatorController.animationClips, "Disable Animation");
                                        }
                                        boneTarget.edit_disablePositionAnimation = edit_disablePositionAnimation;
                                        boneCore.EraseDisableAnimation();
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                            }
                            y += 16;
                            {
                                EditorGUI.BeginChangeCheck();
                                var edit_disableRotationAnimation = GUI.Toggle(new Rect(x, y, 200, 16), boneTarget.edit_disableRotationAnimation, "Disable Rotation Animation", editorCommon.guiStyleToggleLeft);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    bool enable = false;
                                    if (edit_disableRotationAnimation && boneCore.IsHaveEraseDisableRotationAnimation())
                                        enable = EditorUtility.DisplayDialog("Warning", "All rotation animation curve will be deleted.\nAre you sure?", "ok", "cancel");
                                    else
                                        enable = true;
                                    if (enable)
                                    {
                                        Undo.RecordObject(boneTarget, "Disable Animation");
                                        {
                                            var animator = objectTarget.GetComponent<Animator>();
                                            if (animator != null && animator.runtimeAnimatorController != null)
                                                Undo.RecordObjects(animator.runtimeAnimatorController.animationClips, "Disable Animation");
                                        }
                                        boneTarget.edit_disableRotationAnimation = edit_disableRotationAnimation;
                                        boneCore.EraseDisableAnimation();
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                            }
                            y += 16;
                            {
                                EditorGUI.BeginChangeCheck();
                                var edit_disableScaleAnimation = GUI.Toggle(new Rect(x, y, 200, 16), boneTarget.edit_disableScaleAnimation, "Disable Scale Animation", editorCommon.guiStyleToggleLeft);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    bool enable = false;
                                    if (edit_disableScaleAnimation && boneCore.IsHaveEraseDisableScaleAnimation())
                                        enable = EditorUtility.DisplayDialog("Warning", "All scale animation curve will be deleted.\nAre you sure?", "ok", "cancel");
                                    else
                                        enable = true;
                                    if (enable)
                                    {
                                        Undo.RecordObject(boneTarget, "Disable Animation");
                                        {
                                            var animator = objectTarget.GetComponent<Animator>();
                                            if (animator != null && animator.runtimeAnimatorController != null)
                                                Undo.RecordObjects(animator.runtimeAnimatorController.animationClips, "Disable Animation");
                                        }
                                        boneTarget.edit_disableScaleAnimation = edit_disableScaleAnimation;
                                        boneCore.EraseDisableAnimation();
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                            }
                            y += 16;
                            EditorGUI.EndDisabledGroup();
                        }
                        #endregion
                        #region Mirror
                        {
                            EditorGUI.BeginDisabledGroup(boneTarget.mirrorBone == null);
                            EditorGUI.BeginChangeCheck();
                            var edit_mirrorSetBoneAnimation = GUI.Toggle(new Rect(x, y, 200, 32), boneTarget.edit_mirrorSetBoneAnimation, string.Format("Set to mirror bone\n ({0})", boneTarget.mirrorBone != null ? boneTarget.mirrorBone.name : "none"), editorCommon.guiStyleToggleLeft);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(boneTarget, "Mirror");
                                boneTarget.edit_mirrorSetBoneAnimation = edit_mirrorSetBoneAnimation;
                                InternalEditorUtility.RepaintAllViews();
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        x += 16;
                        y += 32;
                        #endregion
                        if (boneTarget.edit_mirrorSetBoneAnimation && boneTarget.mirrorBone != null)
                        {
                            #region Mode
                            {
                                #region Position
                                if (!boneTarget.edit_disablePositionAnimation)
                                {
                                    editorCommon.guiStyleLabel.normal.textColor = new Color(1f, 0.7f, 0.7f);
                                    for (int i = 0; i < objectTarget.edit_mirrorPosition.Length; i++)
                                    {
                                        {
                                            string text = "";
                                            switch (i)
                                            {
                                            case 0: text = "Position X"; break;
                                            case 1: text = "Position Y"; break;
                                            case 2: text = "Position Z"; break;
                                            }
                                            EditorGUI.LabelField(new Rect(x, y, 64, 16), text, editorCommon.guiStyleLabel);

                                        }
                                        EditorGUI.BeginChangeCheck();
                                        var mode = (VoxelSkinnedAnimationObject.Edit_MirrorSetMode)GUI.Toolbar(new Rect(x + 64, y, 118, 16), (int)objectTarget.edit_mirrorPosition[i], Edit_MirrorSetModeString);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            Undo.RecordObject(objectTarget, "Mirror Mode");
                                            objectTarget.edit_mirrorPosition[i] = mode;
                                            InternalEditorUtility.RepaintAllViews();
                                        }
                                        y += 16 + 2;
                                    }
                                }
                                #endregion
                                #region Rotation
                                if (!boneTarget.edit_disableRotationAnimation)
                                {
                                    editorCommon.guiStyleLabel.normal.textColor = new Color(0.7f, 1f, 0.7f);
                                    for (int i = 0; i < objectTarget.edit_mirrorRotation.Length; i++)
                                    {
                                        {
                                            string text = "";
                                            switch (i)
                                            {
                                            case 0: text = "Rotation X"; break;
                                            case 1: text = "Rotation Y"; break;
                                            case 2: text = "Rotation Z"; break;
                                            }
                                            EditorGUI.LabelField(new Rect(x, y, 64, 16), text, editorCommon.guiStyleLabel);

                                        }
                                        EditorGUI.BeginChangeCheck();
                                        var mode = (VoxelSkinnedAnimationObject.Edit_MirrorSetMode)GUI.Toolbar(new Rect(x + 64, y, 118, 16), (int)objectTarget.edit_mirrorRotation[i], Edit_MirrorSetModeString);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            Undo.RecordObject(objectTarget, "Mirror Mode");
                                            objectTarget.edit_mirrorRotation[i] = mode;
                                            InternalEditorUtility.RepaintAllViews();
                                        }
                                        y += 16 + 2;
                                    }
                                }
                                #endregion
                                #region Scale
                                if (!boneTarget.edit_disableScaleAnimation)
                                {
                                    editorCommon.guiStyleLabel.normal.textColor = new Color(0.7f, 0.7f, 1f);
                                    for (int i = 0; i < objectTarget.edit_mirrorScale.Length; i++)
                                    {
                                        {
                                            string text = "";
                                            switch (i)
                                            {
                                            case 0: text = "Scale X"; break;
                                            case 1: text = "Scale Y"; break;
                                            case 2: text = "Scale Z"; break;
                                            }
                                            EditorGUI.LabelField(new Rect(x, y, 64, 16), text, editorCommon.guiStyleLabel);

                                        }
                                        EditorGUI.BeginChangeCheck();
                                        var mode = (VoxelSkinnedAnimationObject.Edit_MirrorSetMode)GUI.Toolbar(new Rect(x + 64, y, 118, 16), (int)objectTarget.edit_mirrorScale[i], Edit_MirrorSetModeString);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            Undo.RecordObject(objectTarget, "Mirror Mode");
                                            objectTarget.edit_mirrorScale[i] = mode;
                                            InternalEditorUtility.RepaintAllViews();
                                        }
                                        y += 16 + 2;
                                    }
                                }
                                #endregion
                            }
                            #endregion
                        }
                        Handles.EndGUI();
                    }
                    #endregion

                    if (drawEditorMesh)
                        GuiBoneButton();

                    if (boneTarget.transform.hasChanged)
                    {
                        boneTarget.transform.hasChanged = false;
                        boneCore.MirrorBoneAnimation();
                    }
                }
                else if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition)
                {
                    #region Tool
                    {
                        Handles.BeginGUI();
                        {
                            var editorBoxRect = new Rect(2, 2, 204, 48);
                            GUI.Box(editorBoxRect, "Bone Position Editor", editorCommon.guiStyleAlphaBox);
                            editorCommon.editorRectList.Add(editorBoxRect);
                        }
                        float x = 4;
                        float y = 20;
                        #region Mirror
                        {
                            EditorGUI.BeginDisabledGroup(boneTarget.mirrorBone == null);
                            EditorGUI.BeginChangeCheck();
                            var edit_mirrorSetBonePosition = GUI.Toggle(new Rect(x, y, 200, 32), boneTarget.edit_mirrorSetBonePosition, string.Format("Set to mirror bone\n ({0})", boneTarget.mirrorBone != null ? boneTarget.mirrorBone.name : "none"), editorCommon.guiStyleToggleLeft);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(boneTarget, "Mirror");
                                boneTarget.edit_mirrorSetBonePosition = edit_mirrorSetBonePosition;
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        #endregion
                        Handles.EndGUI();
                    }
                    #endregion

                    if (drawEditorMesh)
                        GuiBoneButton();

                    #region Handle
                    {
                        Vector3 pos = (objectTarget.bindposes[boneTarget.boneIndex] * objectTarget.transform.worldToLocalMatrix).inverse.GetColumn(3);
                        {
                            EditorGUI.BeginChangeCheck();
                            var worldResult = Handles.PositionHandle(pos, objectTarget.transform.rotation);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Vector3 move = objectTarget.transform.worldToLocalMatrix.MultiplyVector(worldResult - pos);
                                Undo.RecordObject(boneTarget.transform, "Position Move");
                                Undo.RecordObject(boneTarget, "Position Move");
                                boneTarget.transform.localPosition += move;
                                boneTarget.transform.hasChanged = false;
                                boneCore.MirrorBonePosition();
                                animationCore.UpdateBoneBindposes();
                            }
                        }
                    }
                    #endregion
                }
                else if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
                {
                    if (boneTarget == rootTarget)
                    {
                        #region Tool
                        {
                            Handles.BeginGUI();
                            float x = 4;
                            float y = 20;
                            #region Global
                            {
                                var editorBoxRect = new Rect(2, 2, 204, 40);
                                GUI.Box(editorBoxRect, "Bone Weight Editor", editorCommon.guiStyleAlphaBox);
                                editorCommon.editorRectList.Add(editorBoxRect);
                            }
                            y += 4;
                            #region WeightClear
                            {
                                if (GUI.Button(new Rect(x, y, 200, 16), "Clear All Bones Weight"))
                                {
                                    Undo.RecordObject(boneTarget, "Clear");
                                    for (int i = 0; i < objectTarget.bones.Length; i++)
                                    {
                                        objectTarget.bones[i].weightData.ClearWeight();
                                    }
                                    UpdateEnableVoxel(false);
                                }
                            }
                            y += 20;
                            #endregion
                            #endregion
                            Handles.EndGUI();
                        }
                        #endregion

                        if (drawEditorMesh)
                            GuiBoneButton();
                    }
                    else
                    {
                        #region Preview Mesh
                        if (editorCommon.previewMesh != null)
                        {
                            Color color = Color.white;
                            color.a = 0.5f + 0.5f * (1f - editorCommon.AnimationPower);
                            for (int i = 0; i < editorCommon.previewMesh.Length; i++)
                            {
                                if (editorCommon.previewMesh[i] == null) continue;
                                editorCommon.vertexColorTransparentMaterial.color = color;
                                editorCommon.vertexColorTransparentMaterial.SetPass(0);
                                Graphics.DrawMeshNow(editorCommon.previewMesh[i], objectTarget.transform.localToWorldMatrix);
                            }
                            repaint = true;
                        }
                        #endregion

                        #region Cursor Mesh
                        {
                            float color = 0.2f + 0.4f * (1f - editorCommon.AnimationPower);
                            if (editorCommon.cursorMesh != null)
                            {
                                for (int i = 0; i < editorCommon.cursorMesh.Length; i++)
                                {
                                    if (editorCommon.cursorMesh[i] == null) continue;
                                    editorCommon.vertexColorTransparentMaterial.color = new Color(1, 1, 1, color);
                                    editorCommon.vertexColorTransparentMaterial.SetPass(0);
                                    Graphics.DrawMeshNow(editorCommon.cursorMesh[i], objectTarget.transform.localToWorldMatrix);
                                }
                            }
                            repaint = true;
                        }
                        #endregion

                        #region Selection Rect
                        if ((objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Voxel && objectTarget.edit_voxelWeightMode == VoxelSkinnedAnimationObject.Edit_VoxelWeightMode.Rect) ||
                            (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex && objectTarget.edit_vertexWeightMode == VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Rect))
                        {
                            if (editorCommon.selectionRect.Enable)
                            {
                                Handles.BeginGUI();
                                GUI.Box(editorCommon.selectionRect.rect, "", "SelectionRect");
                                Handles.EndGUI();
                                repaint = true;
                            }
                        }
                        #endregion

                        #region Tool
                        {
                            Handles.BeginGUI();
                            float x = 4;
                            float y = 20;
                            {
                                var editorBoxRect = new Rect(2, 2, 204, 220);
                                GUI.Box(editorBoxRect, "Bone Weight Editor", editorCommon.guiStyleAlphaBox);
                                editorCommon.editorRectList.Add(editorBoxRect);
                            }
                            #region VoxelMode
                            {
                                EditorGUI.BeginChangeCheck();
                                var voxelMode = (VoxelSkinnedAnimationObject.Edit_VoxelMode)GUI.Toolbar(new Rect(x, y, 200, 20), (int)objectTarget.edit_voxelMode, Edit_VoxelModeString);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(objectTarget, "Voxel Mode");
                                    objectTarget.edit_voxelMode = voxelMode;
                                    ShowNotification();
                                }
                            }
                            y += 24;
                            #endregion
                            #region Voxel
                            if(objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Voxel)
                            {
                                EditorGUI.BeginChangeCheck();
                                var weightMode = (VoxelSkinnedAnimationObject.Edit_VoxelWeightMode)GUI.Toolbar(new Rect(x, y, 200, 20), (int)objectTarget.edit_voxelWeightMode, Edit_VoxelWeightModeString);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(objectTarget, "Weight Mode");
                                    objectTarget.edit_voxelWeightMode = weightMode;
                                    ShowNotification();
                                }
                            }
                            else if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex)
                            {
                                EditorGUI.BeginChangeCheck();
                                var weightMode = (VoxelSkinnedAnimationObject.Edit_VertexWeightMode)GUI.Toolbar(new Rect(x, y, 200, 20), (int)objectTarget.edit_vertexWeightMode, Edit_VertexWeightModeString);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(objectTarget, "Weight Mode");
                                    objectTarget.edit_vertexWeightMode = weightMode;
                                    ShowNotification();
                                }
                            }
                            y += 24;
                            #endregion
                            #region BlendMode
                            {
                                EditorGUI.BeginChangeCheck();
                                var blendMode = (VoxelSkinnedAnimationObject.Edit_BlendMode)GUI.Toolbar(new Rect(x, y, 200, 20), (int)objectTarget.edit_blendMode, Edit_BlendModeString);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(objectTarget, "Blend Mode");
                                    objectTarget.edit_blendMode = blendMode;
                                }
                            }
                            #endregion
                            y += 24;
                            #region Weight
                            {
                                {
                                    if(boneTarget.edit_weightColorTexture == null)
                                        boneTarget.edit_weightColorTexture = editorCommon.CreateColorTexture(GetWeightColor(objectTarget.edit_weight));
                                    editorCommon.guiStyleLabel.normal.background = boneTarget.edit_weightColorTexture;
                                    EditorGUI.LabelField(new Rect(x, y, 200, 16), "Weight", editorCommon.guiStyleLabel);
                                    editorCommon.guiStyleLabel.normal.background = null;
                                }
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var weight = GUI.HorizontalSlider(new Rect(x + 45, y, 100, 16), objectTarget.edit_weight, 0f, 1f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        Undo.RecordObject(objectTarget, "Weight");
                                        Undo.RecordObject(boneTarget, "Weight");
                                        objectTarget.edit_weight = weight;
                                        boneTarget.edit_weightColorTexture = editorCommon.CreateColorTexture(GetWeightColor(objectTarget.edit_weight));
                                    }
                                }
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var weight = EditorGUI.FloatField(new Rect(x + 150, y, 50, 16), objectTarget.edit_weight);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        weight = Mathf.Clamp(weight, 0f, 1f);
                                        Undo.RecordObject(objectTarget, "Weight");
                                        Undo.RecordObject(boneTarget, "Weight");
                                        objectTarget.edit_weight = weight;
                                        boneTarget.edit_weightColorTexture = editorCommon.CreateColorTexture(GetWeightColor(objectTarget.edit_weight));
                                    }
                                }
                            }
                            y += 20;
                            #endregion
                            #region Auto Normalize
                            {
                                EditorGUI.BeginChangeCheck();
                                var edit_autoNormalize = GUI.Toggle(new Rect(x, y, 200, 16), objectTarget.edit_autoNormalize, "Auto Normalize", editorCommon.guiStyleToggleRight);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(objectTarget, "Auto Normalize");
                                    objectTarget.edit_autoNormalize = edit_autoNormalize;
                                }
                            }
                            y += 20;
                            #endregion
                            #region BrushRadius
                            if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex &&
                                objectTarget.edit_vertexWeightMode == VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Brush)
                            {
                                {
                                    EditorGUI.LabelField(new Rect(x, y, 200, 16), "Radius", editorCommon.guiStyleLabel);
                                }
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var radius = GUI.HorizontalSlider(new Rect(x + 45, y, 100, 16), objectTarget.edit_brushRadius, 1f, 100f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        Undo.RecordObject(objectTarget, "Radius");
                                        objectTarget.edit_brushRadius = radius;
                                    }
                                }
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var radius = EditorGUI.FloatField(new Rect(x + 150, y, 50, 16), objectTarget.edit_brushRadius);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        radius = Mathf.Clamp(radius, 1f, 100f);
                                        Undo.RecordObject(objectTarget, "Radius");
                                        objectTarget.edit_brushRadius = radius;
                                    }
                                }
                            }
                            y += 20;
                            #endregion
                            #region BrushCurve
                            if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex &&
                                objectTarget.edit_vertexWeightMode == VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Brush)
                            {
                                {
                                    EditorGUI.LabelField(new Rect(x, y, 200, 16), "Curve", editorCommon.guiStyleLabel);
                                }
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var curve = EditorGUI.CurveField(new Rect(x + 45, y, 200 - 45, 16), objectTarget.edit_brushCurve);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        Undo.RecordObject(objectTarget, "Curve");
                                        objectTarget.edit_brushCurve = curve;
                                    }
                                }
                            }
                            y += 20;
                            #endregion
                            #region Mirror
                            {
                                EditorGUI.BeginDisabledGroup(boneTarget.mirrorBone == null);
                                EditorGUI.BeginChangeCheck();
                                var edit_mirrorSetBoneWeight = GUI.Toggle(new Rect(x, y, 200, 32), boneTarget.edit_mirrorSetBoneWeight, string.Format("Set to mirror bone\n ({0})", boneTarget.mirrorBone != null ? boneTarget.mirrorBone.name : "none"), editorCommon.guiStyleToggleLeft);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(boneTarget, "Mirror");
                                    boneTarget.edit_mirrorSetBoneWeight = edit_mirrorSetBoneWeight;
                                }
                                EditorGUI.EndDisabledGroup();
                            }
                            y += 32;
                            #endregion
                            #region WeightClear
                            {
                                if (GUI.Button(new Rect(x, y, 200, 16), "Clear Bone Weight"))
                                {
                                    Undo.RecordObject(boneTarget, "Clear");
                                    boneTarget.weightData.ClearWeight();
                                    UpdateEnableVoxel(false);
                                }
                            }
                            y += 20;
                            #endregion
                            Handles.EndGUI();
                        }
                        #endregion

                        if (drawEditorMesh)
                            GuiBoneButton();

                        #region Cursor
                        if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex &&
                            objectTarget.edit_vertexWeightMode == VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Brush)
                        {
                            const float OneRadius = 162 / 2f;
                            Vector3 pos = SceneView.currentDrawingSceneView.camera.ScreenToWorldPoint(new Vector3(e.mousePosition.x, SceneView.currentDrawingSceneView.camera.pixelHeight - e.mousePosition.y, Mathf.Lerp(SceneView.currentDrawingSceneView.camera.nearClipPlane, SceneView.currentDrawingSceneView.camera.farClipPlane, 0.0001f)));
                            float radius = objectTarget.edit_brushRadius / OneRadius * HandleUtility.GetHandleSize(pos);
                            Handles.color = GetWeightColor(objectTarget.edit_weight);
                            Handles.DrawWireDisc(pos, SceneView.currentDrawingSceneView.camera.transform.forward, radius);
                        }
                        #endregion

                        #region ToolTip
                        {
                            if (!string.IsNullOrEmpty(GUI.tooltip))
                            {
                                Handles.BeginGUI();
                                {
                                    var stringSize = GUI.skin.box.CalcSize(new GUIContent(GUI.tooltip));
                                    EditorGUI.LabelField(new Rect(e.mousePosition.x + 16, e.mousePosition.y, stringSize.x, stringSize.y), GUI.tooltip, GUI.skin.box);
                                }
                                Handles.EndGUI();
                            }
                        }
                        #endregion
                    }
                }
            }

            if (repaint)
			{
				SceneView.currentDrawingSceneView.Repaint();
			}
        }

        private void UpdatePreviewMesh()
        {
            editorCommon.ClearPreviewMesh();
            {
                List<VoxelData.Voxel> voxels = new List<VoxelData.Voxel>();
                {
                    editWeightList.AllAction((x, y, z, w) =>
                    {
                        if (objectTarget.voxelData.VoxelTableContains(x, y, z) < 0) return;
                        voxels.Add(new VoxelData.Voxel(x, y, z, -1));
                    });
                }
                List<VoxelObjectCore.Edit_VerticesInfo> infoList = new List<VoxelObjectCore.Edit_VerticesInfo>();
                editorCommon.previewMesh = animationCore.Edit_CreateMesh(voxels, infoList, false);
                for (int i = 0; i < editorCommon.previewMesh.Length; i++)
                {
                    Func<IntVector3, VoxelBase.VoxelVertexIndex, float[], float> GetWeight = (pos, index, power) =>
                    {
                        var w = objectTarget.edit_weight;
                        if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex &&
                            objectTarget.edit_vertexWeightMode == VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Brush)
                        {
                            w *= objectTarget.edit_brushCurve.Evaluate(power[(int)index]);
                        }
                        switch (objectTarget.edit_blendMode)
                        {
                        case VoxelSkinnedAnimationObject.Edit_BlendMode.Replace:
                            break;
                        case VoxelSkinnedAnimationObject.Edit_BlendMode.Add:
                            {
                                var tmp = boneTarget.weightData.GetWeight(pos);
                                if (tmp != null) w = tmp.GetWeight(index) + w;
                            }
                            break;
                        case VoxelSkinnedAnimationObject.Edit_BlendMode.Subtract:
                            {
                                var tmp = boneTarget.weightData.GetWeight(pos);
                                if (tmp != null) w = tmp.GetWeight(index) - w;
                                else w = 0f;
                            }
                            break;
                        default:
                            Assert.IsTrue(false);
                            break;
                        }
                        return Mathf.Clamp(w, 0f, 1f);
                    };
                    Color[] colors = new Color[editorCommon.previewMesh[i].vertexCount];
                    for (int j = 0; j < infoList.Count; j++)
                    {
                        var editWeight = editWeightList.Get(infoList[j].position);
                        float weight = -1f;
                        switch (infoList[j].vertexIndex)
                        {
                        case VoxelBase.VoxelVertexIndex.XYZ:
                            if ((editWeight.flags & VoxelBase.VoxelVertexFlags.XYZ) != 0)
                                weight = GetWeight(infoList[j].position, infoList[j].vertexIndex, editWeight.power);
                            break;
                        case VoxelBase.VoxelVertexIndex._XYZ:
                            if ((editWeight.flags & VoxelBase.VoxelVertexFlags._XYZ) != 0)
                                weight = GetWeight(infoList[j].position, infoList[j].vertexIndex, editWeight.power);
                            break;
                        case VoxelBase.VoxelVertexIndex.X_YZ:
                            if ((editWeight.flags & VoxelBase.VoxelVertexFlags.X_YZ) != 0)
                                weight = GetWeight(infoList[j].position, infoList[j].vertexIndex, editWeight.power);
                            break;
                        case VoxelBase.VoxelVertexIndex.XY_Z:
                            if ((editWeight.flags & VoxelBase.VoxelVertexFlags.XY_Z) != 0)
                                weight = GetWeight(infoList[j].position, infoList[j].vertexIndex, editWeight.power);
                            break;
                        case VoxelBase.VoxelVertexIndex._X_YZ:
                            if ((editWeight.flags & VoxelBase.VoxelVertexFlags._X_YZ) != 0)
                                weight = GetWeight(infoList[j].position, infoList[j].vertexIndex, editWeight.power);
                            break;
                        case VoxelBase.VoxelVertexIndex._XY_Z:
                            if ((editWeight.flags & VoxelBase.VoxelVertexFlags._XY_Z) != 0)
                                weight = GetWeight(infoList[j].position, infoList[j].vertexIndex, editWeight.power);
                            break;
                        case VoxelBase.VoxelVertexIndex.X_Y_Z:
                            if ((editWeight.flags & VoxelBase.VoxelVertexFlags.X_Y_Z) != 0)
                                weight = GetWeight(infoList[j].position, infoList[j].vertexIndex, editWeight.power);
                            break;
                        case VoxelBase.VoxelVertexIndex._X_Y_Z:
                            if ((editWeight.flags & VoxelBase.VoxelVertexFlags._X_Y_Z) != 0)
                                weight = GetWeight(infoList[j].position, infoList[j].vertexIndex, editWeight.power);
                            break;
                        }
                        if (weight >= 0f)
                            colors[j] = GetWeightColor(weight);
                        else
                            colors[j] = Color.clear;
                    }
                    editorCommon.previewMesh[i].colors = colors;
                }
            }
        }
        private void UpdateCursorMesh()
        {
            editorCommon.ClearCursorMesh();
            if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Voxel)
            {
                switch (objectTarget.edit_voxelWeightMode)
                {
                case VoxelSkinnedAnimationObject.Edit_VoxelWeightMode.Voxel:
                    {
                        var result = editorCommon.GetMousePositionVoxel();
                        if (result.HasValue)
                        {
                            editorCommon.cursorMesh = animationCore.Edit_CreateMesh(new List<VoxelData.Voxel>() { new VoxelData.Voxel() { position = result.Value, palette = -1} });
                        }
                    }
                    break;
                case VoxelSkinnedAnimationObject.Edit_VoxelWeightMode.Fill:
                    {
                        var pos = editorCommon.GetMousePositionVoxel();
                        if (pos.HasValue)
                        {
                            var faceAreaTable = editorCommon.GetFillVoxelFaceAreaTable(pos.Value);
                            if (faceAreaTable != null)
                                editorCommon.cursorMesh = new Mesh[1] { animationCore.Edit_CreateMeshOnly_Mesh(faceAreaTable, null, null) };
                        }
                    }
                    break;
                }
            }
        }

        private void ClearMakeAddData()
        {
            editWeightList.Clear();
            editorCommon.selectionRect.Reset();
            editorCommon.ClearPreviewMesh();
            editorCommon.ClearCursorMesh();
        }

        private void EventMouseDrag(bool first)
        {
            UpdateCursorMesh();
            if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Voxel)
            {
                #region Voxel
                switch (objectTarget.edit_voxelWeightMode)
                {
                case VoxelSkinnedAnimationObject.Edit_VoxelWeightMode.Voxel:
                    {
                        var result = editorCommon.GetMousePositionVoxel();
                        if (result.HasValue)
                        {
                            if (!editWeightList.Contains(result.Value))
                            {
                                editWeightList.Set(result.Value, new EditWeight());
                                UpdatePreviewMesh();
                            }
                        }
                    }
                    break;
                case VoxelSkinnedAnimationObject.Edit_VoxelWeightMode.Fill:
                    {
                        var pos = editorCommon.GetMousePositionVoxel();
                        if (pos.HasValue)
                        {
                            var result = editorCommon.GetFillVoxel(pos.Value);
                            if (result != null)
                            {
                                for (int i = 0; i < result.Count; i++)
                                {
                                    if (!editWeightList.Contains(result[i]))
                                    {
                                        editWeightList.Set(result[i], new EditWeight());
                                    }
                                }
                                UpdatePreviewMesh();
                            }
                        }
                    }
                    break;
                case VoxelSkinnedAnimationObject.Edit_VoxelWeightMode.Rect:
                    {
                        var pos = new IntVector2((int)Event.current.mousePosition.x, (int)Event.current.mousePosition.y);
                        if (first) { editorCommon.selectionRect.Reset(); editorCommon.selectionRect.SetStart(pos); }
                        else editorCommon.selectionRect.SetEnd(pos);
                        //
                        editWeightList.Clear();
                        {
                            var list = editorCommon.GetSelectionRectVoxel();
                            for (int i = 0; i < list.Count; i++)
                            {
                                editWeightList.Set(list[i], new EditWeight());
                            }
                        }
                        UpdatePreviewMesh();
                    }
                    break;
                }
                #endregion
            }
            else if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex)
            {
                #region Vertex
                Action<IntVector3, float> AddEditWeightList = (basePos, power) =>
                {
                    Action<IntVector3, VoxelBase.VoxelVertexFlags> AddEditWeightPosition = (pos, flags) =>
                    {
                        int index = -1;
                        switch (flags)
                        {
                        case VoxelBase.VoxelVertexFlags.XYZ: index = (int)VoxelBase.VoxelVertexIndex.XYZ; break;
                        case VoxelBase.VoxelVertexFlags._XYZ: index = (int)VoxelBase.VoxelVertexIndex._XYZ; break;
                        case VoxelBase.VoxelVertexFlags.X_YZ: index = (int)VoxelBase.VoxelVertexIndex.X_YZ; break;
                        case VoxelBase.VoxelVertexFlags.XY_Z: index = (int)VoxelBase.VoxelVertexIndex.XY_Z; break;
                        case VoxelBase.VoxelVertexFlags._X_YZ: index = (int)VoxelBase.VoxelVertexIndex._X_YZ; break;
                        case VoxelBase.VoxelVertexFlags._XY_Z: index = (int)VoxelBase.VoxelVertexIndex._XY_Z; break;
                        case VoxelBase.VoxelVertexFlags.X_Y_Z: index = (int)VoxelBase.VoxelVertexIndex.X_Y_Z; break;
                        case VoxelBase.VoxelVertexFlags._X_Y_Z: index = (int)VoxelBase.VoxelVertexIndex._X_Y_Z; break;
                        default: Assert.IsTrue(false); break;
                        }
                        if (objectTarget.voxelData.VoxelTableContains(pos) >= 0)
                        {
                            if (editWeightList.Contains(pos))
                            {
                                var editWeight = editWeightList.Get(pos);
                                editWeight.flags |= flags;
                                editWeight.power[index] = power;
                            }
                            else
                            {
                                var editWeight = new EditWeight(flags, 0f);
                                editWeight.power[index] = power;
                                editWeightList.Set(pos, editWeight);
                            }
                        }
                    };

                    AddEditWeightPosition(new IntVector3(basePos.x - 1, basePos.y - 1, basePos.z - 1), VoxelBase.VoxelVertexFlags.XYZ);
                    AddEditWeightPosition(new IntVector3(basePos.x, basePos.y - 1, basePos.z - 1), VoxelBase.VoxelVertexFlags._XYZ);
                    AddEditWeightPosition(new IntVector3(basePos.x - 1, basePos.y, basePos.z - 1), VoxelBase.VoxelVertexFlags.X_YZ);
                    AddEditWeightPosition(new IntVector3(basePos.x - 1, basePos.y - 1, basePos.z), VoxelBase.VoxelVertexFlags.XY_Z);
                    AddEditWeightPosition(new IntVector3(basePos.x, basePos.y, basePos.z - 1), VoxelBase.VoxelVertexFlags._X_YZ);
                    AddEditWeightPosition(new IntVector3(basePos.x, basePos.y - 1, basePos.z), VoxelBase.VoxelVertexFlags._XY_Z);
                    AddEditWeightPosition(new IntVector3(basePos.x - 1, basePos.y, basePos.z), VoxelBase.VoxelVertexFlags.X_Y_Z);
                    AddEditWeightPosition(basePos, VoxelBase.VoxelVertexFlags._X_Y_Z);
                };
                switch (objectTarget.edit_vertexWeightMode)
                {
                case VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Brush:
                    {
                        var vertexList = editorCommon.GetMousePositionVertex(objectTarget.edit_brushRadius);
                        for (int i = 0; i < vertexList.Count; i++)
                        {
                            AddEditWeightList(vertexList[i].position, vertexList[i].power);
                        }
                        UpdatePreviewMesh();
                    }
                    break;
                case VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Rect:
                    {
                        var pos = new IntVector2((int)Event.current.mousePosition.x, (int)Event.current.mousePosition.y);
                        if (first) { editorCommon.selectionRect.Reset(); editorCommon.selectionRect.SetStart(pos); }
                        else editorCommon.selectionRect.SetEnd(pos);
                        //
                        editWeightList.Clear();
                        {
                            var list = editorCommon.GetSelectionRectVertex();
                            for (int i = 0; i < list.Count; i++)
                            {
                                AddEditWeightList(list[i], 1f);
                            }
                        }
                        UpdatePreviewMesh();
                    }
                    break;
                }
                #endregion
            }
        }
        private void EventMouseApply()
        {
            if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
            {
                bool update = false;
                
                {
                    Undo.RecordObject(objectTarget, "Weight");
                    Undo.RecordObjects(objectTarget.bones, "Weight");

                    WeightData.VoxelWeight weight = new WeightData.VoxelWeight();
                    Action<IntVector3, VoxelBase.VoxelVertexIndex, float[]> SetWeight = (pos, index, power) =>
                    {
                        var w = objectTarget.edit_weight;
                        var powerR = power[(int)index];
                        if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex &&
                            objectTarget.edit_vertexWeightMode == VoxelSkinnedAnimationObject.Edit_VertexWeightMode.Brush)
                        {
                            powerR = objectTarget.edit_brushCurve.Evaluate(power[(int)index]);
                            w *= powerR;
                        }
                        switch (objectTarget.edit_blendMode)
                        {
                        case VoxelSkinnedAnimationObject.Edit_BlendMode.Replace:
                            break;
                        case VoxelSkinnedAnimationObject.Edit_BlendMode.Add:
                            w = weight.GetWeight(index) + w;
                            break;
                        case VoxelSkinnedAnimationObject.Edit_BlendMode.Subtract:
                            w = weight.GetWeight(index) - w;
                            break;
                        default:
                            Assert.IsTrue(false);
                            break;
                        }
                        w = Mathf.Clamp(w, 0f, 1f);
                        if (objectTarget.edit_autoNormalize)
                        {
                            for (int m = 0; m < 2; m++)
                            {
                                IntVector3 posTmp = IntVector3.zero;
                                VoxelBase.VoxelVertexIndex indexTmp = 0;
                                if (m == 0)
                                {
                                    posTmp = pos;
                                    indexTmp = index;
                                }
                                else
                                {
                                    if(!boneTarget.edit_mirrorSetBoneWeight || boneTarget.mirrorBone == null) break;
                                    posTmp = boneCore.GetMirrorVoxelPosition(pos);
                                    switch (index)
                                    {
                                    case VoxelBase.VoxelVertexIndex.XYZ: indexTmp = VoxelBase.VoxelVertexIndex._XYZ; break;
                                    case VoxelBase.VoxelVertexIndex._XYZ: indexTmp = VoxelBase.VoxelVertexIndex.XYZ; break;
                                    case VoxelBase.VoxelVertexIndex.X_YZ: indexTmp = VoxelBase.VoxelVertexIndex._X_YZ; break;
                                    case VoxelBase.VoxelVertexIndex.XY_Z: indexTmp = VoxelBase.VoxelVertexIndex._XY_Z; break;
                                    case VoxelBase.VoxelVertexIndex._X_YZ: indexTmp = VoxelBase.VoxelVertexIndex.X_YZ; break;
                                    case VoxelBase.VoxelVertexIndex._XY_Z: indexTmp = VoxelBase.VoxelVertexIndex.XY_Z; break;
                                    case VoxelBase.VoxelVertexIndex.X_Y_Z: indexTmp = VoxelBase.VoxelVertexIndex._X_Y_Z; break;
                                    case VoxelBase.VoxelVertexIndex._X_Y_Z: indexTmp = VoxelBase.VoxelVertexIndex.X_Y_Z; break;
                                    default: Assert.IsTrue(false); break;
                                    }
                                }

                                float subPower = 0f;
                                for (int i = 0; i < objectTarget.bones.Length; i++)
                                {
                                    if (objectTarget.bones[i] == boneTarget) continue;
                                    var subWeight = objectTarget.bones[i].weightData.GetWeight(posTmp);
                                    if (subWeight == null) continue;
                                    subPower += subWeight.GetWeight(indexTmp);
                                }
                                if (subPower > 0f)
                                {
                                    float subRate = (1f - w) / subPower;
                                    for (int i = 0; i < objectTarget.bones.Length; i++)
                                    {
                                        if (objectTarget.bones[i] == boneTarget) continue;
                                        var subWeight = objectTarget.bones[i].weightData.GetWeight(posTmp);
                                        if (subWeight == null) continue;
                                        subWeight.SetWeight(indexTmp, subWeight.GetWeight(indexTmp) * subRate);
                                    }
                                }
                            }
                        }
                        weight.SetWeight(index, w);
                    };
                    editWeightList.AllAction((x, y, z, w) =>
                    {
                        if (!update)
                            DisconnectPrefabInstance();

                        var pos = new IntVector3(x, y, z);
                        {
                            var tmp = boneTarget.weightData.GetWeight(pos);
                            if (tmp != null) weight = tmp;
                            else weight = new WeightData.VoxelWeight();
                        }
                        if ((w.flags & VoxelBase.VoxelVertexFlags.XYZ) != 0)
                            SetWeight(pos, VoxelBase.VoxelVertexIndex.XYZ, w.power);
                        if ((w.flags & VoxelBase.VoxelVertexFlags._XYZ) != 0)
                            SetWeight(pos, VoxelBase.VoxelVertexIndex._XYZ, w.power);
                        if ((w.flags & VoxelBase.VoxelVertexFlags.X_YZ) != 0)
                            SetWeight(pos, VoxelBase.VoxelVertexIndex.X_YZ, w.power);
                        if ((w.flags & VoxelBase.VoxelVertexFlags.XY_Z) != 0)
                            SetWeight(pos, VoxelBase.VoxelVertexIndex.XY_Z, w.power);
                        if ((w.flags & VoxelBase.VoxelVertexFlags._X_YZ) != 0)
                            SetWeight(pos, VoxelBase.VoxelVertexIndex._X_YZ, w.power);
                        if ((w.flags & VoxelBase.VoxelVertexFlags._XY_Z) != 0)
                            SetWeight(pos, VoxelBase.VoxelVertexIndex._XY_Z, w.power);
                        if ((w.flags & VoxelBase.VoxelVertexFlags.X_Y_Z) != 0)
                            SetWeight(pos, VoxelBase.VoxelVertexIndex.X_Y_Z, w.power);
                        if ((w.flags & VoxelBase.VoxelVertexFlags._X_Y_Z) != 0)
                            SetWeight(pos, VoxelBase.VoxelVertexIndex._X_Y_Z, w.power);
                        boneTarget.weightData.SetWeight(pos, weight);
                        update = true;
                    });
                    editWeightList.Clear();
                    if (update)
                    {
                        boneCore.MirrorBoneWeight();
                        UpdateEnableVoxel(false);
                    }
                }
            }
        }

        private void ShowNotification()
        {
            if(objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Voxel)
                SceneView.currentDrawingSceneView.ShowNotification(new GUIContent(string.Format("{0} - {1}", objectTarget.edit_voxelMode, objectTarget.edit_voxelWeightMode)));
            else if (objectTarget.edit_voxelMode == VoxelSkinnedAnimationObject.Edit_VoxelMode.Vertex)
                SceneView.currentDrawingSceneView.ShowNotification(new GUIContent(string.Format("{0} - {1}", objectTarget.edit_voxelMode, objectTarget.edit_vertexWeightMode)));
        }

        private void GuiBoneButton()
        {
            Handles.BeginGUI();
            for (int i = 0; i < objectTarget.bones.Length; i++)
            {
                if (Selection.activeGameObject == objectTarget.bones[i].gameObject) continue;

                Vector3 pos;
                if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition ||
                    objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
                {
                    var tr = objectTarget.bones[i];
                    pos = (objectTarget.bindposes[tr.boneIndex] * objectTarget.transform.worldToLocalMatrix).inverse.GetColumn(3);
                }
                else
                {
                    pos = objectTarget.bones[i].transform.position;
                }

                var screen = SceneView.currentDrawingSceneView.camera.WorldToScreenPoint(pos);
                screen.y = SceneView.currentDrawingSceneView.camera.pixelHeight - screen.y;

                const int Size = 16;
                EditorGUI.BeginChangeCheck();
                {
                    var rect = new Rect(screen.x - Size / 2f, screen.y - Size / 2f, Size, Size);
                    GUI.Button(rect, "");
                    editorCommon.editorRectList.Add(rect);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Selection.activeGameObject = objectTarget.bones[i].gameObject;
                    EditorGUIUtility.PingObject(Selection.activeGameObject);
                    break;
                }
            }
            Handles.EndGUI();
        }
        
        private void DrawBoneArrow(Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var ct = t.GetChild(i);
                var ctr = ct.GetComponent<VoxelSkinnedAnimationObjectBone>();
                if (ctr != null)
                {
                    if (boneTarget == ctr)
                        editorCommon.vertexColorTransparentMaterial.color = Color.yellow;
                    else
                        editorCommon.vertexColorTransparentMaterial.color = Color.green;
                    editorCommon.vertexColorTransparentMaterial.SetPass(0);
                    Vector3 posA, posB;
                    if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BonePosition ||
                        objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
                    {
                        var tr = t.GetComponent<VoxelSkinnedAnimationObjectBone>();
                        posA = (objectTarget.bindposes[tr.boneIndex] * objectTarget.transform.worldToLocalMatrix).inverse.GetColumn(3);
                        posB = (objectTarget.bindposes[ctr.boneIndex] * objectTarget.transform.worldToLocalMatrix).inverse.GetColumn(3);
                    }
                    else
                    {
                        posA = t.position;
                        posB = ct.position;
                    }
                    var vec = posB - posA;
                    var length = vec.magnitude;
                    Quaternion qat = length > 0f ? Quaternion.LookRotation(vec) : Quaternion.identity;
                    Matrix4x4 mat = Matrix4x4.TRS(posA, qat, new Vector3(length, length, length));
                    Graphics.DrawMeshNow(editorCommon.arrow, mat);

                    DrawBoneArrow(ct);
                }
            }
        }

        private void UpdateEnableVoxel(bool updateMesh = true)
        {
            if (boneTarget == null || objectTarget == null || rootTarget == null) return;

            if (updateMesh)
            {
                animationCore.ReCreate();
                updateEnableVoxel = false;
            }
            else
            {
                var boneCount = objectTarget.bones.Length;
                animationCore.UpdateBoneWeight();
                if (boneCount != objectTarget.bones.Length)
                {
                    animationCore.ReCreate();
                    updateEnableVoxel = false;
                }
                else
                {
                    updateEnableVoxel = true;
                }
            }
            #region WeightMesh
            if (objectTarget.editMode == VoxelSkinnedAnimationObject.Edit_Mode.BoneWeight)
            {
                List<VoxelObjectCore.Edit_VerticesInfo> infoList = new List<VoxelObjectCore.Edit_VerticesInfo>();
                {
                    List<VoxelData.Voxel> voxels = new List<VoxelData.Voxel>();
                    boneTarget.weightData.AllAction((pos, weights) =>
                    {
                        if (objectTarget.voxelData.VoxelTableContains(pos) < 0)
                            return;
                        voxels.Add(new VoxelData.Voxel(pos.x, pos.y, pos.z, -1));
                    });

                    boneTarget.edit_weightMesh = animationCore.Edit_CreateMesh(voxels, infoList);
                }
                for (int i = 0; i < boneTarget.edit_weightMesh.Length; i++)
                {
                    Color[] colors = new Color[boneTarget.edit_weightMesh[i].vertexCount];
                    for (int j = 0; j < infoList.Count; j++)
                    {
                        var weight = boneTarget.weightData.GetWeight(infoList[j].position);
                        Assert.IsNotNull(weight);
                        colors[j] = GetWeightColor(weight.GetWeight(infoList[j].vertexIndex));
                    }
                    boneTarget.edit_weightMesh[i].colors = colors;
                }
            }
            else
            {
                if (boneTarget.edit_weightMesh != null)
                {
                    for (int i = 0; i < boneTarget.edit_weightMesh.Length; i++)
                    {
                        DestroyImmediate(boneTarget.edit_weightMesh[i]);
                    }
                    boneTarget.edit_weightMesh = null;
                }
            }
            #endregion
        }

        private Color GetWeightColor(float weight, float BaseColor = 0.7f)
        {
            if (weight >= 0.75f)
            {
                return Color.Lerp(new Color(BaseColor, BaseColor, 0, 1), new Color(BaseColor, 0, 0, 1), (weight - 0.75f) / 0.25f);
            }
            else if (weight >= 0.5f)
            {
                return Color.Lerp(new Color(0, BaseColor, 0, 1), new Color(BaseColor, BaseColor, 0, 1), (weight - 0.5f) / 0.25f);
            }
            else if (weight >= 0.25f)
            {
                return Color.Lerp(new Color(0, BaseColor, BaseColor, 1), new Color(0, BaseColor, 0, 1), (weight - 0.25f) / 0.25f);
            }
            else
            {
                return Color.Lerp(new Color(0, 0, BaseColor, 1), new Color(0, BaseColor, BaseColor, 1), (weight) / 0.25f);
            }
        }

        private int EditorOnCurveWasModifiedStack = 0;
        private void EditorOnCurveWasModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType deleted)
        {
            if (boneTarget == null)
                return;
            
            if (boneTarget != null && boneTarget.voxelObject != null && boneTarget.voxelObject.bones != null)
            {
                if (EditorOnCurveWasModifiedStack++ == 0)
                {
                    VoxelSkinnedAnimationObjectBone boneTmp = null;
                    VoxelSkinnedAnimationObjectBoneCore boneCoreTmp = null;
                    for (int i = 0; i < boneTarget.voxelObject.bones.Length; i++)
                    {
                        boneCoreTmp = new VoxelSkinnedAnimationObjectBoneCore(boneTarget.voxelObject.bones[i]);
                        if (boneCoreTmp.fullPathBoneName == binding.path)
                        {
                            boneTmp = boneTarget.voxelObject.bones[i];
                            break;
                        }
                    }
                    if (boneTmp != null && boneCoreTmp != null)
                    {
                        if (deleted == AnimationUtility.CurveModifiedType.CurveModified)
                        {
                            if (boneTmp.edit_disablePositionAnimation || boneTmp.edit_disableRotationAnimation || boneTmp.edit_disableScaleAnimation)
                            {
                                if ((boneTmp.edit_disablePositionAnimation && binding.propertyName.StartsWith("m_LocalPosition.")) ||
                                    (boneTmp.edit_disableRotationAnimation && binding.propertyName.StartsWith("localEulerAnglesRaw.")) ||
                                    (boneTmp.edit_disableScaleAnimation && binding.propertyName.StartsWith("m_LocalScale.")))
                                {
                                    AnimationUtility.SetEditorCurve(clip, binding, null);
                                }
                            }
                        }

                        boneCoreTmp.MirroringAnimation();
                    }
                }
                EditorOnCurveWasModifiedStack--;
            }
        }

        private void DisconnectPrefabInstance()
        {
            if (PrefabUtility.GetPrefabType(boneTarget) == PrefabType.PrefabInstance)
            {
                PrefabUtility.DisconnectPrefabInstance(boneTarget);
            }
        }

        private void EditorUndoRedoPerformed()
        {
            if (boneTarget != null)
            {
                UpdateEnableVoxel(false);
            }
            Repaint();
        }
    }
}
