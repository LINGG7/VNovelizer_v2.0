using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// 存档管理器
/// </summary>
public class SaveManager : BaseManager<SaveManager>
{
    private const string SAVE_DATA_DIR = "SaveData";
    private const string SCREENSHOT_DIR = "Screenshots";
    private const int MAX_SAVE_SLOTS = 60;

    private Texture2D _tempScreenshot;
    private Coroutine _captureCoroutine;
    private readonly Dictionary<int, string> screenshotPreviewBase64Cache = new Dictionary<int, string>();
    public void Init()
    {
        // 创建存档目录
        string saveDir = Path.Combine(Application.persistentDataPath, SAVE_DATA_DIR);
        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
        }
        
        // 创建截图目录
        string screenshotDir = Path.Combine(Application.persistentDataPath, SCREENSHOT_DIR);
        if (!Directory.Exists(screenshotDir))
        {
            Directory.CreateDirectory(screenshotDir);
        }
    }
    
    /// <summary>
    /// 保存游戏
    /// </summary>
    /// <param name="slotIndex">存档槽位</param>
    /// <param name="saveData">存档数据</param>
    public void SaveGame(int slotIndex, SaveData saveData)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SAVE_SLOTS)
            return;

        AttachScreenshotPreview(slotIndex, saveData);

        string savePath = GetSaveFilePath(slotIndex);


        string dir = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json;
        try
        {
            json = LitJson.JsonMapper.ToJson(saveData);
            Debug.Log($"[SaveManager] 序列化成功，JSON长度: {json.Length}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 序列化失败: {e.Message}\n{e.StackTrace}");
            return;
        }
        
        string contentToWrite = json;

        if (VNProjectConfig.Instance.UseAES)
        {
            try
            {
                contentToWrite = AESUtil.Encrypt(json);
                Debug.Log($"[SaveManager] 加密成功");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] 加密失败: {e.Message}\n{e.StackTrace}");
                return;
            }
        }

        try
        {
            File.WriteAllText(savePath, contentToWrite);
            TapMiniGamePersistentStorage.WriteText(GetRelativeSaveFilePath(slotIndex), contentToWrite);
            WebGLFileSystemSync.Sync();
            Debug.Log($"[SaveManager] 存档保存成功: {savePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 文件写入失败: {e.Message}\n{e.StackTrace}");
            return;
        }

        EventCenter.GetInstance().EventTrigger("GameSaved", slotIndex);
    }
    
    /// <summary>
    /// 加载游戏
    /// </summary>
    /// <param name="slotIndex">存档槽位</param>
    /// <returns>存档数据</returns>
    public SaveData LoadGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SAVE_SLOTS)
            return null;
        
        string savePath = GetSaveFilePath(slotIndex);
        string fileContent;
        if (TapMiniGamePersistentStorage.TryReadText(GetRelativeSaveFilePath(slotIndex), out fileContent) || File.Exists(savePath))
        {
            if (string.IsNullOrEmpty(fileContent) && File.Exists(savePath))
            {
                fileContent = File.ReadAllText(savePath);
            }

            string json = fileContent;
            if (VNProjectConfig.Instance.UseAES)
            {
                // 如果开启了加密，先尝试解密
                string decrypted = AESUtil.Decrypt(fileContent);
                if (!string.IsNullOrEmpty(decrypted))
                {
                    json = decrypted; // 解密成功
                }
                else
                {
                    Debug.LogWarning($"[SaveManager] 存档 {slotIndex} 解密失败，尝试按明文读取。");
                }
            }
            else
            { 
            
            }
            try
            {
                SaveData saveData = LitJson.JsonMapper.ToObject<SaveData>(json);
                Debug.Log($"[SaveManager][BGM] Loaded slot={slotIndex}, script='{saveData?.ScriptFileName}', lineID='{saveData?.LineID}', currentBGM='{saveData?.CurrentBGM}'");
                return saveData;
            }
            catch
            {
                // 如果解析失败，说明可能是加密的但没解开，或者文件坏了
                // 这里可以再尝试一次 AES Decrypt (防止 Config 没开但读了加密档)
                string retryDecrypt = AESUtil.Decrypt(fileContent);
                if (!string.IsNullOrEmpty(retryDecrypt))
                    return LitJson.JsonMapper.ToObject<SaveData>(retryDecrypt);

                Debug.LogError($"存档 {slotIndex} 损坏或格式无法识别。");
                return null;
            }
        }
        return null;
    }
    
    /// <summary>
    /// 保存截图
    /// </summary>
    /// <param name="slotIndex">存档槽位</param>
    /// <param name="texture">截图纹理</param>
    /// <returns>截图路径</returns>
    public string SaveScreenshot(int slotIndex, Texture2D texture)
    {
        string screenshotPath = GetScreenshotFilePath(slotIndex);

        // 【新增】双保险：确保目录存在
        string dir = Path.GetDirectoryName(screenshotPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(screenshotPath, bytes);
        TapMiniGamePersistentStorage.WriteBytes(GetRelativeScreenshotFilePath(slotIndex), bytes);
        CacheScreenshotPreview(slotIndex, texture, bytes);
        WebGLFileSystemSync.Sync();
        return screenshotPath;
    }
    
    /// <summary>
    /// 获取截图
    /// </summary>
    /// <param name="slotIndex">存档槽位</param>
    /// <returns>截图Texture2D</returns>
    public Texture2D GetScreenshot(int slotIndex)
    {
        return GetScreenshot(slotIndex, null);
    }

    public Texture2D GetScreenshot(int slotIndex, SaveData saveData)
    {
        string screenshotPath = GetScreenshotFilePath(slotIndex);
        byte[] bytes = GetStoredScreenshotBytes(slotIndex);
        if (bytes != null && bytes.Length > 0)
        {
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(bytes))
                return texture;

            Object.Destroy(texture);
        }

        if (saveData != null && TryDecodeScreenshotPreview(saveData, out Texture2D previewTexture))
            return previewTexture;

        return null;
    }
    
    /// <summary>
    /// 检查存档是否存在
    /// </summary>
    /// <param name="slotIndex">存档槽位</param>
    /// <returns>是否存在</returns>
    public bool IsSaveExists(int slotIndex)
    {
        string savePath = GetSaveFilePath(slotIndex);
        return TapMiniGamePersistentStorage.Exists(GetRelativeSaveFilePath(slotIndex)) || File.Exists(savePath);
    }

    public bool HasAnySave()
    {
        if (TapMiniGamePersistentStorage.DirectoryHasFiles(SAVE_DATA_DIR))
            return true;

        for (int i = 0; i < MAX_SAVE_SLOTS; i++)
        {
            if (File.Exists(GetSaveFilePath(i)))
                return true;
        }

        return false;
    }
    
    /// <summary>
    /// 删除存档
    /// </summary>
    /// <param name="slotIndex">存档槽位</param>
    public void DeleteSave(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SAVE_SLOTS)
            return;
        
        // 删除存档文件
        string savePath = GetSaveFilePath(slotIndex);
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
        }
        TapMiniGamePersistentStorage.Delete(GetRelativeSaveFilePath(slotIndex));
        
        // 删除截图
        string screenshotPath = GetScreenshotFilePath(slotIndex);
        if (File.Exists(screenshotPath))
        {
            File.Delete(screenshotPath);
        }
        TapMiniGamePersistentStorage.Delete(GetRelativeScreenshotFilePath(slotIndex));
        screenshotPreviewBase64Cache.Remove(slotIndex);
        
        WebGLFileSystemSync.Sync();
        EventCenter.GetInstance().EventTrigger("SaveDeleted", slotIndex);
    }
    
    /// <summary>
    /// 获取所有存档数据
    /// </summary>
    /// <returns>存档数据列表</returns>
    public List<SaveData> GetAllSaveData()
    {
        List<SaveData> saveDatas = new List<SaveData>();
        
        for (int i = 0; i < MAX_SAVE_SLOTS; i++)
        {
            SaveData saveData = LoadGame(i);
            if (saveData != null)
            {
                saveDatas.Add(saveData);
            }
        }
        
        return saveDatas;
    }
    
    /// <summary>
    /// 获取存档文件路径
    /// </summary>
    /// <param name="slotIndex">存档槽位</param>
    /// <returns>文件路径</returns>
    private string GetSaveFilePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, SAVE_DATA_DIR, "save_" + slotIndex + ".json");
    }

    private string GetRelativeSaveFilePath(int slotIndex)
    {
        return SAVE_DATA_DIR + "/save_" + slotIndex + ".json";
    }

    /// <summary>
    /// 获取截图文件路径
    /// </summary>
    /// <param name="slotIndex">存档槽位</param>
    /// <returns>文件路径</returns>
    private string GetScreenshotFilePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, SCREENSHOT_DIR, "screenshot_" + slotIndex + ".png");
    }

    private string GetRelativeScreenshotFilePath(int slotIndex)
    {
        return SCREENSHOT_DIR + "/screenshot_" + slotIndex + ".png";
    }

    /// <summary>
    /// 获取最大存档槽位数
    /// </summary>
    /// <returns>最大存档槽位数</returns>
    public int GetMaxSaveSlots()
    {
        return MAX_SAVE_SLOTS;
    }

    public void CaptureCurrentScreen(System.Action onCaptured = null)
    {
        if (_captureCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_captureCoroutine);
            _captureCoroutine = null;
        }

        if (_tempScreenshot != null)
        {
            Object.Destroy(_tempScreenshot);
            _tempScreenshot = null;
        }

        _captureCoroutine = MonoManager.GetInstance().StartCoroutine(CaptureCurrentScreenAtEndOfFrame(onCaptured));
    }

    private IEnumerator CaptureCurrentScreenAtEndOfFrame(System.Action onCaptured)
    {
        yield return new WaitForEndOfFrame();

        try
        {
            _tempScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] Capture screenshot failed: {e.Message}");
            _tempScreenshot = null;
        }

        _captureCoroutine = null;
        onCaptured?.Invoke();
    }

    public string SaveCachedScreenshot(int slotIndex)
    {
        if (_tempScreenshot == null && _captureCoroutine != null)
        {
            Debug.LogWarning("[SaveManager] Screenshot is still being captured; save data will not contain a preview image.");
            return string.Empty;
        }

        if (_tempScreenshot == null)
        {
            Debug.LogWarning("[SaveManager] No cached screenshot, save data will not contain a preview image.");
            return string.Empty;
        }

        string screenshotPath = GetScreenshotFilePath(slotIndex);
        string dir = Path.GetDirectoryName(screenshotPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        try
        {
            byte[] bytes = _tempScreenshot.EncodeToPNG();
            File.WriteAllBytes(screenshotPath, bytes);
            TapMiniGamePersistentStorage.WriteBytes(GetRelativeScreenshotFilePath(slotIndex), bytes);
            CacheScreenshotPreview(slotIndex, _tempScreenshot, bytes);
            WebGLFileSystemSync.Sync();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] Save screenshot failed: {e.Message}\n{e.StackTrace}");
            return string.Empty;
        }

        return screenshotPath;
    }

    private void AttachScreenshotPreview(int slotIndex, SaveData saveData)
    {
        if (saveData == null || !string.IsNullOrEmpty(saveData.ScreenshotPreviewBase64))
            return;

        if (screenshotPreviewBase64Cache.TryGetValue(slotIndex, out string cachedPreview) && !string.IsNullOrEmpty(cachedPreview))
        {
            saveData.ScreenshotPreviewBase64 = cachedPreview;
            return;
        }

        byte[] bytes = GetStoredScreenshotBytes(slotIndex);
        if (bytes != null && bytes.Length > 0)
        {
            saveData.ScreenshotPreviewBase64 = System.Convert.ToBase64String(bytes);
        }
    }

    private void CacheScreenshotPreview(int slotIndex, Texture2D texture, byte[] fallbackBytes)
    {
        if (texture == null)
            return;

        try
        {
            byte[] previewBytes = texture.EncodeToJPG(65);
            if (previewBytes == null || previewBytes.Length == 0)
                previewBytes = fallbackBytes;

            if (previewBytes != null && previewBytes.Length > 0)
                screenshotPreviewBase64Cache[slotIndex] = System.Convert.ToBase64String(previewBytes);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] Screenshot preview cache failed: {e.Message}");
        }
    }

    private byte[] GetStoredScreenshotBytes(int slotIndex)
    {
        string screenshotPath = GetScreenshotFilePath(slotIndex);
        byte[] bytes;
        if (TapMiniGamePersistentStorage.TryReadBytes(GetRelativeScreenshotFilePath(slotIndex), out bytes) && bytes != null && bytes.Length > 0)
            return bytes;

        if (File.Exists(screenshotPath))
            return File.ReadAllBytes(screenshotPath);

        return null;
    }

    private bool TryDecodeScreenshotPreview(SaveData saveData, out Texture2D texture)
    {
        texture = null;
        if (saveData == null || string.IsNullOrEmpty(saveData.ScreenshotPreviewBase64))
            return false;

        try
        {
            byte[] bytes = System.Convert.FromBase64String(saveData.ScreenshotPreviewBase64);
            if (bytes == null || bytes.Length == 0)
                return false;

            Texture2D previewTexture = new Texture2D(2, 2);
            if (!previewTexture.LoadImage(bytes))
            {
                Object.Destroy(previewTexture);
                return false;
            }

            texture = previewTexture;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] Screenshot preview decode failed: {e.Message}");
            return false;
        }
    }
}

