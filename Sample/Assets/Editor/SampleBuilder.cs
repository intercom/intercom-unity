using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SampleBuilder
{
    public static void BuildIosSimulator()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        SetSimulatorArchitecture(1);
        Build(BuildTarget.iOS, "Builds/iOS-Simulator");
    }

    // 0 = x86_64, 1 = arm64. Unity defaults to x86_64, which emits an x86_64-only
    // libiPhone-lib.dylib and fails to link against an arm64 simulator slice.
    private static void SetSimulatorArchitecture(int value)
    {
        var settings = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
        if (settings == null)
        {
            throw new Exception("Could not load PlayerSettings");
        }

        var so = new UnityEditor.SerializedObject(settings);
        var prop = so.FindProperty("iOSSimulatorArchitecture");
        if (prop == null)
        {
            throw new Exception("iOSSimulatorArchitecture property not found");
        }

        prop.intValue = value;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[SampleBuilder] iOSSimulatorArchitecture = {value}");
    }

    // Read from the package's dependency manifest so the assertion can never drift from
    // the version the wrapper actually pins (the old hardcoded constant went stale on
    // every native SDK bump).
    private static string AndroidSdkSpec
    {
        get
        {
            const string deps = "Packages/com.intercom.unity/Editor/IntercomDependencies.xml";
            var match = System.Text.RegularExpressions.Regex.Match(
                System.IO.File.ReadAllText(deps),
                @"io\.intercom\.android:intercom-sdk-base:[0-9]+\.[0-9]+\.[0-9]+");
            if (!match.Success)
            {
                throw new Exception($"Could not find intercom-sdk-base coordinate in {deps}");
            }
            return match.Value;
        }
    }

    public static void BuildAndroid()
    {
        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        // Force GLES3: Vulkan-first (Unity's Android default) renders a black screen on the
        // Android Emulator while audio/scripts keep running. GLES3 renders correctly on the
        // emulator and on physical devices.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
            new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        // Drives compileSdk (**APIVERSION**) too. Intercom 18.x's AAR metadata requires 36.
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
        AssetDatabase.SaveAssets();

        ResolveAndroidDependencies();
        Build(BuildTarget.Android, "Builds/Android/UnitySampleApp.apk");
    }

    // Auto-resolution does not reliably fire in batchmode, which would silently build against
    // whatever version was last injected into mainTemplate.gradle.
    private static void ResolveAndroidDependencies()
    {
        var resolver = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("GooglePlayServices.PlayServicesResolver"))
            .FirstOrDefault(t => t != null);
        if (resolver == null)
        {
            throw new Exception("EDM4U PlayServicesResolver not found");
        }

        var resolveSync = resolver.GetMethod("ResolveSync", new[] { typeof(bool) });
        if (resolveSync == null)
        {
            throw new Exception("EDM4U ResolveSync(bool) not found");
        }

        Debug.Log("[SampleBuilder] Forcing EDM4U resolution");
        resolveSync.Invoke(null, new object[] { true });

        var template = "Assets/Plugins/Android/mainTemplate.gradle";
        var expected = AndroidSdkSpec;
        var contents = System.IO.File.ReadAllText(template);
        if (!contents.Contains(expected))
        {
            throw new Exception($"{template} does not contain {expected} after resolution");
        }
        Debug.Log($"[SampleBuilder] Resolved {expected}");
    }

    public static void BuildIosDevice()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        Build(BuildTarget.iOS, "Builds/iOS-Device");
    }

    private static void Build(BuildTarget target, string path)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            throw new Exception("No enabled scenes in Build Settings");
        }

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = path,
            target = target,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        Debug.Log($"[SampleBuilder] {summary.result} -> {summary.outputPath} ({summary.totalErrors} errors)");
        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
        EditorApplication.Exit(0);
    }
}
