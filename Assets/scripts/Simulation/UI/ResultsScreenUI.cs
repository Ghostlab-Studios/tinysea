using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Controls the Results Screen UI panel.
/// 
/// Shows:
/// - Progress during simulation (progress bar + status text)
/// - Aggregate summary after completion
/// - Individual scenario results with download buttons
/// - Download all options (aggregate CSV, config, ZIP)
/// 
/// Config can be downloaded at ANY time (before, during, or after simulation).
/// </summary>
public class ResultsScreenUI : MonoBehaviour
{
    [Header("Panel Reference")]
    [SerializeField] private GameObject resultsPanel;

    [Header("Configuration Reference")]
    [Tooltip("Reference to get config for download before simulation completes")]
    [SerializeField] private SimulationController simulationController;

    [Header("Header Section")]
    [SerializeField] private TextMeshProUGUI timestampText;
    [SerializeField] private TextMeshProUGUI configLabelText;
    [SerializeField] private Button downloadConfigButton;

    [Header("Progress Section")]
    [SerializeField] private GameObject progressSection;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Button cancelButton;

    [Header("Results Section")]
    [SerializeField] private GameObject resultsSection;
    [SerializeField] private TextMeshProUGUI quickStatsText;
    [SerializeField] private Button downloadAggregateButton;
    [SerializeField] private Button downloadAllZipButton;

    [Header("Scenario List")]
    [SerializeField] private Transform scenarioListContent;
    [SerializeField] private GameObject scenarioRowPrefab;

    [Header("Footer")]
    [SerializeField] private Button closeButton;

    [Header("Editor Settings")]
    [Tooltip("Open files after saving in Editor/Windows builds")]
    [SerializeField] private bool openFilesAfterSave = true;

    // State
    private AggregateResults _currentResults;
    private bool _isRunning = false;
    private bool _cancelRequested = false;
    private List<GameObject> _scenarioRows = new List<GameObject>();

    // Events for external communication
    public System.Action OnCancelRequested;
    public System.Action OnCloseRequested;

