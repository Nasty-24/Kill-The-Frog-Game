using System.IO;
using UnityEditor;

// Unity 6.6 starts asset import workers (including the Unity Search indexer's
// on-demand workers) with Direct3D 12 forced. This PC's Intel HD Graphics can't
// run D3D12 reliably, so those workers crash and the Editor hangs on
// "Hold on... Waiting for Unity's code to finish executing".
// Keep all importing inside the main Editor (which uses DX11) and turn off
// Unity Search indexing, which is what launches the on-demand workers.
[InitializeOnLoad]
static class NoImportWorkers
{
    const string SearchIndex = "UserSettings/Search.index";
    const string SearchSettings = "UserSettings/Search.settings";

    static NoImportWorkers()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess()) return;

        if (EditorUserSettings.desiredImportWorkerCount != 0) EditorUserSettings.desiredImportWorkerCount = 0;
        if (EditorUserSettings.standbyImportWorkerCount != 0) EditorUserSettings.standbyImportWorkerCount = 0;
        if (EditorSettings.refreshImportMode != AssetDatabase.RefreshImportMode.InProcess)
            EditorSettings.refreshImportMode = AssetDatabase.RefreshImportMode.InProcess;

        if (File.Exists(SearchIndex))
        {
            File.Delete(SearchIndex);
            UnityEngine.Debug.Log("[KillTheFrog] Removed " + SearchIndex + " (its indexer crashes on this GPU).");
        }
        if (File.Exists(SearchSettings))
        {
            string text = File.ReadAllText(SearchSettings);
            if (text.Contains("indexOnEditorStartup = true"))
                File.WriteAllText(SearchSettings, text.Replace("indexOnEditorStartup = true", "indexOnEditorStartup = false"));
        }
    }
}
