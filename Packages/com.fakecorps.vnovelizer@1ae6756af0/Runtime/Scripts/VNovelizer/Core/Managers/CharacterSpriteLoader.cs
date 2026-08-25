using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CharacterSpriteLoader
{
    private static readonly Dictionary<string, Sprite> generatedSpriteCache = new Dictionary<string, Sprite>();

    public static IEnumerator LoadSpriteAsync(ElementSprite entry, Action<Sprite> onLoaded)
    {
        Sprite fallbackSprite = entry != null ? entry.Sprite : null;
        string spritePath = entry != null && !string.IsNullOrWhiteSpace(entry.SpritePath)
            ? entry.SpritePath.Trim()
            : string.Empty;

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
