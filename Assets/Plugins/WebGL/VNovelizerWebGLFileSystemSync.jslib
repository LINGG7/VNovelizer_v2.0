mergeInto(LibraryManager.library, {
  $VNovelizer_TapGetFileSystem: function () {
    if (
      typeof tap === "undefined" ||
      !tap ||
      typeof tap.getFileSystemManager !== "function" ||
      !tap.env ||
      !tap.env.USER_DATA_PATH
    ) {
      return null;
    }

    return tap.getFileSystemManager();
  },

  $VNovelizer_TapGetFilePath__deps: ["$VNovelizer_TapGetFileSystem"],
  $VNovelizer_TapGetFilePath: function (relativePathPtr) {
    if (!VNovelizer_TapGetFileSystem()) return "";

    var relativePath = UTF8ToString(relativePathPtr || 0)
      .replace(/\\/g, "/")
      .replace(/^\/+/, "");

    if (!relativePath || relativePath.indexOf("..") !== -1) {
      return "";
    }

    return tap.env.USER_DATA_PATH + "/VNovelizer/" + relativePath;
  },

  $VNovelizer_TapEnsureDirectory__deps: ["$VNovelizer_TapGetFileSystem"],
  $VNovelizer_TapEnsureDirectory: function (filePath) {
    var fs = VNovelizer_TapGetFileSystem();
    if (!fs || !filePath) return;

    var basePath = tap.env.USER_DATA_PATH + "/VNovelizer";
    var directory = filePath.substring(0, filePath.lastIndexOf("/"));
    var parts = directory.substring(basePath.length).split("/");
    var current = basePath;

    try {
      fs.accessSync(basePath);
    } catch (e) {
      fs.mkdirSync(basePath);
    }

    for (var i = 0; i < parts.length; i++) {
      if (!parts[i]) continue;
      current += "/" + parts[i];

      try {
        fs.accessSync(current);
      } catch (e) {
        fs.mkdirSync(current);
      }
    }
  },

  $VNovelizer_TapNormalizeBytes: function (data) {
    if (!data) return new Uint8Array(0);
    if (data instanceof Uint8Array) return data;
    if (data instanceof ArrayBuffer) return new Uint8Array(data);
    if (data.data instanceof Uint8Array) return data.data;
    if (data.data instanceof ArrayBuffer) return new Uint8Array(data.data);
    return new Uint8Array(data);
  },

  VNovelizer_TapHasFileSystem__deps: ["$VNovelizer_TapGetFileSystem"],
  VNovelizer_TapHasFileSystem: function () {
    return VNovelizer_TapGetFileSystem() ? 1 : 0;
  },

  VNovelizer_TapWriteStringFile__deps: [
    "$VNovelizer_TapGetFilePath",
    "$VNovelizer_TapEnsureDirectory"
  ],
  VNovelizer_TapWriteStringFile: function (relativePathPtr, contentPtr) {
    try {
      var file = VNovelizer_TapGetFilePath(relativePathPtr);
      if (!file) return 0;

      VNovelizer_TapEnsureDirectory(file);
      tap.getFileSystemManager().writeFileSync(file, UTF8ToString(contentPtr), "utf8");
      return 1;
    } catch (e) {
      console.warn("[VNovelizer] Tap write string file failed:", e);
      return 0;
    }
  },

  VNovelizer_TapReadStringFile__deps: ["$VNovelizer_TapGetFilePath"],
  VNovelizer_TapReadStringFile: function (relativePathPtr) {
    try {
      var file = VNovelizer_TapGetFilePath(relativePathPtr);
      if (!file) return 0;

      var content = tap.getFileSystemManager().readFileSync(file, "utf8");
      return stringToNewUTF8(content || "");
    } catch (e) {
      return 0;
    }
  },

  VNovelizer_TapWriteBinaryFile__deps: [
    "$VNovelizer_TapGetFilePath",
    "$VNovelizer_TapEnsureDirectory"
  ],
  VNovelizer_TapWriteBinaryFile: function (relativePathPtr, dataPtr, length) {
    try {
      var file = VNovelizer_TapGetFilePath(relativePathPtr);
      if (!file || !dataPtr || length < 0) return 0;

      VNovelizer_TapEnsureDirectory(file);
      var bytes = HEAPU8.slice(dataPtr, dataPtr + length);
      tap.getFileSystemManager().writeFileSync(file, bytes);
      return 1;
    } catch (e) {
      console.warn("[VNovelizer] Tap write binary file failed:", e);
      return 0;
    }
  },

  VNovelizer_TapGetFileSize__deps: [
    "$VNovelizer_TapGetFilePath",
    "$VNovelizer_TapNormalizeBytes"
  ],
  VNovelizer_TapGetFileSize: function (relativePathPtr) {
    try {
      var file = VNovelizer_TapGetFilePath(relativePathPtr);
      if (!file) return -1;

      var fs = tap.getFileSystemManager();
      if (typeof fs.statSync === "function") {
        var stat = fs.statSync(file);
        if (stat && typeof stat.size === "number") {
          return stat.size;
        }
      }

      var data = fs.readFileSync(file);
      return VNovelizer_TapNormalizeBytes(data).length;
    } catch (e) {
      return -1;
    }
  },

  VNovelizer_TapReadBinaryFile__deps: [
    "$VNovelizer_TapGetFilePath",
    "$VNovelizer_TapNormalizeBytes"
  ],
  VNovelizer_TapReadBinaryFile: function (relativePathPtr, bufferPtr, bufferLength) {
    try {
      var file = VNovelizer_TapGetFilePath(relativePathPtr);
      if (!file || !bufferPtr || bufferLength <= 0) return -1;

      var data = tap.getFileSystemManager().readFileSync(file);
      var bytes = VNovelizer_TapNormalizeBytes(data);
      var count = Math.min(bytes.length, bufferLength);
      HEAPU8.set(bytes.subarray(0, count), bufferPtr);
      return count;
    } catch (e) {
      return -1;
    }
  },

  VNovelizer_TapFileExists__deps: ["$VNovelizer_TapGetFilePath"],
  VNovelizer_TapFileExists: function (relativePathPtr) {
    try {
      var file = VNovelizer_TapGetFilePath(relativePathPtr);
      if (!file) return 0;

      tap.getFileSystemManager().accessSync(file);
      return 1;
    } catch (e) {
      return 0;
    }
  },

  VNovelizer_TapDirectoryHasFiles__deps: ["$VNovelizer_TapGetFilePath"],
  VNovelizer_TapDirectoryHasFiles: function (relativePathPtr) {
    try {
      var dir = VNovelizer_TapGetFilePath(relativePathPtr);
      if (!dir) return 0;

      var files = tap.getFileSystemManager().readdirSync(dir);
      return files && files.length > 0 ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  VNovelizer_TapDeleteFile__deps: ["$VNovelizer_TapGetFilePath"],
  VNovelizer_TapDeleteFile: function (relativePathPtr) {
    try {
      var file = VNovelizer_TapGetFilePath(relativePathPtr);
      if (!file) return 0;

      tap.getFileSystemManager().unlinkSync(file);
      return 1;
    } catch (e) {
      return 0;
    }
  },

  VNovelizer_TapFree: function (ptr) {
    if (ptr) _free(ptr);
  },

  VNovelizer_SyncFileSystem: function () {
    if (typeof FS === "undefined" || typeof FS.syncfs !== "function") {
      return;
    }

    var hasIndexedDB =
      (typeof indexedDB !== "undefined" && indexedDB) ||
      (typeof window !== "undefined" && window.indexedDB);

    if (!hasIndexedDB) {
      return;
    }

    if (typeof Module !== "undefined" && Module.vnovelizerSyncInProgress) {
      Module.vnovelizerSyncPending = true;
      return;
    }

    var runSync = function () {
      Module.vnovelizerSyncInProgress = true;
      Module.vnovelizerSyncPending = false;

      FS.syncfs(false, function (syncErr) {
        Module.vnovelizerSyncInProgress = false;

        if (syncErr) {
          console.error("[VNovelizer] WebGL file system sync failed:", syncErr);
        }

        if (Module.vnovelizerSyncPending) {
          runSync();
        }
      });
    };

    runSync();
  }
});
