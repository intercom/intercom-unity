using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Intercom
{
    public class IntercomClient
    {
        public delegate void IntercomCallback();
        public delegate void IntercomErrorCallback(string errorMessage);

        public static event IntercomCallback OnUserRegistrationSuccess;
        public static event IntercomErrorCallback OnUserRegistrationFailure;

        public enum Visibility
        {
            GONE,
            VISIBLE
        }

        public enum LogLevel
        {
            VERBOSE,
            DEBUG,
            INFO,
            WARN,
            ERROR,
            ASSERT,
            DISABLED
        }

        public enum Space
        {
            Home,
            Messages,
            HelpCenter,
            Tickets
        }

#if UNITY_IOS
        [DllImport("__Internal")]
        private static extern void _Initialize(string apiKey, string appId);

        [DllImport("__Internal")]
        private static extern void _SetUnityVersion(string version);

        [DllImport("__Internal")]
        private static extern void _LoginIdentifiedUser(string userId, string email, string attributesJson);
        
        [DllImport("__Internal")]
        private static extern void _LoginUnidentifiedUser();
        
        [DllImport("__Internal")]
        private static extern void _Logout();
        
        [DllImport("__Internal")]
        private static extern void _Present(string space);
        
        [DllImport("__Internal")]
        private static extern void _PresentContent(string contentType, string contentId);
        
        [DllImport("__Internal")]
        private static extern void _DisplayMessageComposer(string initialMessage);
        
        [DllImport("__Internal")]
        private static extern void _SetBottomPadding(int bottomPadding);
        
        [DllImport("__Internal")]
        private static extern void _SetInAppMessageVisibility(string visibility);
        
        [DllImport("__Internal")]
        private static extern void _SetLauncherVisibility(string visibility);
        
        [DllImport("__Internal")]
        private static extern void _HideIntercom();
        
        [DllImport("__Internal")]
        private static extern void _SetUserAttributes(string attributes);
        
        [DllImport("__Internal")]
        private static extern void _LogEvent(string name);
        
        [DllImport("__Internal")]
        private static extern void _LogEventWithMetadata(string name, string metadata);
        
        [DllImport("__Internal")]
        private static extern void _SetUserHash(string userHash);
        
        [DllImport("__Internal")]
        private static extern void _SetUserJwt(string jwt);
        
        [DllImport("__Internal")]
        private static extern void _SetAuthTokens(string authTokens);

        [DllImport("__Internal")]
        private static extern void _SetDeviceToken(string hexToken);

        [DllImport("__Internal")]
        private static extern void _RegisterForPushNotifications();

        [DllImport("__Internal")]
        private static extern void _SetLogLevel(string logLevel);
        
        [DllImport("__Internal")]
        private static extern int _GetUnreadConversationCount();
        
        [DllImport("__Internal")]
        private static extern bool _IsUserLoggedIn();
        
        [DllImport("__Internal")]
        private static extern string _FetchLoggedInUserAttributes();
#elif UNITY_ANDROID
        private static AndroidJavaClass _intercomPlugin;
#endif

        private static bool _isInitialized = false;

        // Wrapper version reported to the native SDK so conversations are attributed to the
        // Unity wrapper (X-INTERCOM-AGENT-WRAPPER: intercom-sdk-unity/<version>).
        // Keep in sync with the "version" field in package.json.
        public const string SdkVersion = "0.0.8";

        // Callback methods that will be called from native code
        private static void HandleUserRegistrationSuccess(string message)
        {
            Debug.Log("[Intercom] User registration successful");
            OnUserRegistrationSuccess?.Invoke();
        }

        private static void HandleUserRegistrationFailure(string errorMessage)
        {
            Debug.LogError($"[Intercom] User registration failed: {errorMessage}");
            OnUserRegistrationFailure?.Invoke(errorMessage);
        }

        /// <summary>
        /// Initialize the Intercom SDK with your API key and app ID
        /// </summary>
        /// <param name="apiKey">Your Intercom API key</param>
        /// <param name="appId">Your Intercom app ID</param>
        public static void Initialize(string apiKey, string appId)
        {
            Debug.Log($"[Intercom] Attempting to initialize with API Key: {apiKey.Substring(0, 4)}... and App ID: {appId}");

            if (_isInitialized)
            {
                Debug.LogWarning("[Intercom] Already initialized, skipping initialization");
                return;
            }

#if UNITY_IOS
            _Initialize(apiKey, appId);
            _SetUnityVersion(SdkVersion);
#elif UNITY_ANDROID
            if (_intercomPlugin == null)
            {
                Debug.Log("[Intercom] Creating Android plugin instance");
                _intercomPlugin = new AndroidJavaClass("com.intercom.unity.IntercomPlugin");
            }
            _intercomPlugin.CallStatic("setUnityVersion", SdkVersion);
            Debug.Log("[Intercom] Calling native Android initialize method");
            _intercomPlugin.CallStatic("initialize", apiKey, appId);
#endif

            _isInitialized = true;
            Debug.Log("[Intercom] Initialization completed successfully");
        }

        /// <summary>
        /// Register an identified user with Intercom
        /// </summary>
        /// <param name="registration">The registration object containing user information</param>
        public static void LoginIdentifiedUser(Registration registration)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before registering a user");
                return;
            }

            if (registration == null)
            {
                Debug.LogError("[Intercom] Registration object cannot be null");
                return;
            }

            var userId = registration.GetUserId();
            var email = registration.GetEmail();

            if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(email))
            {
                Debug.LogError("[Intercom] Either userId or email must be provided for identified user registration");
                return;
            }

            Debug.Log($"[Intercom] Registering user with ID: {userId} and/or email: {email}");

            string attributesJson = IntercomJson.Serialize(registration.GetAttributes());
