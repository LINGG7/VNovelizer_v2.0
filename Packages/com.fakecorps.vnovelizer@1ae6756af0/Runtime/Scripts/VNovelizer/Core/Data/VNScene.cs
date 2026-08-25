using UnityEngine;

/// <summary>
/// 场景回放数据类
/// </summary>
[System.Serializable]
public class VNScene
{
    [Tooltip("场景ID（用于解锁）")]
    public string VNscriptID = "";

    [Tooltip("剧本文件名")]
    public string ScriptName = "";

    [Tooltip("开始行ID")]
    public string StartLineID = "";

    [Tooltip("结束行ID")]
    public string EndLineID = "";

    [Tooltip("Scene detail text")]
    [TextArea(2, 5)]
    public string DetailText = "";

    [Tooltip("未解锁时显示的占位图")]
    public Sprite LockedSprite;

    [Tooltip("Addressables/Resources key used to load the locked scene sprite remotely.")]
    public string LockedSpritePath = "";

    [Tooltip("解锁后显示的缩略图")]
    public Sprite UnLockedSprite;

    [Tooltip("Addressables/Resources key used to load the unlocked scene sprite remotely.")]
    public string UnLockedSpritePath = "";

    [Tooltip("是否已解锁（用于调试）")]
    public bool isUnLocked = false;

    public VNScene()
    {
        VNscriptID = "";
        ScriptName = "";
        StartLineID = "";
        EndLineID = "";
        DetailText = "";
        LockedSpritePath = "";
        UnLockedSpritePath = "";
        isUnLocked = false;
    }

    public VNScene(string id)
    {
        VNscriptID = id;
        ScriptName = "";
        StartLineID = "";
        EndLineID = "";
        DetailText = "";
        LockedSpritePath = "";
        UnLockedSpritePath = "";
        isUnLocked = false;
    }
}






