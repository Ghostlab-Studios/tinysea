using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Provides the output directory for simulation results.
/// Standalone builds: tries "TinySeaResults" folder next to the executable,
/// falls back to Application.persistentDataPath if that location isn't writable.
/// </summary>
public static class SavePaths
{
    private static string _cachedFolder;

    public static string OutputRoot
    {
        get
        {
#if UNITY_EDITOR
            return Application.persistentDataPath;
#elif UNITY_STANDALONE_OSX
            // Application.dataPath = <app>/Contents → parent.parent = folder with .app
            return Directory.GetParent(Application.dataPath).Parent.FullName;
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
            // Application.dataPath = <exe>/TinySea_Data → parent = folder with .exe
            return Directory.GetParent(Application.dataPath).FullName;
#else
            return Application.persistentDataPath;
#endif
        }
    }

    /// <summary>
    /// Returns a writable "TinySeaResults" folder. Tries next to the executable first,
    /// falls back to persistentDataPath if that location isn't writable (e.g. macOS permissions).
    /// </summary>
    public static string ResultsFolder
    {
        get
        {
            if (_cachedFolder != null)
                return _cachedFolder;

            // Try the preferred location (next to exe/app)
            string preferred = Path.Combine(OutputRoot, "TinySeaResults");
            if (TryCreateFolder(preferred))
            {
                _cachedFolder = preferred;
                return _cachedFolder;
            }

            // Fallback to persistentDataPath (always writable)
            string fallback = Path.Combine(Application.persistentDataPath, "TinySeaResults");
            TryCreateFolder(fallback);
            _cachedFolder = fallback;
            Debug.Log($"SavePaths: Using fallback location: {_cachedFolder}");
            return _cachedFolder;
        }
    }

    private static bool TryCreateFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SavePaths: Cannot write to {path}: {e.Message}");
            return false;
        }
    }
}