#if UNITY_IOS
            _LoginIdentifiedUser(userId, email, attributesJson);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("loginIdentifiedUser", userId, email, attributesJson);
#endif
        }

        /// <summary>
        /// Register an unidentified user with Intercom
        /// </summary>
        public static void LoginUnidentifiedUser()
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before registering a user");
                return;
            }

            Debug.Log("[Intercom] Registering unidentified user");

#if UNITY_IOS
            _LoginUnidentifiedUser();
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("loginUnidentifiedUser");
#endif
        }

        /// <summary>
        /// Logout the current user
        /// </summary>
        public static void Logout()
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before logging out a user");
                return;
            }

#if UNITY_IOS
            _Logout();
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("logout");
#endif
        }

        /// <summary>
        /// Present the Intercom messenger
        /// </summary>
        /// <param name="space">The space to present</param>
        public static void Present(Space space = Space.Home)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before presenting");
                return;
            }

#if UNITY_IOS
            _Present(space.ToString());
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("present", space.ToString());
#endif
        }

        /// <summary>
        /// Present specific Intercom content
        /// </summary>
        /// <param name="contentType">The type of content to present</param>
        /// <param name="contentId">The ID of the content (for helpcentercollections, can be comma-separated multiple IDs)</param>
        public static void PresentContent(string contentType, string contentId)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before presenting content");
                return;
            }

#if UNITY_IOS
            _PresentContent(contentType, contentId);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("presentContent", contentType, contentId);
#endif
        }

        /// <summary>
        /// Display the message composer
        /// </summary>
        /// <param name="initialMessage">Optional initial message</param>
        public static void DisplayMessageComposer(string initialMessage = "")
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before displaying message composer");
                return;
            }

#if UNITY_IOS
            _DisplayMessageComposer(initialMessage);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("displayMessageComposer", initialMessage);
#endif
        }

        /// <summary>
        /// Set the bottom padding for Intercom UI elements
        /// </summary>
        /// <param name="bottomPadding">The padding in pixels</param>
        public static void SetBottomPadding(int bottomPadding)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before setting bottom padding");
                return;
            }

#if UNITY_IOS
            _SetBottomPadding(bottomPadding);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("setBottomPadding", bottomPadding);
#endif
        }

        /// <summary>
        /// Set the visibility of in-app messages
        /// </summary>
        /// <param name="visibility">The visibility state</param>
        public static void SetInAppMessageVisibility(Visibility visibility)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before setting in-app message visibility");
                return;
            }

#if UNITY_IOS
            _SetInAppMessageVisibility(visibility.ToString());
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("setInAppMessageVisibility", visibility == Visibility.VISIBLE);
#endif
        }

        /// <summary>
        /// Set the visibility of the launcher
        /// </summary>
        /// <param name="visibility">The visibility state</param>
        public static void SetLauncherVisibility(Visibility visibility)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before setting launcher visibility");
                return;
            }

