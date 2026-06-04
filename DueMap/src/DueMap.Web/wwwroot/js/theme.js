// DueMap — theme bootstrap + toggle
// Loaded synchronously in <head> so the class is applied before first paint
// (prevents the light→dark flash for users who prefer dark).

(function () {
  const KEY = 'duemap-theme';
  const stored = localStorage.getItem(KEY);
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  if (stored === 'dark' || (stored === null && prefersDark)) {
    document.documentElement.classList.add('dark');
  }
})();

window.dueMapTheme = {
  toggle() {
    const isDark = document.documentElement.classList.toggle('dark');
    localStorage.setItem('duemap-theme', isDark ? 'dark' : 'light');
    return isDark;
  },
  current() {
    return document.documentElement.classList.contains('dark') ? 'dark' : 'light';
  }
};
