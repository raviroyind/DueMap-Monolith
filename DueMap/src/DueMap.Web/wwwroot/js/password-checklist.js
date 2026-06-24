// Live password-rule feedback on the Register form. Each rule row carries a
// data-rule (and optional data-len); we evaluate them as the user types and
// flip the row to green+check (met), red+x (typed but unmet), or muted dot
// (not typed yet). The rule set is rendered server-side from the real Identity
// policy, so this only mirrors it — the server stays the source of truth.
(function () {
  function icon(state) {
    if (state === 'ok') {
      return '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"/></svg>';
    }
    if (state === 'bad') {
      return '<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg>';
    }
    // pending
    return '<svg width="8" height="8" viewBox="0 0 24 24" fill="currentColor"><circle cx="12" cy="12" r="6"/></svg>';
  }

  var checks = {
    len:     function (v, n) { return v.length >= n; },
    upper:   function (v) { return /[A-Z]/.test(v); },
    lower:   function (v) { return /[a-z]/.test(v); },
    digit:   function (v) { return /[0-9]/.test(v); },
    special: function (v) { return /[^A-Za-z0-9]/.test(v); },
    unique:  function (v, n) { return new Set(v.split('')).size >= n; }
  };

  function init() {
    var pwd = document.getElementById('password');
    var confirm = document.getElementById('confirm');
    var rules = Array.prototype.slice.call(document.querySelectorAll('#pw-rules .pw-rule'));
    var matchEl = document.getElementById('pw-match');
    var submit = document.getElementById('pw-submit');
    if (!pwd || rules.length === 0) return;

    function setState(el, state) {
      el.classList.remove('text-muted', 'text-success', 'text-danger');
      el.classList.add(state === 'ok' ? 'text-success' : state === 'bad' ? 'text-danger' : 'text-muted');
      var ico = el.querySelector('.pw-ico');
      if (ico) ico.innerHTML = icon(state);
    }

    function evaluate() {
      var v = pwd.value || '';
      var allMet = true;
      rules.forEach(function (li) {
        var key = li.getAttribute('data-rule');
        var n = parseInt(li.getAttribute('data-len') || '0', 10);
        var ok = checks[key] ? checks[key](v, n) : true;
        if (!ok) allMet = false;
        setState(li, v.length === 0 ? 'pending' : (ok ? 'ok' : 'bad'));
      });

      var matchOk = true;
      if (matchEl && confirm) {
        var cv = confirm.value || '';
        if (cv.length === 0) {
          matchEl.style.display = 'none';
          matchOk = false;
        } else {
          matchEl.style.display = 'flex';
          matchOk = (cv === v && v.length > 0);
          setState(matchEl, matchOk ? 'ok' : 'bad');
        }
      }

      // Gate the submit button: all rules met, passwords match, and non-empty.
      // Set via JS only, so if this script never runs the button stays usable
      // (the server validates regardless).
      if (submit) submit.disabled = !(allMet && (matchEl ? matchOk : true) && v.length > 0);
    }

    pwd.addEventListener('input', evaluate);
    if (confirm) confirm.addEventListener('input', evaluate);
    evaluate();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
