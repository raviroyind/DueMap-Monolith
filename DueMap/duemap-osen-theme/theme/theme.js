/* ============================================================================
   DueMap — theme toggle (light / dark)
   ----------------------------------------------------------------------------
   • Applies the saved theme BEFORE paint (no flash of the wrong theme).
   • Defaults to LIGHT. Set data-default="system" on the <html> tag, or pass
     {fallback:'system'} to follow the OS when nothing is stored.
   • Persists the choice in localStorage under "duemap-theme".

   USAGE (Blazor)
   --------------
   Put this in <head>, as early as possible, ideally before your CSS:
       <script src="theme/theme.js"></script>

   Wire any button to flip the theme:
       <button onclick="DueMapTheme.toggle()">…</button>
   Or set explicitly:
       DueMapTheme.set('dark');   DueMapTheme.set('light');   DueMapTheme.set('system');

   The current resolved value ('light' | 'dark') is on <html data-theme="…">,
   and a "duemap:themechange" event fires on document whenever it changes —
   handy for re-rendering charts (ApexCharts) or Blazor interop.
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

  // Resolve a choice ('light'|'dark'|'system') to an actual mode.
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

  // ---- Public API ----
  window.DueMapTheme = {
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
    /** Flip between light and dark (resolves 'system' first). */
    toggle: function () {
      this.set(this.current() === "dark" ? "light" : "dark");
    }
  };
})();
