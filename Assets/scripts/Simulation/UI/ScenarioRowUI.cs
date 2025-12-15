using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VectorGraphics;

/// <summary>
/// Controller for individual scenario row in the results list.
/// Attach this to your ScenarioRow prefab.
/// 
/// PREFAB STRUCTURE:
/// ScenarioRow (this script attached here)
/// ├── StatusIcon (Image - for your SVG checkmark/X)
/// ├── SummaryText (TMP - "Scenario 1: T1=2,450, T2=34")
/// └── DownloadButton (Button)
/// 
/// The script will:
/// - Set the status icon color (green for survived, red for crashed)
/// - Set the summary text
/// - Wire up the download button
/// </summary>
public class ScenarioRowUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private SVGImage statusIcon;
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private Button downloadButton;
    
    [Header("Status Colors")]
    [SerializeField] private Color survivedColor = new Color(0.298f, 0.686f, 0.314f); // #4CAF50 green
    [SerializeField] private Color crashedColor = new Color(0.957f, 0.263f, 0.212f);  // #F44336 red
    
    [Header("Optional: Status Sprites")]
    [Tooltip("If you have separate SVG sprites for checkmark and X, assign them here")]
    [SerializeField] private Sprite survivedSprite;
    [SerializeField] private Sprite crashedSprite;
    
    // Internal state
    private int _scenarioIndex;
    private System.Action<int> _onDownloadClicked;
    
    /// <summary>
    /// Initialize the row with scenario data
    /// </summary>
    /// <param name="scenario">The scenario result data</param>
    /// <param name="onDownloadClicked">Callback when download is clicked (passes scenario index)</param>
    public void Setup(ScenarioResult scenario, System.Action<int> onDownloadClicked)
    {
        _scenarioIndex = scenario.ScenarioIndex;
        _onDownloadClicked = onDownloadClicked;
        
        // Set status icon
        if (statusIcon != null)
        {
            // Set color based on outcome
            statusIcon.color = scenario.Crashed ? crashedColor : survivedColor;
            
            // If you have separate sprites, swap them
            if (scenario.Crashed && crashedSprite != null)
            {
                statusIcon.sprite = crashedSprite;
            }
            else if (!scenario.Crashed && survivedSprite != null)
            {
                statusIcon.sprite = survivedSprite;
            }
        }
        
        // Set summary text
        if (summaryText != null)
        {
            summaryText.text = scenario.GetSummaryLine();
            
            // Optionally color the text too
            // summaryText.color = scenario.Crashed ? crashedColor : Color.black;
        }
        
        // Wire up download button
        if (downloadButton != null)
        {
            downloadButton.onClick.RemoveAllListeners();
            downloadButton.onClick.AddListener(OnDownloadClicked);
        }
    }
    
    /// <summary>
    /// Alternative setup with manual values (if you don't have ScenarioResult)
    /// </summary>
    public void Setup(int scenarioIndex, bool crashed, string summaryLine, System.Action<int> onDownloadClicked)
    {
        _scenarioIndex = scenarioIndex;
        _onDownloadClicked = onDownloadClicked;
        
        if (statusIcon != null)
        {
            statusIcon.color = crashed ? crashedColor : survivedColor;
            
            if (crashed && crashedSprite != null)
                statusIcon.sprite = crashedSprite;
            else if (!crashed && survivedSprite != null)
                statusIcon.sprite = survivedSprite;
        }
        
        if (summaryText != null)
        {
            summaryText.text = summaryLine;
        }
        
        if (downloadButton != null)
        {
            downloadButton.onClick.RemoveAllListeners();
            downloadButton.onClick.AddListener(OnDownloadClicked);
        }
    }
    
    private void OnDownloadClicked()
    {
        _onDownloadClicked?.Invoke(_scenarioIndex);
    }
    
    /// <summary>
    /// Get the scenario index this row represents
    /// </summary>
    public int GetScenarioIndex()
    {
        return _scenarioIndex;
    }
}
