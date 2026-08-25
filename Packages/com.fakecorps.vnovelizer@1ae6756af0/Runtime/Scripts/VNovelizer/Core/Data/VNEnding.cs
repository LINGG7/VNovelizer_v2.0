using UnityEngine;

/// <summary>
/// Gallery ending data.
/// </summary>
[System.Serializable]
public class VNEnding
{
    [Tooltip("Ending ID used for unlock state.")]
    public string EndingID = "";

    [Tooltip("Text shown before this ending is unlocked.")]
    [TextArea(2, 5)]
    public string LockedText = "";

    [Tooltip("Text shown after this ending is unlocked.")]
    [TextArea(2, 5)]
    public string UnlockedText = "";

    [Tooltip("Sprite shown before this ending is unlocked.")]
    public Sprite LockedSprite;

    [Tooltip("Addressables/Resources key used to load the locked ending sprite remotely.")]
    public string LockedSpritePath = "";

    [Tooltip("Sprite shown after this ending is unlocked.")]
    public Sprite UnLockedSprite;

    [Tooltip("Addressables/Resources key used to load the unlocked ending sprite remotely.")]
    public string UnLockedSpritePath = "";

    [Tooltip("Debug unlock state.")]
    public bool isUnLocked = false;

    public VNEnding()
    {
        EndingID = "";
        LockedText = "";
        UnlockedText = "";
        LockedSpritePath = "";
        UnLockedSpritePath = "";
        isUnLocked = false;
    }

    public VNEnding(string id)
    {
        EndingID = id;
        LockedText = "";
        UnlockedText = "";
        LockedSpritePath = "";
        UnLockedSpritePath = "";
        isUnLocked = false;
    }
}