    private void Awake()
    {
        // Wire up buttons
        if (downloadConfigButton != null)
            downloadConfigButton.onClick.AddListener(OnDownloadConfigClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        if (downloadAggregateButton != null)
            downloadAggregateButton.onClick.AddListener(OnDownloadAggregateClicked);

        if (downloadAllZipButton != null)
            downloadAllZipButton.onClick.AddListener(OnDownloadAllZipClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    /// <summary>
    /// Show the results panel and start showing progress
    /// </summary>
    public void Show()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(true);

        // Start in progress mode
        SetProgressMode(true);
        _cancelRequested = false;
        _isRunning = true;

        // Clear previous results
        ClearScenarioList();
        _currentResults = null;

        // Reset progress
        UpdateProgress(0, 1, "Initializing...");

        // Re-enable cancel button
        if (cancelButton != null)
            cancelButton.interactable = true;

        // Config download is ALWAYS available (even during progress)
        if (downloadConfigButton != null)
            downloadConfigButton.interactable = true;
    }

    /// <summary>
    /// Hide the results panel
    /// </summary>
    public void Hide()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        _isRunning = false;
    }

    /// <summary>
    /// Switch between progress mode and results mode
    /// </summary>
    private void SetProgressMode(bool showProgress)
    {
        if (progressSection != null)
            progressSection.SetActive(showProgress);

        if (resultsSection != null)
            resultsSection.SetActive(!showProgress);

        // Config download is ALWAYS enabled
        if (downloadConfigButton != null)
            downloadConfigButton.interactable = true;

        // Aggregate/ZIP buttons only enabled when results are available
        if (downloadAggregateButton != null)
            downloadAggregateButton.interactable = !showProgress;

        if (downloadAllZipButton != null)
            downloadAllZipButton.interactable = !showProgress;
    }

    /// <summary>
    /// Update progress during simulation
    /// </summary>
    public void UpdateProgress(int currentScenario, int totalScenarios, string statusMessage = null)
    {
        if (progressBar != null)
        {
            progressBar.maxValue = totalScenarios;
            progressBar.value = currentScenario;
        }

        if (progressText != null)
        {
            if (string.IsNullOrEmpty(statusMessage))
            {
                progressText.text = $"Running scenario {currentScenario} of {totalScenarios}...";
            }
            else
            {
                progressText.text = statusMessage;
            }
        }
    }

    /// <summary>
    /// Called when a single scenario completes (for real-time list updates if desired)
    /// </summary>
    public void OnScenarioCompleted(ScenarioResult result)
    {
        // Could add row immediately for real-time feedback
        // For now, we'll populate all at once when done
    }

    /// <summary>
    /// Display final results after all scenarios complete
    /// </summary>
    public void DisplayResults(AggregateResults results)
    {
        _currentResults = results;
        _isRunning = false;

        // Switch to results mode
        SetProgressMode(false);

        // Update header
        if (timestampText != null)
            timestampText.text = $"Completed: {results.CompletedAt:MMM dd, yyyy 'at' h:mm tt}";

        if (configLabelText != null)
            configLabelText.text = results.GetConfigLine();

        // Update quick stats
        if (quickStatsText != null)
            quickStatsText.text = results.GetQuickStatsLine();

        // Populate scenario list
        PopulateScenarioList(results.Scenarios);
    }

    /// <summary>
    /// Populate the scrollable list of individual scenarios
    /// </summary>
    private void PopulateScenarioList(List<ScenarioResult> scenarios)
    {
        ClearScenarioList();

        if (scenarioRowPrefab == null || scenarioListContent == null)
        {
            Debug.LogWarning("ScenarioRowPrefab or ScenarioListContent not assigned!");
            return;
        }

        foreach (var scenario in scenarios)
        {
            GameObject row = Instantiate(scenarioRowPrefab, scenarioListContent);
            row.SetActive(true);
            _scenarioRows.Add(row);

            var rowController = row.GetComponent<ScenarioRowUI>();
            if (rowController != null)
            {
                rowController.Setup(scenario, OnDownloadScenarioClicked);
            }
            else
            {
                Debug.LogWarning($"ScenarioRowUI component not found on prefab, using fallback setup");
                SetupRowManually(row, scenario);
            }
        }
    }

    /// <summary>
    /// Fallback setup if ScenarioRowUI component not found
    /// </summary>
    private void SetupRowManually(GameObject row, ScenarioResult scenario)
    {
        var statusIcon = row.transform.Find("StatusIcon")?.GetComponent<TextMeshProUGUI>();
        var summaryText = row.transform.Find("SummaryText")?.GetComponent<TextMeshProUGUI>();
        var downloadBtn = row.transform.Find("DownloadButton")?.GetComponent<Button>();

        if (statusIcon != null)
            statusIcon.text = scenario.GetStatusIcon();

        if (summaryText != null)
            summaryText.text = scenario.GetSummaryLine();

        if (downloadBtn != null)
        {
            int index = scenario.ScenarioIndex;
            downloadBtn.onClick.AddListener(() => OnDownloadScenarioClicked(index));
        }
    }

    /// <summary>
    /// Clear all scenario rows from the list
    /// </summary>
    private void ClearScenarioList()
    {
        foreach (var row in _scenarioRows)
        {
            if (row != null)
                Destroy(row);
        }
        _scenarioRows.Clear();
    }

    /// <summary>
    /// Check if cancel was requested
    /// </summary>
    public bool IsCancelRequested()
    {
        return _cancelRequested;
    }

    // ==================== BUTTON HANDLERS ====================

    private void OnCancelClicked()
    {
        _cancelRequested = true;

        if (progressText != null)
            progressText.text = "Cancelling...";

        if (cancelButton != null)
            cancelButton.interactable = false;

        OnCancelRequested?.Invoke();
    }

    private void OnCloseClicked()
    {
        Hide();
        OnCloseRequested?.Invoke();
    }

    /// <summary>
    /// Download config - works at ANY time (before, during, or after simulation)
    /// Uses ConfigExporter to generate JSON with NO results
    /// </summary>
    private void OnDownloadConfigClicked()
    {
        string csv;
        string timestamp;

        // Try to get config from current results first (if available)
        if (_currentResults != null)
        {
            csv = _currentResults.ToConfigCsv();
            timestamp = _currentResults.CompletedAt.ToString("yyyy-MM-dd_HH-mm-ss");
        }
        // Otherwise get directly from SimulationController's config
        else if (simulationController != null && simulationController.Config != null)
        {
            csv = ConfigExporter.ToCsv(simulationController.Config);
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        }
        else
        {
            Debug.LogError("No configuration available to download!");
            return;
        }

        string filename = $"tinysea_config_{timestamp}.csv";
        TriggerDownload(filename, csv);
    }

    private void OnDownloadAggregateClicked()
    {
        if (_currentResults == null) return;

        string csv = _currentResults.ToAggregateCsv();
        string filename = $"tinysea_aggregate_{_currentResults.CompletedAt:yyyy-MM-dd_HH-mm-ss}.csv";

        TriggerDownload(filename, csv);
    }

    private void OnDownloadScenarioClicked(int scenarioIndex)
    {
        if (_currentResults == null) return;

        var scenario = _currentResults.Scenarios.Find(s => s.ScenarioIndex == scenarioIndex);
        if (scenario == null || string.IsNullOrEmpty(scenario.CsvData))
        {
            Debug.LogWarning($"No CSV data for scenario {scenarioIndex}");
            return;
        }

        string filename = $"tinysea_scenario{scenarioIndex}_{_currentResults.CompletedAt:yyyy-MM-dd_HH-mm-ss}.csv";

        TriggerDownload(filename, scenario.CsvData);
    }

    private void OnDownloadAllZipClicked()
    {
        if (_currentResults == null) return;

        StartCoroutine(DownloadAllAsZip());
    }

    /// <summary>
    /// Trigger a file download (WebGL or Editor)
    /// </summary>
    private void TriggerDownload(string filename, string content)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLDownload.DownloadCsv(filename, content);
        Debug.Log($"Download triggered: {filename}");
#else
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllText(path, content);
        Debug.Log($"Saved to: {path}");

        if (openFilesAfterSave)
        {
            OpenFile(path);
        }
#endif
    }

