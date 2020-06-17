using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

namespace VoxelImporter
{
    [CustomEditor(typeof(VoxelChunksObjectChunk))]
    public class VoxelChunksObjectChunkEditor : Editor
    {
        public VoxelChunksObjectChunk chunkTarget { get; private set; }
        public VoxelChunksObject objectTarget { get; private set; }

        public VoxelChunksObjectChunkCore chunkCore { get; protected set; }
        public VoxelChunksObjectCore objectCore { get; protected set; }

        private GUIStyle guiStyleBold;
        private GUIStyle guiStyleMagentaBold;
        private GUIStyle guiStyleRedBold;
        private GUIStyle guiStyleFoldoutBold;

        void OnEnable()
        {
            chunkTarget = target as VoxelChunksObjectChunk;
            if (chunkTarget == null) return;
            chunkCore = new VoxelChunksObjectChunkCore(chunkTarget);
            objectTarget = chunkTarget.transform.parent.GetComponent<VoxelChunksObject>();
            if (objectTarget == null) return;
            objectCore = new VoxelChunksObjectCore(objectTarget);

            chunkCore.Initialize();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            InspectorGUI();

            serializedObject.ApplyModifiedProperties();
        }

        protected void InspectorGUI()
        {
            if (chunkTarget == null || objectTarget == null)
            {
                DrawDefaultInspector();
                return;
            }

            #region GuiStyle
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
            #endregion

            var prefabType = PrefabUtility.GetPrefabType(objectTarget.gameObject);
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

            #region Object
            if (!string.IsNullOrEmpty(objectTarget.voxelFilePath))
            {
                chunkTarget.edit_objectFoldout = EditorGUILayout.Foldout(chunkTarget.edit_objectFoldout, "Object", guiStyleFoldoutBold);
                if (chunkTarget.edit_objectFoldout)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    #region Mesh
                    {
                        TypeTitle(chunkTarget.mesh, "Mesh");
                        EditorGUI.indentLevel++;
                        #region Mesh
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.ObjectField(chunkTarget.mesh, typeof(Mesh), false);
                                EditorGUI.EndDisabledGroup();
                            }
                            if (chunkTarget.mesh != null)
                            {
                                if (!AssetDatabase.Contains(chunkTarget.mesh))
                                {
                                    if (GUILayout.Button("Save", GUILayout.Width(64)))
                                    {
                                        #region Create Mesh
                                        string path = EditorUtility.SaveFilePanel("Save mesh", chunkCore.GetDefaultPath(), string.Format("{0}_{1}_mesh.asset", objectTarget.gameObject.name, chunkTarget.chunkName), "asset");
                                        if (!string.IsNullOrEmpty(path))
                                        {
                                            if (path.IndexOf(Application.dataPath) < 0)
                                            {
                                                SaveInsideAssetsFolderDisplayDialog();
                                            }
                                            else
                                            {
                                                Undo.RecordObject(objectTarget, "Save Mesh");
                                                Undo.RecordObject(chunkTarget, "Save Mesh");
                                                path = path.Replace(Application.dataPath, "Assets");
                                                AssetDatabase.CreateAsset(Mesh.Instantiate(chunkTarget.mesh), path);
                                                chunkTarget.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
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
                                        Undo.RecordObject(objectTarget, "Reset Mesh");
                                        Undo.RecordObject(chunkTarget, "Reset Mesh");
                                        chunkTarget.mesh = null;
                                        Refresh();
                                        #endregion
                                    }
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        #endregion
                        #region Vertex Count
                        {
                            EditorGUILayout.LabelField("Vertex Count", chunkTarget.mesh != null ? chunkTarget.mesh.vertexCount.ToString() : "");
                        }
                        #endregion
                        EditorGUI.indentLevel--;
                    }
                    #endregion
                    #region Material
                    if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Individual)
                    {
                        {
                            if (chunkTarget.materials == null || chunkTarget.materials.Count == 0)
                                EditorGUILayout.LabelField("Material", guiStyleMagentaBold);
                            else if (prefabEnable)
                            {
                                bool contains = true;
                                for (int i = 0; i < chunkTarget.materials.Count; i++)
                                {
                                    if (!AssetDatabase.Contains(chunkTarget.materials[i]))
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
                        EditorGUI.indentLevel++;
                        #region Material
                        for (int i = 0; i < chunkTarget.materials.Count; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.ObjectField(chunkTarget.materials[i], typeof(Material), false);
                                EditorGUI.EndDisabledGroup();
                            }
                            if (chunkTarget.materials[i] != null)
                            {
                                if (!AssetDatabase.Contains(chunkTarget.materials[i]))
                                {
                                    if (GUILayout.Button("Save", GUILayout.Width(64)))
                                    {
                                        #region Create Material
                                        string defaultName = string.Format("{0}_{1}_mat{2}.mat", objectTarget.gameObject.name, chunkTarget.chunkName, i);
                                        string path = EditorUtility.SaveFilePanel("Save material", chunkCore.GetDefaultPath(), defaultName, "mat");
                                        if (!string.IsNullOrEmpty(path))
                                        {
                                            if (path.IndexOf(Application.dataPath) < 0)
                                            {
                                                SaveInsideAssetsFolderDisplayDialog();
                                            }
                                            else
                                            {
                                                Undo.RecordObject(objectTarget, "Save Material");
                                                Undo.RecordObject(chunkTarget, "Save Material");
                                                path = path.Replace(Application.dataPath, "Assets");
                                                AssetDatabase.CreateAsset(Material.Instantiate(chunkTarget.materials[i]), path);
                                                chunkTarget.materials[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
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
                                        #region Reset Material
                                        Undo.RecordObject(objectTarget, "Reset Material");
                                        Undo.RecordObject(chunkTarget, "Reset Material");
                                        chunkTarget.materials[i] = null;
                                        Refresh();
                                        #endregion
                                    }
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        #endregion
                        EditorGUI.indentLevel--;
                    }
                    #endregion
                    #region Texture
                    if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Individual)
                    {
                        TypeTitle(chunkTarget.atlasTexture, "Texture");
                        EditorGUI.indentLevel++;
                        #region Texture
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.ObjectField(chunkTarget.atlasTexture, typeof(Texture2D), false);
                                EditorGUI.EndDisabledGroup();
                            }
                            if (chunkTarget.atlasTexture != null)
                            {
                                if (!AssetDatabase.Contains(chunkTarget.atlasTexture))
                                {
                                    if (GUILayout.Button("Save", GUILayout.Width(64)))
                                    {
                                        #region Create Texture
                                        string defaultName = "";
                                        if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Combine)
                                        {
                                            defaultName = string.Format("{0}_tex.png", objectTarget.gameObject.name);
                                        }
                                        else if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Individual)
                                        {
                                            defaultName = string.Format("{0}_{1}_tex.png", objectTarget.gameObject.name, chunkTarget.chunkName);
                                        }
                                        else
                                        {
                                            Assert.IsTrue(false);
                                        }
                                        string path = EditorUtility.SaveFilePanel("Save atlas texture", chunkCore.GetDefaultPath(), defaultName, "png");
                                        if (!string.IsNullOrEmpty(path))
                                        {
                                            if (path.IndexOf(Application.dataPath) < 0)
                                            {
                                                SaveInsideAssetsFolderDisplayDialog();
                                            }
                                            else
                                            {
                                                Undo.RecordObject(objectTarget, "Save Atlas Texture");
                                                Undo.RecordObject(chunkTarget, "Save Atlas Texture");
                                                File.WriteAllBytes(path, chunkTarget.atlasTexture.EncodeToPNG());
                                                path = path.Replace(Application.dataPath, "Assets");
                                                AssetDatabase.ImportAsset(path);
                                                {
                                                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                                                    if (importer != null)
                                                    {
                                                        importer.textureType = TextureImporterType.Default;
                                                        importer.filterMode = FilterMode.Point;
                                                        importer.wrapMode = TextureWrapMode.Clamp;
                                                        importer.mipmapEnabled = objectTarget.generateMipMaps;
                                                        importer.borderMipmap = true;
                                                        importer.textureFormat = TextureImporterFormat.AutomaticTruecolor;
                                                        if (Math.Max(chunkTarget.atlasTexture.width, chunkTarget.atlasTexture.height) > importer.maxTextureSize)
                                                            importer.maxTextureSize = Math.Max(chunkTarget.atlasTexture.width, chunkTarget.atlasTexture.height);
                                                        importer.SaveAndReimport();
                                                    }
                                                }
                                                chunkTarget.atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
                                        Undo.RecordObject(objectTarget, "Reset Atlas Texture");
                                        Undo.RecordObject(chunkTarget, "Reset Atlas Texture");
                                        chunkTarget.atlasTexture = null;
                                        Refresh();
                                        #endregion
                                    }
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        #endregion
                        #region Texture Size
                        {
                            EditorGUILayout.LabelField("Texture Size", chunkTarget.atlasTexture != null ? string.Format("{0} x {1}", chunkTarget.atlasTexture.width, chunkTarget.atlasTexture.height) : "");
                        }
                        #endregion
                        EditorGUI.indentLevel--;
                    }
                    #endregion
                    #region HelpBox
                    {
                        if (prefabEnable)
                        {
                            bool materialsContains = chunkTarget.materials != null;
                            if (chunkTarget.materials != null)
                            {
                                for (int i = 0; i < chunkTarget.materials.Count; i++)
                                {
                                    if (!AssetDatabase.Contains(chunkTarget.materials[i]))
                                    {
                                        materialsContains = false;
                                        break;
                                    }
                                }
                            }

                            if (!AssetDatabase.Contains(chunkTarget.mesh) ||
                                (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Individual && (!materialsContains || !AssetDatabase.Contains(chunkTarget.atlasTexture))))
                            {
                                List<string> helpList = new List<string>();
                                if (!AssetDatabase.Contains(chunkTarget.mesh)) { helpList.Add("Mesh"); }
                                if (objectTarget.materialMode == VoxelChunksObject.MaterialMode.Individual)
                                {
                                    if (chunkTarget.materials != null)
                                    {
                                        for (int i = 0; i < chunkTarget.materials.Count; i++)
                                        {
                                            if (!AssetDatabase.Contains(chunkTarget.materials[i]))
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
                                    if (!AssetDatabase.Contains(chunkTarget.atlasTexture)) { helpList.Add("Texture"); }
                                }
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

            #region Refresh
            {
                if (GUILayout.Button("Refresh"))
                {
                    Undo.RecordObject(objectTarget, "Inspector");
                    Undo.RecordObject(chunkTarget, "Inspector");
                    Refresh();
                }
            }
            #endregion
        }

        protected void SaveInsideAssetsFolderDisplayDialog()
        {
            EditorUtility.DisplayDialog("Need to save in the Assets folder", "You need to save the file inside of the project's assets floder", "ok");
        }

        protected void Refresh()
        {
            objectCore.ReCreate();
        }
    }
}
