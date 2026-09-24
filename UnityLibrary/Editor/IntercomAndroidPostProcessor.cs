#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Intercom.Editor
{
    public class IntercomAndroidPostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private const string RegionMetaName = "io.intercom.android.sdk.server.region";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[Intercom] Could not find AndroidManifest.xml at '{manifestPath}'; data hosting region not configured.");
                return;
            }

            Region region = IntercomRegionResolver.Resolve();
            string regionValue = RegionResourceReference(region);

            var doc = new XmlDocument();
            doc.Load(manifestPath);

            XmlNode application = doc.SelectSingleNode("/manifest/application");
            if (application == null)
            {
                Debug.LogWarning("[Intercom] No <application> element in AndroidManifest.xml; data hosting region not configured.");
                return;
            }

            XmlElement meta = FindRegionMetaData(application);
            if (meta == null)
            {
                meta = doc.CreateElement("meta-data");
                meta.SetAttribute("name", AndroidNamespace, RegionMetaName);
                application.AppendChild(meta);
            }

            // Overwrite so switching regions across rebuilds is clean.
            meta.SetAttribute("value", AndroidNamespace, regionValue);
            doc.Save(manifestPath);

            Debug.Log($"[Intercom] Android build configured for {region} data hosting region ({regionValue}).");
        }

        private static XmlElement FindRegionMetaData(XmlNode application)
        {
            foreach (XmlNode node in application.ChildNodes)
            {
                if (node is XmlElement element &&
                    element.LocalName == "meta-data" &&
                    element.GetAttribute("name", AndroidNamespace) == RegionMetaName)
                {
                    return element;
                }
            }

            return null;
        }

        // Resource reference provided by the Intercom Android SDK; note Android spells Australia "aus".
        private static string RegionResourceReference(Region region)
        {
            switch (region)
            {
                case Region.EU:
                    return "@integer/intercom_server_region_eu";
                case Region.AU:
                    return "@integer/intercom_server_region_aus";
                default:
                    return "@integer/intercom_server_region_us";
            }
        }
    }
}
#endif
