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
    private const string CdnRoot = "https://res.huizi888.com/vnovelizer/prod/";
    private const string MiniGameConfigPath = "Assets/TapTapMiniGame/Editor/MiniGameConfig.asset";

    public int callbackOrder => -1000;

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

        settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", CdnRoot + version + "/[BuildTarget]");
        settings.BuildRemoteCatalog = true;
        settings.CatalogRequestsTimeout = 15;
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
}
