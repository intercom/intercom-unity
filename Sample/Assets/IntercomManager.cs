using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Intercom;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IntercomManager : MonoBehaviour
{
    [Header("Intercom Configuration")]
    [SerializeField] private IntercomConfig config;

    private static IntercomManager instance;
    private static bool isInitialized = false;
    private static bool isLoggedIn = false;

    public static IntercomManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<IntercomManager>();
                if (instance == null)
                {
                    Debug.LogWarning("[IntercomManager] No IntercomManager found in scene. Creating one dynamically, but you should add it to your StartMenu scene with proper configuration.");
                    GameObject go = new GameObject("IntercomManager");
                    instance = go.AddComponent<IntercomManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    // Events for registration callbacks
    public delegate void RegistrationCallback();
    public delegate void RegistrationErrorCallback(string errorMessage);
    public event RegistrationCallback OnRegistrationSuccess;
    public event RegistrationErrorCallback OnRegistrationFailure;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[IntercomManager] Multiple IntercomManager instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscribe to Intercom callbacks
        IntercomClient.OnUserRegistrationSuccess += HandleRegistrationSuccess;
        IntercomClient.OnUserRegistrationFailure += HandleRegistrationFailure;

        // Initialize Intercom
        InitializeIntercom();
    }

    private void OnEnable()
    {
        // Ensure we're in the DontDestroyOnLoad scene
        if (gameObject.scene.name != "DontDestroyOnLoad")
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            IntercomClient.OnUserRegistrationSuccess -= HandleRegistrationSuccess;
            IntercomClient.OnUserRegistrationFailure -= HandleRegistrationFailure;
        }
    }

    private void HandleRegistrationSuccess()
    {
        Debug.Log("[IntercomManager] User registration successful");
        isLoggedIn = true;
        OnRegistrationSuccess?.Invoke();
    }

    private void HandleRegistrationFailure(string errorMessage)
    {
        Debug.LogError($"[IntercomManager] User registration failed: {errorMessage}");
        // A failed login must not leave the manager thinking a user is present — the
        // optimistic flag previously turned the next login attempt into a silent logout.
        isLoggedIn = false;
        OnRegistrationFailure?.Invoke(errorMessage);
    }

    private void InitializeIntercom()
    {
        if (isInitialized)
        {
            return;
        }

        if (config == null)
        {
            Debug.LogError("[IntercomManager] Intercom configuration is not assigned in the IntercomManager component!");
            return;
        }

        if (string.IsNullOrEmpty(config.AppId))
        {
            Debug.LogError("[IntercomManager] Intercom App ID is not set in the configuration!");
            return;
        }

        string apiKey = null;
#if UNITY_IOS
        apiKey = config.IosApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[IntercomManager] iOS API Key is not set in the configuration!");
            return;
        }
#elif UNITY_ANDROID
        apiKey = config.AndroidApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[IntercomManager] Android API Key is not set in the configuration!");
            return;
        }
#else
        Debug.LogError("[IntercomManager] Intercom is only supported on iOS and Android platforms!");
        return;
