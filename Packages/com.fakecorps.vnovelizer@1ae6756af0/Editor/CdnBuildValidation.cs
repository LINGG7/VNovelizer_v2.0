using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps the player, Addressables and TapTap content versions on one release line.
/// Run the menu item before building a release; the build callback refuses drift.
/// </summary>
public sealed class CdnBuildValidation : IPreprocessBuildWithReport
{
    private const string CdnRoot = "https://res.huizhihudongtech.com/vn/sntzm2/";
    private const string MiniGameConfigPath = "Assets/TapTapMiniGame/Editor/MiniGameConfig.asset";

    public int callbackOrder => -1000;

    [MenuItem("VNovelizer/Addressables/Use Local Build")]
    public static void UseLocalBuild()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("Addressables settings were not found.");
        settings.profileSettings.SetValue(settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kLocalBuildPathValue);
        settings.profileSettings.SetValue(settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath, AddressableAssetSettings.kLocalLoadPathValue);
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null || !schema.IncludeInBuild) continue;
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            // Keep generated archive filenames ASCII even when labels contain Chinese or slashes.
            schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.OnlyHash;
            EditorUtility.SetDirty(schema);
        }
        settings.BuildRemoteCatalog = false;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        ValidateOrThrow();
        Debug.Log("[CdnBuildValidation] Local Addressables build configured and validated. Rebuild Addressables content before building the player.");
    }

    [MenuItem("VNovelizer/Addressables/Sync CDN Version")]
    public static void SyncCdnVersion()
    {
        string version = PlayerSettings.bundleVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("PlayerSettings.bundleVersion cannot be empty.");
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            throw new InvalidOperationException("Addressables settings were not found.");
        }

        settings.profileSettings.SetValue(settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kRemoteBuildPathValue);
        settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", CdnRoot + version + "/[BuildTarget]");
        settings.BuildRemoteCatalog = true;
        settings.CatalogRequestsTimeout = 15;
        settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
        settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) continue;
            bool remote = group.Name.Contains("Remote", StringComparison.OrdinalIgnoreCase);
            schema.BuildPath.SetVariableByName(settings, remote ? AddressableAssetSettings.kRemoteBuildPath : AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, remote ? AddressableAssetSettings.kRemoteLoadPath : AddressableAssetSettings.kLocalLoadPath);
            if (remote && schema.Timeout <= 0) schema.Timeout = 15;
            // Keep generated archive filenames ASCII even when labels contain Chinese or slashes.
            schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.OnlyHash;
            EditorUtility.SetDirty(schema);
        }
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        if (File.Exists(MiniGameConfigPath))
        {
            string text = File.ReadAllText(MiniGameConfigPath);
            string marker = "CDN: ";
            int markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                int valueStart = markerIndex + marker.Length;
                int valueEnd = text.IndexOf('\n', valueStart);
                if (valueEnd < 0)
                {
                    valueEnd = text.Length;
                }

                string replacement = CdnRoot + version + "/WebGL/";
                text = text.Substring(0, valueStart) + replacement + text.Substring(valueEnd);
                File.WriteAllText(MiniGameConfigPath, text);
                AssetDatabase.ImportAsset(MiniGameConfigPath);
            }
        }

        Debug.Log($"[CdnBuildValidation] CDN version synchronized to {version}.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        ValidateOrThrow();
    }

    public static void ValidateOrThrow()
    {
        ValidateSettings(
            AddressableAssetSettingsDefaultObject.Settings,
            PlayerSettings.bundleVersion,
            File.Exists(MiniGameConfigPath) ? File.ReadAllText(MiniGameConfigPath) : null);
    }

    private static void ValidateSettings(AddressableAssetSettings settings, string version, string miniGameConfigText)
    {
        if (settings == null)
        {
            throw new BuildFailedException("Addressables settings were not found.");
        }

        string remoteLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.LoadPath");
        string remoteBuildPath = settings.profileSettings.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath);
        string builtInBuildPath = settings.profileSettings.EvaluateString(settings.activeProfileId, AddressableAssetSettings.kLocalBuildPathValue);
        string builtInLoadPath = settings.profileSettings.EvaluateString(settings.activeProfileId, AddressableAssetSettings.kLocalLoadPathValue);
        string evaluatedBuildPath = settings.profileSettings.EvaluateString(settings.activeProfileId, remoteBuildPath ?? "");
        string evaluatedLoadPath = settings.profileSettings.EvaluateString(settings.activeProfileId, remoteLoadPath ?? "");
        bool localBuild = string.Equals(evaluatedBuildPath, builtInBuildPath, StringComparison.Ordinal);
        bool localLoad = string.Equals(evaluatedLoadPath, builtInLoadPath, StringComparison.Ordinal);
        if (localBuild || localLoad)
        {
            if (!localBuild || !localLoad)
                throw new BuildFailedException("Local mode requires BOTH Remote.BuildPath and Remote.LoadPath to use Built-In paths. Select Built-In for Remote in Addressables Profiles.");
            ValidateLocalSettings(settings, builtInBuildPath, builtInLoadPath);
            return;
        }

        string expectedPath = CdnRoot + version + "/";
        if (string.IsNullOrEmpty(remoteLoadPath) || !remoteLoadPath.StartsWith(expectedPath, StringComparison.Ordinal))
        {
            throw new BuildFailedException(
                $"Remote.LoadPath must start with {expectedPath}; found {remoteLoadPath}. " +
                "Run VNovelizer/Addressables/Sync CDN Version.");
        }

        if (!settings.BuildRemoteCatalog || settings.CatalogRequestsTimeout <= 0)
        {
            throw new BuildFailedException("Remote Catalog must be enabled with a finite timeout.");
        }

        string expectedRemoteBuildPath = settings.profileSettings.GetValueByName(
            settings.activeProfileId,
            AddressableAssetSettings.kRemoteBuildPath);
        string expectedRemoteLoadPath = settings.profileSettings.GetValueByName(
            settings.activeProfileId,
            AddressableAssetSettings.kRemoteLoadPath);

        expectedRemoteBuildPath = settings.profileSettings.EvaluateString(settings.activeProfileId, expectedRemoteBuildPath);
        expectedRemoteLoadPath = settings.profileSettings.EvaluateString(settings.activeProfileId, expectedRemoteLoadPath);

        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null || !group.Name.Contains("Remote", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                continue;
            }

            string buildPath = schema.BuildPath.GetValue(settings);
            string loadPath = schema.LoadPath.GetValue(settings);
            if (!string.Equals(buildPath, expectedRemoteBuildPath, StringComparison.Ordinal)
                || !string.Equals(loadPath, expectedRemoteLoadPath, StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    $"Remote group {group.Name} has mismatched Addressables paths. " +
                    $"Build expected '{expectedRemoteBuildPath}', actual '{buildPath}'; " +
                    $"Load expected '{expectedRemoteLoadPath}', actual '{loadPath}'.");
            }

            if (schema.Timeout <= 0)
            {
                throw new BuildFailedException($"Remote group {group.Name} has no request timeout.");
            }
        }

        if (miniGameConfigText != null)
        {
            if (!miniGameConfigText.Contains(expectedPath + "WebGL/", StringComparison.Ordinal))
            {
                throw new BuildFailedException("TapTap MiniGameConfig CDN version does not match PlayerSettings.bundleVersion.");
            }
        }
    }
    // Built-In is a delivery mode for Addressables, not a switch to Resources.Load.
    private static void ValidateLocalSettings(AddressableAssetSettings settings, string buildPath, string loadPath)
    {
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null || !schema.IncludeInBuild) continue;
            if (!string.Equals(schema.BuildPath.GetValue(settings), buildPath, StringComparison.Ordinal)
                || !string.Equals(schema.LoadPath.GetValue(settings), loadPath, StringComparison.Ordinal))
                throw new BuildFailedException($"Local mode requires included group '{group.Name}' to use Built-In build and load paths.");
        }

        // A catalog using the built-in paths is safe; an external catalog can reintroduce remote bundles.
        if (settings.BuildRemoteCatalog
            && (!string.Equals(settings.RemoteCatalogBuildPath.GetValue(settings), buildPath, StringComparison.Ordinal)
                || !string.Equals(settings.RemoteCatalogLoadPath.GetValue(settings), loadPath, StringComparison.Ordinal)))
            throw new BuildFailedException("Local mode requires the catalog to use Built-In paths, or Build Remote Catalog to be disabled.");

        Debug.Log("[CdnBuildValidation] Built-In Addressables mode validated; CDN version and network timeout checks do not apply.");
    }

}
