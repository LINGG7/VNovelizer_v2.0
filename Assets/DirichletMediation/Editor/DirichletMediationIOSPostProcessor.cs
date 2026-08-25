#if UNITY_IOS
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace Dirichlet.Mediation.Editor
{
    /// <summary>
    /// iOS build post-processor for Dirichlet Mediation SDK.
    /// Generates dynamic Podfile based on adapter settings and runs CocoaPods installation.
    ///
    /// 重构说明（2026-02-05）：
    /// - 不再在 Podfile 内做 target 猜测，改为 Post Process 直接从 pbxproj 读取真实 target 名称
    /// - 解析失败时直接中断并输出可诊断信息，不再静默兜底
    ///
    /// 修复（2026-03-12）：
    /// - 所有 pods 统一放到 Framework target（UnityFramework），不再分层
    /// - 原因：SDK 通过 NSClassFromString 动态查找 Adapter 类，Adapter 必须与 SDK 在同一 target，
    ///   否则静态库符号会被 strip 导致运行时找不到适配器类
    /// </summary>
    public class DirichletMediationIOSPostProcessor
    {
        private const string SDKVersion = "4.2.0.2";
        private const string MinIOSVersion = "11.0";

        // 环境变量 override（仅在解析失败或接入方工程极端定制时使用）
        private const string ENV_FRAMEWORK_TARGET = "DIRICHLET_UNITY_FRAMEWORK_TARGET";
        private const string ENV_APP_TARGET = "DIRICHLET_UNITY_APP_TARGET";

        /// <summary>
        /// 解析出的 target 信息
        /// </summary>
        private class TargetInfo
        {
            public string FrameworkTargetName { get; set; }
            public string AppTargetName { get; set; }
            public string FrameworkTargetGuid { get; set; }
            public string AppTargetGuid { get; set; }
        }

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget != BuildTarget.iOS)
            {
                return;
            }

            Debug.Log("[DirichletMediation] Starting iOS post-process...");

            try
            {
                // 确保默认值：iOS 适配器默认勾选（避免历史版本的 EditorPrefs 默认值导致“默认不勾选”）
                EnsureDefaultAdapterPrefs();

                // 0. 解析 target 信息（核心改动：不做猜测，直接从 pbxproj 读取）
                var targetInfo = ResolveTargetInfo(pathToBuiltProject);

                // 1. Generate Podfile dynamically based on adapter settings and resolved targets
                GeneratePodfile(pathToBuiltProject, targetInfo);

                // 2. Modify Xcode project settings (before pod install)
                ModifyXcodeProject(pathToBuiltProject, targetInfo);

                // 3. Modify Info.plist for required permissions
                ModifyInfoPlist(pathToBuiltProject);

                // 4. Run pod install
                RunPodInstall(pathToBuiltProject);

                // 5. Embed GDT dynamic frameworks into app target (must run after pod install)
                EmbedGDTDynamicFrameworks(pathToBuiltProject, targetInfo);

                Debug.Log("[DirichletMediation] iOS post-process completed successfully.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DirichletMediation] iOS post-process failed: {ex.Message}");
                Debug.LogError($"[DirichletMediation] Stack trace: {ex.StackTrace}");
                throw; // 重新抛出，让 Unity 构建失败（不静默跳过）
            }
        }

        /// <summary>
        /// 从 pbxproj 解析出 framework target 和 app target 的真实名称
        /// 不做猜测，解析失败时直接中断
        /// </summary>
        private static TargetInfo ResolveTargetInfo(string projectPath)
        {
            Debug.Log("[DirichletMediation] Resolving target info from pbxproj...");

            // 检查环境变量 override
            var envFrameworkTarget = System.Environment.GetEnvironmentVariable(ENV_FRAMEWORK_TARGET);
            var envAppTarget = System.Environment.GetEnvironmentVariable(ENV_APP_TARGET);

            if (!string.IsNullOrEmpty(envFrameworkTarget) && !string.IsNullOrEmpty(envAppTarget))
            {
                Debug.Log($"[DirichletMediation] Using targets from environment variables:");
                Debug.Log($"  Framework target: {envFrameworkTarget}");
                Debug.Log($"  App target: {envAppTarget}");
                return new TargetInfo
                {
                    FrameworkTargetName = envFrameworkTarget,
                    AppTargetName = envAppTarget,
                    // GUID 在需要时从 pbxproj 反查
                };
            }

            // 查找 .xcodeproj 文件
            var xcodeProjectName = DetectXcodeProjectName(projectPath);
            var projectFilePath = Path.Combine(projectPath, $"{xcodeProjectName}.xcodeproj/project.pbxproj");

            if (!File.Exists(projectFilePath))
            {
                throw new System.Exception($"project.pbxproj not found at: {projectFilePath}");
            }

            var pbxProject = new PBXProject();
            pbxProject.ReadFromFile(projectFilePath);

#if UNITY_2019_3_OR_NEWER
            var frameworkGuid = pbxProject.GetUnityFrameworkTargetGuid();
            var appGuid = pbxProject.GetUnityMainTargetGuid();
#else
            // Unity 2019.3 之前只有单一 target
            var frameworkGuid = pbxProject.TargetGuidByName("Unity-iPhone");
            var appGuid = frameworkGuid;
#endif

            // 直接从 pbxproj 解析 GUID -> name 映射（最稳健，避免 TargetGuidByName 行为差异）
            var nativeTargetMap = GetNativeTargetGuidToNameMap(projectFilePath);

            if (string.IsNullOrEmpty(frameworkGuid) || string.IsNullOrEmpty(appGuid))
            {
                var allTargets = nativeTargetMap.Values.Distinct().ToArray();
                var targetList = allTargets.Any() ? string.Join(", ", allTargets) : "(none)";
                throw new System.Exception(
                    $"Unity PBXProject API returned empty target GUID.\n" +
                    $"  .xcodeproj: {xcodeProjectName}\n" +
                    $"  Available targets: {targetList}\n" +
                    $"  Framework GUID: {frameworkGuid ?? "(null)"}\n" +
                    $"  App GUID: {appGuid ?? "(null)"}\n\n" +
                    $"解决方案：\n" +
                    $"  1. 升级 Unity 到 2019.3+（或团结引擎对应版本），确保导出包含 UnityFramework/App 双 target\n" +
                    $"  2. 或使用环境变量强制指定 target：\n" +
                    $"     export {ENV_FRAMEWORK_TARGET}=YourFrameworkTarget\n" +
                    $"     export {ENV_APP_TARGET}=YourAppTarget"
                );
            }

            // 从 GUID 反查 target name
            nativeTargetMap.TryGetValue(frameworkGuid, out var frameworkTargetName);
            nativeTargetMap.TryGetValue(appGuid, out var appTargetName);

            // 兜底：某些 Unity 版本可能返回非 PBXNativeTarget GUID，这时再尝试旧逻辑反查
            frameworkTargetName = frameworkTargetName ?? GetTargetNameByGuid(pbxProject, frameworkGuid, projectFilePath);
            appTargetName = appTargetName ?? GetTargetNameByGuid(pbxProject, appGuid, projectFilePath);

            // 验证解析结果
            if (string.IsNullOrEmpty(frameworkTargetName) || string.IsNullOrEmpty(appTargetName))
            {
                // 输出所有 targets 帮助诊断
                var allTargets = nativeTargetMap.Values.Distinct().ToArray();
                var targetList = allTargets.Any() ? string.Join(", ", allTargets) : "(none)";

                throw new System.Exception(
                    $"Failed to resolve target names from pbxproj.\n" +
                    $"  .xcodeproj: {xcodeProjectName}\n" +
                    $"  Available targets: {targetList}\n" +
                    $"  Framework GUID: {frameworkGuid ?? "(null)"}\n" +
                    $"  App GUID: {appGuid ?? "(null)"}\n\n" +
                    $"解决方案：\n" +
                    $"  1. 设置环境变量强制指定 target：\n" +
                    $"     export {ENV_FRAMEWORK_TARGET}=YourFrameworkTarget\n" +
                    $"     export {ENV_APP_TARGET}=YourAppTarget\n" +
                    $"  2. 检查导出工程是否包含 UnityFramework 和 Unity-iPhone（或对应的团结引擎 target）"
                );
            }

            Debug.Log($"[DirichletMediation] Resolved targets:");
            Debug.Log($"  Framework target: {frameworkTargetName} (GUID: {frameworkGuid})");
            Debug.Log($"  App target: {appTargetName} (GUID: {appGuid})");

            return new TargetInfo
            {
                FrameworkTargetName = frameworkTargetName,
                AppTargetName = appTargetName,
                FrameworkTargetGuid = frameworkGuid,
                AppTargetGuid = appGuid
            };
        }

        /// <summary>
        /// 从 GUID 反查 target name
        /// Unity PBXProject API 没有直接提供此方法，需要通过 TargetGuidByName 反向验证
        /// </summary>
        private static string GetTargetNameByGuid(PBXProject pbxProject, string targetGuid, string projectFilePath)
        {
            if (string.IsNullOrEmpty(targetGuid))
            {
                return null;
            }

            // 最可靠的方式：直接从 pbxproj 解析 GUID -> name
            var nativeTargetMap = GetNativeTargetGuidToNameMap(projectFilePath);
            if (nativeTargetMap.TryGetValue(targetGuid, out var parsedName) && !string.IsNullOrEmpty(parsedName))
            {
                return parsedName;
            }

            // 常见的 Unity/Tuanjie target 名称
            var commonTargetNames = new[]
            {
                "UnityFramework", "Unity-iPhone",
                "TuanjieFramework", "Tuanjie-iPhone",
                "GameAssembly", // 部分定制工程
            };

            foreach (var name in commonTargetNames)
            {
                try
                {
                    var guid = pbxProject.TargetGuidByName(name);
                    if (guid == targetGuid)
                    {
                        return name;
                    }
                }
                catch
                {
                    // Target 不存在，继续尝试下一个
                }
            }

            // 如果常见名称都不匹配，尝试从文件中解析所有 target
            var allTargets = GetAllTargetNames(projectFilePath);
            foreach (var name in allTargets)
            {
                try
                {
                    var guid = pbxProject.TargetGuidByName(name);
                    if (guid == targetGuid)
                    {
                        return name;
                    }
                }
                catch
                {
                    // 继续尝试
                }
            }

            return null;
        }

        /// <summary>
        /// 从 project.pbxproj 解析 PBXNativeTarget 的 GUID -> name 映射
        /// </summary>
        private static System.Collections.Generic.Dictionary<string, string> GetNativeTargetGuidToNameMap(string projectFilePath)
        {
            var result = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

            try
            {
                // 形如：
                // 9D25AB9C213FB47800354C27 /* UnityFramework */ = {
                var entryStartRegex = new System.Text.RegularExpressions.Regex(
                    @"^\s*([0-9A-Fa-f]{24})\s*/\*\s*(.*?)\s*\*/\s*=\s*\{\s*$",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant
                );

                string currentGuid = null;
                string currentName = null;
                var braceDepth = 0;
                var isNativeTarget = false;

                foreach (var line in File.ReadLines(projectFilePath))
                {
                    if (braceDepth == 0)
                    {
                        var m = entryStartRegex.Match(line);
                        if (!m.Success)
                        {
                            continue;
                        }

                        currentGuid = m.Groups[1].Value.Trim();
                        currentName = m.Groups[2].Value.Trim();
                        braceDepth = CountBraceDelta(line); // start line includes '{'
                        isNativeTarget = false;
                        continue;
                    }

                    if (!isNativeTarget && line.IndexOf("isa = PBXNativeTarget;", System.StringComparison.Ordinal) >= 0)
                    {
                        isNativeTarget = true;
                    }

                    braceDepth += CountBraceDelta(line);

                    if (braceDepth <= 0)
                    {
                        if (isNativeTarget && !string.IsNullOrEmpty(currentGuid) && !string.IsNullOrEmpty(currentName))
                        {
                            result[currentGuid] = currentName;
                        }

                        currentGuid = null;
                        currentName = null;
                        braceDepth = 0;
                        isNativeTarget = false;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return result;
        }

        private static int CountBraceDelta(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return 0;
            }

            var delta = 0;
            foreach (var c in line)
            {
                if (c == '{') delta++;
                else if (c == '}') delta--;
            }
            return delta;
        }

        /// <summary>
        /// 从 pbxproj 文件解析所有 target 名称（用于诊断输出）
        /// </summary>
        private static string[] GetAllTargetNames(string projectFilePath)
        {
            var map = GetNativeTargetGuidToNameMap(projectFilePath);
            return map.Values.Distinct().ToArray();
        }

        /// <summary>
        /// 生成 Podfile
        /// 所有 pods 统一放到 Framework target（UnityFramework），因为：
        /// - Bridge (.mm) 和 DirichletMediationSDK 都编译/运行在 Framework target 中
        /// - SDK 通过 NSClassFromString 动态查找 Adapter 类，Adapter 必须与 SDK 在同一 target
        ///   否则静态库符号可能被 strip 或不在同一二进制中，导致运行时找不到类
        /// </summary>
        private static void GeneratePodfile(string projectPath, TargetInfo targetInfo)
        {
            // Read adapter settings from EditorPrefs
            // Note: DirichletAdSDK (DRA adapter) is always enabled as core SDK
            var enableCsj = EditorPrefs.GetBool("Dirichlet.iOS.EnableCSJ", true);
            var enableGdt = EditorPrefs.GetBool("Dirichlet.iOS.EnableGDT", true);

            var podfileContent = new StringBuilder();
            podfileContent.AppendLine("# Generated by Dirichlet Mediation Unity Plugin");
            podfileContent.AppendLine("#");
            podfileContent.AppendLine($"# Framework target: {targetInfo.FrameworkTargetName}");
            podfileContent.AppendLine($"# App target: {targetInfo.AppTargetName}");
            podfileContent.AppendLine();
            podfileContent.AppendLine("source 'https://cdn.cocoapods.org/'");
            podfileContent.AppendLine();
            podfileContent.AppendLine($"platform :ios, '{MinIOSVersion}'");
            podfileContent.AppendLine("use_frameworks! :linkage => :static");
            podfileContent.AppendLine();

            // 所有 pods 统一放到 Framework target
            // SDK 通过 NSClassFromString 查找 Adapter 类，必须在同一 target 中
            podfileContent.AppendLine($"target '{targetInfo.FrameworkTargetName}' do");
            podfileContent.AppendLine($"  pod 'DirichletMediationSDK', '{SDKVersion}'");

            if (enableCsj)
            {
                podfileContent.AppendLine($"  pod 'DirichletMediationAdapterCSJ', '{SDKVersion}'");
            }

            if (enableGdt)
            {
                podfileContent.AppendLine($"  pod 'DirichletMediationAdapterGDT', '{SDKVersion}'");
            }

            // DirichletAdSDK (DRA adapter) is always included as core SDK
            podfileContent.AppendLine($"  pod 'DirichletMediationAdapterDRA', '{SDKVersion}'");

            podfileContent.AppendLine("end");
            podfileContent.AppendLine();

            // Post-install: 基础构建设置
            podfileContent.AppendLine("post_install do |installer|");
            podfileContent.AppendLine("  installer.pods_project.targets.each do |target|");
            podfileContent.AppendLine("    target.build_configurations.each do |config|");
            podfileContent.AppendLine($"      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '{MinIOSVersion}'");
            podfileContent.AppendLine("      config.build_settings['ENABLE_BITCODE'] = 'NO'");
            podfileContent.AppendLine("    end");
            podfileContent.AppendLine("  end");
            podfileContent.AppendLine("end");

            var podfilePath = Path.Combine(projectPath, "Podfile");
            File.WriteAllText(podfilePath, podfileContent.ToString());

            Debug.Log($"[DirichletMediation] Generated Podfile at {podfilePath}");
            Debug.Log($"[DirichletMediation] All pods allocated to {targetInfo.FrameworkTargetName} (CSJ={enableCsj}, GDT={enableGdt}, DRA=always)");
        }

        /// <summary>
        /// 检测 Xcode 项目名称，兼容 Unity/Tuanjie/自定义项目
        /// 动态搜索目录下的 .xcodeproj 文件
        /// </summary>
        private static string DetectXcodeProjectName(string projectPath)
        {
            // 搜索所有 .xcodeproj 目录
            var xcodeprojDirs = Directory.GetDirectories(projectPath, "*.xcodeproj");
            
            if (xcodeprojDirs.Length == 0)
            {
                throw new System.Exception(
                    $"No .xcodeproj found in: {projectPath}\n" +
                    "请确认 Unity 导出路径正确，且导出已完成。"
                );
            }

            // 获取第一个 xcodeproj 的名称（不含扩展名）
            var xcodeprojPath = xcodeprojDirs[0];
            var xcodeprojName = Path.GetFileNameWithoutExtension(xcodeprojPath);
            
            Debug.Log($"[DirichletMediation] Detected Xcode project: {xcodeprojName}");
            return xcodeprojName;
        }

        /// <summary>
        /// 修改 Xcode 工程设置（使用已解析的 target 信息）
        /// </summary>
        private static void ModifyXcodeProject(string projectPath, TargetInfo targetInfo)
        {
            var xcodeProjectName = DetectXcodeProjectName(projectPath);
            var projectFilePath = Path.Combine(projectPath, $"{xcodeProjectName}.xcodeproj/project.pbxproj");
            var pbxProject = new PBXProject();
            pbxProject.ReadFromFile(projectFilePath);

            // 使用已解析的 target GUID，或通过名称反查
            var targetGuid = targetInfo.FrameworkTargetGuid;
            var mainTargetGuid = targetInfo.AppTargetGuid;

            if (string.IsNullOrEmpty(targetGuid))
            {
                targetGuid = pbxProject.TargetGuidByName(targetInfo.FrameworkTargetName);
            }
            if (string.IsNullOrEmpty(mainTargetGuid))
            {
                mainTargetGuid = pbxProject.TargetGuidByName(targetInfo.AppTargetName);
            }

            Debug.Log($"[DirichletMediation] Modifying Xcode project:");
            Debug.Log($"  Framework target: {targetInfo.FrameworkTargetName} (GUID: {targetGuid})");
            Debug.Log($"  App target: {targetInfo.AppTargetName} (GUID: {mainTargetGuid})");

            // NOTE: System frameworks (AdSupport, AVFoundation, WebKit, CoreVideo, etc.) 
            // are declared in SDK podspecs and will be automatically linked by CocoaPods.
            // No need to manually add them here.
            // - DirichletAdSDK.podspec: AdSupport, SystemConfiguration, Security
            // - DirichletCoreSDK.podspec: SystemConfiguration, Security
            // - DirichletMediationAdapterCSJ.podspec: CoreVideo
            // - Third-party SDKs (Ads-CN, GDTMobSDK) declare their own framework dependencies.

            // Set build settings for framework target
            pbxProject.SetBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");
            pbxProject.SetBuildProperty(targetGuid, "CLANG_ENABLE_MODULES", "YES");
            
            // Set build settings for main target
            pbxProject.SetBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
            pbxProject.SetBuildProperty(mainTargetGuid, "CLANG_ENABLE_MODULES", "YES");
            pbxProject.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            
            // Set LD_RUNPATH_SEARCH_PATHS to allow dynamic frameworks to be found at runtime
            pbxProject.SetBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks");
            pbxProject.SetBuildProperty(targetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks @loader_path/Frameworks");

            pbxProject.WriteToFile(projectFilePath);
            Debug.Log("[DirichletMediation] Modified Xcode project settings");
        }

        /// <summary>
        /// GDTMobSDK 提供的是预编译动态库（GDTMobSDK.framework, Tquic.framework），
        /// 必须 embed 到 app bundle 中才能在运行时加载。
        /// 由于所有 pods 都在 Framework target 上，CocoaPods 不会自动 embed 到 app target，
        /// 需要在 pod install 之后手动处理。
        /// 依赖 UnityEditor.iOS.Xcode.Extensions 中的 AddFileToEmbedFrameworks 扩展方法。
        /// </summary>
        private static void EmbedGDTDynamicFrameworks(string projectPath, TargetInfo targetInfo)
        {
            var enableGdt = EditorPrefs.GetBool("Dirichlet.iOS.EnableGDT", true);
            if (!enableGdt)
            {
                Debug.Log("[DirichletMediation] GDT adapter disabled, skipping dynamic framework embedding");
                return;
            }

            var xcodeProjectName = DetectXcodeProjectName(projectPath);
            var projectFilePath = Path.Combine(projectPath, $"{xcodeProjectName}.xcodeproj/project.pbxproj");
            var pbxProject = new PBXProject();
            pbxProject.ReadFromFile(projectFilePath);

            var mainTargetGuid = targetInfo.AppTargetGuid;
            if (string.IsNullOrEmpty(mainTargetGuid))
            {
                mainTargetGuid = pbxProject.TargetGuidByName(targetInfo.AppTargetName);
            }

            // GDTMobSDK 的动态框架列表
            var dynamicFrameworkNames = new[] { "GDTMobSDK.framework", "Tquic.framework" };
            var podsDir = Path.Combine(projectPath, "Pods");
            var embedded = 0;

            foreach (var frameworkName in dynamicFrameworkNames)
            {
                var frameworkPath = FindDynamicFramework(podsDir, frameworkName);
                if (string.IsNullOrEmpty(frameworkPath))
                {
                    Debug.LogWarning($"[DirichletMediation] Dynamic framework not found: {frameworkName}");
                    continue;
                }

                var relativePath = frameworkPath.StartsWith(projectPath)
                    ? frameworkPath.Substring(projectPath.Length + 1)
                    : frameworkPath;

                var fileGuid = pbxProject.AddFile(relativePath, "Frameworks/" + frameworkName);
                PBXProjectExtensions.AddFileToEmbedFrameworks(pbxProject, mainTargetGuid, fileGuid);
                embedded++;
                Debug.Log($"[DirichletMediation] Embedded dynamic framework: {frameworkName}");
            }

            if (embedded > 0)
            {
                pbxProject.WriteToFile(projectFilePath);
                Debug.Log($"[DirichletMediation] Embedded {embedded} GDT dynamic frameworks into {targetInfo.AppTargetName}");
            }
        }

        /// <summary>
        /// 在 Pods 目录中递归搜索指定的 .framework（优先 ios-arm64 真机 slice）
        /// </summary>
        private static string FindDynamicFramework(string podsDir, string frameworkName)
        {
            if (!Directory.Exists(podsDir))
            {
                return null;
            }

            var candidates = Directory.GetDirectories(podsDir, frameworkName, SearchOption.AllDirectories);

            foreach (var candidate in candidates)
            {
                if (candidate.Contains("ios-arm64") && !candidate.Contains("simulator"))
                {
                    return candidate;
                }
            }

            foreach (var candidate in candidates)
            {
                if (!candidate.Contains("simulator"))
                {
                    return candidate;
                }
            }

            return candidates.Length > 0 ? candidates[0] : null;
        }

        private static void ModifyInfoPlist(string projectPath)
        {
            var plistPath = Path.Combine(projectPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var rootDict = plist.root;

            // Note: The following Info.plist keys should be configured by the developer manually
            // to avoid potential App Store review issues:
            // - NSAppTransportSecurity: Configure based on your app's network requirements
            // - NSUserTrackingUsageDescription: Required for iOS 14+ IDFA access, use your custom description
            // - NSLocationWhenInUseUsageDescription: Only add if your app uses location services
            //
            // Reference: https://ssp.dirichlet.cn/docs/dirichlet-mediation-sdk/dirichlet-mediation-sdk-guide-ios/

            // Add SKAdNetwork identifiers for attribution tracking
            AddSKAdNetworkIds(rootDict);

            plist.WriteToFile(plistPath);
            Debug.Log("[DirichletMediation] Modified Info.plist (SKAdNetwork IDs only)");
        }

        private static void AddSKAdNetworkIds(PlistElementDict rootDict)
        {
            if (rootDict.values.ContainsKey("SKAdNetworkItems"))
            {
                Debug.Log("[DirichletMediation] SKAdNetworkItems already exists, skipping");
                return;
            }

            // Add SKAdNetwork IDs required by CSJ (Pangle/穿山甲)
            // Reference: https://www.csjplatform.com/supportcenter/5377
            var skAdNetworkArray = rootDict.CreateArray("SKAdNetworkItems");
            
            var commonSkAdNetworkIds = new[]
            {
                "238da6jt44.skadnetwork",  // 穿山甲 SKAdNetwork ID
                "x2jnk7ly8j.skadnetwork",  // 穿山甲 SKAdNetwork ID
                "22mmun2rn5.skadnetwork"   // 穿山甲 SKAdNetwork ID
            };

            foreach (var skAdNetworkId in commonSkAdNetworkIds)
            {
                var dict = skAdNetworkArray.AddDict();
                dict.SetString("SKAdNetworkIdentifier", skAdNetworkId);
            }

            Debug.Log($"[DirichletMediation] Added {commonSkAdNetworkIds.Length} SKAdNetwork IDs to Info.plist");
        }

        private static void RunPodInstall(string projectPath)
        {
            var podfilePath = Path.Combine(projectPath, "Podfile");
            if (!File.Exists(podfilePath))
            {
                Debug.LogWarning("[DirichletMediation] Podfile not found, skipping pod install");
                return;
            }

            Debug.Log("[DirichletMediation] Running 'pod install'...");
            Debug.Log("[DirichletMediation] Note: This may take a few minutes on first build or when adapters change.");

            try
            {
                // Try to find pod executable
                var podPath = FindPodExecutable();
                if (string.IsNullOrEmpty(podPath))
                {
                    Debug.LogWarning("[DirichletMediation] CocoaPods not found. Please install CocoaPods:");
                    Debug.LogWarning("  sudo gem install cocoapods");
                    Debug.LogWarning("Then run 'pod install' manually in:");
                    Debug.LogWarning($"  {projectPath}");
                    return;
                }

                // 优先不做 repo update（更快、更稳定）；如遇到“找不到 podspec”再退化为 --repo-update
                var firstArgs = "install";
                var exitCode = RunPodCommand(podPath, firstArgs, projectPath, out var output, out var error);

                if (exitCode != 0 && ShouldRetryWithRepoUpdate(output, error))
                {
                    Debug.LogWarning("[DirichletMediation] pod install failed (missing specs suspected). Retrying with --repo-update...");
                    var retryArgs = "install --repo-update";
                    exitCode = RunPodCommand(podPath, retryArgs, projectPath, out output, out error);
                }

                if (exitCode == 0)
                {
                    Debug.Log("[DirichletMediation] pod install completed successfully");
                    if (!string.IsNullOrEmpty(output) && output.Length < 2000)
                    {
                        Debug.Log($"Output:\n{output}");
                    }
                    return;
                }

                Debug.LogError($"[DirichletMediation] pod install failed with exit code {exitCode}");
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"Error:\n{error}");
                }
                if (!string.IsNullOrEmpty(output))
                {
                    Debug.LogError($"Output:\n{output}");
                }

                if (IsCocoaPodsCdnHttp2Error(output, error))
                {
                    Debug.LogWarning(
                        "[DirichletMediation] Detected CocoaPods CDN HTTP/2 error (e.g. 'Error in the HTTP2 framing layer'). " +
                        "This is usually a network/proxy/SSL issue. Try rerun without repo update, switch network, upgrade CocoaPods, " +
                        "or run 'pod install' manually. (Repo doc: docs/FAQ/CocoaPods-SSL证书问题排查SOP.md)"
                    );
                }

                // 失败即中断：避免导出工程处于“半配置”状态，后续 Xcode build 更难排查
                throw new System.Exception($"pod install failed with exit code {exitCode}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DirichletMediation] Failed to run pod install: {ex.Message}");
                Debug.LogWarning("[DirichletMediation] Please run 'pod install' manually in the Xcode project directory:");
                Debug.LogWarning($"  cd {projectPath}");
                Debug.LogWarning("  pod install");
                throw;
            }
        }

        private static int RunPodCommand(string podPath, string arguments, string projectPath, out string output, out string error)
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = podPath,
                Arguments = arguments,
                WorkingDirectory = projectPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Set UTF-8 encoding to avoid CocoaPods encoding issues
            processInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static bool ShouldRetryWithRepoUpdate(string output, string error)
        {
            var combined = (output ?? string.Empty) + "\n" + (error ?? string.Empty);

            // 常见“本地 specs 未更新导致无法解析依赖”的报错关键字
            return combined.Contains("Unable to find a specification for")
                   || combined.Contains("None of your spec sources contain a spec satisfying")
                   || combined.Contains("spec satisfying")
                   || combined.Contains("No podspec found for")
                   || combined.Contains("could not find compatible versions for pod");
        }

        private static bool IsCocoaPodsCdnHttp2Error(string output, string error)
        {
            var combined = (output ?? string.Empty) + "\n" + (error ?? string.Empty);
            return combined.Contains("CDN: trunk Repo update failed")
                   || combined.Contains("Error in the HTTP2 framing layer")
                   || combined.Contains("URL couldn't be downloaded: https://cdn.cocoapods.org/");
        }

        private static string FindPodExecutable()
        {
            // 1. POD_BINARY 环境变量优先，方便 CI 或接入方手动配置
            var envValue = System.Environment.GetEnvironmentVariable("POD_BINARY");
            if (!string.IsNullOrEmpty(envValue))
            {
                var expanded = envValue.Trim();
                if (File.Exists(expanded))
                {
                    Debug.Log($"[DirichletMediation] Found pod via POD_BINARY: {expanded}");
                    return expanded;
                }

                Debug.LogWarning($"[DirichletMediation] POD_BINARY points to non-existing path: {expanded}");
            }

            var possiblePaths = new[]
            {
                "/usr/local/bin/pod",
                "/opt/homebrew/bin/pod",
                "/usr/bin/pod",
                Path.Combine(System.Environment.GetEnvironmentVariable("HOME") ?? "", ".rbenv/shims/pod"),
                Path.Combine(System.Environment.GetEnvironmentVariable("HOME") ?? "", ".rvm/wrappers/default/pod")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Debug.Log($"[DirichletMediation] Found pod at: {path}");
                    return path;
                }
            }

            // Try using 'which pod'
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/env",
                    Arguments = "which pod",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    var error = process.StandardError.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                    {
                        Debug.Log($"[DirichletMediation] Found pod via 'which': {output}");
                        return output;
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning($"[DirichletMediation] 'which pod' failed: {error}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation] Failed to resolve pod via 'which': {ex.Message}");
            }

            return null;
        }

        private const string PrefKeyDefaultsInitialized = "Dirichlet.Mediation.AdapterSettings.DefaultsInitialized.v1";

        private static void EnsureDefaultAdapterPrefs()
        {
            if (EditorPrefs.GetBool(PrefKeyDefaultsInitialized, false))
            {
                return;
            }

            // 默认“勾上”：CSJ/GDT（iOS）
            // 注意：如接入方需要关闭，可在 Adapter Settings 窗口中手动取消勾选。
            EditorPrefs.SetBool("Dirichlet.iOS.EnableCSJ", true);
            EditorPrefs.SetBool("Dirichlet.iOS.EnableGDT", true);

            // 同时保证 Android 默认一致（无副作用）
            EditorPrefs.SetBool("Dirichlet.Android.EnableCSJ", true);
            EditorPrefs.SetBool("Dirichlet.Android.EnableGDT", true);

            EditorPrefs.SetBool(PrefKeyDefaultsInitialized, true);
        }
    }
}

#endif
