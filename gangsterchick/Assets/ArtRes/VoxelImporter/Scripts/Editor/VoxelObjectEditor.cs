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
    [CustomEditor(typeof(VoxelObject))]
    public class VoxelObjectEditor : VoxelBaseEditor
    {
        public VoxelObject objectTarget { get; protected set; }
        public VoxelObjectCore objectCore { get; protected set; }

        public virtual Mesh mesh { get { return objectTarget.mesh; } set { objectTarget.mesh = value; } }
        public virtual List<Material> materials { get { return objectTarget.materials; } set { objectTarget.materials = value; } }
        public virtual Texture2D atlasTexture { get { return objectTarget.atlasTexture; } set { objectTarget.atlasTexture = value; } }

        protected override void OnEnable()
        {
            base.OnEnable();

            objectTarget = target as VoxelObject;
            baseCore = objectCore = new VoxelObjectCore(objectTarget);

            if (objectTarget != null)
            {
                baseCore.Initialize();

                UpdateMaterialList(materials);
                if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
                    UpdateMaterialEnableMesh();
            }
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
                        TypeTitle(mesh, "Mesh");
                        EditorGUI.indentLevel++;
                        #region Mesh
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.ObjectField(mesh, typeof(Mesh), false);
                                EditorGUI.EndDisabledGroup();
                            }
                            if (mesh != null)
                            {
                                if (!AssetDatabase.Contains(mesh))
                                {
                                    if (GUILayout.Button("Save", GUILayout.Width(64)))
                                    {
                                        #region Create Mesh
                                        string path = EditorUtility.SaveFilePanel("Save mesh", objectCore.GetDefaultPath(), string.Format("{0}_mesh.asset", baseTarget.gameObject.name), "asset");
                                        if (!string.IsNullOrEmpty(path))
                                        {
                                            if (path.IndexOf(Application.dataPath) < 0)
                                            {
                                                SaveInsideAssetsFolderDisplayDialog();
                                            }
                                            else
                                            {
                                                UndoRecordObject("Save Mesh");
                                                path = path.Replace(Application.dataPath, "Assets");
                                                AssetDatabase.CreateAsset(Mesh.Instantiate(mesh), path);
                                                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
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
                                        #region Reset Mesh
                                        UndoRecordObject("Reset Mesh");
                                        mesh = null;
                                        Refresh();
                                        #endregion
                                    }
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        #endregion
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
                        #region Vertex Count
                        {
                            EditorGUILayout.LabelField("Vertex Count", mesh != null ? mesh.vertexCount.ToString() : "");
                        }
                        #endregion
                        EditorGUI.indentLevel--;
                    }
                    #endregion
                    #region Material
                    {
                        #region Title
                        {
                            if (materials == null || materials.Count == 0)
                                EditorGUILayout.LabelField("Material", guiStyleMagentaBold);
                            else if (prefabEnable)
                            {
                                bool contains = true;
                                for (int i = 0; i < materials.Count; i++)
                                {
                                    if(materials[i] == null || !AssetDatabase.Contains(materials[i]))
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
                        #endregion
                        EditorGUI.indentLevel++;
                        if (materialList != null)
                        {
                            materialList.DoLayoutList();
                        }
                        #region Configure Material
                        if(baseTarget.materialData != null && baseTarget.materialData.Count > 1)
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
                        TypeTitle(atlasTexture, "Texture");
                        EditorGUI.indentLevel++;
                        #region Texture
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.ObjectField(atlasTexture, typeof(Texture2D), false);
                                EditorGUI.EndDisabledGroup();
                            }
                            if (atlasTexture != null)
                            {
                                if (!AssetDatabase.Contains(atlasTexture))
                                {
                                    if (GUILayout.Button("Save", GUILayout.Width(64)))
                                    {
                                        #region Create Texture
                                        string path = EditorUtility.SaveFilePanel("Save atlas texture", objectCore.GetDefaultPath(), string.Format("{0}_tex.png", baseTarget.gameObject.name), "png");
                                        if (!string.IsNullOrEmpty(path))
                                        {
                                            if (path.IndexOf(Application.dataPath) < 0)
                                            {
                                                SaveInsideAssetsFolderDisplayDialog();
                                            }
                                            else
                                            {
                                                UndoRecordObject("Save Atlas Texture");
                                                File.WriteAllBytes(path, atlasTexture.EncodeToPNG());
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
                                                        if (Math.Max(atlasTexture.width, atlasTexture.height) > importer.maxTextureSize)
                                                            importer.maxTextureSize = Math.Max(atlasTexture.width, atlasTexture.height);
                                                        importer.SaveAndReimport();
                                                    }
                                                }
                                                atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
                                        atlasTexture = null;
                                        Refresh();
                                        #endregion
                                    }
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        #endregion
                        #region Generate Mip Maps
                        if (atlasTexture != null && !AssetDatabase.Contains(atlasTexture))
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
                        {
                            EditorGUILayout.LabelField("Texture Size", atlasTexture != null ? string.Format("{0} x {1}", atlasTexture.width, atlasTexture.height) : "");
                        }
                        #endregion
                        EditorGUI.indentLevel--;
                    }
                    #endregion
                    #region HelpBox
                    {
                        if (prefabEnable)
                        {
                            bool materialsContains = materials != null;
                            if (materials != null)
                            {
                                for (int i = 0; i < materials.Count; i++)
                                {
                                    if (materials[i] == null || !AssetDatabase.Contains(materials[i]))
                                    {
                                        materialsContains = false;
                                        break;
                                    }
                                }
                            }

                            if (!AssetDatabase.Contains(mesh) || !materialsContains || !AssetDatabase.Contains(atlasTexture))
                            {
                                List<string> helpList = new List<string>();
                                if (!AssetDatabase.Contains(mesh)) { helpList.Add("Mesh"); }
                                if (materials != null)
                                {
                                    for (int i = 0; i < materials.Count; i++)
                                    {
                                        if (materials[i] == null || !AssetDatabase.Contains(materials[i]))
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
                                if (!AssetDatabase.Contains(atlasTexture)) { helpList.Add("Texture"); }
                                string text = "";
                                if (helpList.Count >= 3)
                                {
                                    for (int i = 0; i < helpList.Count; i++)
                                    {
                                        if (i == helpList.Count - 1)
                                            text += ", and ";
                                        else if (i != 0)
                                            text += ", ";
                                        text += helpList[i];
                                    }
                                }
                                else if (helpList.Count == 2)
                                {
                                    text = string.Format("{0} and {1}", helpList[0], helpList[1]);
                                }
                                else if (helpList.Count == 1)
                                {
                                    text = helpList[0];
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

            InspectorGUI_Refresh();
        }

        protected override void Refresh()
        {
            base.Refresh();

            if (baseTarget.edit_configureMode == VoxelBase.Edit_configureMode.Material)
                UpdateMaterialEnableMesh();
        }

        protected override void DrawBaseMesh()
        {
            if (mesh != null)
            {
                editorCommon.unlitColorMaterial.color = new Color(0, 0, 0, 1f);
                editorCommon.unlitColorMaterial.SetPass(0);
                Graphics.DrawMeshNow(mesh, baseTarget.transform.localToWorldMatrix);
            }
        }
    }
}

