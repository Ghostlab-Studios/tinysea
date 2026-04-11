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
    private bool _bulkProgressiveReady = false;
    private bool _bulkServerReady = false;
    private bool _isRunning = false;
    private bool _cancelRequested = false;
    private List<GameObject> _scenarioRows = new List<GameObject>();

    // Animated dots state — cycles between ".", "..", "..." to show activity
    private Coroutine _dotsCoroutine;
    private string _baseProgressText = "";
    private int _dotCount = 0;

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
        _bulkProgressiveReady = false;
        _bulkServerReady = false;

        // Reset progress
        UpdateProgress(0, 1, "Initializing...");

        // Re-enable cancel button
        if (cancelButton != null)
            cancelButton.interactable = true;

        // Re-enable all download button GameObjects (bulk may have hidden some)
        if (downloadConfigButton != null)
        {
            downloadConfigButton.gameObject.SetActive(true);
            downloadConfigButton.interactable = true;
        }
        if (downloadAggregateButton != null)
            downloadAggregateButton.gameObject.SetActive(true);
        if (downloadAllZipButton != null)
            downloadAllZipButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// Hide the results panel
    /// </summary>
    public void Hide()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        _isRunning = false;
        StopDotsAnimation();
        _bulkProgressiveReady = false;
        _bulkServerReady = false;
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
    /// Update progress with custom text and explicit progress value (0-1).
    /// Used by bulk simulation for multi-batch progress display.
    /// Starts an animated dots suffix (".", "..", "...") that cycles every 0.4s
    /// to show the system is alive during long synchronous scenario computations.
    /// </summary>
    public void UpdateBulkProgress(string text, float progress01)
    {
        if (progressBar != null)
        {
            progressBar.maxValue = 1f;
            progressBar.value = progress01;
        }

        // Update base text but keep dots cycling continuously
        _baseProgressText = text.TrimEnd('.');

        if (progressText != null)
            progressText.text = _baseProgressText + (_dotCount > 0 ? new string('.', _dotCount) : "");

        // Start dots animation if not already running
        if (_dotsCoroutine == null)
            _dotsCoroutine = StartCoroutine(AnimateDots());
    }

    /// <summary>
    /// Animates ".", "..", "..." suffix on the progress text.
    /// Runs as a coroutine — advances each frame Unity gets control
    /// (between synchronous scenario computations via yield return null).
    /// </summary>
    private IEnumerator AnimateDots()
    {
        float timer = 0f;
        while (_isRunning)
        {
            timer += Time.unscaledDeltaTime;
            if (timer >= 0.4f)
            {
                timer = 0f;
                _dotCount = (_dotCount % 3) + 1;
                if (progressText != null)
                    progressText.text = _baseProgressText + new string('.', _dotCount);
            }
            yield return null;
        }
        _dotsCoroutine = null;
    }

    /// <summary>
    /// Stop the animated dots coroutine.
    /// </summary>
    private void StopDotsAnimation()
    {
        if (_dotsCoroutine != null)
        {
            StopCoroutine(_dotsCoroutine);
            _dotsCoroutine = null;
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
    /// Display bulk simulation completion state.
    /// Shows resultsSection with only the "Download All (ZIP)" button.
    /// Hides config download, aggregate download, and scenario rows.
    ///
    /// CSV files have already been streamed to S3 (server) or progressive ZIP.
    /// Clicking Download will either open the server download URL or finalize the ZIP.
    /// </summary>
    public void DisplayBulkResults(int totalBatches, int totalScenarios, bool serverUpload = false)
    {
        _isRunning = false;
        StopDotsAnimation();
        _bulkServerReady = serverUpload;
        _bulkProgressiveReady = !serverUpload;

        // Switch to results mode
        SetProgressMode(false);

        // Update header
        if (timestampText != null)
            timestampText.text = $"Completed: {System.DateTime.Now:MMM dd, yyyy 'at' h:mm tt}";

        if (configLabelText != null)
            configLabelText.text = $"Bulk run \u2014 {totalBatches} batches, {totalScenarios} scenarios";

        // Quick stats summary
        if (quickStatsText != null)
            quickStatsText.text = $"Bulk run complete: {totalBatches} batches, {totalScenarios} total scenarios";

        // Hide config download (multiple configs in bulk — not applicable)
        if (downloadConfigButton != null)
            downloadConfigButton.gameObject.SetActive(false);

        // Hide aggregate download (not applicable for bulk)
        if (downloadAggregateButton != null)
            downloadAggregateButton.gameObject.SetActive(false);

        // SHOW Download All (ZIP) — this is the only download button for bulk
        if (downloadAllZipButton != null)
        {
            downloadAllZipButton.gameObject.SetActive(true);
            downloadAllZipButton.interactable = true;
        }

        // No individual scenario rows for bulk
        ClearScenarioList();
    }

    /// <summary>
    /// Display final results after all scenarios complete
    /// </summary>
    public void DisplayResults(AggregateResults results)
    {
        _currentResults = results;
        _isRunning = false;
        StopDotsAnimation();

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
        ShowButtonFeedback(downloadConfigButton, "Saved!", "Download Config");
    }

    private void OnDownloadAggregateClicked()
    {
        if (_currentResults == null) return;

        string csv = _currentResults.ToAggregateCsv();
        string filename = $"tinysea_aggregate_{_currentResults.CompletedAt:yyyy-MM-dd_HH-mm-ss}.csv";

        TriggerDownload(filename, csv);
        ShowButtonFeedback(downloadAggregateButton, "Saved!", "Download Aggregate");
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
        // Bulk mode: server download (S3) or progressive ZIP
        if (_bulkServerReady)
        {
            SetDownloadButtonState("Downloading...", false);
            ServerUpload.TriggerDownload();
            _bulkServerReady = false;
            // Re-enable after a short delay (browser handles the actual download)
            StartCoroutine(ResetDownloadButtonAfterDelay(3f));
            return;
        }
        if (_bulkProgressiveReady)
        {
            SetDownloadButtonState("Preparing ZIP...", false);
            StartCoroutine(DownloadBulkAsZip());
            return;
        }

        // Normal mode: build ZIP from current results
        if (_currentResults == null) return;
        SetDownloadButtonState("Building ZIP...", false);
        StartCoroutine(DownloadAllAsZip());
    }

    /// <summary>
    /// Update the download button text and interactable state to show feedback.
    /// </summary>
    private void SetDownloadButtonState(string text, bool interactable)
    {
        if (downloadAllZipButton == null) return;

        downloadAllZipButton.interactable = interactable;
        var label = downloadAllZipButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = text;
    }

    /// <summary>
    /// Show temporary feedback on any button: sets text, disables, then resets after delay.
    /// </summary>
    private void ShowButtonFeedback(Button button, string feedbackText, string originalText, float delay = 2f)
    {
        if (button == null) return;
        button.interactable = false;
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = feedbackText;
        StartCoroutine(ResetButtonAfterDelay(button, originalText, delay));
    }

    private IEnumerator ResetButtonAfterDelay(Button button, string originalText, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (button != null)
        {
            button.interactable = true;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = originalText;
        }
    }

    /// <summary>
    /// Reset the download button after a delay (used for S3 downloads where the
    /// browser handles the actual file download and we have no completion callback).
    /// </summary>
    private IEnumerator ResetDownloadButtonAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetDownloadButtonState("Download All (ZIP)", true);
    }

    /// <summary>
    /// Trigger a file download (WebGL or Editor)
    /// </summary>
    private void TriggerDownload(string filename, string content)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLDownload.DownloadCsv(filename, content);
#else
        string path = Path.Combine(SavePaths.ResultsFolder, filename);
        File.WriteAllText(path, content);

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
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open file: {e.Message}");
        }
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        try
        {
            System.Diagnostics.Process.Start("open", path);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open file: {e.Message}");
        }
