using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Intercom.Editor
{
    public static class IntercomRegionResolver
    {
        public static Region Resolve()
        {
            string[] guids = AssetDatabase.FindAssets("t:IntercomConfig");
            if (guids == null || guids.Length == 0)
            {
                Debug.Log("[Intercom] No IntercomConfig asset found; defaulting to US data hosting region.");
                return Region.US;
            }

            var paths = new List<string>(guids.Length);
            foreach (string guid in guids)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            paths.Sort(StringComparer.Ordinal);
            string chosenPath = paths[0];

            if (paths.Count > 1)
            {
                Debug.LogWarning(
                    $"[Intercom] Found {paths.Count} IntercomConfig assets ({string.Join(", ", paths)}). " +
                    $"Using '{chosenPath}'. Keep a single config or set Region consistently across them.");
            }

            var config = AssetDatabase.LoadAssetAtPath<IntercomConfig>(chosenPath);
            if (config == null)
            {
                Debug.LogWarning($"[Intercom] Could not load IntercomConfig at '{chosenPath}'; defaulting to US region.");
                return Region.US;
            }

            return config.Region;
        }
    }
}
