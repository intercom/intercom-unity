using UnityEngine;

namespace Intercom
{
    [CreateAssetMenu(fileName = "IntercomConfig", menuName = "Intercom/IntercomConfig", order = 1)]
    public class IntercomConfig : ScriptableObject
    {
        [Header("API Configuration")]
        [Tooltip("Your Intercom App ID")]
        [SerializeField] private string appId = "YOUR_APP_ID";

        [Tooltip("Your iOS API Key")]
        [SerializeField] private string iosApiKey = "YOUR_IOS_API_KEY";

        [Tooltip("Your Android API Key")]
        [SerializeField] private string androidApiKey = "YOUR_ANDROID_API_KEY";

        [Header("Data Hosting Region")]
        [Tooltip("The data hosting region for your Intercom workspace. Defaults to US. " +
                 "Applied automatically at build time (iOS Info.plist / Android manifest).")]
        [SerializeField] private Region region = Region.US;

        [Header("Push Notifications (iOS)")]
        [Tooltip("Sets the iOS aps-environment entitlement. Leave OFF for Xcode/development/ad-hoc " +
                 "builds (APNs sandbox). Turn ON for TestFlight and App Store builds (production APNs), " +
                 "otherwise push is silently dropped on those builds.")]
        [SerializeField] private bool useProductionApnsEnvironment = false;

        public string AppId => appId;
        public string IosApiKey => iosApiKey;
        public string AndroidApiKey => androidApiKey;
        public Region Region => region;
        public bool UseProductionApnsEnvironment => useProductionApnsEnvironment;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(appId) || appId == "YOUR_APP_ID")
            {
                Debug.LogWarning("Intercom App ID is not set in the configuration!");
            }
            if (string.IsNullOrEmpty(iosApiKey) || iosApiKey == "YOUR_IOS_API_KEY")
            {
                Debug.LogWarning("iOS API Key is not set in the configuration!");
            }
            if (string.IsNullOrEmpty(androidApiKey) || androidApiKey == "YOUR_ANDROID_API_KEY")
            {
                Debug.LogWarning("Android API Key is not set in the configuration!");
            }
        }
    }
}
