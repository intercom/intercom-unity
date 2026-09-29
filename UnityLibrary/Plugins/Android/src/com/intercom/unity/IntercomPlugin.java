package com.intercom.unity;

import android.app.Activity;
import android.util.Log;
import android.view.View;

import com.unity3d.player.UnityPlayer;

import io.intercom.android.sdk.Company;
import io.intercom.android.sdk.Intercom;
import io.intercom.android.sdk.IntercomContent;
import io.intercom.android.sdk.IntercomError;
import io.intercom.android.sdk.IntercomStatusCallback;
import io.intercom.android.sdk.UserAttributes;
import io.intercom.android.sdk.identity.Registration;
import io.intercom.android.sdk.push.IntercomPushClient;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.Set;

public class IntercomPlugin {
    private static final String TAG = "IntercomPlugin";
    private static Activity unityActivity;
    private static boolean isInitialized = false;

    public static void initialize(String apiKey, String appId) {
        try {
            unityActivity = UnityPlayer.currentActivity;
            if (unityActivity == null) {
                Log.e(TAG, "Unity activity is null");
                return;
            }

            // Unity's UnityPlayerActivity extends plain Activity, not AppCompatActivity, so it
            // has no LifecycleOwner. Jetpack Compose (used internally by Intercom SDK v16+) calls
            // ViewTreeLifecycleOwner.get() on the decor view when rendering overlays — if it
            // finds null it throws a fatal error.
            //
            // We set a LifecycleOwner via reflection so we have zero compile-time dependency on
            // androidx.lifecycle. Unity's generated unityLibrary build.gradle resolves the
            // Intercom SDK from a local Gradle cache without POM-based transitive resolution,
            // so lifecycle-runtime never lands on the compile classpath even though the classes
            // are present at runtime. Reflection bypasses that entirely.
            // Unity's UnityPlayerActivity extends plain Activity, not AppCompatActivity, so it
            // has no LifecycleOwner, SavedStateRegistryOwner, or ViewModelStoreOwner.
            // Jetpack Compose (used internally by Intercom SDK v16+) checks for all three via
            // ViewTree* helpers when a ComposeView attaches to the window — if any is missing
            // it throws a fatal IllegalStateException.
            //
            // We set all three via reflection so we have zero compile-time dependency on
            // androidx.lifecycle or androidx.savedstate. Unity's generated unityLibrary
            // build.gradle resolves the Intercom SDK from a local Gradle cache without
            // POM-based transitive resolution, so these libraries never land on the compile
            // classpath even though the classes are present at runtime.
            unityActivity.runOnUiThread(() -> {
                try {
                    View decorView = unityActivity.getWindow().getDecorView();

                    // ------------------------------------------------------------------
                    // 1. LifecycleOwner — required by Compose and SavedStateRegistry
                    // ------------------------------------------------------------------
                    Class<?> vtlcClass  = Class.forName("androidx.lifecycle.ViewTreeLifecycleOwner");
                    Class<?> ownerClass = Class.forName("androidx.lifecycle.LifecycleOwner");
                    Class<?> regClass   = Class.forName("androidx.lifecycle.LifecycleRegistry");
                    Class<?> eventClass = Class.forName("androidx.lifecycle.Lifecycle$Event");

                    if (vtlcClass.getMethod("get", View.class).invoke(null, decorView) != null) {
                        return; // already configured — nothing to do
                    }

                    // Build a LifecycleOwner backed by LifecycleRegistry.
                    // holder[0] is shared with the SavedStateRegistryOwner proxy below.
                    final Object[] holder = new Object[1];
                    Object ownerProxy = java.lang.reflect.Proxy.newProxyInstance(
                            ownerClass.getClassLoader(),
                            new Class[]{ownerClass},
                            (proxy, method, args) ->
                                    "getLifecycle".equals(method.getName()) ? holder[0] : null
                    );
                    holder[0] = regClass.getConstructor(ownerClass).newInstance(ownerProxy);

                    // Advance to RESUMED so Compose renders immediately
                    java.lang.reflect.Method handleEvent =
                            regClass.getMethod("handleLifecycleEvent", eventClass);
                    for (String name : new String[]{"ON_CREATE", "ON_START", "ON_RESUME"}) {
                        for (Object constant : eventClass.getEnumConstants()) {
                            if (name.equals(((Enum<?>) constant).name())) {
                                handleEvent.invoke(holder[0], constant);
                                break;
                            }
                        }
                    }
                    vtlcClass.getMethod("set", View.class, ownerClass)
                             .invoke(null, decorView, ownerProxy);
                    Log.d(TAG, "ViewTreeLifecycleOwner set on decor view");

                    // ------------------------------------------------------------------
                    // 2. SavedStateRegistryOwner — required by Compose since v1.5
                    //    (AndroidComposeView.onAttachedToWindow checks this explicitly)
                    //
                    //    SavedStateRegistryController.performAttach() requires the lifecycle
                    //    to still be at INITIALIZED state when it is called. We therefore
                    //    create a dedicated LifecycleRegistry for the controller — fresh at
                    //    INITIALIZED — rather than reusing holder[0] which is already at
                    //    RESUMED. After the controller is configured we advance this
                    //    dedicated registry to RESUMED so the save-state machinery is fully
                    //    active.
                    // ------------------------------------------------------------------
                    try {
                        Class<?> ssroClass   = Class.forName("androidx.savedstate.SavedStateRegistryOwner");
                        Class<?> ssrcClass   = Class.forName("androidx.savedstate.SavedStateRegistryController");
                        Class<?> vtssroClass = Class.forName("androidx.savedstate.ViewTreeSavedStateRegistryOwner");

                        // Fresh LifecycleRegistry at INITIALIZED for the controller.
                        final Object[] ssrLifeHolder = new Object[1];
                        Object ssrLifeProxy = java.lang.reflect.Proxy.newProxyInstance(
                                ownerClass.getClassLoader(),
                                new Class[]{ownerClass},
                                (proxy, method, args) ->
                                        "getLifecycle".equals(method.getName()) ? ssrLifeHolder[0] : null
                        );
                        ssrLifeHolder[0] = regClass.getConstructor(ownerClass).newInstance(ssrLifeProxy);
                        // Do NOT advance the lifecycle yet — performAttach() requires INITIALIZED.

                        // ssrHolder[0] is filled once the controller is initialised below.
                        final Object[] ssrHolder = new Object[1];
                        Object ssroProxy = java.lang.reflect.Proxy.newProxyInstance(
                                ssroClass.getClassLoader(),
                                new Class[]{ssroClass},
                                (proxy, method, args) -> {
                                    String n = method.getName();
                                    if ("getLifecycle".equals(n))          return ssrLifeHolder[0];
                                    if ("getSavedStateRegistry".equals(n)) return ssrHolder[0];
                                    return null;
                                }
                        );
                        Object controller = ssrcClass.getMethod("create", ssroClass)
                                                     .invoke(null, ssroProxy);
                        // performAttach() is present in lifecycle-savedstate >= 1.1.0.
                        // On older runtimes the method may not exist; we swallow that and
                        // let performRestore() proceed (it is self-contained on older APIs).
                        try {
                            ssrcClass.getMethod("performAttach").invoke(controller);
                        } catch (NoSuchMethodException ignored) { /* older API */ }
                        ssrcClass.getMethod("performRestore", android.os.Bundle.class)
                                 .invoke(controller, (Object) null);
                        ssrHolder[0] = ssrcClass.getMethod("getSavedStateRegistry")
                                                .invoke(controller);

                        // Now advance the dedicated lifecycle to RESUMED.
                        java.lang.reflect.Method ssrHandleEvent =
                                regClass.getMethod("handleLifecycleEvent", eventClass);
                        for (String evtName : new String[]{"ON_CREATE", "ON_START", "ON_RESUME"}) {
                            for (Object constant : eventClass.getEnumConstants()) {
                                if (evtName.equals(((Enum<?>) constant).name())) {
                                    ssrHandleEvent.invoke(ssrLifeHolder[0], constant);
                                    break;
                                }
                            }
                        }

                        vtssroClass.getMethod("set", View.class, ssroClass)
                                   .invoke(null, decorView, ssroProxy);
                        Log.d(TAG, "ViewTreeSavedStateRegistryOwner set on decor view");
                    } catch (Exception e) {
                        Log.w(TAG, "Could not set SavedStateRegistryOwner", e);
                    }

                    // ------------------------------------------------------------------
                    // 3. ViewModelStoreOwner — used by ViewModel-backed Compose screens
                    // ------------------------------------------------------------------
                    try {
                        Class<?> vmsoClass   = Class.forName("androidx.lifecycle.ViewModelStoreOwner");
                        Class<?> vmsClass    = Class.forName("androidx.lifecycle.ViewModelStore");
                        Class<?> vtvmsoClass = Class.forName("androidx.lifecycle.ViewTreeViewModelStoreOwner");

                        Object vmStore   = vmsClass.getConstructor().newInstance();
                        Object vmsoProxy = java.lang.reflect.Proxy.newProxyInstance(
                                vmsoClass.getClassLoader(),
                                new Class[]{vmsoClass},
                                (proxy, method, args) ->
                                        "getViewModelStore".equals(method.getName()) ? vmStore : null
                        );
                        vtvmsoClass.getMethod("set", View.class, vmsoClass)
                                   .invoke(null, decorView, vmsoProxy);
                        Log.d(TAG, "ViewTreeViewModelStoreOwner set on decor view");
                    } catch (Exception e) {
                        Log.w(TAG, "Could not set ViewModelStoreOwner: " + e.getMessage());
                    }

                } catch (Exception e) {
                    Log.w(TAG, "Could not set view tree owners — in-app messages may not render: "
                            + e.getMessage());
                }
            });

            Intercom.initialize(unityActivity.getApplication(), apiKey, appId);
            isInitialized = true;
            Log.d(TAG, "Intercom initialized successfully");
        } catch (Exception e) {
            Log.e(TAG, "Failed to initialize Intercom: " + e.getMessage());
        }
    }

