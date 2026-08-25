#if UNITY_ANDROID
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace Dirichlet.Mediation.Editor
{
    /// <summary>
    /// Post-processes the Gradle files to inject Dirichlet SDK dependencies.
    /// 
    /// This approach allows coexistence with other SDKs (e.g., TapSDK) by injecting
    /// dependencies into Unity-generated Gradle files rather than shipping static templates.
    /// </summary>
    public class DirichletGradlePostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string TAG = "[DirichletMediation]";
        
        // Marker comments to identify our injected content
        private const string DIRICHLET_DEPS_START = "// Dirichlet Mediation Dependencies Start";
        private const string DIRICHLET_DEPS_END = "// Dirichlet Mediation Dependencies End";
        private const string DIRICHLET_REPOS_START = "// Dirichlet Mediation Repositories Start";
        private const string DIRICHLET_REPOS_END = "// Dirichlet Mediation Repositories End";
        
        public int callbackOrder => 100; // Run after EDM4U (which uses lower values)

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var enableCsj = EditorPrefs.GetBool("Dirichlet.Android.EnableCSJ", true);
            var enableGdt = EditorPrefs.GetBool("Dirichlet.Android.EnableGDT", true);

            Debug.Log($"{TAG} Processing Gradle project at: {path}");
            Debug.Log($"{TAG} CSJ enabled: {enableCsj}, GDT enabled: {enableGdt}");

            ProcessBuildGradle(path, enableCsj, enableGdt);
            ProcessSettingsGradle(path, enableCsj, enableGdt);
        }

        private void ProcessBuildGradle(string projectPath, bool enableCsj, bool enableGdt)
        {
            // Unity 2019.3+: projectPath is unityLibrary folder, build.gradle is directly inside
            // Unity 2019.2 and below: projectPath might be the root, need to search
            var gradlePath = Path.Combine(projectPath, "build.gradle");
            
            if (!File.Exists(gradlePath))
            {
                // Fallback: try to find build.gradle in subdirectories
                var searchPaths = new[]
                {
                    Path.Combine(projectPath, "unityLibrary", "build.gradle"),
                    Path.Combine(projectPath, "src", "main", "build.gradle")
                };
                
                foreach (var path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        gradlePath = path;
                        break;
                    }
                }
            }
            
            if (!File.Exists(gradlePath))
            {
                Debug.LogWarning($"{TAG} Could not find build.gradle at {projectPath}");
                return;
            }
            
            Debug.Log($"{TAG} Found build.gradle at: {gradlePath}");

            var content = File.ReadAllText(gradlePath);
            Debug.Log($"{TAG} Original build.gradle length: {content.Length}");

            // Remove any previously injected content (for clean re-injection)
            content = RemoveInjectedContent(content, DIRICHLET_DEPS_START, DIRICHLET_DEPS_END);
            content = RemoveInjectedContent(content, DIRICHLET_REPOS_START, DIRICHLET_REPOS_END);

            // Inject repositories
            content = InjectRepositories(content, enableCsj, enableGdt);

            // Inject dependencies
            content = InjectDependencies(content, enableCsj, enableGdt);

            File.WriteAllText(gradlePath, content);
            Debug.Log($"{TAG} Updated build.gradle with Dirichlet Mediation dependencies");
        }

        private string InjectRepositories(string content, bool enableCsj, bool enableGdt)
        {
            // Check if our repos are already injected
            if (content.Contains(DIRICHLET_REPOS_START))
            {
                return content;
            }
            
            var reposBlock = new StringBuilder();
            reposBlock.AppendLine(DIRICHLET_REPOS_START);
            reposBlock.AppendLine("    google()");
            reposBlock.AppendLine("    mavenCentral()");
            reposBlock.AppendLine("    flatDir {");
            reposBlock.AppendLine("        dirs 'libs', 'DirichletMediation/libs'");
            reposBlock.AppendLine("    }");
            
            if (enableCsj)
            {
                reposBlock.AppendLine("    maven { url 'https://artifact.bytedance.com/repository/pangle' }");
            }
            if (enableGdt)
            {
                reposBlock.AppendLine("    maven { url 'https://mirrors.tencent.com/nexus/repository/maven-public/' }");
            }
            
            reposBlock.AppendLine($"    {DIRICHLET_REPOS_END}");
            
            // Try to find repositories block and inject after opening brace
            var reposPattern = new Regex(@"(repositories\s*\{)");
            if (reposPattern.IsMatch(content))
            {
                content = reposPattern.Replace(content, m => 
                    m.Groups[1].Value + "\n    " + reposBlock.ToString(), 1);
                Debug.Log($"{TAG} Injected repositories block");
            }
            else
            {
                Debug.LogWarning($"{TAG} Could not find repositories block, adding one");
                var applyPattern = new Regex(@"(apply plugin:\s*'com\.android\.library'[^\n]*\n)");
                if (applyPattern.IsMatch(content))
                {
                    content = applyPattern.Replace(content, m =>
                        m.Groups[1].Value + "\nrepositories {\n    " + reposBlock.ToString() + "}\n", 1);
                }
            }
            
            return content;
        }

        private string InjectDependencies(string content, bool enableCsj, bool enableGdt)
        {
            // Check if our deps are already injected
            if (content.Contains(DIRICHLET_DEPS_START))
            {
                return content;
            }
            
            var depsBlock = new StringBuilder();
            depsBlock.AppendLine(DIRICHLET_DEPS_START);
            
            // Core Mediation AAR
            depsBlock.AppendLine("    implementation(name: 'DirichletAD_Mediation_4.2.4.3', ext: 'aar')");
            
            // CSJ (穿山甲) Adapter and SDK
            if (enableCsj)
            {
                depsBlock.AppendLine("    implementation(name: 'DirichletAD_CSJ_Adapter_4.2.4.3', ext: 'aar')");
                depsBlock.AppendLine("    implementation(name: 'open_ad_sdk_7.4.2.2', ext: 'aar')");
            }
            
            // GDT (广点通) Adapter and SDK
            if (enableGdt)
            {
                depsBlock.AppendLine("    implementation(name: 'DirichletAD_GDT_Adapter_4.2.4.3', ext: 'aar')");
                depsBlock.AppendLine("    implementation(name: 'GDTSDK.unionNormal.4.671.1541', ext: 'aar')");
            }
            
            // Maven dependencies (required for SDK functionality)
            depsBlock.AppendLine("    implementation 'com.android.support:recyclerview-v7:28.0.0'");
            depsBlock.AppendLine("    implementation 'com.github.bumptech.glide:glide:4.9.0'");
            depsBlock.AppendLine("    implementation 'com.android.support:support-v4:28.0.0'");
            depsBlock.AppendLine("    implementation 'com.android.support:support-annotations:28.0.0'");
            depsBlock.AppendLine("    implementation 'com.android.support:appcompat-v7:28.0.0'");
            depsBlock.AppendLine("    implementation 'com.squareup.okhttp3:okhttp:3.12.1'");
            
            depsBlock.AppendLine($"    {DIRICHLET_DEPS_END}");
            
            // Find dependencies block and inject after opening brace
            var depsPattern = new Regex(@"(dependencies\s*\{)");
            if (depsPattern.IsMatch(content))
            {
                content = depsPattern.Replace(content, m => 
                    m.Groups[1].Value + "\n    " + depsBlock.ToString(), 1);
                Debug.Log($"{TAG} Injected dependencies block");
            }
            else
            {
                Debug.LogWarning($"{TAG} Could not find dependencies block");
            }
            
            return content;
        }

        private void ProcessSettingsGradle(string projectPath, bool enableCsj, bool enableGdt)
        {
            var parentDir = Directory.GetParent(projectPath)?.FullName;
            if (string.IsNullOrEmpty(parentDir))
            {
                Debug.LogWarning($"{TAG} Could not get parent directory");
                return;
            }
            
            var settingsPath = Path.Combine(parentDir, "settings.gradle");
            if (!File.Exists(settingsPath))
            {
                Debug.LogWarning($"{TAG} Could not find settings.gradle at {settingsPath}");
                return;
            }

            var content = File.ReadAllText(settingsPath);
            
            // Check if already injected
            if (content.Contains(DIRICHLET_REPOS_START))
            {
                Debug.Log($"{TAG} settings.gradle already has Dirichlet repos");
                return;
            }
            
            // Remove any previously injected content
            content = RemoveInjectedContent(content, DIRICHLET_REPOS_START, DIRICHLET_REPOS_END);
            
            var reposBlock = new StringBuilder();
            reposBlock.AppendLine(DIRICHLET_REPOS_START);
            reposBlock.AppendLine("        google()");
            reposBlock.AppendLine("        mavenCentral()");
            reposBlock.AppendLine("        flatDir {");
            reposBlock.AppendLine("            dirs \"${project(':unityLibrary').projectDir}/libs\", \"${project(':unityLibrary').projectDir}/DirichletMediation/libs\"");
            reposBlock.AppendLine("        }");
            
            if (enableCsj)
            {
                reposBlock.AppendLine("        maven { url 'https://artifact.bytedance.com/repository/pangle' }");
            }
            if (enableGdt)
            {
                reposBlock.AppendLine("        maven { url 'https://mirrors.tencent.com/nexus/repository/maven-public/' }");
            }
            
            reposBlock.AppendLine($"        {DIRICHLET_REPOS_END}");
            
            // Find dependencyResolutionManagement repositories block
            var reposPattern = new Regex(@"(dependencyResolutionManagement\s*\{[\s\S]*?repositories\s*\{)");
            if (reposPattern.IsMatch(content))
            {
                content = reposPattern.Replace(content, m => 
                    m.Groups[1].Value + "\n        " + reposBlock.ToString(), 1);
                File.WriteAllText(settingsPath, content);
                Debug.Log($"{TAG} Updated settings.gradle with Dirichlet Mediation repositories");
            }
            else
            {
                Debug.LogWarning($"{TAG} Could not find dependencyResolutionManagement repositories block in settings.gradle");
            }
        }

        private string RemoveInjectedContent(string content, string startMarker, string endMarker)
        {
            var pattern = new Regex($@"\s*{Regex.Escape(startMarker)}[\s\S]*?{Regex.Escape(endMarker)}\s*");
            return pattern.Replace(content, "\n");
        }
    }
}
#endif
