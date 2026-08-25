using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterResManager : BaseManager<CharacterResManager>
{
    private readonly Dictionary<string, CharacterProfile> characterProfiles = new Dictionary<string, CharacterProfile>();
    private bool isInitialized;
    private bool isInitializing;

    public bool IsInitialized => isInitialized;

    public void Init()
    {
        if (isInitialized || isInitializing)
        {
            return;
        }

        characterProfiles.Clear();
        RegisterProfiles(LoadAllCharacterProfiles());
        isInitialized = true;
    }

    public IEnumerator InitAsync()
    {
        if (isInitialized)
        {
            yield break;
        }

        while (isInitializing)
        {
            yield return null;
        }

        if (isInitialized)
        {
            yield break;
        }

        isInitializing = true;
        characterProfiles.Clear();

        string loadPath = VNProjectConfig.Instance.CharacterResPath;
        CharacterProfile[] profiles = LoadLocalCharacterProfiles(loadPath);
        if (profiles == null || profiles.Length == 0)
        {
            yield return ResourcesManager.GetInstance().LoadAllAsync<CharacterProfile>(loadPath, result => profiles = result);
        }

        RegisterProfiles(profiles);
        isInitialized = true;
        isInitializing = false;
    }

    private CharacterProfile[] LoadAllCharacterProfiles()
    {
        string loadPath = VNProjectConfig.Instance.CharacterResPath;
        CharacterProfile[] localProfiles = LoadLocalCharacterProfiles(loadPath);
        if (localProfiles != null && localProfiles.Length > 0)
        {
            return localProfiles;
        }

        return ResourcesManager.GetInstance().LoadAll<CharacterProfile>(loadPath);
    }

    private CharacterProfile[] LoadLocalCharacterProfiles(string loadPath)
    {
        CharacterProfile[] profiles = Resources.LoadAll<CharacterProfile>(loadPath);
        if (profiles != null && profiles.Length > 0)
        {
            Debug.Log($"[CharacterResManager] Loaded {profiles.Length} local character profiles from Resources: {loadPath}");
            return profiles;
        }

        return null;
    }

    private void RegisterProfiles(CharacterProfile[] profiles)
    {
        string loadPath = VNProjectConfig.Instance.CharacterResPath;

        if (profiles == null || profiles.Length == 0)
        {
            Debug.LogError($"[CharacterResManager] No CharacterProfile assets found under: {loadPath}");
            return;
        }

        Debug.Log($"[CharacterResManager] Found {profiles.Length} character profiles under {loadPath}.");

        foreach (CharacterProfile profile in profiles)
        {
            if (profile == null)
            {
                continue;
            }

            string cleanID = profile.CharacterID != null ? profile.CharacterID.Trim() : string.Empty;
            if (string.IsNullOrEmpty(cleanID))
            {
                Debug.LogError($"[CharacterResManager] CharacterProfile '{profile.name}' has an empty CharacterID.");
                continue;
            }

            if (!characterProfiles.ContainsKey(cleanID))
            {
                characterProfiles[cleanID] = profile;
            }
            else
            {
                Debug.LogWarning($"[CharacterResManager] Duplicate CharacterID skipped: {cleanID}");
            }
        }
    }

    public CharacterProfile GetCharacterProfile(string characterID)
    {
        string cleanID = characterID != null ? characterID.Trim() : string.Empty;
        if (string.IsNullOrEmpty(cleanID))
        {
            Debug.LogError("[CharacterResManager] Empty characterID requested.");
            return null;
        }

        if (characterProfiles.Count == 0 && !isInitialized)
        {
            Debug.LogWarning("[CharacterResManager] Character profiles were requested before async initialization finished. Falling back to synchronous load.");
            Init();
        }

        if (characterProfiles.TryGetValue(cleanID, out CharacterProfile profile))
        {
            return profile;
        }

        string loadedIDs = string.Join(", ", characterProfiles.Keys);
        Debug.LogError($"[CharacterResManager] CharacterID not found: '{cleanID}'. Loaded IDs: [{loadedIDs}]");
        return null;
    }

    public CharacterProfile TryGetCharacterProfile(string characterID)
    {
        string cleanID = characterID != null ? characterID.Trim() : string.Empty;
        if (string.IsNullOrEmpty(cleanID))
        {
            return null;
        }

        if (characterProfiles.Count == 0 && !isInitialized)
        {
            Init();
        }

        characterProfiles.TryGetValue(cleanID, out CharacterProfile profile);
        return profile;
    }

    public Sprite GetCharacterSprite(string characterID, string element)
    {
        CharacterProfile profile = GetCharacterProfile(characterID);
        if (profile != null)
        {
            foreach (ElementSprite emotionSprite in profile.ElementSprites)
            {
                if (emotionSprite.Element == element)
                {
                    return emotionSprite.Sprite;
                }
            }

            Debug.LogError($"[CharacterResManager] Character '{characterID}' has no sprite for element '{element}'.");
        }

        return null;
    }

    public ElementSprite GetCharacterSpriteEntry(string characterID, string element)
    {
        CharacterProfile profile = GetCharacterProfile(characterID);
        if (profile == null)
        {
            return null;
        }

        return FindSpriteEntry(profile.ElementSprites, element, $"Character '{characterID}' has no sprite for element '{element}'.");
    }

    public ElementSprite GetHeadSpriteEntry(string characterID, string element)
    {
        CharacterProfile profile = TryGetCharacterProfile(characterID);
        if (profile == null)
        {
            return null;
        }

        return FindSpriteEntry(profile.HeadSprites, element, $"Character '{characterID}' has no head sprite for emotion '{element}'.");
    }

    private ElementSprite FindSpriteEntry(List<ElementSprite> entries, string element, string missingMessage)
    {
        if (entries == null)
        {
            Debug.LogError($"[CharacterResManager] {missingMessage}");
            return null;
        }

        foreach (ElementSprite entry in entries)
        {
            if (entry != null && entry.Element == element)
            {
                return entry;
            }
        }

        Debug.LogError($"[CharacterResManager] {missingMessage}");
        return null;
    }
}
