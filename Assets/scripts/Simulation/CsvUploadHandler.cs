using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

    private Coroutine hideCoroutine;

    // Events for other scripts
    public System.Action<string> OnRunBulkSimulation;

    void Start()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        TinySea_InitDragDrop();
        #endif

        SetIdleState();

        goBackButton.onClick.AddListener(OnGoBackClicked);
        runSimulationButton.onClick.AddListener(OnRunSimulationClicked);
    }

    private void SetIdleState()
    {
        CancelHideCoroutine();
        uploadOverlayPanel.SetActive(false);
        goBackButton.gameObject.SetActive(false);
        runSimulationButton.gameObject.SetActive(false);
        receivedCsvContent = null;
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
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            ShowError("CSV file is empty.");
            return;
        }

        string[] lines = csvContent.Split(new[] { '\n', '\r' },
            System.StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            ShowError("CSV must have a header row and at least one data row.");
            return;
        }

        string header = lines[0];
        if (!header.Contains(","))
        {
            ShowError("First row does not appear to be a valid CSV header (no commas found).");
            return;
        }

        int dataRows = lines.Length - 1;
        ShowSuccess(dataRows);
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
        Debug.Log("Starting bulk simulation...");
        OnRunBulkSimulation?.Invoke(receivedCsvContent);
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
#endif
}
