using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace VoxelImporter
{
    [CustomEditor(typeof(VoxelChunksObject))]
    public class VoxelChunksObjectEditor : VoxelBaseEditor
    {
        public VoxelChunksObject objectTarget { get; protected set; }
        public VoxelChunksObjectCore objectCore { get; protected set; }

        #region strings
        private static string[] SplitModeNormalStrings =
        {
            VoxelChunksObject.SplitMode.ChunkSize.ToString(),
        };
        #endregion

        protected override void OnEnable()
        {
            base.OnEnable();

            objectTarget = target as VoxelChunksObject;
            if (objectTarget == null) return;
            baseCore = objectCore = new VoxelChunksObjectCore(objectTarget);

            baseCore.Initialize();

            UpdateMaterialList(objectTarget.materials);
            if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
                UpdateMaterialEnableMesh();

            Undo.undoRedoPerformed -= EditorChunksUndoRedoPerformed;
            Undo.undoRedoPerformed += EditorChunksUndoRedoPerformed;
        }
        protected override void OnDisable()
        {
            base.OnDisable();

            Undo.undoRedoPerformed -= EditorChunksUndoRedoPerformed;
        }

        protected override void InspectorGUI()
        {
            base.InspectorGUI();

            var prefabType = PrefabUtility.GetPrefabType(baseTarget.gameObject);
            var prefabEnable = prefabType == PrefabType.Prefab || prefabType == PrefabType.PrefabInstance || prefabType == PrefabType.DisconnectedPrefabInstance;

            Action<UnityEngine.Object, string> TypeTitle = (o, title) =>
            {
                if (o == null)
                    EditorGUILayout.LabelField(title, guiStyleMagentaBold);
                else if (prefabEnable && !AssetDatabase.Contains(o))
                    EditorGUILayout.LabelField(title, guiStyleRedBold);
                else
                    EditorGUILayout.LabelField(title, guiStyleBold);
            };

            InspectorGUI_Import();

            #region Object
            if (!string.IsNullOrEmpty(baseTarget.voxelFilePath))
            {
                //Object
                baseTarget.edit_objectFoldout = EditorGUILayout.Foldout(baseTarget.edit_objectFoldout, "Object", guiStyleFoldoutBold);
                if (baseTarget.edit_objectFoldout)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    #region Mesh
                    {
                        EditorGUILayout.LabelField("Mesh", guiStyleBold);
                        EditorGUI.indentLevel++;
                        #region Generate Lightmap UVs
                        {
                            EditorGUI.BeginChangeCheck();
                            var generateLightmapUVs = EditorGUILayout.Toggle("Generate Lightmap UVs", baseTarget.generateLightmapUVs);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoRecordObject("Inspector");
                                baseTarget.generateLightmapUVs = generateLightmapUVs;
                                Refresh();
                            }
                        }
                        #endregion
                        EditorGUI.indentLevel--;
                    }
                    #endregion
                    #region Material
                    {
                        if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Combine)
                        {
                            if (objectTarget.materials == null || objectTarget.materials.Count == 0)
                                EditorGUILayout.LabelField("Material", guiStyleMagentaBold);
                            else if (prefabEnable)
                            {
                                bool contains = true;
                                for (int i = 0; i < objectTarget.materials.Count; i++)
                                {
                                    if (objectTarget.materials[i] == null || !AssetDatabase.Contains(objectTarget.materials[i]))
                                    {
                                        contains = false;
                                        break;
                                    }
                                }
                                EditorGUILayout.LabelField("Material", contains ? guiStyleBold : guiStyleRedBold);
                            }
                            else
                                EditorGUILayout.LabelField("Material", guiStyleBold);
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Material", guiStyleBold);
                        }
                        EditorGUI.indentLevel++;
                        #region Material Mode
                        {
                            EditorGUI.BeginChangeCheck();
                            var materialMode = (VoxelChunksObject.MaterialMode)EditorGUILayout.EnumPopup("Material Mode", objectTarget.materialMode);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoRecordObject("Inspector");
                                {
                                    var chunkObjects = objectCore.FindChunkComponents();
                                    if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Combine)
                                    {
                                        objectTarget.materials = null;
                                        objectTarget.atlasTexture = null;
                                    }
                                    else if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Individual)
                                    {
                                        for (int i = 0; i < chunkObjects.Length; i++)
                                        {
                                            chunkObjects[i].materials = null;
                                            chunkObjects[i].atlasTexture = null;
                                        }
                                    }
                                }
                                objectTarget.materialMode = materialMode;
                                Refresh();
                                UpdateMaterialList(objectTarget.materialMode == VoxelChunksObject.MaterialMode.Combine ? objectTarget.materials : null);
                            }
                        }
                        #endregion
                        if (materialList != null)
                        {
                            materialList.DoLayoutList();
                        }
                        #region Configure Material
                        if (baseTarget.materialData != null && baseTarget.materialData.Count > 1)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.Space();
                            if (GUILayout.Button("Configure Material", baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material ? guiStyleBoldActiveButton : GUI.skin.button))
                            {
                                UndoRecordObject("Configure Material");
                                if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
                                {
                                    baseTarget.edit_configureMode = VoxelBase.Edit_configureMode.None;
                                    AfterRefresh();
                                }
                                else
                                {
                                    baseTarget.edit_configureMode = VoxelBase.Edit_configureMode.Material;
                                    UpdateMaterialEnableMesh();
                                }
                                InternalEditorUtility.RepaintAllViews();
                            }
                            EditorGUILayout.Space();
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            baseTarget.edit_configureMode = VoxelBase.Edit_configureMode.None;
                        }
                        #endregion
                        EditorGUI.indentLevel--;
                    }
                    #endregion
                    #region Texture
                    {
                        if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Combine)
                            TypeTitle(objectTarget.atlasTexture, "Texture");
                        else
                            EditorGUILayout.LabelField("Texture", guiStyleBold);
                        EditorGUI.indentLevel++;
                        #region Texture
                        if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Combine)
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.ObjectField(objectTarget.atlasTexture, typeof(Texture2D), false);
                                EditorGUI.EndDisabledGroup();
                            }
                            if (objectTarget.atlasTexture != null)
                            {
                                if (!AssetDatabase.Contains(objectTarget.atlasTexture))
                                {
                                    if (GUILayout.Button("Save", GUILayout.Width(64)))
                                    {
                                        #region Create Texture
                                        string path = EditorUtility.SaveFilePanel("Save atlas texture", baseCore.GetDefaultPath(), string.Format("{0}_tex.png", baseTarget.gameObject.name), "png");
                                        if (!string.IsNullOrEmpty(path))
                                        {
                                            if (path.IndexOf(Application.dataPath) < 0)
                                            {
                                                SaveInsideAssetsFolderDisplayDialog();
                                            }
                                            else
                                            {
                                                UndoRecordObject("Save Atlas Texture");
                                                File.WriteAllBytes(path, objectTarget.atlasTexture.EncodeToPNG());
                                                path = path.Replace(Application.dataPath, "Assets");
                                                AssetDatabase.ImportAsset(path);
                                                {
                                                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                                                    if (importer != null)
                                                    {
                                                        importer.textureType = TextureImporterType.Default;
                                                        importer.filterMode = FilterMode.Point;
                                                        importer.wrapMode = TextureWrapMode.Clamp;
                                                        importer.mipmapEnabled = baseTarget.generateMipMaps;
                                                        importer.borderMipmap = true;
                                                        importer.textureFormat = TextureImporterFormat.AutomaticTruecolor;
                                                        if (Math.Max(objectTarget.atlasTexture.width, objectTarget.atlasTexture.height) > importer.maxTextureSize)
                                                            importer.maxTextureSize = Math.Max(objectTarget.atlasTexture.width, objectTarget.atlasTexture.height);
                                                        importer.SaveAndReimport();
                                                    }
                                                }
                                                objectTarget.atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
                                        #region Reset Texture
                                        UndoRecordObject("Reset Atlas Texture");
                                        objectTarget.atlasTexture = null;
                                        Refresh();
                                        #endregion
                                    }
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        #endregion
                        #region Generate Mip Maps
                        {
                            EditorGUI.BeginChangeCheck();
                            var generateMipMaps = EditorGUILayout.Toggle("Generate Mip Maps", baseTarget.generateMipMaps);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoRecordObject("Inspector");
                                baseTarget.generateMipMaps = generateMipMaps;
                                Refresh();
                            }
                        }
                        #endregion
                        #region Texture Size
                        if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Combine)
                        {
                            EditorGUILayout.LabelField("Texture Size", objectTarget.atlasTexture != null ? string.Format("{0} x {1}", objectTarget.atlasTexture.width, objectTarget.atlasTexture.height) : "");
                        }
                        #endregion
                        EditorGUI.indentLevel--;
                    }
                    #endregion
                    #region HelpBox
                    {
                        if (prefabEnable)
                        {
                            var chunkObjects = objectCore.FindChunkComponents();

                            HashSet<string> helpList = new HashSet<string>();
                            if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Combine)
                            {
                                for (int i = 0; i < chunkObjects.Length; i++)
                                {
                                    if (!AssetDatabase.Contains(chunkObjects[i].mesh))
                                    {
                                        helpList.Add("Mesh");
                                        break;
                                    }
                                }
                                if (objectTarget.materials != null)
                                {
                                    for (int i = 0; i < objectTarget.materials.Count; i++)
                                    {
                                        if (objectTarget.materials[i] == null || !AssetDatabase.Contains(objectTarget.materials[i]))
                                        {
                                            helpList.Add("Material");
                                            break;
                                        }
                                    }
                                }
                                if (!AssetDatabase.Contains(objectTarget.atlasTexture))
                                    helpList.Add("Texture");
                            }
                            else if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Individual)
                            {
                                for (int i = 0; i < chunkObjects.Length; i++)
                                {
                                    if (!AssetDatabase.Contains(chunkObjects[i].mesh))
                                        helpList.Add("Mesh");

                                    if (chunkObjects[i].materials != null)
                                    {
                                        for (int j = 0; j < chunkObjects[i].materials.Count; j++)
                                        {
                                            if (!AssetDatabase.Contains(chunkObjects[i].materials[j]))
                                            {
                                                helpList.Add("Material");
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        helpList.Add("Material");
                                    }
                                    if (!AssetDatabase.Contains(chunkObjects[i].atlasTexture))
                                        helpList.Add("Texture");
                                }
                            }
                            else
                            {
                                Assert.IsTrue(false);
                            }
                            if (helpList.Count > 0)
                            {
                                string text = "";
                                if (helpList.Count >= 3)
                                {
                                    int i = 0;
                                    var enu = helpList.GetEnumerator();
                                    while (enu.MoveNext())
                                    {
                                        if (i == helpList.Count - 1)
                                            text += ", and ";
                                        else if (i != 0)
                                            text += ", ";
                                        text += enu.Current;
                                        i++;
                                    }
                                }
                                else if (helpList.Count == 2)
                                {
                                    var enu = helpList.GetEnumerator();
                                    enu.MoveNext();
                                    text += enu.Current;
                                    text += " and ";
                                    enu.MoveNext();
                                    text += enu.Current;
                                }
                                else if (helpList.Count == 1)
                                {
                                    var enu = helpList.GetEnumerator();
                                    enu.MoveNext();
                                    text += enu.Current;
                                }
                                EditorGUILayout.HelpBox(string.Format("Prefab is need save file.\nPlease save {0}.", text), MessageType.Error);
                            }
                        }
                    }
                    #endregion
                    EditorGUILayout.EndVertical();
                }
            }
            #endregion

            #region Refresh
            {
                if (GUILayout.Button("Refresh"))
                {
                    UndoRecordObject("Inspector");
                    Refresh();
                }
            }
            #endregion
        }
        protected override void UndoRecordObject(string text, bool reset = false)
        {
            base.UndoRecordObject(text);

            var chunkObjects = objectCore.FindChunkComponents();
            Undo.RecordObjects(chunkObjects, text);

            if (reset)
            {
                if (objectTarget.splitMode == VoxelChunksObject.SplitMode.QubicleMatrix)
                {
                    objectTarget.splitMode = VoxelChunksObject.SplitMode.ChunkSize;
                }

                objectCore.RemoveAllChunk();
            }
        }
        protected override void InspectorGUI_ImportSettingsExtra()
        {
            #region Split Mode
            if (objectTarget.fileType == VoxelBase.FileType.qb)
            {
                EditorGUI.BeginChangeCheck();
                var splitMode = (VoxelChunksObject.SplitMode)EditorGUILayout.EnumPopup("Split Mode", objectTarget.splitMode);
                if (EditorGUI.EndChangeCheck())
                {
                    UndoRecordObject("Inspector", true);
                    objectTarget.splitMode = splitMode;
                    Refresh();
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var splitMode = (VoxelChunksObject.SplitMode)EditorGUILayout.Popup("Split Mode", (int)objectTarget.splitMode, SplitModeNormalStrings);
                if (EditorGUI.EndChangeCheck())
                {
                    UndoRecordObject("Inspector", true);
                    objectTarget.splitMode = splitMode;
                    Refresh();
                }
            }
            #endregion
            {
                EditorGUI.indentLevel++;
                #region Chunk Size
                if (objectTarget.splitMode == VoxelChunksObject.SplitMode.ChunkSize)
                {
                    EditorGUI.BeginChangeCheck();
                    var chunkSize = EditorGUILayout.Vector3Field("Chunk Size", new Vector3(objectTarget.edit_chunkSize.x, objectTarget.edit_chunkSize.y, objectTarget.edit_chunkSize.z));
                    if (EditorGUI.EndChangeCheck())
                    {
                        UndoRecordObject("Inspector");
                        objectTarget.edit_chunkSize.x = Mathf.RoundToInt(chunkSize.x);
                        objectTarget.edit_chunkSize.y = Mathf.RoundToInt(chunkSize.y);
                        objectTarget.edit_chunkSize.z = Mathf.RoundToInt(chunkSize.z);
                    }
                    if (objectTarget.chunkSize != objectTarget.edit_chunkSize)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Space();
                        EditorGUILayout.Space();
                        if (GUILayout.Button("Revert", GUILayout.Width(64f)))
                        {
                            UndoRecordObject("Inspector");
                            objectTarget.edit_chunkSize = objectTarget.chunkSize;
                        }
                        if (GUILayout.Button("Apply", GUILayout.Width(64f)))
                        {
                            UndoRecordObject("Inspector", true);
                            objectTarget.chunkSize = objectTarget.edit_chunkSize;
                            Refresh();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                #endregion
                EditorGUI.indentLevel--;
            }
            #region Create contact faces of chunks
            {
                EditorGUI.BeginChangeCheck();
                var createContactChunkFaces = EditorGUILayout.Toggle("Create contact faces of chunks", objectTarget.createContactChunkFaces);
                if (EditorGUI.EndChangeCheck())
                {
                    UndoRecordObject("Inspector");
                    objectTarget.createContactChunkFaces = createContactChunkFaces;
                    Refresh();
                }
            }
            #endregion
        }

        protected override void Refresh()
        {
            base.Refresh();

            if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
                UpdateMaterialEnableMesh();
        }

        protected override void DrawBaseMesh()
        {
            var chunkObjects = objectCore.FindChunkComponents();
            for (int i = 0; i < chunkObjects.Length; i++)
            {
                if (chunkObjects[i].mesh == null) continue;
                editorCommon.unlitColorMaterial.color = new Color(0, 0, 0, 1f);
                editorCommon.unlitColorMaterial.SetPass(0);
                Graphics.DrawMeshNow(chunkObjects[i].mesh, chunkObjects[i].transform.localToWorldMatrix);
            }
        }

        private void EditorChunksUndoRedoPerformed()
        {
            UpdateMaterialList(objectTarget.materialMode == VoxelChunksObject.MaterialMode.Combine ? objectTarget.materials : null);

            Repaint();
        }
    }
}

