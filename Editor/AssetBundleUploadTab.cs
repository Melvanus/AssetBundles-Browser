using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System.Threading.Tasks;
using System;
using UnityEngine.Networking;
using VReedback.Utils;
using System.Text;

namespace VReedback.Utils
{
    public class AssetBundleUploader
    {
        private string accountName;
        private string password;
        private string apiEndpoint;

        public AssetBundleUploader(string accountName, string password, string apiEndpoint)
        {
            this.accountName = accountName;
            this.password = password;
            this.apiEndpoint = apiEndpoint;
        }

        public async Task<bool> UploadBundle(int id, string assetBundlePath, IProgress<float> progress = null)
        {
            if (!File.Exists(assetBundlePath))
                return false;
            try
            {
                /*
                byte[] bundleData;
                using (FileStream SourceStream = File.Open(assetBundlePath, FileMode.Open))
                {
                    bundleData = new byte[SourceStream.Length];
                    await SourceStream.ReadAsync(bundleData, 0, (int)SourceStream.Length);
                }
                */
                byte[] bytes = File.ReadAllBytes(assetBundlePath);

                var filename = Path.GetFileName(assetBundlePath);

                List<IMultipartFormSection> form = new List<IMultipartFormSection>();
                form.Add(new MultipartFormDataSection("name", "foo"));
                form.Add(new MultipartFormDataSection("password", "bar"));
                form.Add(new MultipartFormFileSection("file", bytes, filename, "application/octet-stream"));

                {
                    var webRequest = UnityWebRequest.Post(apiEndpoint, form);
                    await AwaitRequest(webRequest.SendWebRequest(), progress);
                    Debug.Log(webRequest.downloadHandler.text);
                }

                if (false)
                {
                    // We have to to stuff... because...
                    // https://forum.unity.com/threads/unitywebrequest-post-multipart-form-data-doesnt-append-files-to-itself.627916/

                    byte[] boundary = UnityWebRequest.GenerateBoundary();
                    //serialize form fields into byte[] => requires a bounday to put in between fields
                    byte[] formSections = UnityWebRequest.SerializeFormSections(form, boundary);
                    byte[] terminate = Encoding.UTF8.GetBytes(String.Concat("\r\n--", Encoding.UTF8.GetString(boundary), "--"));
                    // Make my complete body from the two byte arrays
                    byte[] body = new byte[formSections.Length + terminate.Length];
                    Buffer.BlockCopy(formSections, 0, body, 0, formSections.Length);
                    Buffer.BlockCopy(terminate, 0, body, formSections.Length, terminate.Length);
                    // Set the content type - NO QUOTES around the boundary
                    string contentType = String.Concat("multipart/form-data; boundary=", Encoding.UTF8.GetString(boundary));

                    using (UnityWebRequest www = new UnityWebRequest(apiEndpoint, UnityWebRequest.kHttpVerbPOST))
                    {
                        UploadHandlerRaw uploadHandlerFile = new UploadHandlerRaw(body);
                        www.uploadHandler = (UploadHandler)uploadHandlerFile;
                        www.uploadHandler.contentType = contentType;
                        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                        //www.SetRequestHeader("Content-Type", "multipart/form-data");
                        var handler = await AwaitRequest(www.SendWebRequest(), progress);
                        Debug.Log(www.downloadHandler.text);
                    }

                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }

            return true;
        }

        public static async Task<DownloadHandler> AwaitRequest(UnityWebRequestAsyncOperation request, IProgress<float> progress)
        {
            do
            {
                if (progress != null)
                    progress.Report(request.progress);
                await Task.Yield();
            } while (!request.isDone);

            if (progress != null)
                progress.Report(request.progress);

            if (request.webRequest.result == UnityWebRequest.Result.ConnectionError ||
                request.webRequest.result == UnityWebRequest.Result.DataProcessingError ||
                request.webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                throw new IOException(request.webRequest.error);
            }

            return request.webRequest.downloadHandler;
        }

    }
}

namespace AssetBundleBrowser
{
    [System.Serializable]
    internal abstract class TabData
    {
        [SerializeField]
        private List<string> m_BundlePaths = new List<string>();
        [SerializeField]
        private List<BundleFolderData> m_BundleFolders = new List<BundleFolderData>();