    /// <summary>
    /// Open a file with the default application (Windows/Mac)
    /// </summary>
    private void OpenFile(string path)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path.Replace("/", "\\"),
                UseShellExecute = true
            });
            Debug.Log($"Opened file: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open file: {e.Message}");
        }
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        try
        {
            System.Diagnostics.Process.Start("open", path);
            Debug.Log($"Opened file: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open file: {e.Message}");
        }
#else
        Debug.Log($"File saved at: {path}");
#endif
    }

    /// <summary>
    /// Open a folder in the file explorer
    /// </summary>
    private void OpenFolder(string folderPath)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", folderPath.Replace("/", "\\"));
            Debug.Log($"Opened folder: {folderPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open folder: {e.Message}");
        }
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        try
        {
            System.Diagnostics.Process.Start("open", folderPath);
            Debug.Log($"Opened folder: {folderPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open folder: {e.Message}");
        }
#else
        Debug.Log($"Folder path: {folderPath}");
#endif
    }

    /// <summary>
    /// Download all scenarios as a ZIP file
    /// </summary>
    private IEnumerator DownloadAllAsZip()
    {
        if (_currentResults == null) yield break;

        Debug.Log("Building ZIP file...");

        var files = new List<(string name, string content)>();

        // Add aggregate CSV
        files.Add((
            "aggregate.csv",
            _currentResults.ToAggregateCsv()
        ));

        // Add config CSV (NO results)
        files.Add((
            "config.csv",
            _currentResults.ToConfigCsv()
        ));

        // Add individual scenario CSVs
        foreach (var scenario in _currentResults.Scenarios)
        {
            if (!string.IsNullOrEmpty(scenario.CsvData))
            {
                files.Add((
                    $"scenario_{scenario.ScenarioIndex}.csv",
                    scenario.CsvData
                ));
            }
        }

        string zipFilename = $"tinysea_results_{_currentResults.CompletedAt:yyyy-MM-dd_HH-mm-ss}.zip";

#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLZipDownload.DownloadAsZip(zipFilename, files);
        Debug.Log($"ZIP download triggered: {zipFilename} ({files.Count} files)");
#else
        string folder = Path.Combine(Application.persistentDataPath,
            Path.GetFileNameWithoutExtension(zipFilename));

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        foreach (var (name, content) in files)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, content);
        }

        Debug.Log($"All files saved to: {folder}");

        if (openFilesAfterSave)
        {
            OpenFolder(folder);
        }
#endif

        yield return null;
    }
}