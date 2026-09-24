# Push Notifications

The SDK exposes the primitives needed to deliver Intercom push notifications on both platforms.
The right integration depends on whether Intercom is your **only** push provider or **one of
several**.

## C# API

```csharp
// iOS only — request permission, register for remote notifications, and forward the resulting
// APNs token to Intercom automatically. The recommended iOS entry point; call once after Initialize.
// No-op on Android.
IntercomClient.RegisterForPushNotifications();

// Forward a device push token to Intercom (call after Initialize, and on every token refresh).
//  - iOS: APNs token as a lowercase hex string. Only needed if you acquire the token yourself
//    (e.g. via the Unity Mobile Notifications package) instead of using RegisterForPushNotifications.
//  - Android: FCM registration token.
IntercomClient.SetDeviceToken(string token);

// Android only — inspect/route a push payload from your own FirebaseMessagingService.
// On iOS these are unused: Intercom auto-displays its own pushes once a token is set.
bool IntercomClient.IsIntercomPush(Dictionary<string, string> data);
void IntercomClient.HandlePush(Dictionary<string, string> data);
```

## Android

Push uses Firebase Cloud Messaging. Your project must already have Firebase configured
(`google-services.json` + the `com.google.gms.google-services` Gradle plugin) — the same setup
your other FCM providers need.

### Option A — Intercom is your only push provider

Add a `FirebaseMessagingService` to **your own project** (e.g. under `Assets/Plugins/Android/`).
The SDK does not bundle one, because that would force a compile-time `firebase-messaging`
dependency on every consumer — including projects that don't use push — and Android delivers an
FCM message to only one service anyway. Drop this in:

```java
package com.yourcompany.app;

import com.google.firebase.messaging.FirebaseMessagingService;
import com.google.firebase.messaging.RemoteMessage;
import io.intercom.android.sdk.push.IntercomPushClient;
import java.util.Map;

public class AppFirebaseMessagingService extends FirebaseMessagingService {
    private final IntercomPushClient pushClient = new IntercomPushClient();

    @Override
    public void onNewToken(String token) {
        pushClient.sendTokenToIntercom(getApplication(), token);
    }

    @Override
    public void onMessageReceived(RemoteMessage remoteMessage) {
        Map<String, String> data = remoteMessage.getData();
        if (pushClient.isIntercomPush(data)) {
            pushClient.handlePush(getApplication(), data);
        }
    }
}
```

Register it in your app's `AndroidManifest.xml`:

```xml
<service android:name="com.yourcompany.app.AppFirebaseMessagingService"
         android:exported="false">
    <intent-filter>
        <action android:name="com.google.firebase.MESSAGING_EVENT" />
    </intent-filter>
</service>
```

This compiles because `firebase-messaging` is on your app's classpath (you configured Firebase
above). The `IntercomPushClient` calls come from `intercom-sdk-base`, which the SDK already pulls in.

### Option B — multiple push providers

Do **not** register the bundled service. From your own `FirebaseMessagingService`:

```java
@Override public void onNewToken(String token) {
    // ... your other providers ...
    // forward to Intercom via your C# layer, or call IntercomPushClient directly
}

@Override public void onMessageReceived(RemoteMessage message) {
    Map<String, String> data = message.getData();
    // route to whichever provider owns the message
}
```

From C#, forward the token and route messages:

```csharp
IntercomClient.SetDeviceToken(fcmToken);

if (IntercomClient.IsIntercomPush(data))
{
    IntercomClient.HandlePush(data);
}
```

Android 13+ requires the `POST_NOTIFICATIONS` runtime permission (declared by the SDK) to be
granted before notifications appear.

## iOS

Push uses APNs. Once a device token is set, the Intercom iOS SDK swizzles the notification
handlers and **displays its own pushes automatically** — you do not forward notifications to
Intercom yourself (that is only needed if you set `IntercomAutoIntegratePushNotifications` to `NO`,
which this package does not).

### Option A — Intercom is your only push provider (recommended)

Call once after `Initialize` (and after login):

```csharp
IntercomClient.RegisterForPushNotifications();
```

This requests notification permission, registers for remote notifications, and forwards the APNs
token to Intercom. Token capture and the `UNUserNotificationCenter` delegate are wired up by the
bundled `IntercomAppController` (a `UnityAppController` subclass), so no native code is required.

> If your project already ships its own `UnityAppController` subclass, only one subclass can win.
> In that case do not rely on `RegisterForPushNotifications`; use Option B instead.

### Option B — you already manage notifications (e.g. Unity Mobile Notifications)

If another system owns notification registration (such as the
[Unity Mobile Notifications](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@latest)
package, or your own `UnityAppController` subclass), obtain the APNs token there and forward it:

```csharp
// Unity Mobile Notifications exposes the token as a lowercase hex string:
IntercomClient.SetDeviceToken(request.DeviceToken);
```

That package also installs its own `UNUserNotificationCenter` delegate, which Intercom's swizzle
uses — so tap/foreground handling still works.

### Build configuration (handled automatically)

The build post-processor registers the **Push Notifications** capability and links
`UserNotifications.framework`; the native Intercom iOS SDK itself is added to the generated Xcode
project as a Swift Package by EDM4U, which also takes care of embedding it.

The `aps-environment` entitlement is **`development` by default**. APNs uses a separate production
environment for TestFlight and App Store builds — a development entitlement there causes push to be
**silently dropped**. Set **Use Production APNs Environment** on your `IntercomConfig` asset before
making a TestFlight/App Store build.

You must also enable **Push Notifications** for your App ID in the Apple Developer portal; with
manual signing, regenerate the provisioning profile so it includes `aps-environment`.

APNs pushes are **not delivered on the iOS Simulator** — test on a physical device.

Intercom's messenger also uses the camera, photo library, and microphone for attachments and voice.
Add `NSCameraUsageDescription`, `NSPhotoLibraryUsageDescription`, and `NSMicrophoneUsageDescription`
to your `Info.plist`, or those features are disabled and the App Store will reject the build.

### Not currently supported on iOS

- Manual push routing (`isIntercomPushNotification:` / `handleIntercomPushNotification:`) is not
  bridged — only relevant if you disable Intercom's automatic integration.
- Rich/media push (a Notification Service Extension) is not scaffolded by the post-processor.

## Testing & debugging

- A reply from a teammate to the user's conversation triggers a push when the app is
  backgrounded/closed. Foreground delivery appears as an in-app message, not a system push.
- The most common failure is the **device token never reaching Intercom** for the correct
  workspace/region — verify `RegisterForPushNotifications` (or `SetDeviceToken`) runs after
  `Initialize`, and that the API key, app ID, and **region** match the workspace you expect.
- On TestFlight/App Store, confirm **Use Production APNs Environment** is enabled — a sandbox
  token against production APNs (or vice versa) returns `BadDeviceToken` and no push arrives.
