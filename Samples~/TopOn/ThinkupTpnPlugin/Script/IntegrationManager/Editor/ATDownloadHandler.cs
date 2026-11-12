#if !UNITY_2017_2_OR_NEWER
using System;
using System.IO;
using UnityEngine.Networking;

namespace AnyThink.Scripts.IntegrationManager.Editor
{
    public class ATDownloadHandler : DownloadHandlerScript
    {
        private FileStream outputStream;

        public ATDownloadHandler(string destPath) : base(new byte[4096])
        {
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            try
            {
                outputStream = new FileStream(destPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }
            catch (Exception ex)
            {
                ATLog.logError(string.Format("Cannot open file {0}: {1}", destPath, ex.Message));
            }
        }

        protected override byte[] GetData() { return null; }

        protected override bool ReceiveData(byte[] data, int length)
        {
            if (data == null || data.Length == 0 || outputStream == null) return false;

            try
            {
                outputStream.Write(data, 0, length);
            }
            catch (Exception ex)
            {
                outputStream.Close();
                outputStream = null;
                ATLog.logError("Download write error: " + ex.Message);
            }
            return true;
        }

        protected override void CompleteContent()
        {
            if (outputStream != null) outputStream.Close();
        }
    }
}
#endif