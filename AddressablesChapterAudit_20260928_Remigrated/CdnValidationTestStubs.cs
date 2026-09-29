
namespace UnityEngine { public static class Debug { public static void Log(object x) {} } }
namespace UnityEditor {
 public class MenuItem:System.Attribute { public MenuItem(string s) {} }
 public static class PlayerSettings { public static string bundleVersion="1.1.3"; }
 public static class EditorUtility { public static void SetDirty(object x) {} }
 public static class AssetDatabase { public static void SaveAssets() {} public static void ImportAsset(string s) {} }
}
namespace UnityEditor.Build.Reporting { public class BuildReport {} }
namespace UnityEditor.Build { public interface IPreprocessBuildWithReport { int callbackOrder {get;} void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport r); } public class BuildFailedException:System.Exception { public BuildFailedException(string s):base(s) {} } }
namespace UnityEditor.AddressableAssets { public static class AddressableAssetSettingsDefaultObject { public static Settings.AddressableAssetSettings Settings; } }
namespace UnityEditor.AddressableAssets.Settings {
 public class ProfileValueReference { public string Value; public bool SetVariableByName(AddressableAssetSettings s,string n) { Value=s.profileSettings.GetValueByName(s.activeProfileId,n); return true; } public string GetValue(AddressableAssetSettings s) { return Value; } }
 public class Profiles {
  public System.Collections.Generic.Dictionary<string,string> Values=new System.Collections.Generic.Dictionary<string,string>();
  public string GetValueByName(string id,string key) { return Values.ContainsKey(key)?Values[key]:null; }
  public string EvaluateString(string id,string value) { return value; }
  public void SetValue(string id,string key,string value) { Values[key]=value; }
 }
 public class AddressableAssetGroup { public string Name="Chapter1-Remote"; public GroupSchemas.BundledAssetGroupSchema Schema=new GroupSchemas.BundledAssetGroupSchema(); public T GetSchema<T>() where T:class {return Schema as T;} }
 public class AddressableAssetSettings {
  public const string kLocalBuildPath="Local.BuildPath",kLocalLoadPath="Local.LoadPath",kRemoteBuildPath="Remote.BuildPath",kRemoteLoadPath="Remote.LoadPath",kRemoteBuildPathValue="ServerData/[BuildTarget]",kLocalBuildPathValue="[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]",kLocalLoadPathValue="{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]";
  public string activeProfileId="default"; public Profiles profileSettings=new Profiles(); public bool BuildRemoteCatalog; public int CatalogRequestsTimeout=15;
  public System.Collections.Generic.List<AddressableAssetGroup> groups=new System.Collections.Generic.List<AddressableAssetGroup>();
  public ProfileValueReference RemoteCatalogBuildPath=new ProfileValueReference(),RemoteCatalogLoadPath=new ProfileValueReference();
 }
}
namespace UnityEditor.AddressableAssets.Settings.GroupSchemas { public class BundledAssetGroupSchema { public enum BundleNamingStyle { AppendHash, OnlyHash } public BundleNamingStyle BundleNaming; public bool IncludeInBuild=true; public int Timeout=10; public UnityEditor.AddressableAssets.Settings.ProfileValueReference BuildPath=new UnityEditor.AddressableAssets.Settings.ProfileValueReference(),LoadPath=new UnityEditor.AddressableAssets.Settings.ProfileValueReference(); } }
public static class CdnValidationChecks {
 public static string Run() {
  int count=0;
  foreach(var test in new[]{"local","local_catalog","mixed","local_remote_group","local_remote_catalog","remote","wrong_version","remote_timeout","remote_taptap"}) {
   var s=new UnityEditor.AddressableAssets.Settings.AddressableAssetSettings();
   bool local=test.StartsWith("local")||test=="mixed";
   string b=local?UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.kLocalBuildPathValue:"ServerData/[BuildTarget]";
   string l=local?UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.kLocalLoadPathValue:"https://res.huizhihudongtech.com/vn/sntzm2/1.1.3/[BuildTarget]";
   s.profileSettings.Values["Remote.BuildPath"]=b;s.profileSettings.Values["Remote.LoadPath"]=l;
   var g=new UnityEditor.AddressableAssets.Settings.AddressableAssetGroup();g.Schema.BuildPath.Value=b;g.Schema.LoadPath.Value=l;s.groups.Add(g);
   s.BuildRemoteCatalog=!local||test=="local_catalog"||test=="local_remote_catalog";
   s.RemoteCatalogBuildPath.Value=b;s.RemoteCatalogLoadPath.Value=l;
   string version=test=="wrong_version"?"1.0.0":"1.1.3";
   string mini=local?"old unrelated CDN":"CDN: https://res.huizhihudongtech.com/vn/sntzm2/1.1.3/WebGL/";
   if(test=="mixed")s.profileSettings.Values["Remote.BuildPath"]="ServerData/[BuildTarget]";
   if(test=="local_remote_group")g.Schema.LoadPath.Value="https://example.com/";
   if(test=="local_remote_catalog")s.RemoteCatalogLoadPath.Value="https://example.com/";
   if(test=="remote_timeout")g.Schema.Timeout=0;
   if(test=="remote_taptap")mini="CDN: wrong";
   bool expected=test=="local"||test=="local_catalog"||test=="remote",passed=true;
   try { typeof(CdnBuildValidation).GetMethod("ValidateSettings",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{s,version,mini}); }
   catch(System.Reflection.TargetInvocationException e) { if(!(e.InnerException is UnityEditor.Build.BuildFailedException))throw;passed=false; }
   if(passed!=expected)throw new System.Exception("Unexpected result: "+test);count++;
  }
  return count+" behavior checks passed";
 }
}