        internal IList<string> BundlePaths { get { return m_BundlePaths.AsReadOnly(); } }
        internal IList<BundleFolderData> BundleFolders { get { return m_BundleFolders.AsReadOnly(); } }

        internal void AddPath(string newPath)
        {
            if (!m_BundlePaths.Contains(newPath))
            {
                var possibleFolderData = FolderDataContainingFilePath(newPath);
                if (possibleFolderData == null)
                {
                    m_BundlePaths.Add(newPath);
                }
                else
                {
                    possibleFolderData.ignoredFiles.Remove(newPath);
                }
            }
        }

        internal void AddFolder(string newPath)
        {
            if (!BundleFolderContains(newPath))
                m_BundleFolders.Add(new BundleFolderData(newPath));
        }

        internal void RemovePath(string pathToRemove)
        {
            m_BundlePaths.Remove(pathToRemove);
        }

        internal void RemoveFolder(string pathToRemove)
        {
            m_BundleFolders.Remove(BundleFolders.FirstOrDefault(bfd => bfd.path == pathToRemove));
        }

        internal bool FolderIgnoresFile(string folderPath, string filePath)
        {
            if (BundleFolders == null)
                return false;
            var bundleFolderData = BundleFolders.FirstOrDefault(bfd => bfd.path == folderPath);
            return bundleFolderData != null && bundleFolderData.ignoredFiles.Contains(filePath);
        }

        internal BundleFolderData FolderDataContainingFilePath(string filePath)
        {
            foreach (var bundleFolderData in BundleFolders)
            {
                if (Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(bundleFolderData.path)))
                {
                    return bundleFolderData;
                }
            }
            return null;
        }

        private bool BundleFolderContains(string folderPath)
        {
            foreach (var bundleFolderData in BundleFolders)
            {
                if (Path.GetFullPath(bundleFolderData.path) == Path.GetFullPath(folderPath))
                {
                    return true;
                }
            }
            return false;
        }

        [System.Serializable]
        internal class BundleFolderData
        {
            [SerializeField]
            internal string path;

            [SerializeField]
            private List<string> m_ignoredFiles;
            internal List<string> ignoredFiles
            {
                get
                {
                    if (m_ignoredFiles == null)
                        m_ignoredFiles = new List<string>();
                    return m_ignoredFiles;
                }
            }

            internal BundleFolderData(string p)
            {
                path = p;
            }
        }
    }


    [System.Serializable]
    internal class InspectTabData : TabData
    {

    }


    [System.Serializable]
    internal class UploadTabData : TabData
    {
        [SerializeField]
        public string accountName;
        [SerializeField]
        public string accountPassword;
        [SerializeField]
        public string apiEndpoint;

    }

    internal abstract class AssetBundleTab
    {
        abstract internal void RefreshBundles();
        abstract internal void RemoveBundlePath(string pathToRemove);
        abstract internal void RemoveBundleFolder(string folderToRemove);
        abstract internal void SetBundleItem(IList<InspectTreeItem> selected);

        protected Dictionary<string, List<string>> m_BundleList;

        internal Dictionary<string, List<string>> BundleList
        { get { return m_BundleList; } }
    }

    [System.Serializable]
    internal class AssetBundleUploadTab : AssetBundleTab
    {
        Rect m_Position;

        [SerializeField]
        private UploadTabData m_Data;

        private InspectBundleTree m_BundleTreeView;
        [SerializeField]
        private TreeViewState m_BundleTreeState;

        internal Editor m_Editor = null;

        private AssetBundleBrowserMain m_Parent = null;

        private SingleBundleInspector m_SingleInspector;

        private bool m_uploadProcessRunning;
        private bool m_progressUpdate;
        private bool m_uploadProcessFinished;

        /// <summary>
        /// Collection of loaded asset bundle records indexed by bundle name
        /// </summary>
        private Dictionary<string, AssetBundleRecord> m_loadedAssetBundles;

        private IList<InspectTreeItem> m_SelectedBundleTreeItems;

