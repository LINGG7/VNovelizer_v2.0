using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MusicAssetLoader
{
    private static readonly Dictionary<string, Sprite> generatedSpriteCache = new Dictionary<string, Sprite>();

    public static string GetPicturePath(VNMusic musicData)
    {
        if (musicData == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(musicData.picturePath) ? string.Empty : musicData.picturePath.Trim();
    }

    public static string GetMusicPath(VNMusic musicData)
    {
        if (musicData == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(musicData.musicPath) ? string.Empty : musicData.musicPath.Trim();
    }

    public static Sprite GetFallbackPicture(VNMusic musicData, Sprite defaultSprite)
    {
        if (musicData != null && musicData.picture != null)
        {
            return musicData.picture;
        }

        return defaultSprite;
    }

    public static AudioClip GetFallbackClip(VNMusic musicData)
    {
        return musicData != null ? musicData.music : null;
    }

    public static bool HasPlayableClip(VNMusic musicData)
    {
        return musicData != null && (musicData.music != null || !string.IsNullOrWhiteSpace(musicData.musicPath));
    }

    public static IEnumerator LoadPictureAsync(VNMusic musicData, Sprite defaultSprite, Action<Sprite> onLoaded)
    {
        Sprite fallbackSprite = GetFallbackPicture(musicData, defaultSprite);
        string picturePath = GetPicturePath(musicData);

        if (string.IsNullOrWhiteSpace(picturePath))
        {
            onLoaded?.Invoke(fallbackSprite);
            yield break;
        }

        bool spriteLoaded = false;
        Sprite loadedSprite = null;
        ResourcesManager.GetInstance().LoadOptionalAsync<Sprite>(picturePath, sprite =>
        {
            loadedSprite = sprite;
            spriteLoaded = true;
        });

        while (!spriteLoaded)
        {
            yield return null;
        }

        if (loadedSprite != null)
        {
            onLoaded?.Invoke(loadedSprite);
            yield break;
        }

        if (generatedSpriteCache.TryGetValue(picturePath, out Sprite cachedSprite) && cachedSprite != null)
        {
            onLoaded?.Invoke(cachedSprite);
            yield break;
        }

        bool textureLoaded = false;
        Texture2D loadedTexture = null;
        ResourcesManager.GetInstance().LoadOptionalAsync<Texture2D>(picturePath, texture =>
        {
            loadedTexture = texture;
            textureLoaded = true;
        });

        while (!textureLoaded)
        {
            yield return null;
        }

        if (loadedTexture == null)
        {
            onLoaded?.Invoke(fallbackSprite);
            yield break;
        }

        Sprite generatedSprite = Sprite.Create(
            loadedTexture,
            new Rect(0f, 0f, loadedTexture.width, loadedTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        generatedSprite.name = loadedTexture.name;
        generatedSpriteCache[picturePath] = generatedSprite;
        onLoaded?.Invoke(generatedSprite);
    }

    public static IEnumerator LoadAudioClipAsync(VNMusic musicData, Action<AudioClip> onLoaded)
    {
        AudioClip fallbackClip = GetFallbackClip(musicData);
        string musicPath = GetMusicPath(musicData);

        if (string.IsNullOrWhiteSpace(musicPath))
        {
            onLoaded?.Invoke(fallbackClip);
            yield break;
        }

        bool clipLoaded = false;
        AudioClip loadedClip = null;
        ResourcesManager.GetInstance().LoadOptionalAsync<AudioClip>(musicPath, clip =>
        {
            loadedClip = clip;
            clipLoaded = true;
        });

        while (!clipLoaded)
        {
            yield return null;
        }

        onLoaded?.Invoke(loadedClip != null ? loadedClip : fallbackClip);
    }
}
