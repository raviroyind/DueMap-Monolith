/* ============================================================================
   DueMap — theme toggle (light / dark)  ·  Osen
   ----------------------------------------------------------------------------
   • Applies the saved theme BEFORE paint (no flash of the wrong theme).
   • Defaults to LIGHT. Set data-default="system" on <html> to follow the OS
     when nothing is stored.
   • Persists the choice in localStorage under "duemap-theme".
   • Public API: window.DueMapTheme.{toggle,set,current,choice}.
     window.dueMapTheme is kept as a back-compat alias (older call sites use
     dueMapTheme.toggle()/current()).
   • Fires a "duemap:themechange" event on document whenever the mode changes —
     handy for re-rendering charts / Blazor interop.
============================================================================ */
(function () {
  var KEY = "duemap-theme";
  var root = document.documentElement;

  function stored() {
    try { return localStorage.getItem(KEY); } catch (e) { return null; }
  }
  function prefersDark() {
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
  }
  function fallback() {
    return root.getAttribute("data-default") === "system" ? "system" : "light";
  }
  function resolve(choice) {
    if (choice === "dark") return "dark";
    if (choice === "light") return "light";
    return prefersDark() ? "dark" : "light"; // 'system'
  }
  function apply(mode) {
    root.classList.toggle("dark", mode === "dark");
    root.setAttribute("data-theme", mode);
    try {
      document.dispatchEvent(new CustomEvent("duemap:themechange", { detail: { mode: mode } }));
    } catch (e) {}
  }

  // ---- Run immediately (before paint) ----
  apply(resolve(stored() || fallback()));

  // Keep "system" choice live if the OS preference changes.
  if (window.matchMedia) {
    var mq = window.matchMedia("(prefers-color-scheme: dark)");
    var onChange = function () {
      var choice = stored() || fallback();
      if (choice === "system") apply(resolve("system"));
    };
    if (mq.addEventListener) mq.addEventListener("change", onChange);
    else if (mq.addListener) mq.addListener(onChange);
  }

  var api = {
    /** Current stored choice: 'light' | 'dark' | 'system' (or the fallback). */
    choice: function () { return stored() || fallback(); },
    /** Current resolved mode actually shown: 'light' | 'dark'. */
    current: function () { return root.getAttribute("data-theme") || resolve(this.choice()); },
    /** Set an explicit choice and persist it. */
    set: function (choice) {
      if (choice !== "light" && choice !== "dark" && choice !== "system") return;
      try { localStorage.setItem(KEY, choice); } catch (e) {}
      apply(resolve(choice));
    },
    /** Flip between light and dark (resolves 'system' first). Returns the new mode. */
    toggle: function () {
      var next = this.current() === "dark" ? "light" : "dark";
      this.set(next);
      return next;
    }
  };

  window.DueMapTheme = api;
  window.dueMapTheme = api;   // back-compat alias for older call sites
})();
