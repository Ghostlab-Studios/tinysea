using System.Runtime.InteropServices;
using UnityEngine;

public static class WebGLDownload
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void TinySea_DownloadFile(string filename, byte[] data, int dataLen);
#endif

    public static void DownloadCsv(string filename, string csvText)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csvText);
        TinySea_DownloadFile(filename, bytes, bytes.Length);
#else
        Debug.Log("WebGLDownload.DownloadCsv called, but not running in WebGL build.");
#endif
    }
}