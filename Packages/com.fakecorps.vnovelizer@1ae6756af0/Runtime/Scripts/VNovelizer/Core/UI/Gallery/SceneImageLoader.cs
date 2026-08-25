using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SceneImageLoader
{
    private static readonly Dictionary<string, Sprite> generatedSpriteCache = new Dictionary<string, Sprite>();

    public static string GetSpritePath(VNScene sceneData, bool isUnlocked)
    {
        if (sceneData == null)
        {
            return string.Empty;
        }

        string preferredPath = isUnlocked ? sceneData.UnLockedSpritePath : sceneData.LockedSpritePath;
        return string.IsNullOrWhiteSpace(preferredPath) ? string.Empty : preferredPath.Trim();
    }

    public static Sprite GetFallbackSprite(VNScene sceneData, bool isUnlocked)
    {
        if (sceneData == null)
        {
            return null;
        }

        if (isUnlocked && sceneData.UnLockedSprite != null)
        {
            return sceneData.UnLockedSprite;
        }

        return sceneData.LockedSprite;
    }

    public static IEnumerator LoadSpriteAsync(VNScene sceneData, bool isUnlocked, Action<Sprite> onLoaded)
    {
        Sprite fallbackSprite = GetFallbackSprite(sceneData, isUnlocked);
        string spritePath = GetSpritePath(sceneData, isUnlocked);

        if (string.IsNullOrWhiteSpace(spritePath))
        {
            onLoaded?.Invoke(fallbackSprite);
            yield break;
        }

        bool spriteLoaded = false;
        Sprite loadedSprite = null;
        ResourcesManager.GetInstance().LoadOptionalAsync<Sprite>(spritePath, sprite =>
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

        if (generatedSpriteCache.TryGetValue(spritePath, out Sprite cachedSprite) && cachedSprite != null)
        {
            onLoaded?.Invoke(cachedSprite);
            yield break;
        }

        bool textureLoaded = false;
        Texture2D loadedTexture = null;
        ResourcesManager.GetInstance().LoadOptionalAsync<Texture2D>(spritePath, texture =>
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
        generatedSpriteCache[spritePath] = generatedSprite;
        onLoaded?.Invoke(generatedSprite);
    }
}
