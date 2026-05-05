(function () {
  window.APP_TITLE = 'ForecastApp1';
  var ROLE_ADMIN = 'admin';
  var PAGE_ACCESS = {
    'admin.html': [ROLE_ADMIN],
    'audit.html': [ROLE_ADMIN]
  };
  var mePromise = null;

  function normalizeHref(href) {
    if (!href) return '';
    try {
      return new URL(href, window.location.origin).pathname.split('/').pop().toLowerCase();
    } catch {
      return String(href).split('?')[0].split('/').pop().toLowerCase();
    }
  }

  function hasAnyRole(userRoles, allowedRoles) {
    if (!allowedRoles || allowedRoles.length === 0) return true;
    var set = new Set((userRoles || []).map(function (r) { return String(r).toLowerCase(); }));
    return allowedRoles.some(function (role) { return set.has(String(role).toLowerCase()); });
  }

  function applyNavAccess(userRoles) {
    document.querySelectorAll('a.nav-link[href]').forEach(function (link) {
      var key = normalizeHref(link.getAttribute('href'));
      var allowed = PAGE_ACCESS[key];
      if (!hasAnyRole(userRoles, allowed)) {
        var li = link.closest('li.nav-item');
        if (li) li.remove();
      }
    });
  }

  function guardCurrentPage(userRoles) {
    var current = normalizeHref(window.location.pathname);
    var allowed = PAGE_ACCESS[current];
    if (!hasAnyRole(userRoles, allowed)) {
      window.location.replace('/main.html');
    }
  }

  async function getCurrentUser() {
    if (!mePromise) mePromise = window.api('/api/auth/me');
    return mePromise;
  }

  window.api = async function (path, options = {}) {
    const headers = Object.assign({ Accept: 'application/json' }, options.headers || {});
    if (options.body && typeof options.body === 'object' && !(options.body instanceof FormData))
      headers['Content-Type'] = 'application/json';
    const init = Object.assign({ credentials: 'same-origin' }, options, { headers });
    if (init.body && typeof init.body === 'object' && !(init.body instanceof FormData))
      init.body = JSON.stringify(init.body);
    const res = await fetch(path, init);
    if (res.status === 401 && !path.includes('/auth/login')) {
      window.location.href = '/login.html';
      throw new Error('Требуется вход');
    }
    const text = await res.text();
    let data;
    try {
      data = text ? JSON.parse(text) : null;
    } catch {
      data = text;
    }
    if (!res.ok) {
      const msg = (data && (data.message || data.title)) || text || res.statusText;
      throw new Error(msg);
    }
    return data;
  };

  window.fmtMoney = function (n) {
    return new Intl.NumberFormat('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(n || 0));
  };

  window.fmtDate = function (iso) {
    if (!iso) return '';
    const d = new Date(iso);
    if (isNaN(d)) {
      if (typeof iso === 'string' && /^\d{4}-\d{2}-\d{2}/.test(iso)) {
        const [y, m, day] = iso.slice(0, 10).split('-');
        return `${day}.${m}.${y}`;
      }
      return iso;
    }
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const yy = d.getFullYear();
    return `${dd}.${mm}.${yy}`;
  };

  /** Шапка в стиле Razor _Layout (Bootstrap navbar). activeHref — имя файла, напр. 'main.html' */
  window.layoutHeader = function (activeHref) {
    const items = [
      ['main.html', 'Главная'],
      ['import.html', 'Импорт'],
      ['dictionaries.html', 'Справочники'],
      ['debts.html', 'Долги'],
      ['shipments.html', 'Отгрузки'],
      ['calculation.html', 'Расчёт'],
      ['schedule.html', 'График'],
      ['reports.html', 'Отчёты'],
      ['help.html', 'Справка'],
      ['admin.html', 'Админ'],
      ['audit.html', 'Аудит']
    ];
    const lis = items
      .map(function (pair) {
        const href = pair[0];
        const label = pair[1];
        const active = activeHref === href ? ' active' : '';
        return (
          '<li class="nav-item">' +
          '<a class="nav-link text-dark' +
          active +
          '" href="' +
          href +
          '">' +
          label +
          '</a></li>'
        );
      })
      .join('');
    return (
      '<header>' +
      '<nav class="navbar navbar-expand-sm navbar-toggleable-sm navbar-light app-navbar border-bottom mb-3">' +
      '<div class="container">' +
      '<a class="navbar-brand" href="main.html">' +
      window.APP_TITLE +
      '</a>' +
      '<button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target=".navbar-collapse" ' +
      'aria-controls="navbarSupportedContent" aria-expanded="false" aria-label="Меню">' +
      '<span class="navbar-toggler-icon"></span>' +
      '</button>' +
      '<div class="navbar-collapse collapse d-sm-inline-flex justify-content-between">' +
      '<ul class="navbar-nav flex-grow-1">' +
      lis +
      '</ul>' +
      '<button type="button" class="btn btn-outline-secondary btn-sm" id="btnLogout">Выход</button>' +
      '</div>' +
      '</div>' +
      '</nav>' +
      '</header>'
    );
  };

  window.layoutFooter = function () {
    return (
      '<footer class="border-top footer text-muted">' +
      '<div class="container">' +
      '&copy; 2026 — ' +
      window.APP_TITLE +
      ' — прогноз платежей поставщикам' +
      '</div>' +
      '</footer>'
    );
  };

  /** Совместимость со старыми страницами */
  window.navBar = window.layoutHeader;
  window.getCurrentUser = getCurrentUser;

  document.addEventListener('DOMContentLoaded', function () {
    document.body.addEventListener('click', function (e) {
      if (e.target && e.target.id === 'btnLogout') {
        api('/api/auth/logout', { method: 'POST' }).finally(function () {
          window.location.href = '/login.html';
        });
      }
    });

    getCurrentUser()
      .then(function (u) {
        var roles = (u && u.roles) || [];
        applyNavAccess(roles);
        guardCurrentPage(roles);
      })
      .catch(function () {
        // Не дублируем редирект: window.api уже отправит на /login.html при 401.
      });
  });
})();