public static class TapMiniGamePersistentStorage
{
#if UNITY_WEBGL && !UNITY_EDITOR
    private static int availability = -1;

    [DllImport("__Internal")]
    private static extern int VNovelizer_TapHasFileSystem();

    [DllImport("__Internal")]
    private static extern int VNovelizer_TapWriteStringFile(string relativePath, string content);

    [DllImport("__Internal")]
    private static extern System.IntPtr VNovelizer_TapReadStringFile(string relativePath);

    [DllImport("__Internal")]
    private static extern int VNovelizer_TapWriteBinaryFile(string relativePath, byte[] data, int length);

    [DllImport("__Internal")]
    private static extern int VNovelizer_TapGetFileSize(string relativePath);

    [DllImport("__Internal")]
    private static extern int VNovelizer_TapReadBinaryFile(string relativePath, byte[] buffer, int bufferLength);

    [DllImport("__Internal")]
    private static extern int VNovelizer_TapFileExists(string relativePath);

    [DllImport("__Internal")]
    private static extern int VNovelizer_TapDirectoryHasFiles(string relativePath);

    [DllImport("__Internal")]
    private static extern int VNovelizer_TapDeleteFile(string relativePath);

    [DllImport("__Internal")]
    private static extern void VNovelizer_TapFree(System.IntPtr ptr);
#endif

