using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

[InitializeOnLoad]
public static class VNovelizerAddressablesUtility
{
    private const string ResourcesRoot = "Assets/Resources";
    private const string VNovelizerResourcesRoot = "Assets/Resources/VNovelizerRes";
    private const string ProjectConfigPath = "Assets/Resources/VNProjectConfig.asset";
    private static bool isSyncing;

    static VNovelizerAddressablesUtility()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("VNovelizer/Addressables/Sync Resources To Addressables", false, 80)]
    public static void SyncResourcesToAddressables()
    {
        SyncResourcesToAddressables(true);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            SyncResourcesToAddressables(false);
        }
    }

    private static void SyncResourcesToAddressables(bool refreshAssetDatabase)
    {
        if (isSyncing)
        {
            return;
        }

        isSyncing = true;
        try
        {
            if (refreshAssetDatabase)
            {
                AssetDatabase.Refresh();
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[VNovelizer Addressables] AddressableAssetSettings not found. Create Addressables settings first.");
                return;
            }

            AddressableAssetGroup group = settings.DefaultGroup;
            if (group == null)
            {
                Debug.LogError("[VNovelizer Addressables] Default Addressables group not found.");
                return;
            }

            int syncedCount = 0;
            syncedCount += SyncAsset(ProjectConfigPath, settings, group) ? 1 : 0;
            syncedCount += SyncFolder(VNovelizerResourcesRoot, settings, group);

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VNovelizer Addressables] Synced {syncedCount} asset(s) to Addressables.");
        }
        finally
        {
            isSyncing = false;
        }
    }

    private static int SyncFolder(string folderPath, AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            return 0;
        }

        int count = 0;
        string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                continue;
            }

            if (SyncAsset(assetPath, settings, group))
            {
                count++;
            }
        }

        return count;
    }

    private static bool SyncAsset(string assetPath, AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(ResourcesRoot) || AssetDatabase.IsValidFolder(assetPath))
        {
            return false;
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            return false;
        }

        string address = GetResourcesStyleAddress(assetPath);
        RemoveDuplicateAddressEntries(settings, address, guid);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
        entry.address = address;
        AddFolderLabels(entry, settings, address);
        return true;
    }

    private static void RemoveDuplicateAddressEntries(AddressableAssetSettings settings, string address, string keepGuid)
    {
        List<string> duplicateGuids = new List<string>();

        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null)
            {
                continue;
            }

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry != null && entry.address == address && entry.guid != keepGuid)
                {
                    duplicateGuids.Add(entry.guid);
                }
            }
        }

        foreach (string duplicateGuid in duplicateGuids)
        {
            settings.RemoveAssetEntry(duplicateGuid, false);
        }
    }

    private static string GetResourcesStyleAddress(string assetPath)
    {
        string address = assetPath.Substring(ResourcesRoot.Length + 1);
        address = Path.ChangeExtension(address, null);
        return address.Replace("\\", "/");
    }

    private static void AddFolderLabels(AddressableAssetEntry entry, AddressableAssetSettings settings, string address)
    {
        string label = Path.GetDirectoryName(address)?.Replace("\\", "/");

        while (!string.IsNullOrEmpty(label))
        {
            settings.AddLabel(label, false);
            entry.SetLabel(label, true, true);

            int slashIndex = label.LastIndexOf('/');
            label = slashIndex > 0 ? label.Substring(0, slashIndex) : null;
        }
    }
}
