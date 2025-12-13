using System;

/// <summary>
/// Static event system for species editing.
/// Uses observer pattern to decouple SpeciesUIController from EditSpeciesUI.
/// 
/// Usage:
/// - SpeciesUIController calls SpeciesEditEvents.RequestEdit(index) when edit button clicked
/// - EditSpeciesUI subscribes to OnEditRequested and opens with the received index
/// </summary>
public static class SpeciesEditEvents
{
    /// <summary>
    /// Event fired when a species edit is requested.
    /// Parameter is the index of the species in the RunSpeciesList.
    /// </summary>
    public static event Action<int> OnEditRequested;

    /// <summary>
    /// Event fired when the edit panel is closed.
    /// Can be used to refresh the species list UI if needed.
    /// </summary>
    public static event Action OnEditClosed;

    /// <summary>
    /// Event fired when species data is saved.
    /// Parameter is the index of the saved species.
    /// </summary>
    public static event Action<int> OnSpeciesSaved;

    /// <summary>
    /// Event fired when a species is deleted.
    /// Parameter is the index of the deleted species.
    /// </summary>
    public static event Action<int> OnSpeciesDeleted;

    /// <summary>
    /// Request to open the edit panel for a specific species.
    /// Called by SpeciesUIController when edit button is clicked.
    /// </summary>
    /// <param name="speciesIndex">Index in the RunSpeciesList</param>
    public static void RequestEdit(int speciesIndex)
    {
        OnEditRequested?.Invoke(speciesIndex);
    }

    /// <summary>
    /// Notify that the edit panel has been closed.
    /// Called by EditSpeciesUI when closing.
    /// </summary>
    public static void NotifyEditClosed()
    {
        OnEditClosed?.Invoke();
    }

    /// <summary>
    /// Notify that species data has been saved.
    /// Called by EditSpeciesUI after saving.
    /// </summary>
    /// <param name="speciesIndex">Index of the saved species</param>
    public static void NotifySpeciesSaved(int speciesIndex)
    {
        OnSpeciesSaved?.Invoke(speciesIndex);
    }

    /// <summary>
    /// Notify that a species has been deleted.
    /// Called by EditSpeciesUI after deletion.
    /// </summary>
    /// <param name="speciesIndex">Index of the deleted species</param>
    public static void NotifySpeciesDeleted(int speciesIndex)
    {
        OnSpeciesDeleted?.Invoke(speciesIndex);
    }
}