mergeInto(LibraryManager.library, {
  VNovelizerWebGLRewardedAdIsSupported: function () {
    var host = (typeof window !== "undefined") ? window : globalThis;
    var api = host.tj || host.tt || host.wx;

    if (api && typeof api.createRewardedVideoAd === "function") {
      return 1;
    }

    return 0;
  },

  VNovelizerWebGLRewardedAdShow: function (adUnitIdPtr, gameObjectNamePtr) {
    var adUnitId = UTF8ToString(adUnitIdPtr);
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var host = (typeof window !== "undefined") ? window : globalThis;
    var api = host.tj || host.tt || host.wx;

    function send(method, payload) {
      try {
        if (typeof SendMessage === "function") {
          SendMessage(gameObjectName, method, payload || "");
        } else if (typeof Module !== "undefined" && typeof Module.SendMessage === "function") {
          Module.SendMessage(gameObjectName, method, payload || "");
        }
      } catch (e) {
        console.error("[VNovelizerWebGLRewardedAd] SendMessage failed", method, e);
      }
    }

    function fail(code, message, raw) {
      send("OnWebGLRewardedAdFailed", JSON.stringify({
        code: code || "unknown",
        message: message || "",
        raw: raw || ""
      }));
    }

    function stringify(value) {
      try {
        return JSON.stringify(value || {});
      } catch (e) {
        return String(value || "");
      }
    }

    if (!adUnitId) {
      fail("missing_ad_unit", "Rewarded ad unit id is empty.");
      return;
    }

    if (!api || typeof api.createRewardedVideoAd !== "function") {
      fail("unsupported", "createRewardedVideoAd is not available in this WebGL host.");
      return;
    }

    var ad;
    var completed = false;

    function completeWithReward(payload) {
      if (completed) {
        return;
      }

      completed = true;
      send("OnWebGLRewardedAdRewarded", payload || "");
    }

    function completeWithFailure(code, message, raw) {
      if (completed) {
        return;
      }

      completed = true;
      fail(code, message, raw);
    }

    try {
      ad = api.createRewardedVideoAd({
        adUnitId: adUnitId,
        multiton: true
      });

      if (!ad) {
        completeWithFailure("create_failed", "createRewardedVideoAd returned null.");
        return;
      }

      if (typeof ad.onError === "function") {
        ad.onError(function (res) {
          completeWithFailure("ad_error", (res && (res.errMsg || res.message)) || "Rewarded ad error.", stringify(res));
        });
      }

      if (typeof ad.onLoad === "function") {
        ad.onLoad(function (res) {
          send("OnWebGLRewardedAdLoaded", stringify(res));
        });
      }

      if (typeof ad.onClose === "function") {
        ad.onClose(function (res) {
          var ended = !res || res.isEnded === undefined || res.isEnded === true;
          send("OnWebGLRewardedAdClosed", stringify(res));

          if (ended) {
            completeWithReward(stringify(res));
          } else {
            completeWithFailure("not_finished", "Rewarded ad was closed before completion.", stringify(res));
          }
        });
      }

      function showAd() {
        var showResult = ad.show ? ad.show() : null;

        if (showResult && typeof showResult.then === "function") {
          showResult.then(function () {
            send("OnWebGLRewardedAdShown", "");
          }).catch(function (err) {
            completeWithFailure("show_failed", (err && (err.errMsg || err.message)) || "Rewarded ad show failed.", stringify(err));
          });
        } else {
          send("OnWebGLRewardedAdShown", "");
        }
      }

      if (typeof ad.load === "function") {
        var loadResult = ad.load();

        if (loadResult && typeof loadResult.then === "function") {
          loadResult.then(showAd).catch(function (err) {
            completeWithFailure("load_failed", (err && (err.errMsg || err.message)) || "Rewarded ad load failed.", stringify(err));
          });
        } else {
          showAd();
        }
      } else {
        showAd();
      }
    } catch (e) {
      completeWithFailure("exception", e && e.message ? e.message : String(e), stringify(e));
    }
  }
});
