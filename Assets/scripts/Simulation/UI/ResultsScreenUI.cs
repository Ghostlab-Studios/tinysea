using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls the Results Screen UI panel.
/// 
/// Shows:
/// - Progress during simulation (progress bar + status text)
/// - Aggregate summary after completion
/// - Individual scenario results with download buttons
/// - Download all options (aggregate CSV, config, ZIP)
/// 
/// UI HIERARCHY (create in Unity):
/// 
/// ResultsPanel (this script)
/// ├── Header
/// │   ├── TimestampText (TMP)
/// │   ├── ConfigLabel (TMP) - "365 days × 10 scenarios"
/// │   └── DownloadConfigButton (Button)
/// │
/// ├── ProgressSection (active during running)
/// │   ├── ProgressBar (Slider)
/// │   ├── ProgressText (TMP) - "Running scenario 3 of 10..."
/// │   └── CancelButton (Button)
/// │
/// ├── ResultsSection (active after completion)
/// │   ├── QuickStatsText (TMP) - "✓ 7 survived | ✗ 3 crashed | Avg T1: 2,100"
/// │   │
/// │   ├── DownloadRow
/// │   │   ├── DownloadAggregateButton (Button)
/// │   │   └── DownloadAllZipButton (Button)
/// │   │
/// │   └── ScenarioListSection
/// │       ├── ListHeader (TMP) - "Individual Scenarios"
/// │       └── ScrollView
/// │           └── Content (Vertical Layout Group)
/// │               └── [ScenarioRowPrefab instances]
/// │
/// └── Footer
///     └── CloseButton (Button)
/// 
/// SCENARIO ROW PREFAB:
/// ScenarioRow
/// ├── StatusIcon (TMP) - "✓" or "✗"
/// ├── SummaryText (TMP) - "Scenario 1: T1=2450, T2=34"
/// └── DownloadButton (Button)
/// </summary>
public class ResultsScreenUI : MonoBehaviour
{
    [Header("Panel Reference")]
    [SerializeField] private GameObject resultsPanel;
    
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
        
        // Hide download buttons during progress
        if (downloadConfigButton != null)
            downloadConfigButton.interactable = !showProgress;
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
    /// Called when a single scenario completes (for real-time list updates)
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
            _scenarioRows.Add(row);
            
            // Get components from prefab
            var rowController = row.GetComponent<ScenarioRowUI>();
            if (rowController != null)
            {
                rowController.Setup(scenario, OnDownloadScenarioClicked);
            }
            else
            {
                // Fallback: try to find components directly
                SetupRowManually(row, scenario);
            }
        }
    }
    
    /// <summary>
    /// Fallback setup if ScenarioRowUI component not found
    /// </summary>
    private void SetupRowManually(GameObject row, ScenarioResult scenario)
    {
        // Try to find child components by name
        var statusIcon = row.transform.Find("StatusIcon")?.GetComponent<TextMeshProUGUI>();
        var summaryText = row.transform.Find("SummaryText")?.GetComponent<TextMeshProUGUI>();
        var downloadBtn = row.transform.Find("DownloadButton")?.GetComponent<Button>();
        
        if (statusIcon != null)
            statusIcon.text = scenario.GetStatusIcon();
        
        if (summaryText != null)
            summaryText.text = scenario.GetSummaryLine();
        
        if (downloadBtn != null)
        {
            int index = scenario.ScenarioIndex; // Capture for closure
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
    
    private void OnDownloadConfigClicked()
    {
        if (_currentResults == null) return;
        
        string json = _currentResults.ToConfigJson();
        string filename = $"tinysea_config_{_currentResults.CompletedAt:yyyy-MM-dd_HH-mm-ss}.json";
        
        TriggerDownload(filename, json);
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
        if (scenario == null || string.IsNullOrEmpty(scenario.CsvData)) return;
        
        string filename = $"tinysea_scenario{scenarioIndex}_{_currentResults.CompletedAt:yyyy-MM-dd_HH-mm-ss}.csv";
        
        TriggerDownload(filename, scenario.CsvData);
    }
    
    private void OnDownloadAllZipClicked()
    {
        if (_currentResults == null) return;
        
        // For WebGL, we'll use JSZip via JavaScript interop
        // This requires a jslib file - see WebGLZipDownload.cs
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        StartCoroutine(DownloadAllAsZip());
        #else
        Debug.Log("ZIP download is only available in WebGL builds.");
        // In Editor, could save to disk instead
        #endif
    }
    
    /// <summary>
    /// Trigger a file download (WebGL or Editor)
    /// </summary>
    private void TriggerDownload(string filename, string content)
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        WebGLDownload.DownloadCsv(filename, content);
        #else
        // In Editor, log the content or save to file
        string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
        System.IO.File.WriteAllText(path, content);
        Debug.Log($"Saved to: {path}");
        #endif
    }
    
    /// <summary>
    /// Download all scenarios as a ZIP file (WebGL)
    /// </summary>
    private IEnumerator DownloadAllAsZip()
    {
        if (_currentResults == null) yield break;
        
        // Build list of files for ZIP
        var files = new List<(string name, string content)>();
        
        // Add aggregate CSV
        files.Add((
            $"aggregate.csv",
            _currentResults.ToAggregateCsv()
        ));
        
        // Add config JSON
        files.Add((
            $"config.json",
            _currentResults.ToConfigJson()
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
        
        // Call JavaScript ZIP function
        string zipFilename = $"tinysea_results_{_currentResults.CompletedAt:yyyy-MM-dd_HH-mm-ss}.zip";
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        WebGLZipDownload.DownloadAsZip(zipFilename, files);
        #endif
        
        yield return null;
    }
}
