mergeInto(LibraryManager.library, {
  CinderStorageSet: function (keyPtr, jsonPtr) {
    try {
      var key = UTF8ToString(keyPtr);
      var json = UTF8ToString(jsonPtr);
      window.localStorage.setItem(key, json);
    } catch (e) {
      /* storage optional (private mode etc.) — digest loss is acceptable */
    }
  },
  CinderStorageGet: function (keyPtr) {
    var value = "";
    try {
      value = window.localStorage.getItem(UTF8ToString(keyPtr)) || "";
    } catch (e) { /* private mode: empty */ }
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },
  CinderQueryParam: function (namePtr) {
    var value = "";
    try {
      value = new URLSearchParams(window.location.search)
        .get(UTF8ToString(namePtr)) || "";
    } catch (e) { /* no window/search: empty */ }
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },
  CinderNavigate: function (urlPtr) {
    try {
      window.location.href = UTF8ToString(urlPtr);
    } catch (e) { /* ignore */ }
  }
});
