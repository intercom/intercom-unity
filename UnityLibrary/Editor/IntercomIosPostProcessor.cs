#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Intercom.Editor
{
    public static class IntercomIosPostProcessor
    {
        private const string PlistKey = "IntercomRegion";
        private const string SwiftPackageRepository = "intercom-ios-sp";
        private const string SwiftPackageProduct = "Intercom";

        private static readonly Regex ObjectStart = new Regex(@"^\s*([0-9A-Fa-f]{24})\b.*=\s*\{\s*$");

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[Intercom] Could not find Info.plist at '{plistPath}'; data hosting region not configured.");
                return;
            }

            Region region = IntercomRegionResolver.Resolve();

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString(PlistKey, region.ToString());
            plist.WriteToFile(plistPath);

            Debug.Log($"[Intercom] iOS build configured for {region} data hosting region ({PlistKey} = {region}).");

            ConfigureXcodeProject(pathToBuiltProject);
        }

        private static void ConfigureXcodeProject(string buildPath)
        {
            string projPath = PBXProject.GetPBXProjectPath(buildPath);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            string mainTarget = proj.GetUnityMainTargetGuid();
            string frameworkTarget = proj.GetUnityFrameworkTargetGuid();

            LinkSwiftPackageToMainTarget(proj, projPath, mainTarget);

            // The plugin's native code (UNUserNotificationCenter) needs UserNotifications linked.
            // The .mm sources compile into the UnityFramework target; the main target link is
            // harmless and keeps both targets consistent.
            proj.AddFrameworkToProject(frameworkTarget, "UserNotifications.framework", false);
            proj.AddFrameworkToProject(mainTarget, "UserNotifications.framework", false);

            // Reuse whatever entitlements file the project already points at (Unity, or another
            // plugin such as Sign in with Apple / App Groups) so we merge aps-environment into it
            // rather than orphaning the existing capabilities.
            string existingEntitlements = proj.GetBuildPropertyForAnyConfig(mainTarget, "CODE_SIGN_ENTITLEMENTS");

            proj.WriteToFile(projPath);

            AddPushCapability(projPath, mainTarget, existingEntitlements);
        }

        // EDM4U's iOS resolver adds the Intercom Swift Package (Editor/IntercomDependencies.xml)
        // to the UnityFramework target only. Intercom.xcframework is a dynamic framework, so
        // unless the app target links it too Xcode never copies it into the .app bundle and the
        // app dies at launch with "Library not loaded: @rpath/Intercom.framework/Intercom".
        // Xcode embeds and signs Swift Package products linked by an app target automatically,
        // so no explicit "Embed Frameworks" entry is added here — that would duplicate Xcode's
        // own copy and fail the build with "Multiple commands produce".
        private static void LinkSwiftPackageToMainTarget(PBXProject proj, string projPath, string mainTarget)
        {
            string[] lines = File.ReadAllLines(projPath);

            string packageGuid = FindSwiftPackageGuid(lines);
            if (string.IsNullOrEmpty(packageGuid))
            {
                Debug.LogWarning(
                    "[Intercom] The Intercom Swift Package is missing from the generated Xcode project, " +
                    "so it could not be linked to the app target. Check that EDM4U's iOS resolver ran; " +
                    "otherwise the app crashes at launch with \"Library not loaded: @rpath/Intercom.framework/Intercom\".");
                return;
            }

            if (TargetLinksPackage(lines, mainTarget, packageGuid))
            {
                return;
            }

            proj.AddRemotePackageFrameworkToProject(mainTarget, SwiftPackageProduct, packageGuid, false);
            Debug.Log("[Intercom] Linked the Intercom Swift Package to the app target so Xcode embeds it.");
        }

        private static string FindSwiftPackageGuid(string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("repositoryURL") && lines[i].Contains(SwiftPackageRepository))
                {
                    return EnclosingObjectGuid(lines, i);
                }
            }

            return null;
        }

        // Keeps an "Append" build — where Unity re-runs post-processors over the Xcode project
        // generated by a previous build — from linking the package a second time.
        private static bool TargetLinksPackage(string[] lines, string targetGuid, string packageGuid)
        {
            var products = new HashSet<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], @"^\s*package\s*=\s*" + packageGuid + @"\b"))
                {
                    string product = EnclosingObjectGuid(lines, i);
                    if (product != null)
                    {
                        products.Add(product);
                    }
                }
            }

            foreach (string guid in ReadGuidList(lines, targetGuid, "packageProductDependencies"))
            {
                if (products.Contains(guid))
                {
                    return true;
                }
            }

            return false;
        }

        private static string EnclosingObjectGuid(string[] lines, int index)
        {
            for (int i = index; i >= 0; i--)
            {
                Match match = ObjectStart.Match(lines[i]);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        // Reads the GUIDs out of a "<key> = ( ... );" list belonging to the given pbxproj object.
        private static IEnumerable<string> ReadGuidList(string[] lines, string objectGuid, string key)
        {
            int start = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                Match match = ObjectStart.Match(lines[i]);
                if (match.Success && match.Groups[1].Value == objectGuid)
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
            {
                yield break;
            }

            bool inList = false;
            for (int i = start + 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!inList)
                {
                    if (line == "};")
                    {
                        yield break;
                    }

                    inList = line.StartsWith(key + " = (", StringComparison.Ordinal);
                    continue;
                }

                if (line.StartsWith(")", StringComparison.Ordinal))
                {
                    yield break;
                }

                Match guid = Regex.Match(line, @"^([0-9A-Fa-f]{24})\b");
                if (guid.Success)
                {
                    yield return guid.Groups[1].Value;
                }
            }
        }

        // Uses ProjectCapabilityManager so the "Push Notifications" capability is actually
        // registered (not just a raw aps-environment string), the entitlement is merged into the
        // existing entitlements file, and automatic signing can provision a push-enabled profile.
        private static void AddPushCapability(string projPath, string mainTargetGuid, string existingEntitlements)
        {
            bool production = ResolveUsesProductionApns();
            string entitlementsFileName = string.IsNullOrEmpty(existingEntitlements)
                ? "Unity-iPhone.entitlements"
                : existingEntitlements;

            try
            {
                // Pass the main target by GUID (targetName must be null when a GUID is supplied).
                var capabilities = new ProjectCapabilityManager(projPath, entitlementsFileName, null, mainTargetGuid);
                capabilities.AddPushNotifications(development: !production);
                capabilities.WriteToFile();

                Debug.Log(
                    $"[Intercom] Registered Push Notifications capability " +
                    $"(aps-environment = {(production ? "production" : "development")}, entitlements = {entitlementsFileName}).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Intercom] Failed to register the Push Notifications capability: {e.Message}");
            }
        }

        // aps-environment must match the APNs environment of the build's provisioning profile:
        // "development" for Xcode/dev/ad-hoc builds, "production" for TestFlight and App Store.
        // Unity cannot infer this from the build, so it is read from the IntercomConfig asset
        // (UseProductionApnsEnvironment), defaulting to development when unset.
        private static bool ResolveUsesProductionApns()
        {
            string[] guids = AssetDatabase.FindAssets("t:IntercomConfig");
            if (guids == null || guids.Length == 0)
            {
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var config = AssetDatabase.LoadAssetAtPath<IntercomConfig>(path);
            return config != null && config.UseProductionApnsEnvironment;
        }
    }
}
#endif