#else
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
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open folder: {e.Message}");
        }
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        try
        {
            System.Diagnostics.Process.Start("open", folderPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open folder: {e.Message}");
        }
#else
#endif
    }

    /// <summary>
    /// Download all scenarios as a ZIP file
    /// </summary>
    private IEnumerator DownloadAllAsZip()
    {
        if (_currentResults == null) yield break;


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
#else
        string folder = Path.Combine(SavePaths.ResultsFolder,
            Path.GetFileNameWithoutExtension(zipFilename));

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        foreach (var (name, content) in files)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, content);
        }


        if (openFilesAfterSave)
        {
            OpenFolder(folder);
        }
#endif

        // Show "Saved!" feedback, then re-enable after 2s
        SetDownloadButtonState("Saved!", false);
        yield return new WaitForSeconds(2f);
        SetDownloadButtonState("Download All (ZIP)", true);
    }

    /// <summary>
    /// Finalize and download the progressive bulk ZIP.
    /// Files have already been streamed to WebGLZipDownload during simulation.
    /// In WebGL: triggers JSZip to build the archive and start browser download.
    /// In Editor: files are already on disk; opens the output folder.
    /// </summary>
    private IEnumerator DownloadBulkAsZip()
    {
        if (!_bulkProgressiveReady) yield break;


        // Yield a frame so "Preparing ZIP..." text renders before the blocking call
        yield return null;

        string outputFolder = WebGLZipDownload.FinalizeProgressiveZip();
        _bulkProgressiveReady = false;

        // In Editor/standalone, open the output folder
        if (outputFolder != null && openFilesAfterSave)
        {
            OpenFolder(outputFolder);
        }

        // Show "Saved!" feedback, then re-enable after 2s
        SetDownloadButtonState("Saved!", false);
        yield return new WaitForSeconds(2f);
        SetDownloadButtonState("Download All (ZIP)", true);
    }
}