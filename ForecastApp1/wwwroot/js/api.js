(function () {
  window.APP_TITLE = 'ForecastApp1';
  var ROLE_ADMIN = 'admin';
  var PAGE_ACCESS = {
    'admin.html': [ROLE_ADMIN],
    'audit.html': [ROLE_ADMIN]
  };
  var IMPORT_STATUS_LABELS = {
    uploaded: 'Файл загружен, идет проверка',
    parsing: 'Файл загружен, идет проверка',
    parsed: 'Файл проверен, можно сохранить данные',
    validated: 'Файл проверен, можно сохранить данные',
    awaiting_commit: 'Ожидает подтверждения',
    committed: 'Данные сохранены в системе',
    committed_with_warnings: 'Данные сохранены, но есть предупреждения',
    validation_failed: 'В файле найдены ошибки',
    rolled_back: 'Загрузка отменена',
    rollbacked: 'Загрузка отменена',
    failed: 'В файле найдены ошибки'
  };
  var CALC_STATUS_LABELS = {
    created: 'Расчет создан',
    running: 'Идет расчет',
    completed: 'Расчет выполнен',
    completed_with_warnings: 'Расчет выполнен, но есть предупреждения',
    failed: 'Расчет завершился с ошибкой'
  };
  var ERROR_MESSAGE_REPLACEMENTS = [
    { from: 'Internal server error', to: 'Не удалось выполнить действие. Попробуйте еще раз или обратитесь к администратору.' },
    { from: 'server error', to: 'Внутренняя ошибка системы.' },
    { from: 'API error', to: 'Ошибка при выполнении операции.' },
    { from: 'Foreign key constraint error', to: 'Не найдена связанная запись в справочнике. Проверьте поставщика, договор и другие связанные данные.' },
    { from: 'foreign key constraint', to: 'Не найдена связанная запись в справочнике.' },
    { from: 'Duplicate key', to: 'Такая запись уже есть в системе.' },
    { from: 'unique constraint', to: 'Такая запись уже есть в системе.' }
  ];
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

  function toDisplayStatus(value, map, fallback) {
    var raw = value == null ? '' : String(value);
    var key = raw.toLowerCase();
    return map[key] || fallback || raw || '—';
  }

  function toDisplayImportStatus(value) {
    return toDisplayStatus(value, IMPORT_STATUS_LABELS, 'Статус обрабатывается');
  }

  function toDisplayCalculationStatus(value) {
    return toDisplayStatus(value, CALC_STATUS_LABELS, 'Статус обрабатывается');
  }

  function toDisplaySeverity(value) {
    var key = String(value || '').toLowerCase();
    if (key === 'error') return 'Ошибка';
    if (key === 'warning') return 'Предупреждение';
    if (key === 'info') return 'Подсказка';
    return value || '—';
  }

  function toDisplayErrorMessage(message) {
    var text = String(message || '');
    if (!text) return 'Не удалось выполнить действие. Попробуйте еще раз.';
    for (var i = 0; i < ERROR_MESSAGE_REPLACEMENTS.length; i++) {
      var pair = ERROR_MESSAGE_REPLACEMENTS[i];
      if (text.toLowerCase().indexOf(pair.from.toLowerCase()) >= 0) return pair.to;
    }
    return text;
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
      const raw = (data && (data.message || data.title)) || text || res.statusText;
      const msg = toDisplayErrorMessage(raw);
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
  window.toDisplayImportStatus = toDisplayImportStatus;
  window.toDisplayCalculationStatus = toDisplayCalculationStatus;
  window.toDisplaySeverity = toDisplaySeverity;
  window.toDisplayErrorMessage = toDisplayErrorMessage;

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
