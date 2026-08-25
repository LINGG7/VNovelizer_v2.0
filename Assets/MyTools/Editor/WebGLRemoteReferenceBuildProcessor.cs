using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class WebGLRemoteReferenceBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string RemoteContentPrefix = "Assets/RemoteContent/";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
        {
            return;
        }

        int cleared = PrepareWebGLRemoteReferences(false);
        Debug.Log($"[WebGLRemoteReferenceBuildProcessor] Cleared {cleared} remote asset references before WebGL build.");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
        {
            return;
        }

        int restored = RestoreWebGLRemoteReferences(false);
        Debug.Log($"[WebGLRemoteReferenceBuildProcessor] Restored {restored} remote asset references after WebGL build.");
    }

    [MenuItem("VNovelizer/WebGL/Prepare Build - Clear Remote References")]
    public static void PrepareWebGLRemoteReferencesMenu()
    {
        int cleared = PrepareWebGLRemoteReferences(true);
        EditorUtility.DisplayDialog("WebGL Remote References", $"Cleared {cleared} remote references for WebGL build.", "OK");
    }

    [MenuItem("VNovelizer/WebGL/Restore Remote References From Keys")]
    public static void RestoreWebGLRemoteReferencesMenu()
    {
        int restored = RestoreWebGLRemoteReferences(true);
        EditorUtility.DisplayDialog("WebGL Remote References", $"Restored {restored} remote references from Remote Keys.", "OK");
    }

    private static int PrepareWebGLRemoteReferences(bool log)
    {
        int changed = 0;
        changed += PrepareCharacterProfiles();
        changed += PrepareMusicData();
        changed += PrepareSceneData();
        changed += PrepareEndingData();

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
        }

        if (log)
        {
            Debug.Log($"[WebGLRemoteReferenceBuildProcessor] Cleared {changed} remote references.");
        }

        return changed;
    }

    private static int RestoreWebGLRemoteReferences(bool log)
    {
        int changed = 0;
        changed += RestoreCharacterProfiles();
        changed += RestoreMusicData();
        changed += RestoreSceneData();
        changed += RestoreEndingData();

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
        }

        if (log)
        {
            Debug.Log($"[WebGLRemoteReferenceBuildProcessor] Restored {changed} remote references.");
        }

        return changed;
    }

    private static int PrepareCharacterProfiles()
    {
        int changed = 0;
        foreach (CharacterProfile profile in FindAssets<CharacterProfile>("Assets/Resources"))
        {
            if (profile == null)
            {
                continue;
            }

            bool dirty = false;
            dirty |= PrepareElementSprites(profile.ElementSprites, ref changed);
            dirty |= PrepareElementSprites(profile.HeadSprites, ref changed);

            if (dirty)
            {
                EditorUtility.SetDirty(profile);
            }
        }

        return changed;
    }

    private static bool PrepareElementSprites(List<ElementSprite> entries, ref int changed)
    {
        if (entries == null)
        {
            return false;
        }

        bool dirty = false;
        foreach (ElementSprite entry in entries)
        {
            if (entry == null || entry.Sprite == null)
            {
                continue;
            }

            string remoteKey = BuildRemoteKey(entry.Sprite);
            if (string.IsNullOrEmpty(remoteKey))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.SpritePath))
            {
                entry.SpritePath = remoteKey;
            }

            entry.Sprite = null;
            changed++;
            dirty = true;
        }

        return dirty;
    }

    private static int RestoreCharacterProfiles()
    {
        int changed = 0;
        foreach (CharacterProfile profile in FindAssets<CharacterProfile>("Assets/Resources"))
        {
            if (profile == null)
            {
                continue;
            }

            bool dirty = false;
            dirty |= RestoreElementSprites(profile.ElementSprites, ref changed);
            dirty |= RestoreElementSprites(profile.HeadSprites, ref changed);

            if (dirty)
            {
                EditorUtility.SetDirty(profile);
            }
        }

        return changed;
    }

    private static bool RestoreElementSprites(List<ElementSprite> entries, ref int changed)
    {
        if (entries == null)
        {
            return false;
        }

        bool dirty = false;
        foreach (ElementSprite entry in entries)
        {
            if (entry == null || entry.Sprite != null || string.IsNullOrWhiteSpace(entry.SpritePath))
            {
                continue;
            }

            Sprite sprite = LoadRemoteAsset<Sprite>(entry.SpritePath);
            if (sprite == null)
            {
                continue;
            }

            entry.Sprite = sprite;
            changed++;
            dirty = true;
        }

        return dirty;
    }

    private static int PrepareMusicData()
    {
        int changed = 0;
        foreach (MusicDataContainer container in FindAssets<MusicDataContainer>("Assets/Resources"))
        {
            if (container == null || container.musicList == null)
            {
                continue;
            }

            bool dirty = false;
            foreach (VNMusic music in container.musicList)
            {
                if (music == null)
                {
                    continue;
                }

                dirty |= PrepareRemoteSprite(ref music.picture, ref music.picturePath, ref changed);
                dirty |= PrepareRemoteAudio(ref music.music, ref music.musicPath, ref changed);
            }

            if (dirty)
            {
                EditorUtility.SetDirty(container);
            }
        }

        return changed;
    }

    private static int RestoreMusicData()
    {
        int changed = 0;
        foreach (MusicDataContainer container in FindAssets<MusicDataContainer>("Assets/Resources"))
        {
            if (container == null || container.musicList == null)
            {
                continue;
            }

            bool dirty = false;
            foreach (VNMusic music in container.musicList)
            {
                if (music == null)
                {
                    continue;
                }

                dirty |= RestoreRemoteSprite(ref music.picture, music.picturePath, ref changed);
                dirty |= RestoreRemoteAudio(ref music.music, music.musicPath, ref changed);
            }

            if (dirty)
            {
                EditorUtility.SetDirty(container);
            }
        }

        return changed;
    }

    private static int PrepareSceneData()
    {
        int changed = 0;
        foreach (SceneDataContainer container in FindAssets<SceneDataContainer>("Assets/Resources"))
        {
            if (container == null || container.sceneList == null)
            {
                continue;
            }

            bool dirty = false;
            foreach (VNScene scene in container.sceneList)
            {
                if (scene == null)
                {
                    continue;
                }

                dirty |= PrepareRemoteSprite(ref scene.LockedSprite, ref scene.LockedSpritePath, ref changed);
                dirty |= PrepareRemoteSprite(ref scene.UnLockedSprite, ref scene.UnLockedSpritePath, ref changed);
            }

            if (dirty)
            {
                EditorUtility.SetDirty(container);
            }
        }

        return changed;
    }

    private static int RestoreSceneData()
    {
        int changed = 0;
        foreach (SceneDataContainer container in FindAssets<SceneDataContainer>("Assets/Resources"))
        {
            if (container == null || container.sceneList == null)
            {
                continue;
            }

            bool dirty = false;
            foreach (VNScene scene in container.sceneList)
            {
                if (scene == null)
                {
                    continue;
                }

                dirty |= RestoreRemoteSprite(ref scene.LockedSprite, scene.LockedSpritePath, ref changed);
                dirty |= RestoreRemoteSprite(ref scene.UnLockedSprite, scene.UnLockedSpritePath, ref changed);
            }

            if (dirty)
            {
                EditorUtility.SetDirty(container);
            }
        }

        return changed;
    }

    private static int PrepareEndingData()
    {
        int changed = 0;
        foreach (EndingDataContainer container in FindAssets<EndingDataContainer>("Assets/Resources"))
        {
            if (container == null || container.endingList == null)
            {
                continue;
            }

            bool dirty = false;
            foreach (VNEnding ending in container.endingList)
            {
                if (ending == null)
                {
                    continue;
                }

                dirty |= PrepareRemoteSprite(ref ending.LockedSprite, ref ending.LockedSpritePath, ref changed);
                dirty |= PrepareRemoteSprite(ref ending.UnLockedSprite, ref ending.UnLockedSpritePath, ref changed);
            }

            if (dirty)
            {
                EditorUtility.SetDirty(container);
            }
        }

        return changed;
    }

    private static int RestoreEndingData()
    {
        int changed = 0;
        foreach (EndingDataContainer container in FindAssets<EndingDataContainer>("Assets/Resources"))
        {
            if (container == null || container.endingList == null)
            {
                continue;
            }

            bool dirty = false;
            foreach (VNEnding ending in container.endingList)
            {
                if (ending == null)
                {
                    continue;
                }

                dirty |= RestoreRemoteSprite(ref ending.LockedSprite, ending.LockedSpritePath, ref changed);
                dirty |= RestoreRemoteSprite(ref ending.UnLockedSprite, ending.UnLockedSpritePath, ref changed);
            }

            if (dirty)
            {
                EditorUtility.SetDirty(container);
            }
        }

        return changed;
    }

    private static bool PrepareRemoteSprite(ref Sprite sprite, ref string remoteKey, ref int changed)
    {
        if (sprite == null)
        {
            return false;
        }

        string key = BuildRemoteKey(sprite);
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(remoteKey))
        {
            remoteKey = key;
        }

        sprite = null;
        changed++;
        return true;
    }

    private static bool PrepareRemoteAudio(ref AudioClip clip, ref string remoteKey, ref int changed)
    {
        if (clip == null)
        {
            return false;
        }

        string key = BuildRemoteKey(clip);
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(remoteKey))
        {
            remoteKey = key;
        }

        clip = null;
        changed++;
        return true;
    }

    private static bool RestoreRemoteSprite(ref Sprite sprite, string remoteKey, ref int changed)
    {
        if (sprite != null || string.IsNullOrWhiteSpace(remoteKey))
        {
            return false;
        }

        Sprite restored = LoadRemoteAsset<Sprite>(remoteKey);
        if (restored == null)
        {
            return false;
        }

        sprite = restored;
        changed++;
        return true;
    }

    private static bool RestoreRemoteAudio(ref AudioClip clip, string remoteKey, ref int changed)
    {
        if (clip != null || string.IsNullOrWhiteSpace(remoteKey))
        {
            return false;
        }

        AudioClip restored = LoadRemoteAsset<AudioClip>(remoteKey);
        if (restored == null)
        {
            return false;
        }

        clip = restored;
        changed++;
        return true;
    }

    private static string BuildRemoteKey(Object asset)
    {
        if (asset == null)
        {
            return string.Empty;
        }

        string assetPath = AssetDatabase.GetAssetPath(asset).Replace("\\", "/");
        if (!assetPath.StartsWith(RemoteContentPrefix))
        {
            return string.Empty;
        }

        return StripExtension(assetPath.Substring(RemoteContentPrefix.Length));
    }

    private static T LoadRemoteAsset<T>(string remoteKey) where T : Object
    {
        string assetPath = RemoteContentPrefix + remoteKey.Trim().Replace("\\", "/");
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset != null)
        {
            return asset;
        }

        string[] guids = AssetDatabase.FindAssets(Path.GetFileName(assetPath), new[] { RemoteContentPrefix.TrimEnd('/') });
        foreach (string guid in guids)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(guid).Replace("\\", "/");
            if (StripExtension(candidatePath) != assetPath)
            {
                continue;
            }

            asset = AssetDatabase.LoadAssetAtPath<T>(candidatePath);
            if (asset != null)
            {
                return asset;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindAssets<T>(string folder) where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
        foreach (string guid in guids)
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
            {
                yield return asset;
            }
        }
    }

    private static string StripExtension(string path)
    {
        int slashIndex = path.LastIndexOf('/');
        int dotIndex = path.LastIndexOf('.');
        if (dotIndex > slashIndex)
        {
            return path.Substring(0, dotIndex);
        }

        return path;
    }
}
