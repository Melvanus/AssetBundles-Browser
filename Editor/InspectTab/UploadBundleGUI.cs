using UnityEditor;
using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using System;
using VReedback.Utils;
using System.Runtime.Serialization.Formatters.Binary;

namespace AssetBundleBrowser
{

    [System.Serializable]
    class UploadFieldsData
    {
        public string accountName;
        public string accountPassword;
        public string url;
    }

    class UploadBundleGUI
    {
        internal static string currentPath { get; set; }

        internal AssetBundleInspectTab m_Parent;

        internal UploadBundleGUI(AssetBundleInspectTab parent) { m_Parent = parent; }

        //private Editor m_Editor = null;

        private Rect m_Position;

        [SerializeField]
        private Vector2 m_ScrollPosition;

        [SerializeField]
        private UploadFieldsData m_Data;

        private AssetBundleInspectTab m_assetBundleInspectTab = null;
        private AssetBundleInspectTab.InspectTabData m_inspectTabData = null;

        private AssetBundle selectedBundle;
        private bool validBundleSelected => selectedBundle != null;

        private bool m_uploadProcessRunning;
        private bool m_progressUpdate;
        private bool m_uploadProcessFinished;

        internal void SetBundle(AssetBundle bundle, string path = "", AssetBundleInspectTab.InspectTabData inspectTabData = null, AssetBundleInspectTab assetBundleInspectTab = null)
        {
            //static var...
            currentPath = path;
            m_inspectTabData = inspectTabData;
            m_assetBundleInspectTab = assetBundleInspectTab;

            //members
            //m_Editor = null;
            selectedBundle = bundle;
            if (bundle != null)
            {
                //m_Editor = Editor.CreateEditor(bundle);
            }
        }

        internal void OnEnable(Rect pos)
        {
            m_Position = pos;
            if (m_Data == null)
                m_Data = new UploadFieldsData();

            //LoadData...
            var dataPath = System.IO.Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AssetBundleBrowserUploadFieldsData.dat";

            if (File.Exists(dataPath))
            {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream file = File.Open(dataPath, FileMode.Open);
                var data = bf.Deserialize(file) as UploadFieldsData;
                if (data != null)
                    m_Data = data;
                file.Close();
            }
        }

        internal void OnDisable()
        {
            var dataPath = System.IO.Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AssetBundleBrowserUploadFieldsData.dat";

            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(dataPath);

            bf.Serialize(file, m_Data);
            file.Close();
        }

        internal void OnGUI(Rect pos)
        {
            m_Position = pos;

            DrawBundleData();
        }

        private void DrawBundleData()
        {

            GUILayout.BeginArea(m_Position);

            m_Data.accountName = EditorGUILayout.TextField("Account Name", m_Data.accountName);
            m_Data.accountPassword = EditorGUILayout.PasswordField("Password", m_Data.accountPassword);
            m_Data.url = EditorGUILayout.TextField("API Endpoint", m_Data.url);

            GUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(!validBundleSelected || m_uploadProcessRunning);
            if (GUILayout.Button("Upload selected Bundle", GUILayout.MaxWidth(150f)))
            {
                m_uploadProcessRunning = true;
                m_uploadProcessFinished = false;
                _ = Upload(currentPath, m_Data.accountName, m_Data.accountPassword, m_Data.url);
                Debug.LogError("Upload");
                //m_Parent.Repaint();
                Debug.LogError("After repaint call");
            }
            EditorGUI.EndDisabledGroup();
            EditorUtility.ClearProgressBar();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            if (m_uploadProcessRunning)
            {
                if (m_uploadProcessFinished)
                {
                    m_uploadProcessRunning = false;
                    m_progressUpdate = false;
                    EditorUtility.ClearProgressBar();
                    //m_Parent.Repaint();
                }
                else
                {
                    EditorUtility.DisplayProgressBar("Upload", "Uploading asset bundle...", uploadProgress);
                    m_progressUpdate = false;
                }
            }
        }

        private async Task Upload(string path, string name, string pass, string url)
        {
            try
            {
                Progress<float> progress = new Progress<float>(p =>
                   UpdateProgress(p)
                );
                var uploader = new AssetBundleUploader(name, pass, url);
                var success = await uploader.UploadBundle(0001, path, progress);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                m_uploadProcessFinished = true;
            }
        }

        private void UpdateProgress(float p)
        {
            uploadProgress = p;
            m_progressUpdate = true;
            //m_assetBundleInspectTab.Repaint();
            Debug.Log("Progress:" + p);
        }

        float uploadProgress = 0f;




    }

}