#if UNITY_IOS
            _SetLauncherVisibility(visibility.ToString());
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("setLauncherVisibility", visibility == Visibility.VISIBLE);
#endif
        }

        /// <summary>
        /// Hide all Intercom UI elements
        /// </summary>
        public static void HideIntercom()
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before hiding");
                return;
            }

#if UNITY_IOS
            _HideIntercom();
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("hideIntercom");
#endif
        }

        /// <summary>
        /// Set user attributes
        /// </summary>
        /// <param name="attributes">Dictionary of user attributes</param>
        public static void SetUserAttributes(Dictionary<string, object> attributes)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before setting user attributes");
                return;
            }

            string json = IntercomJson.Serialize(attributes);
#if UNITY_IOS
            _SetUserAttributes(json);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("setUserAttributes", json);
#endif
        }

        /// <summary>
        /// Log an event with a given name
        /// </summary>
        /// <param name="name">The name of the event</param>
        public static void LogEvent(string name)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before logging events");
                return;
            }

#if UNITY_IOS
            _LogEvent(name);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("logEvent", name);
#endif
        }

        /// <summary>
        /// Log an event with a given name and metadata
        /// </summary>
        /// <param name="name">The name of the event</param>
        /// <param name="metadata">Dictionary of metadata</param>
        public static void LogEvent(string name, Dictionary<string, object> metadata)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before logging events");
                return;
            }

            string json = IntercomJson.Serialize(metadata);
#if UNITY_IOS
            _LogEventWithMetadata(name, json);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("logEventWithMetadata", name, json);
#endif
        }

        /// <summary>
        /// Set the user hash for identity verification
        /// </summary>
        /// <param name="userHash">The user hash</param>
        public static void SetUserHash(string userHash)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before setting user hash");
                return;
            }

#if UNITY_IOS
            _SetUserHash(userHash);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("setUserHash", userHash);
#endif
        }

        /// <summary>
        /// Set the JWT token for the user
        /// </summary>
        /// <param name="jwt">The JWT token</param>
        public static void SetUserJwt(string jwt)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before setting JWT");
                return;
            }

#if UNITY_IOS
            _SetUserJwt(jwt);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("setUserJwt", jwt);
#endif
        }

        /// <summary>
        /// Set auth tokens for the user
        /// </summary>
        /// <param name="authTokens">Dictionary of auth tokens</param>
        public static void SetAuthTokens(Dictionary<string, string> authTokens)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before setting auth tokens");
                return;
            }

            string json = IntercomJson.Serialize(authTokens);
#if UNITY_IOS
            _SetAuthTokens(json);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("setAuthTokens", json);
#endif
        }

        /// <summary>
        /// Set the log level for Intercom
        /// </summary>
        /// <param name="logLevel">The log level to set</param>
        public static void SetLogLevel(LogLevel logLevel)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before setting log level");
                return;
            }

#if UNITY_IOS
            _SetLogLevel(logLevel.ToString());
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("setLogLevel", logLevel.ToString());
#endif
        }

        /// <summary>
        /// Get the number of unread conversations
        /// </summary>
        /// <returns>The number of unread conversations</returns>
        public static int GetUnreadConversationCount()
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before getting unread count");
                return 0;
            }

#if UNITY_IOS
            return _GetUnreadConversationCount();
#elif UNITY_ANDROID
            return _intercomPlugin.CallStatic<int>("getUnreadConversationCount");
#else
            return 0;
#endif
        }

        /// <summary>
        /// Check if a user is currently logged in
        /// </summary>
        /// <returns>True if a user is logged in, false otherwise</returns>
        public static bool IsUserLoggedIn()
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before checking login status");
                return false;
            }

#if UNITY_IOS
            return _IsUserLoggedIn();
#elif UNITY_ANDROID
            return _intercomPlugin.CallStatic<bool>("isUserLoggedIn");
#else
            return false;
#endif
        }

        /// <summary>
        /// Fetch the attributes of the currently logged in user
        /// </summary>
        /// <returns>Dictionary containing user attributes</returns>
        public static Dictionary<string, object> FetchLoggedInUserAttributes()
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before fetching user attributes");
                return new Dictionary<string, object>();
            }

