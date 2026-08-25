using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class GalleryEditor : EditorWindow
{
    private enum EditorMode { CG, Music, Scene, Ending }

    private EditorMode currentMode = EditorMode.Scene;

    private CGDataContainer cgContainer;
    private MusicDataContainer musicContainer;
    private SceneDataContainer sceneContainer;
    private EndingDataContainer endingContainer;

    private VisualElement root;
    private ListView leftList;
    private VisualElement rightPane;
    private object selectedItem;

    [MenuItem("VNovelizer/画廊编辑器 (Gallery Editor)", false, 26)]
    public static void ShowWindow()
    {
        var wnd = GetWindow<GalleryEditor>();
        wnd.titleContent = new GUIContent("画廊编辑器");
        wnd.minSize = new Vector2(900, 600);
    }

    private void OnEnable()
    {
        LoadContainers();
    }

    private void LoadContainers()
    {
        string cgPath = GetConfiguredPath(VNProjectConfig.Instance?.CG_DataPath, "VNovelizerRes/Data", "CGDataContainer");
        cgContainer = LoadResourcesAsset<CGDataContainer>(cgPath);

        string musicPath = GetConfiguredPath(VNProjectConfig.Instance?.Music_DataPath, "VNovelizerRes/Data", "MusicDataContainer");
        musicContainer = LoadResourcesAsset<MusicDataContainer>(musicPath);
        BackfillMusicAssetPaths();

        string scenePath = GetConfiguredPath(VNProjectConfig.Instance?.Scene_DataPath, "VNovelizerRes/Data", "SceneDataContainer");
        sceneContainer = LoadResourcesAsset<SceneDataContainer>(scenePath);
        BackfillSceneSpritePaths();

        string endingPath = GetConfiguredPath(VNProjectConfig.Instance?.Ending_DataPath, "VNovelizerRes/Data", "EndingDataContainer");
        endingContainer = LoadResourcesAsset<EndingDataContainer>(endingPath);
        BackfillEndingSpritePaths();
    }

    private static string GetConfiguredPath(string folder, string fallbackFolder, string assetName)
    {
        string baseFolder = string.IsNullOrEmpty(folder) ? fallbackFolder : folder;
        return baseFolder + "/" + assetName;
    }

    private static T LoadResourcesAsset<T>(string resourcesPath) where T : Object
    {
        string assetPath = $"Assets/Resources/{resourcesPath}.asset";
        return AssetDatabase.LoadAssetAtPath<T>(assetPath);
    }

    public void CreateGUI()
    {
        root = rootVisualElement;
        root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

        VisualElement toolbar = CreateToolbar();
        root.Add(toolbar);

        var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
        root.Add(splitView);

        var leftPane = new VisualElement();
        leftPane.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

        var listToolbar = new VisualElement();
        listToolbar.style.flexDirection = FlexDirection.Row;
        listToolbar.style.paddingTop = 5;
        listToolbar.style.paddingBottom = 5;
        listToolbar.style.paddingLeft = 5;
        listToolbar.style.paddingRight = 5;
        listToolbar.Add(new Button(CreateNewItem) { text = "新建项目", style = { flexGrow = 1 } });
        leftPane.Add(listToolbar);

        leftList = new ListView();
        leftList.makeItem = () => new Label { style = { paddingLeft = 10, paddingTop = 5, paddingBottom = 5 } };
        leftList.selectionType = SelectionType.Single;
        leftList.selectionChanged += OnSelectionChanged;
        leftList.style.flexGrow = 1;
        leftPane.Add(leftList);
        splitView.Add(leftPane);

        rightPane = new VisualElement();
        rightPane.style.paddingTop = 10;
        rightPane.style.paddingLeft = 20;
        rightPane.style.paddingRight = 20;
        splitView.Add(rightPane);

        SwitchMode(EditorMode.CG);
    }

    private VisualElement CreateToolbar()
    {
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.height = 40;
        toolbar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        toolbar.style.alignItems = Align.Center;
        toolbar.style.paddingLeft = 10;
        toolbar.style.borderBottomWidth = 1;
        toolbar.style.borderBottomColor = Color.black;

        toolbar.Add(new Button(() => SwitchMode(EditorMode.Scene)) { text = "场景管理", style = { height = 30, width = 120 } });
        toolbar.Add(new Button(() => SwitchMode(EditorMode.Music)) { text = "音乐管理", style = { height = 30, width = 120 } });
        toolbar.Add(new Button(() => SwitchMode(EditorMode.Ending)) { text = "结局管理", style = { height = 30, width = 120 } });
        toolbar.Add(new Button(() => SwitchMode(EditorMode.CG)) { text = "CG 管理", style = { height = 30, width = 120 } });

        return toolbar;
    }

    private void SwitchMode(EditorMode mode)
    {
        currentMode = mode;
        rightPane.Clear();
        selectedItem = null;
        leftList.ClearSelection();
        leftList.itemsSource = null;

        if (mode == EditorMode.CG)
        {
            if (cgContainer == null)
            {
                rightPane.Add(CreateMissingContainerUI("CGDataContainer", EditorMode.CG));
                return;
            }
            leftList.bindItem = (e, i) => { (e as Label).text = string.IsNullOrEmpty(cgContainer.cgList[i].cgName) ? "[未命名]" : cgContainer.cgList[i].cgName; };
            leftList.itemsSource = cgContainer.cgList;
        }
        else if (mode == EditorMode.Music)
        {
            if (musicContainer == null)
            {
                rightPane.Add(CreateMissingContainerUI("MusicDataContainer", EditorMode.Music));
                return;
            }
            leftList.bindItem = (e, i) => { (e as Label).text = string.IsNullOrEmpty(musicContainer.musicList[i].name) ? "[未命名]" : musicContainer.musicList[i].name; };
            leftList.itemsSource = musicContainer.musicList;
        }
        else if (mode == EditorMode.Scene)
        {
            if (sceneContainer == null)
            {
                rightPane.Add(CreateMissingContainerUI("SceneDataContainer", EditorMode.Scene));
                return;
            }
            leftList.bindItem = (e, i) => { (e as Label).text = string.IsNullOrEmpty(sceneContainer.sceneList[i].VNscriptID) ? "[未命名]" : sceneContainer.sceneList[i].VNscriptID; };
            leftList.itemsSource = sceneContainer.sceneList;
        }
        else
        {
            if (endingContainer == null)
            {
                rightPane.Add(CreateMissingContainerUI("EndingDataContainer", EditorMode.Ending));
                return;
            }
            leftList.bindItem = (e, i) => { (e as Label).text = string.IsNullOrEmpty(endingContainer.endingList[i].EndingID) ? "[未命名]" : endingContainer.endingList[i].EndingID; };
            leftList.itemsSource = endingContainer.endingList;
        }

        leftList.Rebuild();
    }

    private VisualElement CreateMissingContainerUI(string name, EditorMode mode)
    {
        var box = new VisualElement();
        box.style.alignItems = Align.Center;
        box.style.paddingTop = 50;

        box.Add(new Label($"未找到 {name}。\n请点击下方按钮创建。") { style = { color = Color.red, fontSize = 16, unityTextAlign = TextAnchor.MiddleCenter } });
        box.Add(new Button(() => CreateContainer(name, mode)) { text = "立即创建", style = { width = 150, marginTop = 20 } });

        return box;
    }

    private void CreateContainer(string name, EditorMode mode)
    {
        string folder = GetContainerFolder(mode);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        ScriptableObject so;
        if (name == "CGDataContainer") so = ScriptableObject.CreateInstance<CGDataContainer>();
        else if (name == "MusicDataContainer") so = ScriptableObject.CreateInstance<MusicDataContainer>();
        else if (name == "SceneDataContainer") so = ScriptableObject.CreateInstance<SceneDataContainer>();
        else so = ScriptableObject.CreateInstance<EndingDataContainer>();

        AssetDatabase.CreateAsset(so, $"{folder}/{name}.asset");
        AssetDatabase.SaveAssets();

        LoadContainers();
        SwitchMode(currentMode);
    }

    private string GetContainerFolder(EditorMode mode)
    {
        string folder = "Assets/Resources/VNovelizerRes/Data";
        if (VNProjectConfig.Instance == null)
        {
            return folder;
        }

        if (mode == EditorMode.CG && !string.IsNullOrEmpty(VNProjectConfig.Instance.CG_DataPath))
            folder = "Assets/Resources/" + VNProjectConfig.Instance.CG_DataPath;
        else if (mode == EditorMode.Music && !string.IsNullOrEmpty(VNProjectConfig.Instance.Music_DataPath))
            folder = "Assets/Resources/" + VNProjectConfig.Instance.Music_DataPath;
        else if (mode == EditorMode.Scene && !string.IsNullOrEmpty(VNProjectConfig.Instance.Scene_DataPath))
            folder = "Assets/Resources/" + VNProjectConfig.Instance.Scene_DataPath;
        else if (mode == EditorMode.Ending && !string.IsNullOrEmpty(VNProjectConfig.Instance.Ending_DataPath))
            folder = "Assets/Resources/" + VNProjectConfig.Instance.Ending_DataPath;

        return folder;
    }

    private void CreateNewItem()
    {
        if (currentMode == EditorMode.CG)
        {
            if (cgContainer == null) return;
            cgContainer.AddCGData(new CGData($"New_CG_{cgContainer.cgList.Count + 1}"));
            EditorUtility.SetDirty(cgContainer);
        }
        else if (currentMode == EditorMode.Music)
        {
            if (musicContainer == null) return;
            musicContainer.AddMusic(new VNMusic($"New_Music_{musicContainer.musicList.Count + 1}"));
            EditorUtility.SetDirty(musicContainer);
        }
        else if (currentMode == EditorMode.Scene)
        {
            if (sceneContainer == null) return;
            sceneContainer.AddScene(new VNScene($"New_Scene_{sceneContainer.sceneList.Count + 1}"));
            EditorUtility.SetDirty(sceneContainer);
        }
        else
        {
            if (endingContainer == null) return;
            endingContainer.AddEnding(new VNEnding($"New_Ending_{endingContainer.endingList.Count + 1}"));
            EditorUtility.SetDirty(endingContainer);
        }

        leftList.Rebuild();
        leftList.SetSelection(leftList.itemsSource.Count - 1);
    }

    private void OnSelectionChanged(IEnumerable<object> selection)
    {
        rightPane.Clear();
        foreach (object item in selection)
        {
            selectedItem = item;
            if (currentMode == EditorMode.CG) DrawCGDetail(item as CGData);
            else if (currentMode == EditorMode.Music) DrawMusicDetail(item as VNMusic);
            else if (currentMode == EditorMode.Scene) DrawSceneDetail(item as VNScene);
            else DrawEndingDetail(item as VNEnding);
            break;
        }
    }

    private void DrawCGDetail(CGData cg)
    {
        if (cg == null) return;

        rightPane.Clear();
        DrawHeader("CG ID", cg.cgName, val =>
        {
            cg.cgName = val;
            EditorUtility.SetDirty(cgContainer);
            leftList.RefreshItem(cgContainer.cgList.IndexOf(cg));
        }, () =>
        {
            if (EditorUtility.DisplayDialog("删除", $"确定删除 {cg.cgName} 吗?", "是", "否"))
            {
                cgContainer.RemoveCGData(cg);
                EditorUtility.SetDirty(cgContainer);
                rightPane.Clear();
                leftList.Rebuild();
            }
        });

        var unlock = new Toggle("已解锁 (Debug)") { value = cg.isUnlocked };
        unlock.RegisterValueChangedCallback(evt => { cg.isUnlocked = evt.newValue; EditorUtility.SetDirty(cgContainer); });
        rightPane.Add(unlock);

        DrawSpriteField("未解锁占位图", cg.lockedSprite, val => { cg.lockedSprite = val; EditorUtility.SetDirty(cgContainer); });

        var listHeader = new VisualElement();
        listHeader.style.flexDirection = FlexDirection.Row;
        listHeader.style.marginTop = 20;
        listHeader.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
        listHeader.style.paddingLeft = 5;
        listHeader.style.paddingRight = 5;
        listHeader.style.height = 24;
        listHeader.style.alignItems = Align.Center;
        listHeader.Add(new Label("差分图片 (Sprites)") { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } });
        listHeader.Add(new Button(() =>
        {
            cg.sprites.Add(null);
            EditorUtility.SetDirty(cgContainer);
            DrawCGDetail(cg);
        }) { text = "+" });
        rightPane.Add(listHeader);

        var spriteList = new ListView();
        spriteList.style.flexGrow = 1;
        spriteList.style.minHeight = 200;
        spriteList.itemsSource = cg.sprites;
        spriteList.fixedItemHeight = 70;
        spriteList.makeItem = () => new VisualElement();
        spriteList.bindItem = (element, index) =>
        {
            element.Clear();
            DrawSpriteListRow(element, cg.sprites[index], val =>
            {
                cg.sprites[index] = val;
                EditorUtility.SetDirty(cgContainer);
            }, () =>
            {
                cg.sprites.RemoveAt(index);
                EditorUtility.SetDirty(cgContainer);
                spriteList.Rebuild();
            });
        };

        rightPane.Add(spriteList);
    }

    private void DrawMusicDetail(VNMusic music)
    {
        if (music == null) return;

        DrawHeader("音乐名称", music.name, val =>
        {
            music.name = val;
            EditorUtility.SetDirty(musicContainer);
            leftList.RefreshItem(musicContainer.musicList.IndexOf(music));
        }, () =>
        {
            if (EditorUtility.DisplayDialog("删除", $"确定删除 {music.name} 吗?", "是", "否"))
            {
                musicContainer.RemoveMusic(music);
                EditorUtility.SetDirty(musicContainer);
                rightPane.Clear();
                leftList.Rebuild();
            }
        });

        var unlock = new Toggle("已解锁 (Debug)") { value = music.isUnlocked };
        unlock.RegisterValueChangedCallback(evt => { music.isUnlocked = evt.newValue; EditorUtility.SetDirty(musicContainer); });
        rightPane.Add(unlock);

        DrawRemoteSpriteField("Cover", music.picture, music.picturePath, (sprite, path) =>
        {
            music.picture = sprite;
            music.picturePath = path;
            EditorUtility.SetDirty(musicContainer);
        });

        DrawRemoteAudioField("Clip", music.music, music.musicPath, (clip, path) =>
        {
            music.music = clip;
            music.musicPath = path;
            EditorUtility.SetDirty(musicContainer);
        });
    }

    private void DrawSceneDetail(VNScene scene)
    {
        if (scene == null) return;

        DrawHeader("场景 ID", scene.VNscriptID, val =>
        {
            scene.VNscriptID = val;
            EditorUtility.SetDirty(sceneContainer);
            leftList.RefreshItem(sceneContainer.sceneList.IndexOf(scene));
        }, () =>
        {
            if (EditorUtility.DisplayDialog("删除", $"确定删除场景 '{scene.VNscriptID}' 吗?", "是", "否"))
            {
                sceneContainer.RemoveScene(scene);
                EditorUtility.SetDirty(sceneContainer);
                rightPane.Clear();
                leftList.Rebuild();
            }
        });

        var unlock = new Toggle("已解锁 (Debug)") { value = scene.isUnLocked };
        unlock.RegisterValueChangedCallback(evt => { scene.isUnLocked = evt.newValue; EditorUtility.SetDirty(sceneContainer); });
        rightPane.Add(unlock);

        DrawRemoteSpriteField("Locked", scene.LockedSprite, scene.LockedSpritePath, (sprite, path) =>
        {
            scene.LockedSprite = sprite;
            scene.LockedSpritePath = path;
            EditorUtility.SetDirty(sceneContainer);
        });
        DrawRemoteSpriteField("Unlocked", scene.UnLockedSprite, scene.UnLockedSpritePath, (sprite, path) =>
        {
            scene.UnLockedSprite = sprite;
            scene.UnLockedSpritePath = path;
            EditorUtility.SetDirty(sceneContainer);
        });

        var scriptNameField = new TextField("剧本文件名") { value = scene.ScriptName };
        scriptNameField.RegisterValueChangedCallback(evt => { scene.ScriptName = evt.newValue; EditorUtility.SetDirty(sceneContainer); });
        rightPane.Add(scriptNameField);

        var startLineField = new TextField("起始行 ID") { value = scene.StartLineID };
        startLineField.RegisterValueChangedCallback(evt => { scene.StartLineID = evt.newValue; EditorUtility.SetDirty(sceneContainer); });
        rightPane.Add(startLineField);

        var endLineField = new TextField("结束行 ID") { value = scene.EndLineID };
        endLineField.RegisterValueChangedCallback(evt => { scene.EndLineID = evt.newValue; EditorUtility.SetDirty(sceneContainer); });
        rightPane.Add(endLineField);

        var detailTextField = new TextField("详情文本 (DetailText)") { value = scene.DetailText, multiline = true };
        detailTextField.style.marginTop = 10;
        detailTextField.style.minHeight = 80;
        detailTextField.RegisterValueChangedCallback(evt => { scene.DetailText = evt.newValue; EditorUtility.SetDirty(sceneContainer); });
        rightPane.Add(detailTextField);
    }

    private void DrawEndingDetail(VNEnding ending)
    {
        if (ending == null) return;

        DrawHeader("结局 ID", ending.EndingID, val =>
        {
            ending.EndingID = val;
            EditorUtility.SetDirty(endingContainer);
            leftList.RefreshItem(endingContainer.endingList.IndexOf(ending));
        }, () =>
        {
            if (EditorUtility.DisplayDialog("删除", $"确定删除结局 '{ending.EndingID}' 吗?", "是", "否"))
            {
                endingContainer.RemoveEnding(ending);
                EditorUtility.SetDirty(endingContainer);
                rightPane.Clear();
                leftList.Rebuild();
            }
        });

        var unlock = new Toggle("已解锁 (Debug)") { value = ending.isUnLocked };
        unlock.RegisterValueChangedCallback(evt => { ending.isUnLocked = evt.newValue; EditorUtility.SetDirty(endingContainer); });
        rightPane.Add(unlock);

        DrawRemoteSpriteField("Locked", ending.LockedSprite, ending.LockedSpritePath, (sprite, path) =>
        {
            ending.LockedSprite = sprite;
            ending.LockedSpritePath = path;
            EditorUtility.SetDirty(endingContainer);
        });
        DrawRemoteSpriteField("Unlocked", ending.UnLockedSprite, ending.UnLockedSpritePath, (sprite, path) =>
        {
            ending.UnLockedSprite = sprite;
            ending.UnLockedSpritePath = path;
            EditorUtility.SetDirty(endingContainer);
        });

        var lockedTextField = new TextField("未解锁文本") { value = ending.LockedText, multiline = true };
        lockedTextField.style.marginTop = 10;
        lockedTextField.style.minHeight = 80;
        lockedTextField.RegisterValueChangedCallback(evt => { ending.LockedText = evt.newValue; EditorUtility.SetDirty(endingContainer); });
        rightPane.Add(lockedTextField);

        var unlockedTextField = new TextField("已解锁文本") { value = ending.UnlockedText, multiline = true };
        unlockedTextField.style.marginTop = 10;
        unlockedTextField.style.minHeight = 80;
        unlockedTextField.RegisterValueChangedCallback(evt => { ending.UnlockedText = evt.newValue; EditorUtility.SetDirty(endingContainer); });
        rightPane.Add(unlockedTextField);
    }

    private void DrawHeader(string label, string value, System.Action<string> onNameChange, System.Action onDelete)
    {
        var box = new VisualElement();
        box.style.flexDirection = FlexDirection.Row;
        box.style.marginBottom = 10;
        box.style.borderBottomWidth = 1;
        box.style.borderBottomColor = Color.gray;
        box.style.paddingBottom = 10;

        var nameField = new TextField(label) { value = value, style = { flexGrow = 1 } };
        nameField.RegisterValueChangedCallback(evt => onNameChange(evt.newValue));

        var delBtn = new Button(onDelete) { text = "删除" };
        delBtn.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f);

        box.Add(nameField);
        box.Add(delBtn);
        rightPane.Add(box);
    }

    private void DrawSpriteField(string label, Sprite value, System.Action<Sprite> onChange)
    {
        var box = new Box();
        box.style.marginTop = 10;
        box.style.flexDirection = FlexDirection.Row;
        box.style.alignItems = Align.Center;
        box.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);

        var preview = new Image();
        preview.style.width = 60;
        preview.style.height = 60;
        preview.scaleMode = ScaleMode.ScaleToFit;
        if (value != null) preview.image = AssetPreview.GetAssetPreview(value);

        var field = new ObjectField(label) { objectType = typeof(Sprite), value = value, style = { flexGrow = 1, marginLeft = 10 } };
        field.RegisterValueChangedCallback(evt =>
        {
            var sprite = evt.newValue as Sprite;
            onChange(sprite);
            preview.image = sprite ? AssetPreview.GetAssetPreview(sprite) : null;
        });

        box.Add(preview);
        box.Add(field);
        rightPane.Add(box);
    }

    private void DrawRemoteAudioField(string label, AudioClip value, string remotePath, System.Action<AudioClip, string> onChange)
    {
        var box = new Box();
        box.style.marginTop = 10;
        box.style.paddingTop = 6;
        box.style.paddingBottom = 6;
        box.style.paddingLeft = 6;
        box.style.paddingRight = 6;
        box.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);

        var field = new ObjectField(label) { objectType = typeof(AudioClip), value = value };
        var pathField = new TextField("Remote Key") { value = remotePath ?? string.Empty };
        pathField.style.marginTop = 6;

        field.RegisterValueChangedCallback(evt =>
        {
            var clip = evt.newValue as AudioClip;
            string clipPath = BuildRemoteAssetKey(clip);
            onChange(clip, clipPath);
            pathField.SetValueWithoutNotify(clipPath);
        });

        pathField.RegisterValueChangedCallback(evt => onChange(field.value as AudioClip, evt.newValue));

        box.Add(field);
        box.Add(pathField);
        rightPane.Add(box);
    }

    private void DrawRemoteSpriteField(string label, Sprite value, string remotePath, System.Action<Sprite, string> onChange)
    {
        var box = new Box();
        box.style.marginTop = 10;
        box.style.paddingTop = 6;
        box.style.paddingBottom = 6;
        box.style.paddingLeft = 6;
        box.style.paddingRight = 6;
        box.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        var preview = new Image();
        preview.style.width = 60;
        preview.style.height = 60;
        preview.scaleMode = ScaleMode.ScaleToFit;
        if (value != null) preview.image = AssetPreview.GetAssetPreview(value);

        var field = new ObjectField(label) { objectType = typeof(Sprite), value = value, style = { flexGrow = 1, marginLeft = 10 } };
        var pathField = new TextField("Remote Key") { value = remotePath ?? string.Empty };
        pathField.style.marginTop = 6;

        field.RegisterValueChangedCallback(evt =>
        {
            var sprite = evt.newValue as Sprite;
            string spritePath = BuildRemoteSpriteKey(sprite);
            onChange(sprite, spritePath);
            pathField.SetValueWithoutNotify(spritePath);
            preview.image = sprite ? AssetPreview.GetAssetPreview(sprite) : null;
        });

        pathField.RegisterValueChangedCallback(evt => onChange(field.value as Sprite, evt.newValue));

        row.Add(preview);
        row.Add(field);
        box.Add(row);
        box.Add(pathField);
        rightPane.Add(box);
    }

    private void BackfillMusicAssetPaths()
    {
        if (musicContainer == null || musicContainer.musicList == null)
        {
            return;
        }

        bool changed = false;
        foreach (VNMusic music in musicContainer.musicList)
        {
            if (music == null)
            {
                continue;
            }

            string picturePath = BuildRemoteSpriteKey(music.picture);
            if (string.IsNullOrEmpty(music.picturePath) && !string.IsNullOrEmpty(picturePath))
            {
                music.picturePath = picturePath;
                changed = true;
            }

            string audioPath = BuildRemoteAssetKey(music.music);
            if (string.IsNullOrEmpty(music.musicPath) && !string.IsNullOrEmpty(audioPath))
            {
                music.musicPath = audioPath;
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(musicContainer);
            AssetDatabase.SaveAssets();
        }
    }

    private void BackfillSceneSpritePaths()
    {
        if (sceneContainer == null || sceneContainer.sceneList == null)
        {
            return;
        }

        bool changed = false;
        foreach (VNScene scene in sceneContainer.sceneList)
        {
            if (scene == null)
            {
                continue;
            }

            string lockedPath = BuildRemoteSpriteKey(scene.LockedSprite);
            if (string.IsNullOrEmpty(scene.LockedSpritePath) && !string.IsNullOrEmpty(lockedPath))
            {
                scene.LockedSpritePath = lockedPath;
                changed = true;
            }

            string unlockedPath = BuildRemoteSpriteKey(scene.UnLockedSprite);
            if (string.IsNullOrEmpty(scene.UnLockedSpritePath) && !string.IsNullOrEmpty(unlockedPath))
            {
                scene.UnLockedSpritePath = unlockedPath;
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(sceneContainer);
            AssetDatabase.SaveAssets();
        }
    }

    private void BackfillEndingSpritePaths()
    {
        if (endingContainer == null || endingContainer.endingList == null)
        {
            return;
        }

        bool changed = false;
        foreach (VNEnding ending in endingContainer.endingList)
        {
            if (ending == null)
            {
                continue;
            }

            string lockedPath = BuildRemoteSpriteKey(ending.LockedSprite);
            if (string.IsNullOrEmpty(ending.LockedSpritePath) && !string.IsNullOrEmpty(lockedPath))
            {
                ending.LockedSpritePath = lockedPath;
                changed = true;
            }

            string unlockedPath = BuildRemoteSpriteKey(ending.UnLockedSprite);
            if (string.IsNullOrEmpty(ending.UnLockedSpritePath) && !string.IsNullOrEmpty(unlockedPath))
            {
                ending.UnLockedSpritePath = unlockedPath;
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(endingContainer);
            AssetDatabase.SaveAssets();
        }
    }

    private static string BuildRemoteSpriteKey(Sprite sprite)
    {
        return BuildRemoteAssetKey(sprite);
    }

    private static string BuildRemoteAssetKey(Object asset)
    {
        if (asset == null)
        {
            return string.Empty;
        }

        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath))
        {
            return string.Empty;
        }

        assetPath = assetPath.Replace("\\", "/");

        const string resourcesPrefix = "Assets/Resources/";
        if (assetPath.StartsWith(resourcesPrefix))
        {
            return Path.ChangeExtension(assetPath.Substring(resourcesPrefix.Length), null);
        }

        const string remotePrefix = "Assets/RemoteContent/";
        if (assetPath.StartsWith(remotePrefix))
        {
            return Path.ChangeExtension(assetPath.Substring(remotePrefix.Length), null);
        }

        return string.Empty;
    }

    private void DrawSpriteListRow(VisualElement parent, Sprite value, System.Action<Sprite> onChange, System.Action onDelete)
    {
        var box = new Box();
        box.style.flexDirection = FlexDirection.Row;
        box.style.marginBottom = 5;
        box.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
        box.style.borderBottomWidth = 1;
        box.style.borderBottomColor = Color.black;

        var preview = new Image();
        preview.style.width = 50;
        preview.style.height = 50;
        preview.scaleMode = ScaleMode.ScaleToFit;
        if (value != null) preview.image = AssetPreview.GetAssetPreview(value);
        box.Add(preview);

        var field = new ObjectField { objectType = typeof(Sprite), value = value, style = { flexGrow = 1 } };
        field.RegisterValueChangedCallback(evt =>
        {
            var sprite = evt.newValue as Sprite;
            onChange(sprite);
            preview.image = sprite ? AssetPreview.GetAssetPreview(sprite) : null;
        });
        box.Add(field);

        var delBtn = new Button(onDelete) { text = "X" };
        delBtn.style.backgroundColor = new Color(0.5f, 0.2f, 0.2f);
        box.Add(delBtn);

        parent.Add(box);
    }
}
