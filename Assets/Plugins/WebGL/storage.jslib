mergeInto(LibraryManager.library, {
  CinderStorageSet: function (keyPtr, jsonPtr) {
    try {
      var key = UTF8ToString(keyPtr);
      var json = UTF8ToString(jsonPtr);
      window.localStorage.setItem(key, json);
    } catch (e) {
      /* storage optional (private mode etc.) — digest loss is acceptable */
    }
  }
});