    public static bool IsAvailable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (availability >= 0)
            return availability == 1;

        try
        {
            availability = VNovelizer_TapHasFileSystem() == 1 ? 1 : 0;
            return availability == 1;
        }
        catch
        {
            availability = 0;
            return false;
        }
#else
        return false;
#endif
    }

    public static bool WriteText(string relativePath, string content)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsAvailable())
            return false;

        try
        {
            return VNovelizer_TapWriteStringFile(relativePath, content ?? string.Empty) == 1;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TapMiniGamePersistentStorage] WriteText failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }

    public static bool TryReadText(string relativePath, out string content)
    {
        content = null;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsAvailable())
            return false;

        System.IntPtr ptr = System.IntPtr.Zero;
        try
        {
            ptr = VNovelizer_TapReadStringFile(relativePath);
            if (ptr == System.IntPtr.Zero)
                return false;

            content = PtrToUtf8String(ptr);
            return content != null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TapMiniGamePersistentStorage] TryReadText failed: {e.Message}");
            return false;
        }
        finally
        {
            if (ptr != System.IntPtr.Zero)
                VNovelizer_TapFree(ptr);
        }
#else
        return false;
#endif
    }

    public static bool WriteBytes(string relativePath, byte[] bytes)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsAvailable() || bytes == null)
            return false;

        try
        {
            return VNovelizer_TapWriteBinaryFile(relativePath, bytes, bytes.Length) == 1;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TapMiniGamePersistentStorage] WriteBytes failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }

    public static bool TryReadBytes(string relativePath, out byte[] bytes)
    {
        bytes = null;
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsAvailable())
            return false;

        try
        {
            int size = VNovelizer_TapGetFileSize(relativePath);
            if (size < 0)
                return false;

            bytes = new byte[size];
            if (size == 0)
                return true;

            int read = VNovelizer_TapReadBinaryFile(relativePath, bytes, bytes.Length);
            if (read < 0)
            {
                bytes = null;
                return false;
            }

            if (read != size)
            {
                byte[] exactBytes = new byte[read];
                System.Buffer.BlockCopy(bytes, 0, exactBytes, 0, read);
                bytes = exactBytes;
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TapMiniGamePersistentStorage] TryReadBytes failed: {e.Message}");
            bytes = null;
            return false;
        }
#else
        return false;
#endif
    }

    public static bool Exists(string relativePath)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsAvailable())
            return false;

        try
        {
            return VNovelizer_TapFileExists(relativePath) == 1;
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

    public static bool DirectoryHasFiles(string relativePath)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsAvailable())
            return false;

        try
        {
            return VNovelizer_TapDirectoryHasFiles(relativePath) == 1;
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

    public static bool Delete(string relativePath)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!IsAvailable())
            return false;

        try
        {
            return VNovelizer_TapDeleteFile(relativePath) == 1;
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static string PtrToUtf8String(System.IntPtr ptr)
    {
        int length = 0;
        while (Marshal.ReadByte(ptr, length) != 0)
            length++;

        if (length == 0)
            return string.Empty;

        byte[] bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }
#endif
}

public static class WebGLFileSystemSync
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void VNovelizer_SyncFileSystem();
#endif

    public static void Sync()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            VNovelizer_SyncFileSystem();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[WebGLFileSystemSync] Sync failed: {e.Message}");
        }
#endif
    }
}
