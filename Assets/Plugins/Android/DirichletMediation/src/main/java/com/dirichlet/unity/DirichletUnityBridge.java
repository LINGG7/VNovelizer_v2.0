package com.dirichlet.unity;

import android.app.Activity;
import android.app.Application;
import android.graphics.Color;
import android.os.Handler;
import android.os.Looper;
import android.text.TextUtils;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewParent;
import android.widget.FrameLayout;

import com.tapsdk.tapad.group.AdNetworkType;
import com.tapsdk.tapad.group.DirichletAdConfig;
import com.tapsdk.tapad.group.DirichletAdManager;
import com.tapsdk.tapad.group.DirichletAdNative;
import com.tapsdk.tapad.group.DirichletAdRequest;
import com.tapsdk.tapad.group.DirichletSdk;
import com.tapsdk.tapad.group.ads.DirichletBannerAd;
import com.tapsdk.tapad.group.ads.DirichletInterstitialAd;
import com.tapsdk.tapad.group.ads.DirichletRewardVideoAd;
import com.tapsdk.tapad.group.ads.DirichletSplashAd;
import com.tapsdk.tapad.group.DirichletAdCustomController;
import com.unity3d.player.UnityPlayer;

import org.json.JSONException;
import org.json.JSONObject;

import java.lang.reflect.Method;
import java.util.Map;
import java.util.UUID;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Locale;
import java.util.Collections;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Unity bridge for Dirichlet Mediation SDK.
 * 
 * This bridge provides a Unity-compatible interface that closely mirrors the native SDK API.
 * The API design follows the pattern:
 * - DirichletAdManager.get().createAdNative(context) -> loadXXXAd() methods
 * - Ad objects returned from loadXXXAd() -> show() and destroy() methods
 * 
 * Usage pattern:
 * 1. Initialize SDK: initialize(...)
 * 2. Load ad: loadRewardVideoAd(...) / loadInterstitialAd(...) / etc.
 * 3. Show ad: showAd(handle, options)
 * 4. Destroy ad: destroyAd(handle)
 * 
 * @see com.tapsdk.tapad.group.DirichletAdNative
 * @see com.tapsdk.tapad.group.DirichletAdManager
 */
@SuppressWarnings("unused")
public final class DirichletUnityBridge {

    private static final String TAG = "DirichletUnityBridge";
    private static final long INIT_TIMEOUT_MS = 5_000L;

    private static final String UNITY_CALLBACK_OBJECT = "DirichletMediationEventReceiver";
    private static final String UNITY_CALLBACK_METHOD = "OnNativeEvent";
    private static final String EVENT_SHOW = "show";
    private static final String EVENT_CLOSE = "close";
    private static final String EVENT_CLICK = "click";
    private static final String EVENT_REWARD = "reward";

    private static final int TYPE_REWARD = 0;
    private static final int TYPE_INTERSTITIAL = 1;
    private static final int TYPE_BANNER = 2;
    private static final int TYPE_SPLASH = 3;
    private static final int TYPE_EXPRESS_FEED = 4;
    private static final int TYPE_NATIVE_FEED = 5;

    /**
     * Cache of ad entries mapping handle IDs to ad instances.
     * This allows Unity to reference Java objects via string handles.
     */
    private static final Map<String, AdEntry> AD_CACHE = new ConcurrentHashMap<>();
    private static final DirichletAdCustomController UNITY_CUSTOM_CONTROLLER = new UnityCustomController();

    /**
     * Singleton DirichletAdNative instance for auto-ad caching.
     * Must be created once and reused to preserve the internal ad cache (rewardVideoAdMap).
     * Used specifically by showRewardVideoAutoAd to maintain cache across calls.
     */
    private static volatile DirichletAdNative sAutoAdNative = null;
    private static final Object sAutoAdNativeLock = new Object();

    private static final Map<String, String> REQUEST_BUILDER_METHODS = buildRequestMethodMap();

    private DirichletUnityBridge() {
        // Private constructor to prevent instantiation
    }

    /**
     * Listener interface for ad load callbacks.
     * Mirrors the native SDK listener pattern.
     */
    public interface LoadListener {
        /**
         * Called when ad load succeeds.
         */
        void onSuccess();

        /**
         * Called when ad load fails.
         * 
         * @param code Error code
         * @param message Error message
         */
        void onError(String code, String message);
    }

    public static boolean initialize(String appId,
                                     String channel,
                                     String subChannel,
                                     boolean enableLog,
                                     String mediaName,
                                     String mediaKey,
                                     String tapClientId,
                                     String dataJson,
                                     boolean shakeEnabled) {
        // Never block the Android main thread (Unity often runs game loop on it).
        // The synchronous boolean return is best-effort only.
        if (Looper.getMainLooper() == Looper.myLooper()) {
            Log.w(TAG, "initialize called on main thread; falling back to async init to avoid ANR");
            initializeAsync(appId, channel, subChannel, enableLog, mediaName, mediaKey, tapClientId, dataJson, shakeEnabled, null);
            return true;
        }

        return performInitialization(appId, channel, subChannel, enableLog, mediaName, mediaKey, tapClientId, dataJson, shakeEnabled);
    }

