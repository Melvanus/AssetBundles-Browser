using UnityEditor;
using UnityEngine;
using System.IO;

namespace AssetBundleBrowser
{
    class SingleBundleInspector
    {
        internal static string currentPath { get; set; }


        internal SingleBundleInspector() { }

        private Editor m_inspectEditor = null;

        private Rect m_Position;

        [SerializeField]
        private Vector2 m_ScrollPosition;

        private AssetBundleTab m_assetBundleTab = null;
        private TabData m_inspectTabData = null;

        internal void SetBundle(AssetBundle bundle, string path = "", TabData inspectTabData = null, AssetBundleTab assetBundleTab = null)
        {
            //static var...
            currentPath = path;
            m_inspectTabData = inspectTabData;
            m_assetBundleTab = assetBundleTab;

            //members
            m_inspectEditor = null;
            if (bundle != null)
            {
                m_inspectEditor = Editor.CreateEditor(bundle);
            }
            
        }

        internal void OnGUI(Rect pos)
        {
            m_Position = pos;

            DrawBundleData();
        }

        private void DrawBundleData()
        {
            if (m_inspectEditor != null)
            {
                GUILayout.BeginArea(m_Position);
                m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
                m_inspectEditor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            else if(!string.IsNullOrEmpty(currentPath))
            {
                var style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.wordWrap = true;
                GUI.Label(m_Position, new GUIContent("Invalid bundle selected"), style);

                if (m_inspectTabData != null && GUI.Button(new Rect(new Vector2((m_Position.position.x + m_Position.width / 2f) - 37.5f, (m_Position.position.y + m_Position.height / 2f) + 15), new Vector2(75, 30)), "Ignore file"))
                {
                    var possibleFolderData = m_inspectTabData.FolderDataContainingFilePath(currentPath);
                    if (possibleFolderData != null)
                    {
                        if (!possibleFolderData.ignoredFiles.Contains(currentPath))
                            possibleFolderData.ignoredFiles.Add(currentPath);

                        if(m_assetBundleTab != null)
                            m_assetBundleTab.RefreshBundles();
                    }
                } 
            }
        }
    }

    [CustomEditor(typeof(AssetBundle))]
    internal class AssetBundleEditor : Editor
    {
        internal bool pathFoldout = false;
        internal bool advancedFoldout = false;
        
        
        public override void OnInspectorGUI()
        {
            AssetBundle bundle = target as AssetBundle;

            using (new EditorGUI.DisabledScope(true))
            {
                var leftStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
                leftStyle.alignment = TextAnchor.UpperLeft;
                GUILayout.Label(new GUIContent("Name: " + bundle.name), leftStyle);

                long fileSize = -1;
                if(!System.String.IsNullOrEmpty(SingleBundleInspector.currentPath) && File.Exists(SingleBundleInspector.currentPath) )
                {
                    System.IO.FileInfo fileInfo = new System.IO.FileInfo(SingleBundleInspector.currentPath);
                    fileSize = fileInfo.Length;
                }

                if (fileSize < 0)
                    GUILayout.Label(new GUIContent("Size: unknown"), leftStyle);
                else
                    GUILayout.Label(new GUIContent("Size: " + EditorUtility.FormatBytes(fileSize)), leftStyle);

                var assetNames = bundle.GetAllAssetNames();
                pathFoldout = EditorGUILayout.Foldout(pathFoldout, "Source Asset Paths");
                if (pathFoldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (var asset in assetNames)
                        EditorGUILayout.LabelField(asset);
                    EditorGUI.indentLevel--;
                }


                advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced Data");

            }

            if (advancedFoldout)
            {
                EditorGUI.indentLevel++;
                base.OnInspectorGUI();
                EditorGUI.indentLevel--;
            }
        }

    }
}