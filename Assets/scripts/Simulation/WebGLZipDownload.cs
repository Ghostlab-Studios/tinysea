using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// WebGL helper for downloading multiple files as a ZIP.
/// Requires the accompanying .jslib file to be in Plugins/WebGL/
///
/// Uses JSZip library loaded from CDN.
///
/// Two APIs:
///   1. Batch API (DownloadAsZip) — pass all files at once. Used by single-config runs.
///   2. Progressive API (Init/AddFile/Finalize) — stream files one at a time.
///      Used by bulk simulation to avoid holding all CSV data in WASM memory.
///      In WebGL: each AddFile transfers the string to JS heap immediately,
///      allowing C# to GC the string from the 512 MB WASM heap.
///      In Editor: each AddFile writes the file to disk immediately.
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

    [DllImport("__Internal")]
    private static extern void TinySea_ClearZipDownload();
#endif

    // Progressive ZIP state (Editor/standalone)
    private static string _progressiveFolder;
    private static string _progressiveZipName;
    private static int _progressiveFileCount;

    /// <summary>
    /// Folder where progressive ZIP files are written (Editor/standalone only).
    /// Null in WebGL builds.
    /// </summary>
    public static string ProgressiveOutputFolder => _progressiveFolder;

    /// <summary>
    /// Number of files added to the current progressive ZIP.
    /// </summary>
    public static int ProgressiveFileCount => _progressiveFileCount;

    // ==================== BATCH API (existing, for single-config runs) ====================

    /// <summary>
    /// Download multiple files as a single ZIP archive.
    /// All files must be in memory at once — use Progressive API for large bulk runs.
    /// </summary>
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

    // ==================== PROGRESSIVE API (for bulk simulation) ====================

    /// <summary>
    /// Start building a ZIP progressively. Call AddFileToProgressiveZip() for each file,
    /// then FinalizeProgressiveZip() to trigger the download.
    ///
    /// In WebGL: files are transferred to JS heap immediately, freeing WASM memory.
    /// In Editor: files are written to disk immediately.
    ///
    /// Any previous progressive ZIP state is cleared.
    /// </summary>
    public static void InitProgressiveZip(string zipFilename)
    {
        _progressiveZipName = zipFilename;
        _progressiveFileCount = 0;

#if UNITY_WEBGL && !UNITY_EDITOR
        TinySea_InitZipDownload(zipFilename);
        Debug.Log($"Progressive ZIP initialized (WebGL): {zipFilename}");
#else
        _progressiveFolder = System.IO.Path.Combine(Application.persistentDataPath,
            System.IO.Path.GetFileNameWithoutExtension(zipFilename));

        // Clean up previous run if folder exists
        if (System.IO.Directory.Exists(_progressiveFolder))
            System.IO.Directory.Delete(_progressiveFolder, true);
        System.IO.Directory.CreateDirectory(_progressiveFolder);

        Debug.Log($"Progressive ZIP initialized (Editor): {_progressiveFolder}");
#endif
    }

    /// <summary>
    /// Add a file to the progressive ZIP.
    /// In WebGL: transfers string to JS heap — caller can null the string after this call
    /// to allow GC to reclaim WASM heap memory.
    /// In Editor: writes file to disk immediately.
    /// </summary>
    public static void AddFileToProgressiveZip(string filename, string content)
    {
        _progressiveFileCount++;

#if UNITY_WEBGL && !UNITY_EDITOR
        TinySea_AddFileToZip(filename, content);
#else
        string path = System.IO.Path.Combine(_progressiveFolder, filename);
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(path, content);
#endif
    }

    /// <summary>
    /// Finalize the progressive ZIP and trigger download.
    /// In WebGL: builds ZIP from JS-side file array and triggers browser download.
    /// In Editor: files are already on disk; returns the output folder path.
    /// </summary>
    /// <returns>Output folder path (Editor/standalone) or null (WebGL)</returns>
    public static string FinalizeProgressiveZip()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TinySea_FinalizeZipDownload();
        Debug.Log($"Progressive ZIP download triggered: {_progressiveZipName} ({_progressiveFileCount} files)");
        _progressiveFileCount = 0;
        _progressiveZipName = null;
        return null;
#else
        Debug.Log($"Progressive ZIP complete: {_progressiveFileCount} files in {_progressiveFolder}");
        string folder = _progressiveFolder;
        _progressiveFileCount = 0;
        _progressiveZipName = null;
        return folder;
#endif
    }

    /// <summary>
    /// Cancel/clear a progressive ZIP without downloading.
    /// Frees JS-side memory (WebGL) or cleans up temp folder (Editor).
    /// Safe to call even if no progressive ZIP is in progress.
    /// </summary>
    public static void ClearProgressiveZip()
    {
        _progressiveFileCount = 0;
        _progressiveZipName = null;

#if UNITY_WEBGL && !UNITY_EDITOR
        TinySea_ClearZipDownload();
#else
        if (!string.IsNullOrEmpty(_progressiveFolder) && System.IO.Directory.Exists(_progressiveFolder))
        {
            System.IO.Directory.Delete(_progressiveFolder, true);
            Debug.Log($"Progressive ZIP cleared: {_progressiveFolder}");
        }
        _progressiveFolder = null;
#endif
    }
}