#endif

        try
        {
            IntercomClient.Initialize(apiKey, config.AppId);
            isInitialized = true;

            // The data hosting region is applied at build time (it cannot be set at
            // runtime), so this just reports the region configured on the asset.
            Debug.Log($"[IntercomManager] Intercom data hosting region: {config.Region} (configured at build time).");

            IntercomClient.SetLauncherVisibility(IntercomClient.Visibility.VISIBLE);

            IntercomClient.SetInAppMessageVisibility(IntercomClient.Visibility.VISIBLE);

            IntercomClient.RegisterForPushNotifications();
        }
        catch (Exception e)
        {
            Debug.LogError($"[IntercomManager] Failed to initialize Intercom: {e.Message}");
            isInitialized = false;
        }
    }

    public void RegisterUser()
    {
        if (!isInitialized)
        {
            Debug.LogError("[IntercomManager] Cannot register user - Intercom is not initialized");
            return;
        }

        try
        {
            if (isLoggedIn)
            {
                IntercomClient.Logout();
                isLoggedIn = false;
            }
            else
            {
                var registration = Registration.Create()
                    .WithUserId("Unity123")
                    .WithEmail("unity@test.com")
                    .WithUserAttributes(BuildVerificationAttributes());
                IntercomClient.LoginIdentifiedUser(registration);
                // isLoggedIn is set by HandleRegistrationSuccess (see RegisterUserWithEmail).
                LogVerificationEvent();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[IntercomManager] Error registering user: {e.Message}");
        }
    }

    public void RegisterUserWithEmail(string email)
    {
        if (!isInitialized)
        {
            Debug.LogError("[IntercomManager] Cannot register user - Intercom is not initialized");
            return;
        }

        if (string.IsNullOrEmpty(email))
        {
            Debug.LogError("[IntercomManager] Email cannot be empty");
            return;
        }

        try
        {
            if (isLoggedIn)
            {
                IntercomClient.Logout();
                isLoggedIn = false;
            }
            else
            {
                var registration = Registration.Create()
                    .WithEmail(email)
                    .WithUserAttributes(BuildVerificationAttributes());
                IntercomClient.LoginIdentifiedUser(registration);
                // isLoggedIn is set by HandleRegistrationSuccess — setting it here
                // optimistically turned a failed login into a phantom session.
                LogVerificationEvent();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[IntercomManager] Error registering user: {e.Message}");
        }
    }

    private static string PlatformTag
    {
        get
        {
#if UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#else
            return "Editor";
#endif
        }
    }

    private static Dictionary<string, object> BuildVerificationAttributes()
    {
        string platform = PlatformTag;

        return new Dictionary<string, object>
        {
            { "name", $"Unity Test User ({platform})" },
            { "phone", "+353871234567" },
            { "signed_up_at", 1735689600L },
            { "unsubscribed_from_emails", false },
            { "language_override", "en" },
            { "test_platform", platform },
            { "is_vip", true },
            { "level", 42 },
            { "high_score", 13.5f },
            { "favourite_bird", "flappy" },
            {
                "companies", new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        { "company_id", $"unity-sample-co-{platform.ToLowerInvariant()}" },
                        { "name", $"Unity Sample Co ({platform})" },
                        { "created_at", 1735689600L },
                        { "monthly_spend", 99 },
                        { "plan", "Sample Plan" },
                        {
                            "custom_attributes", new Dictionary<string, object>
                            {
                                { "seats", 3 },
                                { "trial", true }
                            }
                        }
                    }
                }
            }
        };
    }

    private static void LogVerificationEvent()
    {
        IntercomClient.LogEvent("unity_sdk_verification", new Dictionary<string, object>
        {
            { "wrapper_version", IntercomClient.SdkVersion },
            { "platform", Application.platform.ToString() },
            { "is_vip", true }
        });
    }

    public void UpdateVerificationAttributes()
    {
        if (!isInitialized)
        {
            Debug.LogError("[IntercomManager] Cannot update attributes - Intercom is not initialized");
            return;
        }

        IntercomClient.SetUserAttributes(new Dictionary<string, object>
        {
            { "name", $"Unity Test User ({PlatformTag}, updated)" },
            { "is_vip", false },
            { "level", 43 }
        });
    }

    public void ShowChat()
    {
        if (!isInitialized)
        {
            Debug.LogError("[IntercomManager] Cannot show messenger - Intercom is not initialized");
            return;
        }

        try
        {
            IntercomClient.DisplayMessageComposer();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[IntercomManager] Error showing messenger: {e.Message}");
        }
    }

    public void ShowMessenger()
    {
        if (!isInitialized)
        {
            Debug.LogError("[IntercomManager] Cannot show messenger - Intercom is not initialized");
            return;
        }

        try
        {
            IntercomClient.Present(IntercomClient.Space.Home);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[IntercomManager] Error showing messenger: {e.Message}");
        }
    }

    // Optional: Method to hide the messenger
    public void HideMessenger()
    {
        if (!isInitialized)
        {
            Debug.LogError("[IntercomManager] Cannot hide messenger - Intercom is not initialized");
            return;
        }

        try
        {
            IntercomClient.HideIntercom();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[IntercomManager] Error hiding messenger: {e.Message}");
        }
    }
}
