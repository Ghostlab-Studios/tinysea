using System.IO;
using UnityEngine;

/// <summary>
/// Provides the output directory for simulation results.
/// Standalone builds: "TinySeaResults" folder next to the executable.
/// Editor/WebGL: falls back to Application.persistentDataPath.
/// </summary>
public static class SavePaths
{
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
    /// Returns a "TinySeaResults" folder next to the executable (standalone)
    /// or inside persistentDataPath (editor). Creates it if needed.
    /// </summary>
    public static string ResultsFolder
    {
        get
        {
            string folder = Path.Combine(OutputRoot, "TinySeaResults");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