    // Reports the Unity wrapper version to the native SDK so conversations are attributed to
    // Unity (X-INTERCOM-AGENT-WRAPPER: intercom-sdk-unity/<version>). HeaderInterceptor is
    // package-private in the SDK (annotated @UsedInCordova); the Cordova / React Native wrappers
    // reach its setXxxVersion methods reflectively, so we do the same rather than importing the
    // internal class directly. Resolves to a no-op on SDK versions that predate setUnityVersion.
    // Must be called BEFORE Intercom.initialize(): HeaderInterceptor.create() snapshots the stored
    // version once at init, so a later write only takes effect on the next app launch. Resolves its
    // own activity for that reason — initialize() has not populated unityActivity yet.
    public static void setUnityVersion(String version) {
        try {
            Activity activity = unityActivity != null ? unityActivity : UnityPlayer.currentActivity;
            if (activity == null) {
                Log.e(TAG, "Cannot set Unity version: no Unity activity");
                return;
            }
            Class.forName("io.intercom.android.sdk.api.HeaderInterceptor")
                    .getMethod("setUnityVersion", android.content.Context.class, String.class)
                    .invoke(null, activity, version);
        } catch (Exception e) {
            Log.e(TAG, "Failed to set Unity version: " + e.getMessage());
        }
    }