    /**
     * Asynchronous initialization. This is the preferred entry point for Unity C# to avoid blocking the game loop.
     * It reports completion via the provided listener. A timeout is enforced so Unity always receives a result.
     */
    public static void initializeAsync(String appId,
                                       String channel,
                                       String subChannel,
                                       boolean enableLog,
                                       String mediaName,
                                       String mediaKey,
                                       String tapClientId,
                                       String dataJson,
                                       boolean shakeEnabled,
                                       LoadListener listener) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            Log.e(TAG, "initializeAsync: Unity activity is null");
            if (listener != null) {
                listener.onError("activity_null", "Unity activity is null");
            }
            return;
        }

        final Application application = activity.getApplication();
        final AtomicBoolean completed = new AtomicBoolean(false);
        final Handler handler = new Handler(Looper.getMainLooper());

        final Runnable timeoutTask = () -> {
            if (listener == null) {
                return;
            }
            if (completed.compareAndSet(false, true)) {
                Log.w(TAG, "initializeAsync timed out waiting for callback");
                listener.onError("timeout", "initialize timed out");
            }
        };
        handler.postDelayed(timeoutTask, INIT_TIMEOUT_MS);

        activity.runOnUiThread(() -> {
            try {
                long mediaId = safeParseLong(appId, 0L);
                String dataPayload = mergeDataPayload(subChannel, dataJson);
                DirichletAdConfig config = buildConfig(mediaId, channel, enableLog, mediaName, mediaKey, tapClientId, dataPayload, shakeEnabled);

                DirichletSdk.InitListener sdkListener = new DirichletSdk.InitListener() {
                    @Override
                    public void onInitSuccess() {
                        if (listener == null) {
                            return;
                        }
                        if (completed.compareAndSet(false, true)) {
                            handler.removeCallbacks(timeoutTask);
                            listener.onSuccess();
                        }
                    }

                    @Override
                    public void onInitFail(int code, String msg) {
                        if (listener == null) {
                            return;
                        }
                        if (completed.compareAndSet(false, true)) {
                            handler.removeCallbacks(timeoutTask);
                            listener.onError(String.valueOf(code), msg);
                        }
                    }
                };

                DirichletSdk.init(application, config, sdkListener);
            } catch (Throwable t) {
                Log.e(TAG, "initializeAsync error", t);
                if (listener != null && completed.compareAndSet(false, true)) {
                    handler.removeCallbacks(timeoutTask);
                    listener.onError("exception", t.getMessage() != null ? t.getMessage() : t.getClass().getSimpleName());
                }
            }
        });
    }

    private static boolean performInitialization(String appId,
                                                  String channel,
                                                  String subChannel,
                                                  boolean enableLog,
                                                  String mediaName,
                                                  String mediaKey,
                                                  String tapClientId,
                                                  String dataJson,
                                                  boolean shakeEnabled) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            Log.e(TAG, "initialize: Unity activity is null");
            return false;
        }

        final Application application = activity.getApplication();
        final CountDownLatch latch = new CountDownLatch(1);
        final AtomicBoolean success = new AtomicBoolean(false);
        final AtomicReference<String> failureMessage = new AtomicReference<>();

        activity.runOnUiThread(() -> {
            try {
                long mediaId = safeParseLong(appId, 0L);
                String dataPayload = mergeDataPayload(subChannel, dataJson);
                DirichletAdConfig config = buildConfig(mediaId, channel, enableLog, mediaName, mediaKey, tapClientId, dataPayload, shakeEnabled);

                DirichletSdk.InitListener listener = new DirichletSdk.InitListener() {
                    @Override
                    public void onInitSuccess() {
                        success.set(true);
                        latch.countDown();
                    }

                    @Override
                    public void onInitFail(int code, String msg) {
                        failureMessage.set("code=" + code + ", msg=" + msg);
                        latch.countDown();
                    }
                };

                DirichletSdk.init(application, config, listener);
            } catch (Throwable t) {
                Log.e(TAG, "initialize error", t);
                failureMessage.set(t.getMessage());
                latch.countDown();
            }
        });

        try {
            if (!latch.await(INIT_TIMEOUT_MS, TimeUnit.MILLISECONDS)) {
                Log.w(TAG, "initialize timed out waiting for callback");
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }

        if (!success.get() && failureMessage.get() != null) {
            Log.e(TAG, "initialize failed: " + failureMessage.get());
        }

        return success.get();
    }

    private static DirichletAdConfig buildConfig(long mediaId,
                                                 String channel,
                                                 boolean enableLog,
                                                 String mediaName,
                                                 String mediaKey,
                                                 String tapClientId,
                                                 String dataPayload,
                                                 boolean shakeEnabled) {
        DirichletAdConfig.Builder builder = new DirichletAdConfig.Builder()
                .withMediaId(mediaId)
                .enableDebug(enableLog)
                .shakeEnabled(shakeEnabled)
                .withCustomController(UNITY_CUSTOM_CONTROLLER);

        if (!TextUtils.isEmpty(mediaName)) {
            builder.withMediaName(mediaName);
        }

        if (!TextUtils.isEmpty(mediaKey)) {
            builder.withMediaKey(mediaKey);
        }

        if (!TextUtils.isEmpty(channel)) {
            builder.withGameChannel(channel);
        }

        if (!TextUtils.isEmpty(tapClientId)) {
            builder.withTapClientId(tapClientId);
        }

        if (!TextUtils.isEmpty(dataPayload)) {
            builder.withData(dataPayload);
        }

        return builder.build();
    }

    private static Integer toInteger(Object value) {
        if (value instanceof Integer) {
            return (Integer) value;
        }
        if (value instanceof Long) {
            return ((Long) value).intValue();
        }
        if (value instanceof Double) {
            return ((Double) value).intValue();
        }
        if (value instanceof String) {
            try {
                return Integer.parseInt(((String) value).trim());
            } catch (NumberFormatException ignore) {
                return null;
            }
        }
        return null;
    }

    private static void applyGenericBuilderValue(DirichletAdRequest.Builder builder, String normalizedKey, Object value) {
        if (builder == null || normalizedKey == null || normalizedKey.isEmpty()) {
            return;
        }

        String methodName = REQUEST_BUILDER_METHODS.get(normalizedKey);
        if (methodName == null) {
            methodName = "with" + toPascalCase(normalizedKey);
        }

        if (methodName == null || methodName.isEmpty()) {
            return;
        }

        if (!invokeBuilderWithSingleArg(builder, methodName, value)) {
            if (!(value instanceof String)) {
                invokeBuilderWithSingleArg(builder, methodName, String.valueOf(value));
            }
        }
    }

    private static boolean invokeBuilderWithSingleArg(Object target, String methodName, Object value) {
        if (target == null || value == null || methodName == null || methodName.isEmpty()) {
            return false;
        }

        if (value instanceof Integer) {
            int intValue = (Integer) value;
            if (tryInvoke(target, methodName, int.class, intValue)) return true;
            if (tryInvoke(target, methodName, Integer.class, intValue)) return true;
            if (tryInvoke(target, methodName, long.class, (long) intValue)) return true;
            if (tryInvoke(target, methodName, Long.class, (long) intValue)) return true;
        } else if (value instanceof Long) {
            long longValue = (Long) value;
            if (tryInvoke(target, methodName, long.class, longValue)) return true;
            if (tryInvoke(target, methodName, Long.class, longValue)) return true;
            if (tryInvoke(target, methodName, int.class, (int) longValue)) return true;
            if (tryInvoke(target, methodName, Integer.class, (int) longValue)) return true;
        } else if (value instanceof Double) {
            double doubleValue = (Double) value;
            if (tryInvoke(target, methodName, double.class, doubleValue)) return true;
            if (tryInvoke(target, methodName, Double.class, doubleValue)) return true;
            if (tryInvoke(target, methodName, float.class, (float) doubleValue)) return true;
            if (tryInvoke(target, methodName, Float.class, (float) doubleValue)) return true;
        } else if (value instanceof Boolean) {
            boolean boolValue = (Boolean) value;
            if (tryInvoke(target, methodName, boolean.class, boolValue)) return true;
            if (tryInvoke(target, methodName, Boolean.class, boolValue)) return true;
        } else if (value instanceof String) {
            String stringValue = ((String) value).trim();
            if (!stringValue.isEmpty()) {
                try {
                    int parsed = Integer.parseInt(stringValue);
                    if (tryInvoke(target, methodName, int.class, parsed)) return true;
                    if (tryInvoke(target, methodName, Integer.class, parsed)) return true;
                    if (tryInvoke(target, methodName, long.class, (long) parsed)) return true;
                    if (tryInvoke(target, methodName, Long.class, (long) parsed)) return true;
                } catch (NumberFormatException ignore) {
                    try {
                        long parsedLong = Long.parseLong(stringValue);
                        if (tryInvoke(target, methodName, long.class, parsedLong)) return true;
                        if (tryInvoke(target, methodName, Long.class, parsedLong)) return true;
                    } catch (NumberFormatException ignoredLong) {
                        try {
                            double parsedDouble = Double.parseDouble(stringValue);
                            if (tryInvoke(target, methodName, double.class, parsedDouble)) return true;
                            if (tryInvoke(target, methodName, Double.class, parsedDouble)) return true;
                        } catch (NumberFormatException ignoredDouble) {
                            // fall through to string invocation
                        }
                    }
                }
                if (tryInvoke(target, methodName, String.class, stringValue)) {
                    return true;
                }
            }
            if (tryInvoke(target, methodName, String.class, stringValue)) {
                return true;
            }
            return false;
        }

        return tryInvoke(target, methodName, String.class, value.toString());
    }

    private static void invokeBuilderTwoInts(Object target, String methodName, int first, int second) {
        if (target == null || methodName == null || methodName.isEmpty()) {
            return;
        }

        if (tryInvokeTwoArgs(target, methodName, int.class, int.class, first, second)) {
            return;
        }

        tryInvokeTwoArgs(target, methodName, Integer.class, Integer.class, first, second);
    }

    private static boolean tryInvoke(Object target, String methodName, Class<?> paramType, Object argument) {
        try {
            Method method = target.getClass().getMethod(methodName, paramType);
            method.setAccessible(true);
            method.invoke(target, argument);
            return true;
        } catch (NoSuchMethodException ignored) {
            return false;
        } catch (Throwable t) {
            Log.w(TAG, "Failed to invoke " + methodName + " with " + paramType + ": " + t.getMessage());
            return false;
        }
    }

    private static boolean tryInvokeTwoArgs(Object target, String methodName, Class<?> firstType, Class<?> secondType, Object arg1, Object arg2) {
        try {
            Method method = target.getClass().getMethod(methodName, firstType, secondType);
            method.setAccessible(true);
            method.invoke(target, arg1, arg2);
            return true;
        } catch (NoSuchMethodException ignored) {
            return false;
        } catch (Throwable t) {
            Log.w(TAG, "Failed to invoke " + methodName + " with two args: " + t.getMessage());
            return false;
        }
    }

    private static String toPascalCase(String key) {
        if (key == null || key.isEmpty()) {
            return "";
        }

        StringBuilder builder = new StringBuilder(key.length());
        boolean capitalizeNext = true;
        for (int i = 0; i < key.length(); i++) {
            char ch = key.charAt(i);
            if (Character.isLetterOrDigit(ch)) {
                if (capitalizeNext) {
                    builder.append(Character.toUpperCase(ch));
                    capitalizeNext = false;
                } else {
                    builder.append(ch);
                }
            } else {
                capitalizeNext = true;
            }
        }

        return builder.toString();
    }

    private static JSONObject cloneJson(JSONObject value) {
        if (value == null) {
            return null;
        }
        try {
            return new JSONObject(value.toString());
        } catch (Exception ignored) {
            return null;
        }
    }

    private static void applyAdOptionSetters(AdEntry entry, JSONObject options) {
        if (entry == null || options == null) {
            return;
        }

        Object target = resolveAdObject(entry);
        if (target == null) {
            return;
        }

        Iterator<String> keys = options.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            if (key == null) {
                continue;
            }

            String normalized = key.trim().toLowerCase(Locale.US);
            if ("banner_baseline".equals(normalized) || "banner_offset".equals(normalized)) {
                continue; // handled explicitly in show logic
            }

            Object value = options.opt(key);
            if (value == null || JSONObject.NULL.equals(value)) {
                continue;
            }

            String methodName = "set" + toPascalCase(normalized);
            invokeBuilderWithSingleArg(target, methodName, value);
        }
    }

    private static Object resolveAdObject(AdEntry entry) {
        return entry != null ? entry.getAdObject() : null;
    }

    public static void requestPermissionIfNeeded() {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            Log.w(TAG, "requestPermissionIfNeeded: activity is null");
            return;
        }

        activity.runOnUiThread(() -> DirichletAdManager.get().requestPermissionIfNecessary(activity));
    }

    public static String getSdkVersion() {
        try {
            return String.valueOf(DirichletSdk.getVersion());
        } catch (Throwable t) {
            Log.w(TAG, "getSdkVersion failed", t);
            return "android-error";
        }
    }

    /**
     * Loads a reward video ad, matching the native SDK pattern.
     * This follows DirichletAdNative.loadRewardVideoAd() from the native SDK.
     * 
     * @param extras Request parameters as JSON (must include space_id)
     * @param listener Callback for load success/failure
     * @return Handle ID for the ad instance (can be used for show/destroy operations)
     */
    public static String loadRewardVideoAd(JSONObject extras, LoadListener listener) {
        return loadAdInternal(TYPE_REWARD, extras, listener);
    }

    /**
     * Loads an interstitial ad, matching the native SDK pattern.
     * This follows DirichletAdNative.loadInterstitialAd() from the native SDK.
     * 
     * @param extras Request parameters as JSON (must include space_id)
     * @param listener Callback for load success/failure
     * @return Handle ID for the ad instance (can be used for show/destroy operations)
     */
    public static String loadInterstitialAd(JSONObject extras, LoadListener listener) {
        return loadAdInternal(TYPE_INTERSTITIAL, extras, listener);
    }

    /**
     * Loads a banner ad, matching the native SDK pattern.
     * This follows DirichletAdNative.loadBannerAd() from the native SDK.
     * 
     * @param extras Request parameters as JSON (must include space_id)
     * @param listener Callback for load success/failure
     * @return Handle ID for the ad instance (can be used for show/destroy operations)
     */
    public static String loadBannerAd(JSONObject extras, LoadListener listener) {
        return loadAdInternal(TYPE_BANNER, extras, listener);
    }

    /**
     * Loads a splash ad, matching the native SDK pattern.
     * This follows DirichletAdNative.loadSplashAd() from the native SDK.
     * 
     * @param extras Request parameters as JSON (must include space_id)
     * @param listener Callback for load success/failure
     * @return Handle ID for the ad instance (can be used for show/destroy operations)
     */
    public static String loadSplashAd(JSONObject extras, LoadListener listener) {
        return loadAdInternal(TYPE_SPLASH, extras, listener);
    }

    /**
     * Listener interface for auto reward video ad callbacks.
     * This combines load and show callbacks into a single interface.
     * Mirrors DirichletAdNative.RewardVideoAutoAdListener from the native SDK.
     */
    public interface RewardVideoAutoAdListener {
        /**
         * Called when ad fails to load or show.
         * 
         * @param code Error code
         * @param message Error message
         */
        void onError(String code, String message);

        /**
         * Called when ad is shown to the user.
         */
        void onAdShow();

        /**
         * Called when ad is closed.
         */
        void onAdClose();

        /**
         * Called when reward verification is completed.
         * 
         * @param rewardVerify Whether the reward was verified
         * @param rewardAmount Reward amount
         * @param rewardName Reward name
         * @param code Verification code
         * @param msg Verification message
         */
        void onRewardVerify(boolean rewardVerify, int rewardAmount, String rewardName, int code, String msg);

        /**
         * Called when ad is clicked.
         */
        void onAdClick();
    }

    /**
     * Shows a reward video ad with automatic load-and-show logic.
     * This follows DirichletAdNative.showRewardVideoAutoAd() from the native SDK.
     * 
     * The method will:
     * 1. Show cached ad immediately if available and valid
     * 2. Load a new ad in the background for next time
     * 3. If no cached ad, wait for load and then show
     * 
     * @param extras Request parameters as JSON (must include space_id)
     * @param listener Callback for all ad events (load/show/close/reward/click/error)
     */
    public static void showRewardVideoAutoAd(JSONObject extras, RewardVideoAutoAdListener listener) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            if (listener != null) {
                listener.onError("activity_null", "Unity activity is null");
            }
            return;
        }

        if (extras == null) {
            if (listener != null) {
                listener.onError("invalid_request", "Request extras cannot be null, must include space_id");
            }
            return;
        }

        long spaceId = extras.optLong("space_id", 0L);
        if (spaceId <= 0) {
            if (listener != null) {
                listener.onError("invalid_space_id", "space_id must be provided and greater than zero in extras");
            }
            return;
        }

        activity.runOnUiThread(() -> {
            try {
                // Use singleton to preserve cache (rewardVideoAdMap) across calls
                DirichletAdNative adNative = getOrCreateAutoAdNative(activity);
                DirichletAdRequest.Builder builder = new DirichletAdRequest.Builder().withSpaceId(spaceId);

                // Apply optional request parameters
                String userId = extras.optString("user_id", null);
                if (userId != null && !userId.isEmpty()) {
                    builder.withUserId(userId);
                }

                String rewardName = extras.optString("reward_name", null);
                if (rewardName != null && !rewardName.isEmpty()) {
                    builder.withRewardName(rewardName);
                }

                int rewardAmount = extras.optInt("reward_amount", 0);
                if (rewardAmount > 0) {
                    builder.withRewardAmount(rewardAmount);
                }

                String query = extras.optString("query", null);
                if (query != null && !query.isEmpty()) {
                    builder.withQuery(query);
                }

                String extra1 = extras.optString("extra1", null);
                if (extra1 != null && !extra1.isEmpty()) {
                    builder.withExtra1(extra1);
                }

                DirichletAdRequest request = builder.build();

                // Create the native listener that bridges to Unity callbacks
                DirichletAdNative.RewardVideoAutoAdListener nativeListener = new DirichletAdNative.RewardVideoAutoAdListener() {
                    @Override
                    public void onError(int code, String message) {
                        if (listener != null) {
                            listener.onError(String.valueOf(code), message);
                        }
                    }

                    @Override
                    public void onAdShow() {
                        if (listener != null) {
                            listener.onAdShow();
                        }
                    }

                    @Override
                    public void onAdClose() {
                        if (listener != null) {
                            listener.onAdClose();
                        }
                    }

                    @Override
                    public void onRewardVerify(boolean rewardVerify, int rewardAmount, String rewardName, int code, String msg) {
                        if (listener != null) {
                            listener.onRewardVerify(rewardVerify, rewardAmount, rewardName, code, msg);
                        }
                    }

                    @Override
                    public void onAdClick() {
                        if (listener != null) {
                            listener.onAdClick();
                        }
                    }
                };

                adNative.showRewardVideoAutoAd(request, activity, nativeListener);
            } catch (Throwable t) {
                Log.e(TAG, "showRewardVideoAutoAd exception", t);
                if (listener != null) {
                    listener.onError("exception", t.getMessage() != null ? t.getMessage() : t.getClass().getSimpleName());
                }
            }
        });
    }

    /**
     * Gets or creates a singleton DirichletAdNative instance for auto-ad caching.
     * The cache (rewardVideoAdMap) is stored as an instance variable in DirichletAdNativeImpl,
     * so we must reuse the same instance to preserve cached ads across showRewardVideoAutoAd calls.
     * 
     * @param activity The current activity context
     * @return The singleton DirichletAdNative instance
     */
    private static DirichletAdNative getOrCreateAutoAdNative(Activity activity) {
        if (sAutoAdNative == null) {
            synchronized (sAutoAdNativeLock) {
                if (sAutoAdNative == null) {
                    sAutoAdNative = DirichletAdManager.get().createAdNative(activity);
                    Log.d(TAG, "Created singleton DirichletAdNative for auto-ad caching");
                }
            }
        }
        return sAutoAdNative;
    }

    /**
     * Internal method to load an ad of the specified type.
     * Creates an AdEntry, stores it in cache, and initiates the load process.
     */
    private static String loadAdInternal(int adType, JSONObject extras, LoadListener listener) {
        String handle = UUID.randomUUID().toString();
        AdEntry entry = new AdEntry(handle, adType);
        // Only add to cache after validation passes
        // If loadAdWithEntry fails early, entry won't be in cache
        loadAdWithEntry(entry, extras, listener);
        return handle;
    }

    /**
     * Internal method to perform the actual ad loading using DirichletAdNative.
     * This matches the native SDK pattern: DirichletAdManager.get().createAdNative(context)
     */
    private static void loadAdWithEntry(AdEntry entry, JSONObject extras, LoadListener listener) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            if (listener != null) {
                listener.onError("activity_null", "Unity activity is null");
            }
            return;
        }

        // Validate extras before adding to cache
        try {
            if (extras == null) {
                if (listener != null) {
                    listener.onError("invalid_request", "Request extras cannot be null, must include space_id");
                }
                return;
            }

            long spaceId = extras.optLong("space_id", 0L);
            if (spaceId <= 0) {
                if (listener != null) {
                    listener.onError("invalid_space_id", "space_id must be provided and greater than zero in extras");
                }
                return;
            }
        } catch (Exception e) {
            Log.e(TAG, "loadAd validation error", e);
            if (listener != null) {
                listener.onError("invalid_request", "Failed to validate request: " + e.getMessage());
            }
            return;
        }

        // Only add to cache after validation passes
        AD_CACHE.put(entry.handle, entry);

        activity.runOnUiThread(() -> {
            try {
                // Create AdNative instance, matching DirichletAdManager.get().createAdNative(context)
                DirichletAdNative adNative = DirichletAdManager.get().createAdNative(activity);
                DirichletAdRequest request = buildRequest(entry, extras);

                switch (entry.type) {
                    case TYPE_REWARD:
                        adNative.loadRewardVideoAd(request, new DirichletAdNative.RewardVideoAdListener() {
                            @Override
                            public void onRewardVideoAdLoad(DirichletRewardVideoAd rewardVideoAd) {
                                entry.rewardAd = rewardVideoAd;
                                attachRewardInteraction(entry);
                                if (listener != null) {
                                    listener.onSuccess();
                                }
                            }

                            @Override
                            public void onError(int code, String message) {
                                if (listener != null) {
                                    listener.onError(String.valueOf(code), message);
                                }
                            }
                        });
                        break;
                    case TYPE_INTERSTITIAL:
                        adNative.loadInterstitialAd(request, new DirichletAdNative.InterstitialAdListener() {
                            @Override
                            public void onInterstitialAdLoad(DirichletInterstitialAd interstitialAd) {
                                entry.interstitialAd = interstitialAd;
                                attachInterstitialInteraction(entry);
                                if (listener != null) {
                                    listener.onSuccess();
                                }
                            }

                            @Override
                            public void onError(int code, String message) {
                                if (listener != null) {
                                    listener.onError(String.valueOf(code), message);
                                }
                            }
                        });
                        break;
                    case TYPE_BANNER:
                        adNative.loadBannerAd(request, new DirichletAdNative.BannerAdListener() {
                            @Override
                            public void onBannerAdLoad(DirichletBannerAd bannerAd) {
                                entry.bannerAd = bannerAd;
                                attachBannerInteraction(entry);
                                if (listener != null) {
                                    listener.onSuccess();
                                }
                            }

                            @Override
                            public void onError(int code, String message) {
                                if (listener != null) {
                                    listener.onError(String.valueOf(code), message);
                                }
                            }
                        });
                        break;
                    case TYPE_SPLASH:
                        adNative.loadSplashAd(request, new DirichletAdNative.SplashAdListener() {
                            @Override
                            public void onSplashAdLoad(DirichletSplashAd splashAd) {
                                entry.splashAd = splashAd;
                                attachSplashInteraction(entry);
                                if (listener != null) {
                                    listener.onSuccess();
                                }
                            }

                            @Override
                            public void onError(int code, String message) {
                                if (listener != null) {
                                    listener.onError(String.valueOf(code), message);
                                }
                            }
                        });
                        break;
                    default:
                        if (listener != null) {
                            listener.onError("unsupported_type", "Unsupported ad type: " + entry.type);
                        }
                        break;
                }
            } catch (IllegalArgumentException e) {
                // Remove entry from cache on validation failure
                AD_CACHE.remove(entry.handle);
                Log.e(TAG, "loadAd validation failed", e);
                if (listener != null) {
                    listener.onError("invalid_request", e.getMessage());
                }
            } catch (Throwable t) {
                // Remove entry from cache on other exceptions
                AD_CACHE.remove(entry.handle);
                Log.e(TAG, "loadAd exception", t);
                if (listener != null) {
                    String errorCode = t instanceof IllegalArgumentException ? "invalid_request" : "exception";
                    listener.onError(errorCode, t.getMessage() != null ? t.getMessage() : t.getClass().getSimpleName());
                }
            }
        });
    }

    /**
     * Shows an ad using the handle returned from loadRewardVideoAd/loadInterstitialAd/etc.
     * This matches the native SDK pattern where ad objects have show() methods.
     * 
     * @param handle The handle ID returned from the load method
     * @param options Show options (e.g., banner alignment, offset)
     * @return true if the ad was shown successfully, false otherwise
     */
    public static boolean showAd(String handle, JSONObject options) {
        AdEntry entry = AD_CACHE.get(handle);
        if (entry == null) {
            Log.w(TAG, "showAd: handle not found " + handle);
            return false;
        }

        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            Log.w(TAG, "showAd: activity null");
            return false;
        }

        CountDownLatch latch = new CountDownLatch(1);
        AtomicBoolean result = new AtomicBoolean(false);

        activity.runOnUiThread(() -> {
            try {
                JSONObject localOptions = cloneJson(options);
                applyAdOptionSetters(entry, localOptions);

                switch (entry.type) {
                    case TYPE_REWARD:
                        if (entry.rewardAd != null) {
                            entry.rewardAd.showRewardVideoAd(activity);
                            result.set(true);
                        }
                        break;
                    case TYPE_INTERSTITIAL:
                        if (entry.interstitialAd != null) {
                            entry.interstitialAd.show(activity);
                            result.set(true);
                        }
                        break;
                    case TYPE_BANNER:
                        if (entry.bannerAd != null) {
                            int baseline = localOptions != null ? localOptions.optInt("banner_baseline", 1) : 1;
                            int offset = localOptions != null ? localOptions.optInt("banner_offset", 0) : 0;
                            entry.bannerAd.show(activity, baseline, offset);
                            // Hardware acceleration will be enabled in onAdShow callback
                            // when the view is actually added to the hierarchy
                            result.set(true);
                        }
                        break;
                    case TYPE_SPLASH:
                        if (entry.splashAd != null) {
                            FrameLayout container = attachSplashContainer(activity, entry);
                            if (container != null) {
                                attachSplashListener(activity, entry);
                                entry.splashAd.show(container);
                                result.set(true);
                            } else {
                                Log.w(TAG, "showAd: failed to attach splash container");
                            }
                        }
                        break;
                    default:
                        Log.w(TAG, "showAd: unsupported type " + entry.type);
                        break;
                }
            } catch (Throwable t) {
                Log.e(TAG, "showAd exception", t);
                detachSplashContainer(entry);
            } finally {
                latch.countDown();
            }
        });

        try {
            latch.await(1, TimeUnit.SECONDS);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }

        return result.get();
    }

    private static FrameLayout attachSplashContainer(Activity activity, AdEntry entry) {
        if (activity == null || entry == null) {
            return null;
        }

        ViewGroup root = activity.findViewById(android.R.id.content);
        if (root == null) {
            Log.w(TAG, "attachSplashContainer: root content view is null");
            return null;
        }

        detachSplashContainer(entry);

        FrameLayout overlay = new FrameLayout(activity);
        FrameLayout.LayoutParams overlayParams = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT);
        overlay.setLayoutParams(overlayParams);
        overlay.setClickable(true);
        overlay.setFocusable(true);
        overlay.setBackgroundColor(Color.BLACK);
        // Enable hardware acceleration layer for better video playback and rounded corners rendering
        overlay.setLayerType(View.LAYER_TYPE_HARDWARE, null);

        int width = entry.splashWidth > 0 ? entry.splashWidth : FrameLayout.LayoutParams.MATCH_PARENT;
        int height = entry.splashHeight > 0 ? entry.splashHeight : FrameLayout.LayoutParams.MATCH_PARENT;
        FrameLayout.LayoutParams slotParams = new FrameLayout.LayoutParams(width, height);
        slotParams.gravity = Gravity.CENTER;

        FrameLayout slot = new FrameLayout(activity);
        slot.setLayoutParams(slotParams);
        // Enable hardware acceleration layer for ad content container
        slot.setLayerType(View.LAYER_TYPE_HARDWARE, null);

        overlay.addView(slot);
        root.addView(overlay);
        entry.splashContainer = overlay;
        return slot;
    }

    private static void attachSplashListener(Activity activity, AdEntry entry) {
        if (entry == null || entry.splashAd == null) {
            return;
        }

        entry.splashAd.setSplashInteractionListener(new DirichletSplashAd.AdInteractionListener() {
            @Override
            public void onAdClick() {
                // no-op for Unity bridge; host app can listen via native SDK logs if needed
            }

            @Override
            public void onAdShow() {
                // no-op
            }

            @Override
            public void onAdClose() {
                if (activity != null) {
                    activity.runOnUiThread(() -> detachSplashContainer(entry));
                } else {
                    detachSplashContainer(entry);
                }
            }
        });
    }

    private static void detachSplashContainer(AdEntry entry) {
        if (entry == null) {
            return;
        }

        FrameLayout container = entry.splashContainer;
        if (container == null) {
            return;
        }

        entry.splashContainer = null;
        ViewParent parent = container.getParent();
        if (parent instanceof ViewGroup) {
            ((ViewGroup) parent).removeView(container);
        }
    }

    /**
     * Checks if an ad is still valid and can be shown.
     * This should be called before show() to ensure the ad hasn't expired.
     * 
     * @param handle The handle ID returned from the load method
     * @return true if the ad is valid and can be shown, false otherwise
     */
    public static boolean isAdValid(String handle) {
        AdEntry entry = AD_CACHE.get(handle);
        if (entry == null) {
            Log.w(TAG, "isAdValid: handle not found " + handle);
            return false;
        }

        try {
            switch (entry.type) {
                case TYPE_REWARD:
                    return entry.rewardAd != null && entry.rewardAd.isValid();
                case TYPE_INTERSTITIAL:
                    return entry.interstitialAd != null && entry.interstitialAd.isValid();
                case TYPE_BANNER:
                    return entry.bannerAd != null;
                case TYPE_SPLASH:
                    return entry.splashAd != null;
                default:
                    return false;
            }
        } catch (Throwable t) {
            Log.w(TAG, "isAdValid exception", t);
            return false;
        }
    }

    /**
     * Destroys an ad instance and releases resources.
     * This matches the native SDK pattern where ad objects have destroy() methods.
     * 
     * @param handle The handle ID returned from the load method
     */
    public static void destroyAd(String handle) {
        AdEntry entry = AD_CACHE.remove(handle);
        if (entry == null) {
            return;
        }

        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            entry.destroy();
            return;
        }

        activity.runOnUiThread(entry::destroy);
    }

    /**
     * Builds a DirichletAdRequest from JSON extras.
     * Note: Validation should be done before calling this method.
     * This method assumes extras is not null and contains valid space_id.
     */
    private static DirichletAdRequest buildRequest(AdEntry entry, JSONObject extras) {
        // Validation already done in loadAdWithEntry, but add defensive check
        if (extras == null) {
            throw new IllegalArgumentException("extras cannot be null, must include space_id");
        }

        long spaceId = extras.optLong("space_id", 0L);
        if (spaceId <= 0) {
            throw new IllegalArgumentException("space_id must be provided and greater than zero in extras");
        }

        DirichletAdRequest.Builder builder = new DirichletAdRequest.Builder()
                .withSpaceId(spaceId);

        Integer expressWidth = null;
        Integer expressHeight = null;
        Integer expressImageWidth = null;
        Integer expressImageHeight = null;

        Iterator<String> keys = extras.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            Object value = extras.opt(key);
            if (value == null || JSONObject.NULL.equals(value)) {
                continue;
            }

            String normalizedKey = key == null ? "" : key.trim().toLowerCase(Locale.US);
            switch (normalizedKey) {
                case "space_id":
                    // Already set above, skip to avoid duplicate
                    continue;
                case "express_width":
                case "express_view_width":
                    expressWidth = toInteger(value);
                    break;
                case "express_height":
                case "express_view_height":
                    expressHeight = toInteger(value);
                    break;
                case "express_image_width":
                    expressImageWidth = toInteger(value);
                    break;
                case "express_image_height":
                    expressImageHeight = toInteger(value);
                    break;
                default:
                    applyGenericBuilderValue(builder, normalizedKey, value);
                    break;
            }
        }

        if (expressWidth != null || expressHeight != null) {
            int width = expressWidth != null ? expressWidth : -1;
            int height = expressHeight != null ? expressHeight : -1;
            invokeBuilderTwoInts(builder, "withExpressViewAcceptedSize", width, height);
            if (entry != null && entry.type == TYPE_SPLASH) {
                entry.splashWidth = width;
                entry.splashHeight = height;
            }
        }

        if (expressImageWidth != null || expressImageHeight != null) {
            int width = expressImageWidth != null ? expressImageWidth : -1;
            int height = expressImageHeight != null ? expressImageHeight : -1;
            invokeBuilderTwoInts(builder, "withExpressImageAcceptedSize", width, height);
        }

        return builder.build();
    }

    private static void attachRewardInteraction(final AdEntry entry) {
        if (entry == null || entry.rewardAd == null) {
            return;
        }

        entry.rewardAd.setRewardAdInteractionListener(new DirichletRewardVideoAd.RewardAdInteractionListener() {
            @Override
            public void onAdShow() {
                emitEvent(entry.handle, EVENT_SHOW, resolveAdType(entry.type));
            }

            @Override
            public void onAdClose() {
                emitEvent(entry.handle, EVENT_CLOSE, resolveAdType(entry.type));
            }

            @Override
            public void onRewardVerify(boolean rewardVerify, int rewardAmount, String rewardName, int code, String msg) {
                JSONObject data = new JSONObject();
                try {
                    data.put("rewardVerify", rewardVerify);
                    data.put("rewardAmount", rewardAmount);
                    data.put("rewardName", rewardName == null ? "" : rewardName);
                    data.put("code", code);
                    data.put("message", msg == null ? "" : msg);
                } catch (JSONException e) {
                    Log.w(TAG, "Failed to build reward payload", e);
                }
                emitEvent(entry.handle, EVENT_REWARD, resolveAdType(entry.type), data);
            }

            @Override
            public void onAdClick() {
                emitEvent(entry.handle, EVENT_CLICK, resolveAdType(entry.type));
            }
        });
    }

    private static void attachInterstitialInteraction(final AdEntry entry) {
        if (entry == null || entry.interstitialAd == null) {
            return;
        }

        entry.interstitialAd.setInteractionListener(new DirichletInterstitialAd.InterstitialAdInteractionListener() {
            @Override
            public void onAdShow() {
                emitEvent(entry.handle, EVENT_SHOW, resolveAdType(entry.type));
            }

            @Override
            public void onAdClose() {
                emitEvent(entry.handle, EVENT_CLOSE, resolveAdType(entry.type));
            }

            @Override
            public void onAdClick() {
                emitEvent(entry.handle, EVENT_CLICK, resolveAdType(entry.type));
            }
        });
    }

    private static void attachBannerInteraction(final AdEntry entry) {
        if (entry == null || entry.bannerAd == null) {
            return;
        }

        entry.bannerAd.setBannerInteractionListener(new DirichletBannerAd.BannerInteractionListener() {
            @Override
            public void onAdShow() {
                emitEvent(entry.handle, EVENT_SHOW, resolveAdType(entry.type));
            }

            @Override
            public void onAdClose() {
                emitEvent(entry.handle, EVENT_CLOSE, resolveAdType(entry.type));
            }

            @Override
            public void onAdClick() {
                emitEvent(entry.handle, EVENT_CLICK, resolveAdType(entry.type));
            }
        });
    }


    private static void attachSplashInteraction(final AdEntry entry) {
        if (entry == null || entry.splashAd == null) {
            return;
        }

        entry.splashAd.setSplashInteractionListener(new DirichletSplashAd.AdInteractionListener() {
            @Override
            public void onAdClick() {
                emitEvent(entry.handle, EVENT_CLICK, resolveAdType(entry.type));
            }

            @Override
            public void onAdShow() {
                emitEvent(entry.handle, EVENT_SHOW, resolveAdType(entry.type));
            }

            @Override
            public void onAdClose() {
                emitEvent(entry.handle, EVENT_CLOSE, resolveAdType(entry.type));
            }
        });
    }

    private static void emitEvent(String handle, String eventName, String adType) {
        emitEvent(handle, eventName, adType, null);
    }

    private static void emitEvent(String handle, String eventName, String adType, JSONObject data) {
        if (TextUtils.isEmpty(handle) || TextUtils.isEmpty(eventName)) {
            return;
        }

        try {
            JSONObject message = new JSONObject();
            message.put("handle", handle);
            message.put("eventName", eventName);
            if (!TextUtils.isEmpty(adType)) {
                message.put("adType", adType);
            }
            if (data != null && data.length() > 0) {
                message.put("data", data);
            }
            UnityPlayer.UnitySendMessage(UNITY_CALLBACK_OBJECT, UNITY_CALLBACK_METHOD, message.toString());
        } catch (Exception ex) {
            Log.w(TAG, "Failed to emit unity event", ex);
        }
    }

    private static String resolveAdType(int type) {
        switch (type) {
            case TYPE_REWARD:
                return "reward";
            case TYPE_INTERSTITIAL:
                return "interstitial";
            case TYPE_BANNER:
                return "banner";
            case TYPE_SPLASH:
                return "splash";
            case TYPE_EXPRESS_FEED:
                return "express_feed";
            case TYPE_NATIVE_FEED:
                return "native_feed";
            default:
                return "unknown";
        }
    }

    private static Map<String, String> buildRequestMethodMap() {
        Map<String, String> map = new HashMap<>(16);
        map.put("space_id", "withSpaceId");
        map.put("extra1", "withExtra1");
        map.put("user_id", "withUserId");
        map.put("reward_name", "withRewardName");
        map.put("reward_amount", "withRewardAmount");
        map.put("query", "withQuery");
        map.put("express_width", "withExpressViewAcceptedSize");
        map.put("express_height", "withExpressViewAcceptedSize");
        map.put("express_view_width", "withExpressViewAcceptedSize");
        map.put("express_view_height", "withExpressViewAcceptedSize");
        map.put("express_image_width", "withExpressImageAcceptedSize");
        map.put("express_image_height", "withExpressImageAcceptedSize");
        map.put("mina_id", "withMinaId");
        return Collections.unmodifiableMap(map);
    }

    private static String mergeDataPayload(String subChannel, String dataJson) {
        if (!TextUtils.isEmpty(dataJson)) {
            return dataJson;
        }

        if (TextUtils.isEmpty(subChannel)) {
            return null;
        }

        try {
            JSONObject json = new JSONObject();
            json.put("sub_channel", subChannel);
            return json.toString();
        } catch (JSONException e) {
            Log.w(TAG, "mergeDataPayload error", e);
            return null;
        }
    }

    private static long safeParseLong(String value, long fallback) {
        if (TextUtils.isEmpty(value)) {
            return fallback;
        }

        try {
            return Long.parseLong(value);
        } catch (NumberFormatException ignore) {
            return fallback;
        }
    }

    /**
     * Internal data structure to hold ad instances and associated state.
     * 
     * Since Unity cannot directly hold Java objects, this class serves as a bridge
     * to store ad instances returned from DirichletAdNative.loadXXXAd() methods.
     * The handle ID is used as a key in AD_CACHE to map Unity's string reference
     * to the actual Java ad object.
     * 
     * This design is necessary because:
     * 1. Unity can only pass strings between C# and Java
     * 2. Different ad types have different interfaces (rewardAd.showRewardVideoAd() vs interstitialAd.show())
     * 3. Splash ads require additional UI state (container, dimensions)
     * 4. Event callbacks need handle and type information
     */
    private static final class AdEntry {
        final String handle;
        final int type;

        // Ad object - only one will be set based on type
        DirichletRewardVideoAd rewardAd;
        DirichletInterstitialAd interstitialAd;
        DirichletBannerAd bannerAd;
        DirichletSplashAd splashAd;
        
        // Splash-specific state
        FrameLayout splashContainer;
        int splashWidth;
        int splashHeight;

        AdEntry(String handle, int type) {
            this.handle = handle;
            this.type = type;
        }

        /**
         * Gets the ad object based on type. Used for generic operations.
         */
        Object getAdObject() {
            switch (type) {
                case TYPE_REWARD:
                    return rewardAd;
                case TYPE_INTERSTITIAL:
                    return interstitialAd;
                case TYPE_BANNER:
                    return bannerAd;
                case TYPE_SPLASH:
                    return splashAd;
                default:
                    return null;
            }
        }

        /**
         * Destroys the ad instance and releases resources.
         * This matches the native SDK pattern where ad objects have destroy() methods.
         */
        void destroy() {
            try {
                switch (type) {
                    case TYPE_REWARD:
                        if (rewardAd != null) {
                            rewardAd.destroy();
                            rewardAd = null;
                        }
                        break;
                    case TYPE_INTERSTITIAL:
                        if (interstitialAd != null) {
                            interstitialAd.destroy();
                            interstitialAd = null;
                        }
                        break;
                    case TYPE_BANNER:
                        if (bannerAd != null) {
                            bannerAd.destroy();
                            bannerAd = null;
                        }
                        break;
                    case TYPE_SPLASH:
                        if (splashAd != null) {
                            splashAd.setSplashInteractionListener(null);
                            splashAd.destroy();
                            splashAd = null;
                        }
                        detachSplashContainer(this);
                        break;
                }
            } catch (Throwable t) {
                Log.w(TAG, "destroyAd exception", t);
            }
        }
    }

    private static final class UnityCustomController extends DirichletAdCustomController {
    }
}