#if UNITY_IOS
            string json = _FetchLoggedInUserAttributes();
            return JsonUtility.FromJson<Dictionary<string, object>>(json);
#elif UNITY_ANDROID
            string json = _intercomPlugin.CallStatic<string>("fetchLoggedInUserAttributes");
            return JsonUtility.FromJson<Dictionary<string, object>>(json);
#else
            return new Dictionary<string, object>();
#endif
        }

        /// <summary>
        /// Request notification permission and register for remote notifications.
        ///
        /// On iOS, the APNs device token is captured natively on grant and forwarded to Intercom
        /// automatically (no need to call <see cref="SetDeviceToken"/>) — this is the recommended
        /// iOS path. On Android 13+, this requests the POST_NOTIFICATIONS runtime permission; the
        /// FCM token itself is still delivered by your FirebaseMessagingService (see
        /// Documentation~/push-notifications.md). Call it once after Initialize().
        /// </summary>
        public static void RegisterForPushNotifications()
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before registering for push notifications");
                return;
            }

#if UNITY_IOS
            _RegisterForPushNotifications();
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("requestPushPermission");
#endif
        }

        /// <summary>
        /// Forward a device push token to Intercom so it can deliver push notifications.
        /// iOS: pass the APNs device token as a lowercase hex string (from
        /// <c>didRegisterForRemoteNotificationsWithDeviceToken</c>). Android: pass the FCM
        /// registration token (from FirebaseMessaging's <c>getToken()</c> / <c>onNewToken()</c>).
        /// Call this each time a token is issued or refreshed, after Initialize().
        ///
        /// iOS note: prefer <see cref="RegisterForPushNotifications"/>, which acquires and forwards
        /// the token for you. Use this only if you obtain the APNs token yourself (e.g. via the
        /// Unity Mobile Notifications package).
        /// </summary>
        /// <param name="token">The platform push token.</param>
        public static void SetDeviceToken(string token)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[Intercom] Must be initialized before setting the device token");
                return;
            }

            if (string.IsNullOrEmpty(token))
            {
                Debug.LogError("[Intercom] Device token cannot be null or empty");
                return;
            }

#if UNITY_IOS
            _SetDeviceToken(token);
#elif UNITY_ANDROID
            _intercomPlugin.CallStatic("sendPushTokenToIntercom", token);
#endif
        }

        /// <summary>
        /// Returns true if the given push payload originated from Intercom. Use this from your
        /// own messaging service when you run multiple push providers, to decide whether Intercom
        /// should handle the message. Android only; returns false on other platforms.
        /// </summary>
        /// <param name="data">The push payload data map (e.g. RemoteMessage.getData()).</param>
        public static bool IsIntercomPush(Dictionary<string, string> data)
        {
#if UNITY_ANDROID
            if (_intercomPlugin == null)
            {
                Debug.LogError("[Intercom] Must be initialized before inspecting a push payload");
                return false;
            }
            return _intercomPlugin.CallStatic<bool>("isIntercomPush", IntercomJson.Serialize(data));
#else
            return false;
#endif
        }

        /// <summary>
        /// Hand an Intercom push payload to the Intercom SDK to be displayed. Call this only when
        /// <see cref="IsIntercomPush"/> returns true. Android only.
        ///
        /// iOS does not need this: once a device token is set (via
        /// <see cref="RegisterForPushNotifications"/> or <see cref="SetDeviceToken"/>), the Intercom
        /// iOS SDK automatically intercepts and displays its own pushes. This method is a no-op on
        /// iOS.
        /// </summary>
        /// <param name="data">The push payload data map confirmed to be an Intercom push.</param>
        public static void HandlePush(Dictionary<string, string> data)
        {
#if UNITY_ANDROID
            if (_intercomPlugin == null)
            {
                Debug.LogError("[Intercom] Must be initialized before handling a push payload");
                return;
            }
            _intercomPlugin.CallStatic("handlePush", IntercomJson.Serialize(data));
#else
            Debug.Log("[Intercom] HandlePush is Android-only; on iOS, Intercom displays its own pushes automatically once a device token is set.");
#endif
        }
    }
}