    // Attributes cross as JSON and are mapped here rather than on the C# side: Unity's JNI helper
    // scores method signatures without boxing, so driving UserAttributes.Builder from C# throws
    // NoSuchMethodError on every primitive overload (withSignedUpAt(Long), withCustomAttribute(
    // String, Integer), ...) and aborts the whole login. Matches the React Native / Cordova design.
    public static void loginIdentifiedUser(String userId, String email, String attributesJson) {
        if (!checkInitialized()) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Registration registration = Registration.create();

            if (userId != null && !userId.isEmpty()) {
                registration = registration.withUserId(userId);
            }

            if (email != null && !email.isEmpty()) {
                registration = registration.withEmail(email);
            }

            if (attributesJson != null && !attributesJson.isEmpty()) {
                JSONObject json = new JSONObject(attributesJson);
                if (json.length() > 0) {
                    registration = registration.withUserAttributes(buildUserAttributes(json));
                }
            }

            loginIdentifiedUser(registration);
        } catch (Exception e) {
            Log.e(TAG, "Failed to build registration: " + e.getMessage(), e);
            UnityPlayer.UnitySendMessage(
                    "IntercomManager", "HandleRegistrationFailure", String.valueOf(e.getMessage()));
        }
    }

    public static void loginIdentifiedUser(Registration registration) {
        if (!checkInitialized()) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Log.d(TAG, "Starting identified user login");
            if (registration == null) {
                Log.e(TAG, "Registration object is null");
                return;
            }

            Log.d(TAG, "Creating IntercomStatusCallback");
            IntercomStatusCallback callback =
                    new IntercomStatusCallback() {
                        @Override
                        public void onSuccess() {
                            Log.d(TAG, "User login successful");
                            UnityPlayer.UnitySendMessage(
                                    "IntercomManager", "HandleRegistrationSuccess", "");
                        }

                        @Override
                        public void onFailure(IntercomError error) {
                            Log.e(TAG, "User login failed: " + error.toString());
                            UnityPlayer.UnitySendMessage(
                                    "IntercomManager",
                                    "HandleRegistrationFailure",
                                    error.toString());
                        }
                    };

            Log.d(TAG, "Calling Intercom.client().loginIdentifiedUser");
            Intercom.client().loginIdentifiedUser(registration, callback);
            Log.d(TAG, "User login initiated successfully");
        } catch (Exception e) {
            Log.e(TAG, "Failed to login user: " + e.getMessage(), e);
            UnityPlayer.UnitySendMessage(
                    "IntercomManager", "HandleRegistrationFailure", e.getMessage());
        }
    }

    public static void loginUnidentifiedUser() {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client()
                    .loginUnidentifiedUser(
                            new IntercomStatusCallback() {
                                @Override
                                public void onSuccess() {
                                    Log.d(TAG, "Unidentified user login successful");
                                }

                                @Override
                                public void onFailure(IntercomError error) {
                                    Log.e(
                                            TAG,
                                            "Unidentified user login failed: " + error.toString());
                                }
                            });
        } catch (Exception e) {
            Log.e(TAG, "Error logging in unidentified user: " + e.getMessage());
        }
    }

    public static void logout() {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client().logout();
        } catch (Exception e) {
            Log.e(TAG, "Error logging out: " + e.getMessage());
        }
    }

    public static void present(String space) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            switch (space.toLowerCase()) {
                case "home":
                    Intercom.client().present();
                    break;
                case "messages":
                    Intercom.client().displayMessageComposer();
                    break;
                case "helpcenter":
                    Intercom.client().displayHelpCenter();
                    break;
                default:
                    Log.e(TAG, "Unknown space: " + space);
                    break;
            }
        } catch (Exception e) {
            Log.e(TAG, "Error presenting space: " + e.getMessage());
        }
    }

    public static void presentContent(String contentType, String contentId) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            switch (contentType.toLowerCase()) {
                case "article":
                    Intercom.client().displayArticle(contentId);
                    break;
                case "carousel":
                    Intercom.client().displayCarousel(contentId);
                    break;
                case "survey":
                    Intercom.client().displaySurvey(contentId);
                    break;
                case "conversation":
                    Intercom.client().presentContent(new IntercomContent.Conversation(contentId));
                    break;
                case "ticket":
                    Intercom.client().presentContent(new IntercomContent.Ticket(contentId));
                    break;
                case "helpcentercollections":
                    // Parse collection IDs - supports both single ID and comma-separated multiple IDs
                    String[] collectionIds = contentId.split(",");
                    List<String> trimmedIds = new ArrayList<>();

                    for (String id : collectionIds) {
                        String trimmedId = id.trim();
                        if (!trimmedId.isEmpty()) {
                            trimmedIds.add(trimmedId);
                        }
                    }

                    if (!trimmedIds.isEmpty()) {
                        Intercom.client().displayHelpCenterCollections(trimmedIds);
                    } else {
                        Log.e(TAG, "No valid collection IDs found in: " + contentId);
                    }
                    break;
                default:
                    Log.e(TAG, "Unknown content type: " + contentType);
                    break;
            }
        } catch (Exception e) {
            Log.e(TAG, "Error presenting content: " + e.getMessage());
        }
    }

    public static void displayMessageComposer(String initialMessage) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client().displayMessageComposer(initialMessage);
        } catch (Exception e) {
            Log.e(TAG, "Error displaying message composer: " + e.getMessage());
        }
    }

    public static void setBottomPadding(int bottomPadding) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client().setBottomPadding(bottomPadding);
        } catch (Exception e) {
            Log.e(TAG, "Error setting bottom padding: " + e.getMessage());
        }
    }

    public static void setInAppMessageVisibility(boolean isVisible) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client()
                    .setInAppMessageVisibility(
                            isVisible ? Intercom.Visibility.VISIBLE : Intercom.Visibility.GONE);
        } catch (Exception e) {
            Log.e(TAG, "Error setting in-app message visibility: " + e.getMessage());
        }
    }

    public static void setLauncherVisibility(boolean isVisible) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client()
                    .setLauncherVisibility(
                            isVisible ? Intercom.Visibility.VISIBLE : Intercom.Visibility.GONE);
        } catch (Exception e) {
            Log.e(TAG, "Error setting launcher visibility: " + e.getMessage());
        }
    }

    public static void hideIntercom() {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client().hideIntercom();
        } catch (Exception e) {
            Log.e(TAG, "Error hiding Intercom: " + e.getMessage());
        }
    }

    public static void setUserAttributes(String attributesJson) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            JSONObject json = new JSONObject(attributesJson);
            Intercom.client()
                    .updateUser(
                            buildUserAttributes(json),
                            new IntercomStatusCallback() {
                                @Override
                                public void onSuccess() {
                                    Log.d(TAG, "User attributes updated successfully");
                                }

                                @Override
                                public void onFailure(IntercomError error) {
                                    Log.e(TAG, "Failed to update user attributes: " + error.toString());
                                }
                            });
        } catch (Exception e) {
            Log.e(TAG, "Error setting user attributes: " + e.getMessage(), e);
        }
    }

    private static final Set<String> STANDARD_USER_ATTRIBUTE_KEYS = new HashSet<>(Arrays.asList(
            "name", "email", "phone",
            "userId", "user_id",
            "languageOverride", "language_override",
            "signedUpAt", "signed_up_at",
            "unsubscribedFromEmails", "unsubscribed_from_emails",
            "companies", "customAttributes", "custom_attributes"));

    private static UserAttributes buildUserAttributes(JSONObject json) throws Exception {
        UserAttributes.Builder builder = new UserAttributes.Builder();

        String name = firstString(json, "name");
        if (name != null) {
            builder.withName(name);
        }

        String email = firstString(json, "email");
        if (email != null) {
            builder.withEmail(email);
        }

        String phone = firstString(json, "phone");
        if (phone != null) {
            builder.withPhone(phone);
        }

        String userId = firstString(json, "userId", "user_id");
        if (userId != null) {
            builder.withUserId(userId);
        }

        String languageOverride = firstString(json, "languageOverride", "language_override");
        if (languageOverride != null) {
            builder.withLanguageOverride(languageOverride);
        }

        Long signedUpAt = firstUnixSeconds(json, "signedUpAt", "signed_up_at");
        if (signedUpAt != null) {
            builder.withSignedUpAt(signedUpAt);
        }

        Boolean unsubscribed = firstBoolean(json, "unsubscribedFromEmails", "unsubscribed_from_emails");
        if (unsubscribed != null) {
            builder.withUnsubscribedFromEmails(unsubscribed);
        }

        JSONArray companies = firstArray(json, "companies");
        if (companies != null) {
            for (int i = 0; i < companies.length(); i++) {
                JSONObject companyJson = companies.optJSONObject(i);
                if (companyJson != null) {
                    builder.withCompany(buildCompany(companyJson));
                }
            }
        }

        JSONObject customAttributes = firstObject(json, "customAttributes", "custom_attributes");
        if (customAttributes != null) {
            applyCustomAttributes(builder, customAttributes);
        }

        Iterator<String> keys = json.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            if (STANDARD_USER_ATTRIBUTE_KEYS.contains(key)) {
                continue;
            }
            Object value = scalarOrNull(json.get(key));
            if (value != null) {
                builder.withCustomAttribute(key, value);
            }
        }

        return builder.build();
    }

    private static Company buildCompany(JSONObject json) throws Exception {
        Company.Builder builder = new Company.Builder();

        String companyId = firstString(json, "companyId", "company_id");
        if (companyId != null) {
            builder.withCompanyId(companyId);
        }

        String name = firstString(json, "name");
        if (name != null) {
            builder.withName(name);
        }

        Long createdAt = firstUnixSeconds(json, "createdAt", "created_at");
        if (createdAt != null) {
            builder.withCreatedAt(createdAt);
        }

        Integer monthlySpend = firstInteger(json, "monthlySpend", "monthly_spend");
        if (monthlySpend != null) {
            builder.withMonthlySpend(monthlySpend);
        }

        String plan = firstString(json, "plan");
        if (plan != null) {
            builder.withPlan(plan);
        }

        JSONObject customAttributes = firstObject(json, "customAttributes", "custom_attributes");
        if (customAttributes != null) {
            applyCustomAttributes(builder, customAttributes);
        }

        return builder.build();
    }

    private static void applyCustomAttributes(UserAttributes.Builder builder, JSONObject json) throws Exception {
        Iterator<String> keys = json.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            Object value = scalarOrNull(json.get(key));
            if (value != null) {
                builder.withCustomAttribute(key, value);
            }
        }
    }

    private static void applyCustomAttributes(Company.Builder builder, JSONObject json) throws Exception {
        Iterator<String> keys = json.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            Object value = scalarOrNull(json.get(key));
            if (value != null) {
                builder.withCustomAttribute(key, value);
            }
        }
    }

    // withCustomAttribute takes Object, so every JSON scalar passes through unchanged. Nested
    // objects/arrays and JSONObject.NULL are dropped — the platform rejects them as attribute values.
    private static Object scalarOrNull(Object value) {
        if (value instanceof String || value instanceof Number || value instanceof Boolean) {
            return value;
        }
        return null;
    }

    private static Object firstValue(JSONObject json, String... keys) throws Exception {
        for (String key : keys) {
            if (json.has(key) && !json.isNull(key)) {
                return json.get(key);
            }
        }
        return null;
    }

    private static String firstString(JSONObject json, String... keys) throws Exception {
        Object value = firstValue(json, keys);
        return value instanceof String ? (String) value : null;
    }

    private static Boolean firstBoolean(JSONObject json, String... keys) throws Exception {
        Object value = firstValue(json, keys);
        return value instanceof Boolean ? (Boolean) value : null;
    }

    private static Integer firstInteger(JSONObject json, String... keys) throws Exception {
        Object value = firstValue(json, keys);
        return value instanceof Number ? ((Number) value).intValue() : null;
    }

    private static Long firstUnixSeconds(JSONObject json, String... keys) throws Exception {
        Object value = firstValue(json, keys);
        return value instanceof Number ? ((Number) value).longValue() : null;
    }

    private static JSONObject firstObject(JSONObject json, String... keys) throws Exception {
        Object value = firstValue(json, keys);
        return value instanceof JSONObject ? (JSONObject) value : null;
    }

    private static JSONArray firstArray(JSONObject json, String... keys) throws Exception {
        Object value = firstValue(json, keys);
        return value instanceof JSONArray ? (JSONArray) value : null;
    }

    public static void logEvent(String name) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client().logEvent(name);
        } catch (Exception e) {
            Log.e(TAG, "Error logging event: " + e.getMessage());
        }
    }

    public static void logEventWithMetadata(String name, String metadataJson) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Map<String, Object> metadata = new HashMap<>();
            JSONObject json = new JSONObject(metadataJson);

            Iterator<String> keys = json.keys();
            while (keys.hasNext()) {
                String key = keys.next();
                Object value = json.get(key);
                metadata.put(key, value);
            }

            Intercom.client().logEvent(name, metadata);
        } catch (Exception e) {
            Log.e(TAG, "Error logging event with metadata: " + e.getMessage());
        }
    }

    public static void setUserHash(String userHash) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client().setUserHash(userHash);
        } catch (Exception e) {
            Log.e(TAG, "Error setting user hash: " + e.getMessage());
        }
    }

    public static void setUserJwt(String jwt) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            Intercom.client().setUserJwt(jwt);
        } catch (Exception e) {
            Log.e(TAG, "Error setting user JWT: " + e.getMessage());
        }
    }

    public static void setAuthTokens(String authTokensJson) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            JSONObject json = new JSONObject(authTokensJson);
            if (json.has("userHash")) {
                setUserHash(json.getString("userHash"));
            }
            if (json.has("jwt")) {
                setUserJwt(json.getString("jwt"));
            }
        } catch (Exception e) {
            Log.e(TAG, "Error setting auth tokens: " + e.getMessage());
        }
    }

    public static void setLogLevel(String logLevel) {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return;
        }

        try {
            switch (logLevel.toLowerCase()) {
                case "verbose":
                    Intercom.client().setLogLevel(Intercom.LogLevel.VERBOSE);
                    break;
                case "debug":
                    Intercom.client().setLogLevel(Intercom.LogLevel.DEBUG);
                    break;
                case "info":
                    Intercom.client().setLogLevel(Intercom.LogLevel.INFO);
                    break;
                case "warn":
                    Intercom.client().setLogLevel(Intercom.LogLevel.WARN);
                    break;
                case "error":
                    Intercom.client().setLogLevel(Intercom.LogLevel.ERROR);
                    break;
                case "assert":
                    Intercom.client().setLogLevel(Intercom.LogLevel.ASSERT);
                    break;
                case "disabled":
                    Intercom.client().setLogLevel(Intercom.LogLevel.DISABLED);
                    break;
                default:
                    Log.e(TAG, "Unknown log level: " + logLevel);
                    break;
            }
        } catch (Exception e) {
            Log.e(TAG, "Error setting log level: " + e.getMessage());
        }
    }

    public static int getUnreadConversationCount() {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return 0;
        }

        try {
            return Intercom.client().getUnreadConversationCount();
        } catch (Exception e) {
            Log.e(TAG, "Error getting unread conversation count: " + e.getMessage());
            return 0;
        }
    }

    public static boolean isUserLoggedIn() {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return false;
        }

        try {
            return Intercom.client().isUserLoggedIn();
        } catch (Exception e) {
            Log.e(TAG, "Error checking if user is logged in: " + e.getMessage());
            return false;
        }
    }

    public static String fetchLoggedInUserAttributes() {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return "{}";
        }

        try {
            if (!Intercom.client().isUserLoggedIn()) {
                return "{}";
            }

            JSONObject json = new JSONObject();
            Registration registration = Intercom.client().fetchLoggedInUserAttributes();
            if (registration != null) {
                json.put("userId", registration.getUserId());
                json.put("email", registration.getEmail());
            }

            return json.toString();
        } catch (Exception e) {
            Log.e(TAG, "Error fetching user attributes: " + e.getMessage());
            return "{}";
        }
    }

    public static void sendPushTokenToIntercom(String token) {
        if (!checkInitialized()) {
            return;
        }

        try {
            new IntercomPushClient().sendTokenToIntercom(unityActivity.getApplication(), token);
            Log.d(TAG, "Push token sent to Intercom");
        } catch (Exception e) {
            Log.e(TAG, "Error sending push token to Intercom: " + e.getMessage());
        }
    }

    public static void requestPushPermission() {
        if (unityActivity == null) {
            Log.e(TAG, "Cannot request notification permission before initialization");
            return;
        }

        if (android.os.Build.VERSION.SDK_INT >= 33) {
            try {
                unityActivity.requestPermissions(
                        new String[] {"android.permission.POST_NOTIFICATIONS"}, 0);
            } catch (Exception e) {
                Log.e(TAG, "Error requesting notification permission: " + e.getMessage());
            }
        }
    }

    public static boolean isIntercomPush(String dataJson) {
        try {
            return new IntercomPushClient().isIntercomPush(jsonToMap(dataJson));
        } catch (Exception e) {
            Log.e(TAG, "Error checking Intercom push: " + e.getMessage());
            return false;
        }
    }

    public static void handlePush(String dataJson) {
        if (!checkInitialized()) {
            return;
        }

        try {
            new IntercomPushClient().handlePush(unityActivity.getApplication(), jsonToMap(dataJson));
        } catch (Exception e) {
            Log.e(TAG, "Error handling Intercom push: " + e.getMessage());
        }
    }

    private static Map<String, String> jsonToMap(String dataJson) throws Exception {
        Map<String, String> map = new HashMap<>();
        if (dataJson == null || dataJson.isEmpty()) {
            return map;
        }

        JSONObject json = new JSONObject(dataJson);
        Iterator<String> keys = json.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            map.put(key, json.getString(key));
        }
        return map;
    }

    private static boolean checkInitialized() {
        if (!isInitialized) {
            Log.e(TAG, "Intercom not initialized");
            return false;
        }
        return true;
    }
}
