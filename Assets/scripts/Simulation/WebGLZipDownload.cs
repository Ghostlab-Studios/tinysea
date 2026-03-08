using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// WebGL helper for downloading multiple files as a ZIP.
/// Requires the accompanying .jslib file to be in Plugins/WebGL/
/// 
/// Uses JSZip library loaded from CDN.
/// </summary>
public static class WebGLZipDownload
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void TinySea_InitZipDownload(string zipFilename);
    
    [DllImport("__Internal")]
    private static extern void TinySea_AddFileToZip(string filename, string content);
    
    [DllImport("__Internal")]
    private static extern void TinySea_FinalizeZipDownload();
#endif

    /// <summary>
    /// Download multiple files as a single ZIP archive.
    /// </summary>
    /// <param name="zipFilename">Name for the ZIP file</param>
    /// <param name="files">List of (filename, content) tuples</param>
    public static void DownloadAsZip(string zipFilename, List<(string name, string content)> files)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Initialize ZIP
        TinySea_InitZipDownload(zipFilename);
        
        // Add each file
        foreach (var (name, content) in files)
        {
            TinySea_AddFileToZip(name, content);
        }
        
        // Finalize and trigger download
        TinySea_FinalizeZipDownload();
#else
        Debug.Log($"WebGLZipDownload.DownloadAsZip called with {files.Count} files, but not running in WebGL build.");
        
        // In Editor, save files individually to persistentDataPath
        string folder = System.IO.Path.Combine(Application.persistentDataPath, 
            System.IO.Path.GetFileNameWithoutExtension(zipFilename));
        
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);
        
        foreach (var (name, content) in files)
        {
            string path = System.IO.Path.Combine(folder, name);
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(path, content);
            Debug.Log($"Saved: {path}");
        }
        
        Debug.Log($"All files saved to: {folder}");
#endif
    }
}
