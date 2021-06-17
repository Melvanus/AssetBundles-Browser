using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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