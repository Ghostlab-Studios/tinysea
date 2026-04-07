using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class CsvUploadHandler : MonoBehaviour
{
    [Header("Upload Overlay UI")]
    [SerializeField] private GameObject uploadOverlayPanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button goBackButton;
    [SerializeField] private Button runSimulationButton;

    [DllImport("__Internal")]
    private static extern void TinySea_InitDragDrop();

    private string receivedCsvContent;
    public string ReceivedCsvContent => receivedCsvContent;

    private List<BulkBatchConfig> parsedBatches;

    private Coroutine hideCoroutine;

    // Event fires with parsed batch configs (consumed by BulkSimulationController)
    public System.Action<List<BulkBatchConfig>> OnRunBulkSimulation;

    void Start()
    {
        // Drag-drop is now handled by the website's index.php page-level JavaScript.
        // The page JS calls unityInstance.SendMessage() directly on drop.
        // No need for TinySea_InitDragDrop() — it registered duplicate listeners
        // that produced "object not found" errors.

        SetIdleState();
    }

    void OnEnable()
    {
        if (goBackButton != null)
        {
            goBackButton.onClick.RemoveListener(OnGoBackClicked);
            goBackButton.onClick.AddListener(OnGoBackClicked);
        }

        if (runSimulationButton != null)
        {
            runSimulationButton.onClick.RemoveListener(OnRunSimulationClicked);
            runSimulationButton.onClick.AddListener(OnRunSimulationClicked);
        }
    }

    void OnDisable()
    {
        if (goBackButton != null)
            goBackButton.onClick.RemoveListener(OnGoBackClicked);

        if (runSimulationButton != null)
            runSimulationButton.onClick.RemoveListener(OnRunSimulationClicked);
    }

    private void SetIdleState()
    {
        CancelHideCoroutine();
        uploadOverlayPanel.SetActive(false);
        goBackButton.gameObject.SetActive(false);
        runSimulationButton.gameObject.SetActive(false);
        receivedCsvContent = null;
        parsedBatches = null;
    }

    /// <summary>
    /// Public reset — hides overlay, clears state. Used by BulkSimulationController on close.
    /// </summary>
    public void ResetToIdle()
    {
        SetIdleState();
    }

    // Called from JS
    public void OnCsvDragOver()
    {
        CancelHideCoroutine();
        uploadOverlayPanel.SetActive(true);
        statusText.text = "Drop CSV file here...";
        goBackButton.gameObject.SetActive(false);
        runSimulationButton.gameObject.SetActive(false);
    }

    // Called from JS
    public void OnCsvDragLeave()
    {
        if (string.IsNullOrEmpty(receivedCsvContent))
            hideCoroutine = StartCoroutine(HideOverlayAfterDelay(3f));
    }

    // Called from JS on valid CSV drop
    public void OnCsvFileReceived(string csvContent)
    {
        CancelHideCoroutine();
        receivedCsvContent = csvContent;
        uploadOverlayPanel.SetActive(true);
        statusText.text = "CSV loaded. Validating...";
        goBackButton.gameObject.SetActive(false);
        runSimulationButton.gameObject.SetActive(false);

        ValidateCsv(csvContent);
    }

    // Called from JS on wrong file type
    public void OnCsvUploadError(string errorMessage)
    {
        CancelHideCoroutine();
        uploadOverlayPanel.SetActive(true);
        statusText.text = $"Error: {errorMessage}";
        goBackButton.gameObject.SetActive(true);
        runSimulationButton.gameObject.SetActive(false);
    }

    private void CancelHideCoroutine()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    private System.Collections.IEnumerator HideOverlayAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        uploadOverlayPanel.SetActive(false);
        hideCoroutine = null;
    }

    private void ValidateCsv(string csvContent)
    {
        List<BulkBatchConfig> batches;
        List<string> errors;

        if (CsvBatchParser.TryParse(csvContent, out batches, out errors))
        {
            parsedBatches = batches;
            ShowSuccess(batches.Count);
        }
        else
        {
            parsedBatches = null;
            // Show first 10 errors, indicate if there are more
            int showCount = System.Math.Min(errors.Count, 10);
            string errorMsg = string.Join("\n", errors.GetRange(0, showCount));
            if (errors.Count > showCount)
                errorMsg += $"\n\n... and {errors.Count - showCount} more errors.";
            ShowError(errorMsg);
        }
    }

    private void ShowError(string message)
    {
        statusText.text = $"CSV Validation Failed:\n\n{message}";
        goBackButton.gameObject.SetActive(true);
        runSimulationButton.gameObject.SetActive(false);
    }

    private void ShowSuccess(int batchCount)
    {
        string batchWord = batchCount == 1 ? "batch" : "batches";
        statusText.text = $"CSV is valid. Ready to run {batchCount} {batchWord}.";
        goBackButton.gameObject.SetActive(true);
        runSimulationButton.gameObject.SetActive(true);
    }

    private void OnGoBackClicked()
    {
        SetIdleState();
    }

    private void OnRunSimulationClicked()
    {
        if (parsedBatches == null || parsedBatches.Count == 0) return;

        // Progress shown in UI

        // Hide buttons, keep overlay visible for BulkSimulationController progress
        goBackButton.gameObject.SetActive(false);
        runSimulationButton.gameObject.SetActive(false);
        statusText.text = "Starting simulation...";

        OnRunBulkSimulation?.Invoke(parsedBatches);
    }

#if UNITY_EDITOR
    void Update()
    {
        // Press L to load a test CSV in Editor
        if (Input.GetKeyDown(KeyCode.L))
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("Load CSV", "", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                string content = System.IO.File.ReadAllText(path);
                OnCsvFileReceived(content);
            }
        }
    }

    void OnGUI()
    {
        Event e = Event.current;

        if (e.type == EventType.DragUpdated)
        {
            if (UnityEditor.DragAndDrop.paths.Length > 0 &&
                UnityEditor.DragAndDrop.paths[0].EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
            {
                UnityEditor.DragAndDrop.visualMode = UnityEditor.DragAndDropVisualMode.Copy;
                e.Use();
                OnCsvDragOver();
            }
        }
        else if (e.type == EventType.DragPerform)
        {
            UnityEditor.DragAndDrop.AcceptDrag();
            e.Use();

            if (UnityEditor.DragAndDrop.paths.Length > 0)
            {
                string path = UnityEditor.DragAndDrop.paths[0];
                if (path.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
                {
                    string content = System.IO.File.ReadAllText(path);
                    OnCsvFileReceived(content);
                }
                else
                {
                    OnCsvUploadError("Only CSV files are accepted. Please drop a .csv file.");
                }
            }
        }
        else if (e.type == EventType.DragExited)
        {
            OnCsvDragLeave();
        }
    }
#endif
}
