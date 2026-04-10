using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Cross-platform native file dialogs for standalone builds (Windows + macOS).
/// Does nothing in WebGL or Editor (Editor has its own EditorUtility dialogs).
/// </summary>
public static class StandaloneFileBrowser
{
#if UNITY_STANDALONE_WIN
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetSaveFileName(ref OPENFILENAME ofn);
#endif

    /// <summary>
    /// Open a native file dialog to select a file.
    /// Returns the selected file path, or null if cancelled.
    /// </summary>
    public static string OpenFilePanel(string title, string extension)
    {
#if UNITY_STANDALONE_WIN
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = $"{extension.ToUpper()} Files (*.{extension})\0*.{extension}\0All Files (*.*)\0*.*\0";
        ofn.lpstrFile = new string(new char[512]);
        ofn.nMaxFile = ofn.lpstrFile.Length;
        ofn.lpstrFileTitle = new string(new char[256]);
        ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
        ofn.lpstrTitle = title;
        ofn.lpstrDefExt = extension;
        // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR
        ofn.Flags = 0x00080000 | 0x00001000 | 0x00000008;

        if (GetOpenFileName(ref ofn))
            return ofn.lpstrFile.TrimEnd('\0');
        return null;
#elif UNITY_STANDALONE_OSX
        try
        {
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "osascript";
            process.StartInfo.Arguments =
                $"-e 'POSIX path of (choose file of type {{\"{extension}\"}} with prompt \"{title}\")'";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            string result = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode == 0 && !string.IsNullOrEmpty(result))
                return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"File dialog failed: {e.Message}");
        }
        return null;
#else
        return null;
#endif
    }

    /// <summary>
    /// Open a native save dialog.
    /// Returns the selected save path, or null if cancelled.
    /// </summary>
    public static string SaveFilePanel(string title, string defaultName, string extension)
    {
#if UNITY_STANDALONE_WIN
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = $"{extension.ToUpper()} Files (*.{extension})\0*.{extension}\0All Files (*.*)\0*.*\0";
        // Pre-fill with default filename
        char[] fileBuffer = new char[512];
        defaultName.CopyTo(0, fileBuffer, 0, Math.Min(defaultName.Length, 511));
        ofn.lpstrFile = new string(fileBuffer);
        ofn.nMaxFile = 512;
        ofn.lpstrFileTitle = new string(new char[256]);
        ofn.nMaxFileTitle = 256;
        ofn.lpstrTitle = title;
        ofn.lpstrDefExt = extension;
        // OFN_EXPLORER | OFN_OVERWRITEPROMPT | OFN_NOCHANGEDIR
        ofn.Flags = 0x00080000 | 0x00000002 | 0x00000008;

        if (GetSaveFileName(ref ofn))
            return ofn.lpstrFile.TrimEnd('\0');
        return null;
#elif UNITY_STANDALONE_OSX
        try
        {
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "osascript";
            process.StartInfo.Arguments =
                $"-e 'POSIX path of (choose file name with prompt \"{title}\" default name \"{defaultName}\")'";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            string result = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode == 0 && !string.IsNullOrEmpty(result))
                return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"Save dialog failed: {e.Message}");
        }
        return null;
#else
        return null;
#endif
    }
}
