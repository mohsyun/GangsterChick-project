using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace VoxelImporter
{
    public abstract class VoxelBaseEditor : Editor
    {
        public VoxelBase baseTarget { get; protected set; }
        public VoxelBaseCore baseCore { get; protected set; }

        protected ReorderableList materialList;

        protected VoxelEditorCommon editorCommon;

        protected bool drawEditorMesh = true;
        protected FlagTable3 editVoxelList = new FlagTable3();

        #region GUIStyle
        protected GUIStyle guiStyleBold;
        protected GUIStyle guiStyleMagentaBold;
        protected GUIStyle guiStyleRedBold;
        protected GUIStyle guiStyleFoldoutBold;
        protected GUIStyle guiStyleBoldActiveButton;
        protected GUIStyle guiStyleDropDown;
        protected GUIStyle guiStyleLabelMiddleLeftItalic;
        protected GUIStyle guiStyleTextFieldMiddleLeft;
        #endregion

        #region strings
        public static readonly string[] Edit_MaterialModeString =
        {
            VoxelBase.Edit_MaterialMode.Add.ToString(),
            VoxelBase.Edit_MaterialMode.Remove.ToString(),
        };
        public static readonly string[] Edit_MaterialTypeModeString =
        {
            VoxelBase.Edit_MaterialTypeMode.Voxel.ToString(),
            VoxelBase.Edit_MaterialTypeMode.Fill.ToString(),
            VoxelBase.Edit_MaterialTypeMode.Rect.ToString(),
        };
        #endregion

        protected virtual void OnEnable()
        {
            baseTarget = target as VoxelBase;
            if (baseTarget == null) return;

            editorCommon = new VoxelEditorCommon(baseTarget);

            Undo.undoRedoPerformed -= EditorUndoRedoPerformed;
            Undo.undoRedoPerformed += EditorUndoRedoPerformed;
        }
        protected virtual void OnDisable()
        {
            AfterRefresh();

            EditEnableMeshDestroy();

            baseCore.SetSelectedWireframeHidden(false);

            Undo.undoRedoPerformed -= EditorUndoRedoPerformed;
        }
        protected virtual void OnDestroy()
        {
            OnDisable();
        }

        public virtual void GUIStyleReady()
        {
            //Styles
            if (guiStyleBold == null)
                guiStyleBold = new GUIStyle(EditorStyles.boldLabel);
            if (guiStyleMagentaBold == null)
                guiStyleMagentaBold = new GUIStyle(EditorStyles.boldLabel);
            guiStyleMagentaBold.normal.textColor = Color.magenta;
            if (guiStyleRedBold == null)
                guiStyleRedBold = new GUIStyle(EditorStyles.boldLabel);
            guiStyleRedBold.normal.textColor = Color.red;
            if (guiStyleFoldoutBold == null)
                guiStyleFoldoutBold = new GUIStyle(EditorStyles.foldout);
            guiStyleFoldoutBold.fontStyle = FontStyle.Bold;
            if (guiStyleBoldActiveButton == null)
                guiStyleBoldActiveButton = new GUIStyle(GUI.skin.button);
            guiStyleBoldActiveButton.normal = guiStyleBoldActiveButton.active;
            if (guiStyleDropDown == null)
                guiStyleDropDown = new GUIStyle("DropDown");
            if (guiStyleLabelMiddleLeftItalic == null)
                guiStyleLabelMiddleLeftItalic = new GUIStyle(EditorStyles.label);
            guiStyleLabelMiddleLeftItalic.alignment = TextAnchor.MiddleLeft;
            guiStyleLabelMiddleLeftItalic.fontStyle = FontStyle.Italic;
            if (guiStyleTextFieldMiddleLeft == null)
                guiStyleTextFieldMiddleLeft = new GUIStyle(EditorStyles.textField);
            guiStyleTextFieldMiddleLeft.alignment = TextAnchor.MiddleLeft;
        }

        public override void OnInspectorGUI()
        {
            if (baseCore == null)
            {
                DrawDefaultInspector();
                return;
            }

            baseCore.AutoSetSelectedWireframeHidden();

            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(PrefabUtility.GetPrefabType(baseTarget) == PrefabType.Prefab);

            InspectorGUI();

            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void InspectorGUI()
        {
            GUIStyleReady();
            editorCommon.GUIStyleReady();
        }

        protected void InspectorGUI_Import()
        {
            baseTarget.edit_importFoldout = EditorGUILayout.Foldout(baseTarget.edit_importFoldout, "Import", guiStyleFoldoutBold);
            if (baseTarget.edit_importFoldout)
            {
                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                EditorGUILayout.BeginVertical();
                #region Voxel File
                {
                    bool fileExists = baseCore.IsVoxelFileExists();
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (string.IsNullOrEmpty(baseTarget.voxelFilePath))
                            EditorGUILayout.LabelField("Voxel File", guiStyleMagentaBold);
                        else if (!fileExists)
                            EditorGUILayout.LabelField("Voxel File", guiStyleRedBold);
                        else
                            EditorGUILayout.LabelField("Voxel File", guiStyleBold);
                        if (GUILayout.Button("Open", GUILayout.Width(64)))
                        {
                            Action<string> OpenFile = (path) =>
                            {
                                UndoRecordObject("Open Voxel File", true);
                                baseTarget.edit_configureMode = VoxelBase.Edit_configureMode.None;
                                baseTarget.materialData = new List<MaterialData>();
                                baseTarget.materialData.Add(new MaterialData());
                                baseCore.Create(path);
                            };

                            InspectorGUI_ImportOpenBefore();
                            GenericMenu menu = new GenericMenu();
                            #region vox
                            menu.AddItem(new GUIContent("MagicaVoxel (*.vox)"), false, () =>
                            {
                                var path = EditorUtility.OpenFilePanel("Open MagicaVoxel File", !string.IsNullOrEmpty(baseTarget.voxelFilePath) ? Path.GetDirectoryName(baseTarget.voxelFilePath) : "", "vox");
                                if (!string.IsNullOrEmpty(path))
                                {
                                    OpenFile(path);
                                }
                            });
                            #endregion
                            #region qb
                            menu.AddItem(new GUIContent("Qubicle Binary (*.qb)"), false, () =>
                            {
                                var path = EditorUtility.OpenFilePanel("Open Qubicle Binary File", !string.IsNullOrEmpty(baseTarget.voxelFilePath) ? Path.GetDirectoryName(baseTarget.voxelFilePath) : "", "qb");
                                if (!string.IsNullOrEmpty(path))
                                {
                                    OpenFile(path);
                                }
                            });
                            #endregion
                            #region png
                            menu.AddItem(new GUIContent("Pixel Art (*.png)"), false, () =>
                            {
                                var path = EditorUtility.OpenFilePanel("Open Pixel Art File", !string.IsNullOrEmpty(baseTarget.voxelFilePath) ? Path.GetDirectoryName(baseTarget.voxelFilePath) : "", "png");
                                if (!string.IsNullOrEmpty(path))
                                {
                                    OpenFile(path);
                                }
                            });
                            #endregion
                            menu.ShowAsContext();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    {
                        EditorGUI.indentLevel++;
                        {
                            EditorGUILayout.LabelField("File Name", Path.GetFileName(baseTarget.voxelFilePath));
                            if (!fileExists)
                            {
                                EditorGUILayout.HelpBox("Voxel file not found. Please open file.", MessageType.Error);
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                #endregion
                #region Settings
                {
                    EditorGUILayout.LabelField("Settings", guiStyleBold);
                    EditorGUI.indentLevel++;
                    {
                        #region Import Mode
                        {
                            EditorGUI.BeginChangeCheck();
                            var importMode = (VoxelObject.ImportMode)EditorGUILayout.EnumPopup("Import Mode", baseTarget.importMode);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoRecordObject("Inspector");
                                baseTarget.importMode = importMode;
                                Refresh();
                            }
                        }
                        #endregion
                        #region Import Flag
                        {
                            EditorGUI.BeginChangeCheck();
                            var importFlags = (VoxelObject.ImportFlag)EditorGUILayout.EnumMaskField("Import Flag", baseTarget.importFlags);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoRecordObject("Inspector", true);
                                baseTarget.importFlags = importFlags;
                                Refresh();
                            }
                        }
                        #endregion
                        #region Import Scale
                        {
                            EditorGUI.BeginChangeCheck();
                            var importScale = EditorGUILayout.Vector3Field("Import Scale", baseTarget.importScale);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoRecordObject("Inspector", true);
                                baseTarget.importScale = importScale;
                                Refresh();
                            }
                        }
                        #endregion
                        #region Import Offset
                        {
                            EditorGUI.BeginChangeCheck();
                            var importOffset = EditorGUILayout.Vector3Field("Import Offset", baseTarget.importOffset);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoRecordObject("Inspector", true);
                                baseTarget.importOffset = importOffset;
                                Refresh();
                            }
                        }
                        #endregion
                        #region Enable Face
                        {
                            EditorGUI.BeginChangeCheck();
                            var enableFaceFlags = (VoxelBase.Face)EditorGUILayout.EnumMaskField("Enable Face", baseTarget.enableFaceFlags);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoRecordObject("Inspector");
                                baseTarget.enableFaceFlags = enableFaceFlags;
                                Refresh();
                            }
                        }
                        #endregion
                        InspectorGUI_ImportSettingsExtra();
                    }
                    EditorGUI.indentLevel--;
                }
                #endregion
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
        }
        protected virtual void UndoRecordObject(string text, bool reset = false)
        {
            if (baseTarget != null)
                Undo.RecordObject(baseTarget, text);
        }
        protected virtual void InspectorGUI_ImportOpenBefore() { }
        protected virtual void InspectorGUI_ImportSettingsExtra() { }
        protected virtual void InspectorGUI_Refresh()
        {
            if (GUILayout.Button("Refresh"))
            {
                UndoRecordObject("Inspector");
                Refresh();
            }
        }

        protected virtual void OnSceneGUI()
        {
            if (baseTarget == null) return;

            GUIStyleReady();
            editorCommon.GUIStyleReady();

            Event e = Event.current;
            bool repaint = false;

            #region Configure Material
            if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
            {
                if (baseTarget.materialData != null && materialList != null &&
                    baseTarget.edit_configureMaterialIndex >= 0 && baseTarget.edit_configureMaterialIndex < baseTarget.materialData.Count)
                {
                    #region Event
                    {
                        Tools.current = Tool.None;
                        switch (e.type)
                        {
                        case EventType.MouseMove:
                            editVoxelList.Clear();
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
                                if (e.keyCode == KeyCode.Space)
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

                    if (drawEditorMesh)
                    {
                        DrawBaseMesh();

                        #region MaterialMesh
                        if (baseTarget.edit_enableMesh != null)
                        {
                            for (int i = 0; i < baseTarget.edit_enableMesh.Length; i++)
                            {
                                if (baseTarget.edit_enableMesh[i] == null) continue;
                                editorCommon.vertexColorMaterial.color = new Color(1, 0, 0, 1);
                                editorCommon.vertexColorMaterial.SetPass(0);
                                Graphics.DrawMeshNow(baseTarget.edit_enableMesh[i], baseTarget.transform.localToWorldMatrix);
                            }
                        }
                        #endregion
                    }

                    if (SceneView.currentDrawingSceneView == SceneView.lastActiveSceneView)
                    {
                        #region Preview Mesh
                        if (editorCommon.previewMesh != null)
                        {
                            Color color = Color.white;
                            if (baseTarget.edit_materialMode == VoxelBase.Edit_MaterialMode.Add)
                            {
                                color = new Color(1, 0, 0, 1);
                            }
                            else if (baseTarget.edit_materialMode == VoxelBase.Edit_MaterialMode.Remove)
                            {
                                color = new Color(0, 0, 1, 1);
                            }
                            color.a = 0.5f + 0.5f * (1f - editorCommon.AnimationPower);
                            for (int i = 0; i < editorCommon.previewMesh.Length; i++)
                            {
                                if (editorCommon.previewMesh[i] == null) continue;
                                editorCommon.vertexColorTransparentMaterial.color = color;
                                editorCommon.vertexColorTransparentMaterial.SetPass(0);
                                Graphics.DrawMeshNow(editorCommon.previewMesh[i], baseTarget.transform.localToWorldMatrix);
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
                                    Graphics.DrawMeshNow(editorCommon.cursorMesh[i], baseTarget.transform.localToWorldMatrix);
                                }
                            }
                            repaint = true;
                        }
                        #endregion

                        #region Selection Rect
                        if (baseTarget.edit_materialTypeMode == VoxelBase.Edit_MaterialTypeMode.Rect)
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
                        if (baseTarget.edit_configureMaterialIndex > 0)
                        {
                            Handles.BeginGUI();
                            {
                                var editorBoxRect = new Rect(2, 2, 204, 104);
                                GUI.Box(editorBoxRect, "Material Editor", editorCommon.guiStyleAlphaBox);
                                editorCommon.editorRectList.Add(editorBoxRect);
                            }
                            float x = 4;
                            float y = 20;
                            #region MaterialMode
                            {
                                EditorGUI.BeginChangeCheck();
                                var edit_materialMode = (VoxelBase.Edit_MaterialMode)GUI.Toolbar(new Rect(x, y, 200, 20), (int)baseTarget.edit_materialMode, Edit_MaterialModeString);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(baseTarget, "Material Mode");
                                    baseTarget.edit_materialMode = edit_materialMode;
                                    ShowNotification();
                                }
                            }
                            y += 24;
                            #endregion
                            #region MaterialTypeMode
                            {
                                EditorGUI.BeginChangeCheck();
                                var edit_materialTypeMode = (VoxelBase.Edit_MaterialTypeMode)GUI.Toolbar(new Rect(x, y, 200, 20), (int)baseTarget.edit_materialTypeMode, Edit_MaterialTypeModeString);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(baseTarget, "Material Type Mode");
                                    baseTarget.edit_materialTypeMode = edit_materialTypeMode;
                                    ShowNotification();
                                }
                            }
                            y += 24;
                            #endregion
                            #region Transparent
                            {
                                EditorGUI.BeginChangeCheck();
                                var transparent = GUI.Toggle(new Rect(x, y, 200, 16), baseTarget.materialData[baseTarget.edit_configureMaterialIndex].transparent, "Transparent", editorCommon.guiStyleToggleRight);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(baseTarget, "Transparent");
                                    baseTarget.materialData[baseTarget.edit_configureMaterialIndex].transparent = transparent;
                                    baseTarget.edit_afterRefresh = true;
                                }
                            }
                            y += 20;
                            #endregion
                            #region WeightClear
                            {
                                if (GUI.Button(new Rect(x, y, 200, 16), "Clear"))
                                {
                                    Undo.RecordObject(baseTarget, "Clear");
                                    baseTarget.materialData[baseTarget.edit_configureMaterialIndex].ClearMaterial();
                                    UpdateMaterialEnableMesh();
                                    baseTarget.edit_afterRefresh = true;
                                }
                            }
                            y += 20;
                            #endregion
                            Handles.EndGUI();
                        }
                        #endregion
                    }
                }
            }
            #endregion

            if (repaint)
            {
                SceneView.currentDrawingSceneView.Repaint();
            }
        }

        protected abstract void DrawBaseMesh();
        
        private void UpdatePreviewMesh()
        {
            editorCommon.ClearPreviewMesh();

            if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material &&
                baseTarget.edit_configureMaterialIndex > 0 && baseTarget.edit_configureMaterialIndex < baseTarget.materialData.Count)
            {
                List<VoxelData.Voxel> voxels = new List<VoxelData.Voxel>();
                editVoxelList.AllAction((x, y, z) =>
                {
                    var index = baseTarget.voxelData.VoxelTableContains(x, y, z);
                    if (index < 0) return;
                    var voxel = baseTarget.voxelData.voxels[index];
                    voxel.palette = -1;
                    voxels.Add(voxel);
                });
                if (voxels.Count > 0)
                {
                    editorCommon.previewMesh = baseCore.Edit_CreateMesh(voxels, null, false);
                }
            }
        }
        private void UpdateCursorMesh()
        {
            editorCommon.ClearCursorMesh();

            if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material &&
                baseTarget.edit_configureMaterialIndex > 0 && baseTarget.edit_configureMaterialIndex < baseTarget.materialData.Count)
            {
                switch (baseTarget.edit_materialTypeMode)
                {
                case VoxelBase.Edit_MaterialTypeMode.Voxel:
                    {
                        var result = editorCommon.GetMousePositionVoxel();
                        if (result.HasValue)
                        {
                            editorCommon.cursorMesh = baseCore.Edit_CreateMesh(new List<VoxelData.Voxel>() { new VoxelData.Voxel() { position = result.Value, palette = -1 } });
                        }
                    }
                    break;
                case VoxelBase.Edit_MaterialTypeMode.Fill:
                    {
                        var pos = editorCommon.GetMousePositionVoxel();
                        if (pos.HasValue)
                        {
                            var faceAreaTable = editorCommon.GetFillVoxelFaceAreaTable(pos.Value);
                            if (faceAreaTable != null)
                                editorCommon.cursorMesh = new Mesh[1] { baseCore.Edit_CreateMeshOnly_Mesh(faceAreaTable, null, null) };
                        }
                    }
                    break;
                }
            }
        }

        private void ClearMakeAddData()
        {
            editVoxelList.Clear();
            editorCommon.selectionRect.Reset();
            editorCommon.ClearPreviewMesh();
            editorCommon.ClearCursorMesh();
        }

        private void EventMouseDrag(bool first)
        {
            if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
            {
                UpdateCursorMesh();
                switch (baseTarget.edit_materialTypeMode)
                {
                case VoxelBase.Edit_MaterialTypeMode.Voxel:
                    {
                        var result = editorCommon.GetMousePositionVoxel();
                        if (result.HasValue)
                        {
                            editVoxelList.Set(result.Value, true);
                            UpdatePreviewMesh();
                        }
                    }
                    break;
                case VoxelBase.Edit_MaterialTypeMode.Fill:
                    {
                        var pos = editorCommon.GetMousePositionVoxel();
                        if (pos.HasValue)
                        {
                            var result = editorCommon.GetFillVoxel(pos.Value);
                            if (result != null)
                            {
                                for (int i = 0; i < result.Count; i++)
                                    editVoxelList.Set(result[i], true);
                                UpdatePreviewMesh();
                            }
                        }
                    }
                    break;
                case VoxelBase.Edit_MaterialTypeMode.Rect:
                    {
                        var pos = new IntVector2((int)Event.current.mousePosition.x, (int)Event.current.mousePosition.y);
                        if (first) { editorCommon.selectionRect.Reset(); editorCommon.selectionRect.SetStart(pos); }
                        else editorCommon.selectionRect.SetEnd(pos);
                        //
                        editVoxelList.Clear();
                        {
                            var list = editorCommon.GetSelectionRectVoxel();
                            for (int i = 0; i < list.Count; i++)
                                editVoxelList.Set(list[i], true);
                        }
                        UpdatePreviewMesh();
                    }
                    break;
                }
            }
        }
        private void EventMouseApply()
        {
            if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
            {
                Undo.RecordObject(baseTarget, "Material");
                
                bool update = false;
                if (baseTarget.edit_materialMode == VoxelBase.Edit_MaterialMode.Add)
                {
                    editVoxelList.AllAction((x, y, z) =>
                    {
                        if (!update)
                            DisconnectPrefabInstance();

                        for (int i = 0; i < baseTarget.materialData.Count; i++)
                        {
                            if (i == baseTarget.edit_configureMaterialIndex) continue;
                            if (baseTarget.materialData[i].GetMaterial(new IntVector3(x, y, z)))
                            {
                                baseTarget.materialData[i].RemoveMaterial(new IntVector3(x, y, z));
                            }
                        }
                        baseTarget.materialData[baseTarget.edit_configureMaterialIndex].SetMaterial(new IntVector3(x, y, z));
                        update = true;
                    });
                }
                else if (baseTarget.edit_materialMode == VoxelBase.Edit_MaterialMode.Remove)
                {
                    editVoxelList.AllAction((x, y, z) =>
                    {
                        if (baseTarget.materialData[baseTarget.edit_configureMaterialIndex].GetMaterial(new IntVector3(x, y, z)))
                        {
                            if (!update)
                                DisconnectPrefabInstance();

                            baseTarget.materialData[baseTarget.edit_configureMaterialIndex].RemoveMaterial(new IntVector3(x, y, z));
                            update = true;
                        }
                    });
                }
                else
                {
                    Assert.IsTrue(false);
                }
                if (update)
                {
                    UpdateMaterialEnableMesh();
                    baseTarget.edit_afterRefresh = true;
                }
                editVoxelList.Clear();
            }
        }

        private void ShowNotification()
        {
            SceneView.currentDrawingSceneView.ShowNotification(new GUIContent(string.Format("{0} - {1}", baseTarget.edit_materialMode, baseTarget.edit_materialTypeMode)));
        }

        protected void SaveInsideAssetsFolderDisplayDialog()
        {
            EditorUtility.DisplayDialog("Need to save in the Assets folder", "You need to save the file inside of the project's assets floder", "ok");
        }
        
        protected void UpdateMaterialList(List<Material> materials)
        {
            materialList = new ReorderableList(
                serializedObject,
                serializedObject.FindProperty("materialData"),
                false, true, true, true
            );
            materialList.elementHeight = 22;
            materialList.drawHeaderCallback = (rect) =>
            {
                Rect r = rect;
                EditorGUI.LabelField(r, "Name", guiStyleBold);
                r.x = 182;
                if (materials != null)
                    EditorGUI.LabelField(r, "Material", guiStyleBold);
                r.x = 182;
            };
            materialList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.yMin += 2;
                rect.yMax -= 2;
                if (index < baseTarget.materialData.Count)
                {
                    #region Name
                    {
                        Rect r = rect;
                        r.width = 144;
                        if (index == 0)
                        {
                            EditorGUI.LabelField(r, "default", guiStyleLabelMiddleLeftItalic);
                        }
                        else
                        {
                            EditorGUI.BeginChangeCheck();
                            string name = EditorGUI.TextField(r, baseTarget.materialData[index].name, guiStyleTextFieldMiddleLeft);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoRecordObject("Inspector");
                                baseTarget.materialData[index].name = name;
                            }
                        }
                    }
                    #endregion
                    #region Material
                    if(materials != null && index < materials.Count)
                    {
                        {
                            Rect r = rect;
                            r.xMin = 182;
                            r.width = rect.width - r.xMin - 80;
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUI.ObjectField(r, materials[index], typeof(Material), false);
                            EditorGUI.EndDisabledGroup();
                        }
                        if (materials[index] != null)
                        {
                            Rect r = rect;
                            r.xMin += rect.width - 64;
                            r.width = 64;
                            if (!AssetDatabase.Contains(materials[index]))
                            {
                                if (GUI.Button(r, "Save"))
                                {
                                    #region Create Material
                                    string path = EditorUtility.SaveFilePanel("Save material", baseCore.GetDefaultPath(), string.Format("{0}_mat{1}.mat", baseTarget.gameObject.name, index), "mat");
                                    if (!string.IsNullOrEmpty(path))
                                    {
                                        if (path.IndexOf(Application.dataPath) < 0)
                                        {
                                            SaveInsideAssetsFolderDisplayDialog();
                                        }
                                        else
                                        {
                                            UndoRecordObject("Save Material");
                                            path = path.Replace(Application.dataPath, "Assets");
                                            AssetDatabase.CreateAsset(Material.Instantiate(materials[index]), path);
                                            materials[index] = AssetDatabase.LoadAssetAtPath<Material>(path);
                                            baseCore.SetRendererCompornent();
                                        }
                                    }

                                    #endregion
                                }
                            }
                            else
                            {
                                if (GUI.Button(r, "Reset"))
                                {
                                    #region Reset Material
                                    UndoRecordObject("Reset Material");
                                    materials[index] = null;
                                    Refresh();
                                    #endregion
                                }
                            }
                        }
                    }
                    #endregion
                }
            };
            materialList.onSelectCallback = (list) =>
            {
                UndoRecordObject("Inspector");
                baseTarget.edit_configureMaterialIndex = list.index;
                if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
                    UpdateMaterialEnableMesh();
                InternalEditorUtility.RepaintAllViews();
            };
            materialList.onAddCallback = (list) =>
            {
                UndoRecordObject("Inspector");
                var data = new MaterialData();
                data.name = baseTarget.materialData.Count.ToString();
                baseTarget.materialData.Add(data);
                if (materials != null)
                    materials.Add(null);
                Refresh();
                baseTarget.edit_configureMaterialIndex = list.count;
                list.index = baseTarget.edit_configureMaterialIndex;
                InternalEditorUtility.RepaintAllViews();
            };
            materialList.onRemoveCallback = (list) =>
            {
                if (list.index > 0 && list.index < baseTarget.materialData.Count)
                {
                    UndoRecordObject("Inspector");
                    baseTarget.materialData.RemoveAt(list.index);
                    if (materials != null)
                        materials.RemoveAt(list.index);
                    Refresh();
                    baseTarget.edit_configureMaterialIndex = -1;
                    if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
                        UpdateMaterialEnableMesh();
                    InternalEditorUtility.RepaintAllViews();
                }
            };
            if (baseTarget.edit_configureMaterialIndex >= 0 && baseTarget.materialData != null && baseTarget.edit_configureMaterialIndex < baseTarget.materialData.Count)
                materialList.index = baseTarget.edit_configureMaterialIndex;
            else
                baseTarget.edit_configureMaterialIndex = 0;
        }

        protected void UpdateMaterialEnableMesh()
        {
            if (baseTarget.materialData == null || baseTarget.voxelData == null)
            {
                EditEnableMeshDestroy();
                return;
            }

            UndoRecordObject("Inspector");

            List<VoxelData.Voxel> voxels = new List<VoxelData.Voxel>(baseTarget.voxelData.voxels.Length);
            if(baseTarget.edit_configureMaterialIndex == 0)
            {
                for (int i = 0; i < baseTarget.voxelData.voxels.Length; i++)
                {
                    {
                        bool enable = true;
                        for (int j = 0; j < baseTarget.materialData.Count; j++)
                        {
                            if (baseTarget.materialData[j].GetMaterial(baseTarget.voxelData.voxels[i].position))
                            {
                                enable = false;
                                break;
                            }
                        }
                        if (!enable) continue;
                    }
                    var voxel = baseTarget.voxelData.voxels[i];
                    voxel.palette = -1;
                    voxels.Add(voxel);
                }
            }
            else if(baseTarget.edit_configureMaterialIndex >= 0 && baseTarget.edit_configureMaterialIndex < baseTarget.materialData.Count)
            {
                baseTarget.materialData[baseTarget.edit_configureMaterialIndex].AllAction((pos) =>
                {
                    var index = baseTarget.voxelData.VoxelTableContains(pos);
                    if (index < 0) return;

                    var voxel = baseTarget.voxelData.voxels[index];
                    voxel.palette = -1;
                    voxels.Add(voxel);
                });
            }
            baseTarget.edit_enableMesh = baseCore.Edit_CreateMesh(voxels);
        }

        public void EditEnableMeshDestroy()
        {
            if (baseTarget.edit_enableMesh != null)
            {
                UndoRecordObject("Inspector");

                for (int i = 0; i < baseTarget.edit_enableMesh.Length; i++)
                {
                    MonoBehaviour.DestroyImmediate(baseTarget.edit_enableMesh[i]);
                }
                baseTarget.edit_enableMesh = null;
            }
        }
        
        protected void AfterRefresh()
        {
            if (baseTarget.edit_afterRefresh)
                Refresh();
        }
        protected virtual void Refresh()
        {
            baseCore.ReCreate();
            baseTarget.edit_afterRefresh = false;
        }

        protected void DisconnectPrefabInstance()
        {
            if (PrefabUtility.GetPrefabType(baseTarget) == PrefabType.PrefabInstance)
            {
                PrefabUtility.DisconnectPrefabInstance(baseTarget);
            }
        }

        protected virtual void EditorUndoRedoPerformed()
        {
            if (baseTarget != null && baseCore != null)
            {
                if (baseCore.RefreshCheckerCheck())
                {
                    Refresh();
                }
                else
                {
                    baseCore.SetRendererCompornent();
                }
            }
            Repaint();
        }
    }
}