        /// <summary>
        /// Returns the record for a loaded asset bundle by name if it exists in our container.
        /// </summary>
        /// <returns>Asset bundle record instance if loaded, otherwise null.</returns>
        /// <param name="bundleName">Name of the loaded asset bundle, excluding the variant extension</param>
        private AssetBundleRecord GetLoadedBundleRecordByName(string bundleName)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                return null;
            }

            if (!m_loadedAssetBundles.ContainsKey(bundleName))
            {
                return null;
            }

            return m_loadedAssetBundles[bundleName];
        }

        internal AssetBundleUploadTab()
        {
            m_BundleList = new Dictionary<string, List<string>>();
            m_SingleInspector = new SingleBundleInspector();
            m_loadedAssetBundles = new Dictionary<string, AssetBundleRecord>();
        }

        internal void OnEnable(Rect pos, AssetBundleBrowserMain editorWindow)
        {
            m_Parent = editorWindow;

            m_Position = pos;
            if (m_Data == null)
                m_Data = new UploadTabData();

            //LoadData...
            var dataPath = System.IO.Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AssetBundleBrowserUpload.dat";

            if (File.Exists(dataPath))
            {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream file = File.Open(dataPath, FileMode.Open);
                var data = bf.Deserialize(file) as UploadTabData;
                if (data != null)
                    m_Data = data;
                file.Close();
            }


            if (m_BundleList == null)
                m_BundleList = new Dictionary<string, List<string>>();

            if (m_BundleTreeState == null)
                m_BundleTreeState = new TreeViewState();
            m_BundleTreeView = new InspectBundleTree(m_BundleTreeState, this);


            RefreshBundles();
        }

        internal void OnDisable()
        {
            ClearData();

            var dataPath = System.IO.Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AssetBundleBrowserUpload.dat";

            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(dataPath);

            bf.Serialize(file, m_Data);
            file.Close();
        }

        internal void OnGUI(Rect pos)
        {
            m_Position = pos;

            if (Application.isPlaying)
            {
                var style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.wordWrap = true;
                GUI.Label(
                    new Rect(m_Position.x + 1f, m_Position.y + 1f, m_Position.width - 2f, m_Position.height - 2f),
                    new GUIContent("Inspector unavailable while in PLAY mode"),
                    style);
            }
            else
            {
                OnGUIEditor();
            }
        }

        private async Task Upload(string name, string pass, string url)
        {
            try
            {
                Progress<float> progress = new Progress<float>(p =>
                   UpdateProgress(p)
                );
                var uploader = new AssetBundleUploader(name, pass, url);
                var success = await uploader.UploadBundle(0001, m_SelectedBundleTreeItems[0].bundlePath, progress);
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
            m_Parent.Repaint();
            Debug.Log("Progress:" + p);   
        }

        float uploadProgress = 0f;

        private void OnGUIEditor()
        {
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Add File", GUILayout.MaxWidth(75f)))
            {
                BrowseForFile();
            }
            if (GUILayout.Button("Add Folder", GUILayout.MaxWidth(75f)))
            {
                BrowseForFolder();
            }

            GUILayout.EndHorizontal();
            EditorGUILayout.Space();

            m_Data.accountName = EditorGUILayout.TextField("Account Name", m_Data.accountName);
            m_Data.accountPassword = EditorGUILayout.PasswordField("Password", m_Data.accountPassword);
            m_Data.apiEndpoint = EditorGUILayout.TextField("API Endpoint", m_Data.apiEndpoint);

            GUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(!SelectedBundleValidForUpload() || m_uploadProcessRunning);
            if (GUILayout.Button("Upload selected Bundle", GUILayout.MaxWidth(150f)))
            {
                m_uploadProcessRunning = true;
                m_uploadProcessFinished = false;
                _ = Upload(m_Data.accountName, m_Data.accountPassword, m_Data.apiEndpoint);
                Debug.LogError("Upload");
                m_Parent.Repaint();
                Debug.LogError("After repaint call");
            }
            EditorGUI.EndDisabledGroup();
            EditorUtility.ClearProgressBar();
            GUILayout.EndHorizontal();

            if (m_BundleList.Count > 0)
            {
                int halfWidth = (int)(m_Position.width / 2.0f);
                m_BundleTreeView.OnGUI(new Rect(m_Position.x, m_Position.y + 120, halfWidth, m_Position.height - 120));
                m_SingleInspector.OnGUI(new Rect(m_Position.x + halfWidth, m_Position.y + 120, halfWidth, m_Position.height - 120));
            }

            if (m_uploadProcessRunning)
            {
                if (m_uploadProcessFinished)
                {
                    m_uploadProcessRunning = false;
                    m_progressUpdate = false;
                    EditorUtility.ClearProgressBar();
                    m_Parent.Repaint();
                } else
                {
                    EditorUtility.DisplayProgressBar("Upload", "Uploading asset bundle...", uploadProgress); 
                    m_progressUpdate = false;
                }
            }
        }

        internal override void RemoveBundlePath(string pathToRemove)
        {
            UnloadBundle(pathToRemove);
            m_Data.RemovePath(pathToRemove);
        }

        internal override void RemoveBundleFolder(string pathToRemove)
        {
            List<string> paths = null;
            if (m_BundleList.TryGetValue(pathToRemove, out paths))
            {
                foreach (var p in paths)
                {
                    UnloadBundle(p);
                }
            }
            m_Data.RemoveFolder(pathToRemove);
        }

        private void BrowseForFile()
        {
            var newPath = EditorUtility.OpenFilePanelWithFilters("Bundle Folder", string.Empty, new string[] { });
            if (!string.IsNullOrEmpty(newPath))
            {
                var gamePath = System.IO.Path.GetFullPath(".");//TODO - FileUtil.GetProjectRelativePath??
                gamePath = gamePath.Replace("\\", "/");
                if (newPath.StartsWith(gamePath))
                    newPath = newPath.Remove(0, gamePath.Length + 1);

                m_Data.AddPath(newPath);

                RefreshBundles();
            }
        }

        //TODO - this is largely copied from BuildTab, should maybe be shared code.
        private void BrowseForFolder(string folderPath = null)
        {
            folderPath = EditorUtility.OpenFolderPanel("Bundle Folder", string.Empty, string.Empty);
            if (!string.IsNullOrEmpty(folderPath))
            {
                var gamePath = System.IO.Path.GetFullPath(".");//TODO - FileUtil.GetProjectRelativePath??
                gamePath = gamePath.Replace("\\", "/");
                if (folderPath.Length > gamePath.Length && folderPath.StartsWith(gamePath))
                    folderPath = folderPath.Remove(0, gamePath.Length + 1);

                AddBundleFolder(folderPath);

                RefreshBundles();
            }
        }

        internal void AddBundleFolder(string folderPath)
        {
            m_Data.AddFolder(folderPath);
        }

        private void ClearData()
        {
            m_SingleInspector.SetBundle(null);

            if (null != m_loadedAssetBundles)
            {
                List<AssetBundleRecord> records = new List<AssetBundleRecord>(m_loadedAssetBundles.Values);
                foreach (AssetBundleRecord record in records)
                {
                    record.bundle.Unload(true);
                }

                m_loadedAssetBundles.Clear();
            }
        }

        internal override void RefreshBundles()
        {
            ClearData();


            if (m_Data.BundlePaths == null)
                return;

            //find assets
            if (m_BundleList == null)
                m_BundleList = new Dictionary<string, List<string>>();

            m_BundleList.Clear();
            var pathsToRemove = new List<string>();
            foreach (var filePath in m_Data.BundlePaths)
            {
                if (File.Exists(filePath))
                {
                    AddBundleToList(string.Empty, filePath);
                }
                else
                {
                    Debug.Log("Expected bundle not found: " + filePath);
                    pathsToRemove.Add(filePath);
                }
            }
            foreach (var path in pathsToRemove)
            {
                m_Data.RemovePath(path);
            }
            pathsToRemove.Clear();

            foreach (var folder in m_Data.BundleFolders)
            {
                if (Directory.Exists(folder.path))
                {
                    AddFilePathToList(folder.path, folder.path);
                }
                else
                {
                    Debug.Log("Expected folder not found: " + folder);
                    pathsToRemove.Add(folder.path);
                }
            }
            foreach (var path in pathsToRemove)
            {
                m_Data.RemoveFolder(path);
            }

            m_BundleTreeView.Reload();
        }

        private void AddBundleToList(string parent, string bundlePath)
        {
            List<string> bundles = null;
            m_BundleList.TryGetValue(parent, out bundles);

            if (bundles == null)
            {
                bundles = new List<string>();
                m_BundleList.Add(parent, bundles);
            }
            bundles.Add(bundlePath);
        }

        private void AddFilePathToList(string rootPath, string path)
        {
            var notAllowedExtensions = new string[] { ".meta", ".manifest", ".dll", ".cs", ".exe", ".js" };
            foreach (var file in Directory.GetFiles(path))
            {
                var ext = Path.GetExtension(file);
                if (!notAllowedExtensions.Contains(ext))
                {
                    var f = file.Replace('\\', '/');
                    if (File.Exists(file) && !m_Data.FolderIgnoresFile(rootPath, f))
                    {
                        AddBundleToList(rootPath, f);
                    }
                }
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                AddFilePathToList(rootPath, dir);
            }
        }

        internal override void SetBundleItem(IList<InspectTreeItem> selected)
        {
            m_SelectedBundleTreeItems = selected;
            if (selected == null || selected.Count == 0 || selected[0] == null)
            {
                m_SingleInspector.SetBundle(null);
            }
            else if (selected.Count == 1)
            {
                AssetBundle bundle = LoadBundle(selected[0].bundlePath);
                m_SingleInspector.SetBundle(bundle, selected[0].bundlePath, m_Data, this);
            }
            else
            {
                m_SingleInspector.SetBundle(null);

                //perhaps there should be a way to set a message in the inspector, to tell it...
                //var style = GUI.skin.label;
                //style.alignment = TextAnchor.MiddleCenter;
                //style.wordWrap = true;
                //GUI.Label(
                //    inspectorRect,
                //    new GUIContent("Multi-select inspection not supported"),
                //    style);
            }
        }


        /// <summary>
        /// Returns the bundle at the specified path, loading it if necessary.
        /// Unloads previously loaded bundles if necessary when dealing with variants.
        /// </summary>
        /// <returns>Returns the loaded bundle, null if it could not be loaded.</returns>
        /// <param name="path">Path of bundle to get</param>
        private AssetBundle LoadBundle(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string extension = Path.GetExtension(path);

            string bundleName = path.Substring(0, path.Length - extension.Length);

            // Check if we have a record for this bundle
            AssetBundleRecord record = GetLoadedBundleRecordByName(bundleName);
            AssetBundle bundle = null;
            if (null != record)
            {
                // Unload existing bundle if variant names differ, otherwise use existing bundle
                if (!record.path.Equals(path))
                {
                    UnloadBundle(bundleName);
                }
                else
                {
                    bundle = record.bundle;
                }
            }

            if (null == bundle)
            {
                // Load the bundle
                bundle = AssetBundle.LoadFromFile(path);
                if (null == bundle)
                {
                    return null;
                }

                m_loadedAssetBundles[bundleName] = new AssetBundleRecord(path, bundle);

                // Load the bundle's assets
                string[] assetNames = bundle.GetAllAssetNames();
                foreach (string name in assetNames)
                {
                    bundle.LoadAsset(name);
                }
            }

            return bundle;
        }

        /// <summary>
        /// Unloads the bundle with the given name.
        /// </summary>
        /// <param name="bundleName">Name of the bundle to unload without variant extension</param>
        private void UnloadBundle(string bundleName)
        {
            AssetBundleRecord record = this.GetLoadedBundleRecordByName(bundleName);
            if (null == record)
            {
                return;
            }

            record.bundle.Unload(true);
            m_loadedAssetBundles.Remove(bundleName);
        }

        private bool SelectedBundleValidForUpload()
        {
            return
                m_SelectedBundleTreeItems != null &&
                m_SelectedBundleTreeItems[0] != null &&
                !string.IsNullOrEmpty(m_SelectedBundleTreeItems[0].bundlePath);
        }
    }
}
