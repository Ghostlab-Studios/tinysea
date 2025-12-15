using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller for individual scenario row in the results list.
/// Attach this to your ScenarioRow prefab.
/// 
/// PREFAB STRUCTURE:
/// ScenarioRow (this script attached here)
/// ├── StatusIcon (Image or SVGImage - for checkmark/X)
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
    [Tooltip("Can be regular Image or SVGImage - both work via Graphic base class")]
    [SerializeField] private Graphic statusIcon;  // Graphic works for both Image and SVGImage
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private Button downloadButton;
    
    [Header("Status Colors")]
    [SerializeField] private Color survivedColor = new Color(0.298f, 0.686f, 0.314f); // #4CAF50 green
    [SerializeField] private Color crashedColor = new Color(0.957f, 0.263f, 0.212f);  // #F44336 red
    
    [Header("Optional: Status Sprites (for regular Image only)")]
    [Tooltip("If using regular Image and have separate sprites for checkmark and X")]
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
        
        // Set status icon color
        if (statusIcon != null)
        {
            statusIcon.color = scenario.Crashed ? crashedColor : survivedColor;
            
            // If using regular Image and have separate sprites, swap them
            var imageComponent = statusIcon as Image;
            if (imageComponent != null)
            {
                if (scenario.Crashed && crashedSprite != null)
                {
                    imageComponent.sprite = crashedSprite;
                }
                else if (!scenario.Crashed && survivedSprite != null)
                {
                    imageComponent.sprite = survivedSprite;
                }
            }
        }
        
        // Set summary text
        if (summaryText != null)
        {
            summaryText.text = scenario.GetSummaryLine();
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
            
            var imageComponent = statusIcon as Image;
            if (imageComponent != null)
            {
                if (crashed && crashedSprite != null)
                    imageComponent.sprite = crashedSprite;
                else if (!crashed && survivedSprite != null)
                    imageComponent.sprite = survivedSprite;
            }
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
