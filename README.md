# Intercom Unity Wrapper

A Unity wrapper for the [Intercom](https://www.intercom.com) iOS and Android SDKs, letting you integrate Intercom's customer messaging platform — Messenger, Fin AI Agent, Help Center, and push notifications — into your Unity applications.

The wrapper contains only thin C# / native bridge code. The native Intercom SDKs are resolved at build time by the [External Dependency Manager for Unity (EDM4U)](https://github.com/googlesamples/unity-jar-resolver):

- **Android** — resolved from Maven Central (`io.intercom.android`)
- **iOS** — resolved as a Swift Package from [`intercom-ios-sp`](https://github.com/intercom/intercom-ios-sp)

The pinned native SDK versions live in [`Editor/IntercomDependencies.xml`](UnityLibrary/Editor/IntercomDependencies.xml).

## 📋 Requirements

- **Unity**: 6000.2 or later — earlier editors cannot add the iOS SDK (EDM4U's Swift Package
  support requires Unity 2021.3+) and don't meet the Android Gradle floors below out of the box
- **Platforms**: iOS 15.0+ and Android API 23+ (other platforms are not supported)
- An **Intercom account** with mobile API keys

### Android build requirements

The native Intercom Android SDK publishes AAR metadata that fails the build unless your Gradle toolchain meets all three of:

| Requirement | Where to set it |
| --- | --- |
| **Target API Level 36** | Player Settings → Android → Target API Level (this drives `compileSdk`) |
| **Android Gradle Plugin 8.9.1+** | Supplied by the editor, or `Assets/Plugins/Android/baseProjectTemplate.gradle` if you use a custom template |
| **Gradle 8.11.1+** | Bundled with the editor, or Preferences → External Tools → Gradle |

Any Unity editor works as long as the toolchain meets these — either use an editor that already satisfies them, or install your own Gradle and point Unity at it in **Preferences → External Tools** (untick "Use embedded"). Unity's [Android Gradle version compatibility table](https://docs.unity3d.com/6000.2/Documentation/Manual/android-gradle-version-compatibility.html) lists what each editor bundles.

## 🚀 Quick Start

### 1. Installation

**Step 1 — Add the scoped registries.** This package is published to npm, and depends on EDM4U, which lives on OpenUPM. Open `Packages/manifest.json` and add both registries next to `dependencies`:

```json
{
  "scopedRegistries": [
    {
      "name": "Intercom",
      "url": "https://registry.npmjs.org",
      "scopes": ["com.intercom"]
    },
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.google.external-dependency-manager"]
    }
  ],
  "dependencies": { "...": "...your existing entries..." }
}
```

**Step 2 — Add the package.** Add it to the `dependencies` block:

```json
"com.intercom.unity": "0.1.0"
```

Return to Unity — it imports the package and resolves EDM4U automatically. Confirm **"Intercom Unity Wrapper"** appears in **Window → Package Manager**.

**Alternative install methods:**

- **Git URL** — in Package Manager, choose **Add package from git URL…** and enter
  `https://github.com/intercom/intercom-unity.git?path=/UnityLibrary#v0.1.0`
  (the `?path=` part is required — the package lives in the `UnityLibrary/` subfolder, not the
  repo root). Step 1's OpenUPM registry is still required for EDM4U.
- **Tarball** — download `com.intercom.unity-<version>.tgz` from a GitHub release and use
  **Add package from tarball…**. Step 1's OpenUPM registry is still required for EDM4U.

On iOS, the native SDK is added as a Swift Package by EDM4U's iOS resolver — make sure
**Swift Package Manager integration** stays enabled (it is by default) under
**Preferences → External Dependency Manager → iOS Resolver** if your project also uses CocoaPods.

### 2. Configuration Setup

The SDK is configured through a ScriptableObject:

1. In the Unity **Project** window (the Assets browser — **not** the Hierarchy), right-click your desired folder (e.g. `Assets/`)
2. Select **Create → Intercom → IntercomConfig**
3. Name the asset `IntercomConfig`
4. Select it and fill in your Intercom credentials in the Inspector:
   - **App ID**: Your Intercom App ID
   - **iOS API Key**: Your iOS API Key
   - **Android API Key**: Your Android API Key
   - **Region**: Your workspace's data hosting region — `US` (default), `EU`, or `AU`

You'll find these values in your Intercom dashboard under **Settings → Installation**.

### 3. Initialize Intercom

Initialize Intercom once at startup with `IntercomClient.Initialize(apiKey, appId)`. A minimal `IntercomManager` MonoBehaviour that reads your `IntercomConfig`:

```csharp
using Intercom;
using UnityEngine;

public class IntercomManager : MonoBehaviour
{
    [SerializeField] private IntercomConfig config;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

#if UNITY_IOS
        string apiKey = config.IosApiKey;
#elif UNITY_ANDROID
        string apiKey = config.AndroidApiKey;
#else
        string apiKey = null;
#endif

        if (string.IsNullOrEmpty(config.AppId) || string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[Intercom] IntercomConfig is missing an App ID or platform API key.");
            return;
        }

        IntercomClient.Initialize(apiKey, config.AppId);
    }
}
```

Create an empty GameObject in your first scene, add the **IntercomManager** component, and drag your `IntercomConfig` asset into the **Config** field. Region and iOS production-APNs are applied automatically at build time from the asset.

### 4. Basic Usage

```csharp
// Login an identified user
var registration = Registration.Create()
    .WithUserId("user123")
    .WithEmail("user@example.com");
IntercomClient.LoginIdentifiedUser(registration);

// Login an unidentified user
IntercomClient.LoginUnidentifiedUser();

// Show the messenger
IntercomClient.Present();

// Show the message composer
IntercomClient.DisplayMessageComposer("Hello, I need help!");

// Show a specific space
IntercomClient.Present(IntercomClient.Space.HelpCenter);
```

## 🔧 Advanced Configuration

### Data Hosting Region (US / EU / AU)

If your Intercom workspace is hosted in the EU or Australia, set the **Region** field on your `IntercomConfig` asset to `EU` or `AU` (it defaults to `US`). When you build, the SDK configures the native region for you:

- **iOS**: writes the `IntercomRegion` key into the generated Xcode project's `Info.plist`.
- **Android**: injects the `io.intercom.android.sdk.server.region` meta-data into the generated `AndroidManifest.xml`.

> **Important**: The region is applied at **build time**, not at runtime — the native SDK reads it at app startup, before your code runs. Choose your region before building; there is no runtime API to change it. If multiple `IntercomConfig` assets exist in a project, the build uses one of them and logs a warning, so keep a single config (or set Region consistently across them).

### Push Notifications

If Intercom is your only push provider, call `RegisterForPushNotifications()` after initialization and the SDK handles permission, registration, and device-token delivery for you:

```csharp
IntercomClient.RegisterForPushNotifications();
```

If you run multiple push providers, forward the token yourself instead:

```csharp
IntercomClient.SetDeviceToken(token); // iOS: APNs hex string · Android: FCM token
```

iOS requires a physical device and the Push Notifications capability; Android requires Firebase Cloud Messaging. See **[Documentation~/push-notifications.md](UnityLibrary/Documentation~/push-notifications.md)** for the full setup, release checklist, and debugging guide.

### User Attributes and Events

```csharp
// Set user attributes
var attributes = new Dictionary<string, object>
{
    {"name", "John Doe"},
    {"plan", "premium"}
};
IntercomClient.SetUserAttributes(attributes);

// Log events, with optional metadata
IntercomClient.LogEvent("purchase_completed");

var metadata = new Dictionary<string, object>
{
    {"product_id", "123"},
    {"price", 29.99}
};
IntercomClient.LogEvent("purchase_completed", metadata);
```

## 📖 Features

- **User Management**: Login identified and unidentified users
- **Messaging**: Present the Intercom messenger and message composer
- **Content Presentation**: Display articles, surveys, carousels, and help center collections
- **User Attributes**: Set custom user attributes
- **Event Tracking**: Log events with optional metadata
- **Push Notifications**: Permission prompt and device-token delivery for iOS (APNs) and Android (FCM)
- **Data Hosting Regions**: US, EU, and AU, configured at build time
- **Platform Consistency**: Unified C# API across iOS and Android

## 🔒 Security Note

⚠️ **Important**: Never commit your `IntercomConfig.asset` file to version control — it contains your API keys.

## 🛠️ Troubleshooting

**Package Not Found**
- Ensure you're using Unity 6000.2 or later
- Confirm both scoped registries were added *before* the package (see [Installation](#1-installation))

**Build Errors**
- Make sure you've created and configured the IntercomConfig asset
- Check that API keys are valid for your platform
- Ensure you're building for supported platforms (iOS/Android)
- On Android, confirm your toolchain meets the [Android build requirements](#android-build-requirements)

**Runtime Errors**
- Verify your initialization runs at startup (e.g. your initialization GameObject is in the first scene)
- Check that IntercomConfig is assigned to your initialization component
- Ensure you're calling initialization before using other methods

**Works in the Editor but not in a device build**
- Confirm the scene containing your initialization GameObject is added to **File → Build Settings → Scenes In Build** — if it isn't, that component never runs on device.
- Confirm the `IntercomConfig` asset is assigned to the component **in that scene** (a reference that's fine in Play Mode can still be unset in the scene that's actually built).
- If a device build behaves as though a recent change didn't apply, do a **clean build** (build into an empty output folder, or delete the project's `Library/` folder and rebuild) — Unity's incremental build can ship stale player data.

**Reading SDK logs (your main debugging tool)**
- Every SDK message is prefixed `[Intercom]`. View them with `adb logcat` on Android or the Xcode console on iOS.
- `[Intercom] Initialization completed successfully` — initialization worked.
- `[Intercom] Must be initialized before presenting` — a `Present()`/messenger call ran before `Initialize()` succeeded. Check that your initialization ran *and* completed.
- `IntercomConfig is missing an App ID or platform API key` — the config asset is unassigned, or its App ID / platform API key is empty.


## 📚 Documentation

- [Push Notifications](UnityLibrary/Documentation~/push-notifications.md) — push setup, release checklist, and debugging

## 🤝 Support

- Check the [Installation](#1-installation) and Troubleshooting sections above for setup help
- Open an issue on GitHub
- Contact Intercom support

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
