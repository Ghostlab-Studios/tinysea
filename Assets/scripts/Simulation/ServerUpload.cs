using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Handles uploading simulation CSV files to the server, which stores them in S3.
/// Used in WebGL bulk simulation to avoid holding all CSV data in browser memory.
///
/// Flow:
/// 1. CreateSession() → gets a session ID from the server
/// 2. UploadFile() per scenario/aggregate/config → POSTs each file
/// 3. TriggerDownload() → opens the server's ZIP download URL
///
/// In Editor/standalone, this class is not used — files go to disk via WebGLZipDownload.
/// </summary>
public static class ServerUpload
{
    private static string _sessionId;
    private static string _apiBase;
    private static int _uploadedFileCount;

    /// <summary>Current session ID (null if no session started).</summary>
    public static string SessionId => _sessionId;

    /// <summary>Number of files uploaded in the current session.</summary>
    public static int UploadedFileCount => _uploadedFileCount;

    /// <summary>Whether server upload is available (WebGL only).</summary>
    public static bool IsAvailable
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Create a new upload session on the server.
    /// Must be called before any UploadFile() calls.
    /// Use as: yield return ServerUpload.CreateSession(callback)
    /// </summary>
    public static IEnumerator CreateSession(Action<bool, string> onComplete)
    {
        _sessionId = null;
        _uploadedFileCount = 0;
        _apiBase = GetApiBase();

        var request = new UnityWebRequest($"{_apiBase}/api/session.php", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"ServerUpload: Failed to create session: {request.error}");
            onComplete?.Invoke(false, null);
            yield break;
        }

        var response = JsonUtility.FromJson<SessionResponse>(request.downloadHandler.text);
        _sessionId = response.session;
        onComplete?.Invoke(true, _sessionId);
    }

    /// <summary>
    /// Upload a single file to S3 via the server.
    /// The content string can be nulled by the caller after this coroutine completes
    /// to free memory.
    /// </summary>
    public static IEnumerator UploadFile(string filename, string content, Action<bool> onComplete = null)
    {
        if (string.IsNullOrEmpty(_sessionId))
        {
            Debug.LogError("ServerUpload: No active session. Call CreateSession first.");
            onComplete?.Invoke(false);
            yield break;
        }

        var body = JsonUtility.ToJson(new UploadRequest
        {
            session = _sessionId,
            filename = filename,
            content = content,
        });

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var request = new UnityWebRequest($"{_apiBase}/api/upload.php", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"ServerUpload: Failed to upload {filename}: {request.error}");
            // Try once more
            request.Dispose();
            request = new UnityWebRequest($"{_apiBase}/api/upload.php", "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"ServerUpload: Retry failed for {filename}: {request.error}");
                onComplete?.Invoke(false);
                yield break;
            }
        }

        _uploadedFileCount++;
        onComplete?.Invoke(true);
    }

    /// <summary>
    /// Get the download URL for the current session's ZIP.
    /// </summary>
    public static string GetDownloadUrl()
    {
        if (string.IsNullOrEmpty(_sessionId)) return null;
        return $"{_apiBase}/api/download.php?session={_sessionId}";
    }

    /// <summary>
    /// Trigger browser download of the session ZIP.
    /// </summary>
    public static void TriggerDownload()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string url = GetDownloadUrl();
        if (url != null)
        {
            // Use the existing jslib download mechanism or open URL
            Application.OpenURL(url);
        }
#else
        // Standalone: no browser download available
#endif
    }

    /// <summary>
    /// Clear the session state.
    /// </summary>
    public static void Clear()
    {
        _sessionId = null;
        _uploadedFileCount = 0;
    }

    private static string GetApiBase()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // In WebGL, the API is on the same origin as the page
        return "";
#else
        // In Editor, point to wherever the dev server is
        return "http://localhost";
#endif
    }

    // JSON serialization helpers
    [Serializable]
    private class SessionResponse
    {
        public string session;
    }

    [Serializable]
    private class UploadRequest
    {
        public string session;
        public string filename;
        public string content;
    }
}
