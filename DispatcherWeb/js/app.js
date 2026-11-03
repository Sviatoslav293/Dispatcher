import { getAircraft, getFlightCommands, getFlights, getHealth, getRoute, postFlightCommand } from './api.js';
import { clearRoute, initMap, showRoute, updateAircraftLayer } from './map.js';

const state = {
  flights: [],
  aircraft: [],
  activeTab: 'current',
  selectedFlightId: null,
  selectedFlightVersion: 0,
  commandHistory: [],
  routeState: {
    flightId: null,
    routeId: null,
    signature: null,
    loaded: false
  }
};

const FLIGHT_STATUS_LABELS = {
  scheduled: 'Заплановано',
  active: 'Активний',
  completed: 'Завершено',
  cancelled: 'Скасовано',
  default: 'Невідомо'
};

const TELEMETRY_STATUS_LABELS = {
  scheduled: 'Заплановано',
  active: 'Активний',
  completed: 'Завершено',
  cancelled: 'Скасовано',
  enroute: 'У польоті',
  routecompleted: 'Маршрут завершено',
  landed: 'Приземлення',
  historical: 'Історичні дані',
  default: 'Невідомий статус'
};

const COMMAND_TYPE_LABELS = {
  setheading: 'Зміна курсу',
  setaltitude: 'Зміна висоти',
  setspeed: 'Зміна швидкості'
};

const COMMAND_STATUS_LABELS = {
  pending: 'Очікує',
  sent: 'Надіслано',
  acknowledged: 'Підтверджено',
  failed: 'Помилка'
};

const COMMAND_MESSAGE_LABELS = {
  'Command target applied; aircraft will move toward it within the configured kinematic limits.':
    'Команду прийнято. Літак поступово змінюватиме параметри руху в допустимих межах.',
  'Command could not be applied due to an internal error.':
    'Не вдалося виконати команду через внутрішню помилку.',
  'CommandId must not be empty.':
    'Ідентифікатор команди не може бути порожнім.',
  'Duplicate CommandId was already processed.':
    'Команду з таким ідентифікатором уже оброблено.',
  'Duplicate CommandId was already received.':
    'Команду з таким ідентифікатором уже отримано.',
  'AircraftId does not match this aircraft.':
    'Ідентифікатор літака не відповідає цьому літаку.',
  'AircraftId does not match this simulator.':
    'Ідентифікатор літака не відповідає цьому симулятору.',
  'FlightId does not match the active flight.':
    'Ідентифікатор рейсу не відповідає активному рейсу.',
  'FlightId does not match this simulator.':
    'Ідентифікатор рейсу не відповідає цьому симулятору.',
  'Command value must be finite.':
    'Значення команди має бути скінченним числом.',
  'Command timestamps must be UTC.':
    'Часові позначки команди мають бути в часовому поясі UTC.',
  'IssuedAtUtc and ExpiresAtUtc must be UTC timestamps.':
    'Час створення та завершення дії команди мають бути в часовому поясі UTC.',
  'Command has expired.':
    'Термін дії команди завершився.',
  'ExpiresAtUtc must be later than IssuedAtUtc.':
    'Час завершення дії команди має бути пізнішим за час її створення.',
  'The aircraft is no longer active.':
    'Літак більше не активний.',
  'Command type is not supported.':
    'Цей тип команди не підтримується.',
  'Невідомий тип команди або недопустиме значення.':
    'Невідомий тип команди або недопустиме значення.',
  'Рейс не знайдено.':
    'Рейс не знайдено.',
  'Керувати можна лише активним рейсом.':
    'Керувати можна лише активним рейсом.',
  'Не вдалося передати команду літаку.':
    'Не вдалося передати команду літаку.'
};

const TERMINAL_FLIGHT_STATUSES = new Set(['completed', 'cancelled']);

function getRouteSignature(route) {
  if (!route || !Array.isArray(route.waypoints)) {
    return null;
  }

  return JSON.stringify(
    route.waypoints
      .slice()
      .sort((a, b) => a.sequenceNumber - b.sequenceNumber)
      .map((waypoint) => ({
        waypointId: waypoint.waypointId,
        sequenceNumber: waypoint.sequenceNumber,
        latitude: Number(waypoint.latitude),
        longitude: Number(waypoint.longitude),
        name: waypoint.name
      }))
  );
}

function isTerminalFlightStatus(status) {
  return TERMINAL_FLIGHT_STATUSES.has(String(status || '').trim().toLowerCase());
}

function getFlightStatusLabel(status) {
  const key = String(status || '').trim().toLowerCase();
  return FLIGHT_STATUS_LABELS[key] || FLIGHT_STATUS_LABELS.default;
}

function getTelemetryStatusLabel(status, isHistorical = false) {
  const key = String(status || '').trim().toLowerCase();
  if (isHistorical) {
    return TELEMETRY_STATUS_LABELS.historical;
  }

  return TELEMETRY_STATUS_LABELS[key] || TELEMETRY_STATUS_LABELS.default;
}

function localizeCommandType(type) {
  const key = String(type || '').trim().toLowerCase();
  return COMMAND_TYPE_LABELS[key] || (key ? String(type) : 'Невідома команда');
}

function localizeCommandStatus(status) {
  const key = String(status || '').trim().toLowerCase();
  return COMMAND_STATUS_LABELS[key] || 'Невідомий статус';
}

function localizeCommandMessage(message) {
  const original = String(message || '').trim();
  if (!original) {
    return { text: 'Без повідомлення', original: '', isKnown: true };
  }

  if (Object.prototype.hasOwnProperty.call(COMMAND_MESSAGE_LABELS, original)) {
    return { text: COMMAND_MESSAGE_LABELS[original], original, isKnown: true };
  }

  const speedRange = original.match(/^Speed must be between (.+) and (.+) km\/h\.$/);
  if (speedRange) {
    return {
      text: `Швидкість має бути від ${speedRange[1]} до ${speedRange[2]} км/год.`,
      original,
      isKnown: true
    };
  }

  const altitudeRange = original.match(/^Altitude must be between (.+) and (.+) meters\.$/);
  if (altitudeRange) {
    return {
      text: `Висота має бути від ${altitudeRange[1]} до ${altitudeRange[2]} м.`,
      original,
      isKnown: true
    };
  }

  return {
    text: 'Отримано результат виконання команди. Перегляньте технічне повідомлення.',
    original,
    isKnown: false
  };
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, (character) => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;'
  })[character]);
}

function isTelemetryFresh(snapshot) {
  if (!snapshot || !snapshot.timestampUtc) {
    return false;
  }

  const timestamp = Date.parse(snapshot.timestampUtc);
  return Number.isFinite(timestamp) && Date.now() - timestamp <= 90_000;
}

function getVisibleFlights() {
  return state.flights.filter((flight) => {
    if (state.activeTab === 'history') {
      return isTerminalFlightStatus(flight.status);
    }

    return !isTerminalFlightStatus(flight.status);
  });
}

function getSelectedFlight() {
  return state.flights.find((flight) => Number(flight.id) === Number(state.selectedFlightId)) || null;
}

function setSelectedFlightId(flightId) {
  if (state.selectedFlightId === flightId) {
    return;
  }

  state.selectedFlightId = flightId;
  state.selectedFlightVersion += 1;
}

function syncSelectedFlightWithTab() {
  const visibleFlights = getVisibleFlights();
  if (!visibleFlights.length) {
    setSelectedFlightId(null);
    return;
  }

  const selectedStillVisible = visibleFlights.some((flight) => Number(flight.id) === Number(state.selectedFlightId));
  if (!selectedStillVisible) {
    setSelectedFlightId(Number(visibleFlights[0].id));
  }
}

function isCurrentSelection(flightId, selectionVersion) {
  return state.selectedFlightVersion === selectionVersion
    && Number(state.selectedFlightId) === Number(flightId);
}

async function ensureSelectedFlightRoute({ centerOnRoute = false, force = false, selectionVersion = state.selectedFlightVersion } = {}) {
  const flight = getSelectedFlight();
  if (!flight) {
    clearRoute();
    state.routeState = { flightId: null, routeId: null, signature: null, loaded: false };
    return;
  }

  const routeId = Number(flight.routeId);
  const routeState = state.routeState;
  const shouldLoadRoute = force || !routeState.loaded || routeState.flightId !== Number(flight.id) || routeState.routeId !== routeId;

  if (!routeId) {
    clearRoute();
    state.routeState = { flightId: null, routeId: null, signature: null, loaded: false };
    return;
  }

  if (!shouldLoadRoute) {
    return;
  }

  try {
    const route = await getRoute(routeId);
    if (!isCurrentSelection(flight.id, selectionVersion)) {
      return;
    }

    const signature = getRouteSignature(route);
    const routeChanged = routeState.routeId !== routeId || routeState.signature !== signature;

    if (route && (routeChanged || !routeState.loaded || routeState.flightId !== Number(flight.id))) {
      showRoute(route, { centerOnRoute });
      state.routeState = {
        flightId: Number(flight.id),
        routeId,
        signature,
        loaded: true
      };
    }
  } catch (error) {
    console.error('DispatcherWeb: failed to load route for selected flight.', error);
  }
}

const elements = {
  flightList: document.querySelector('#flight-list'),
  flightTabs: document.querySelectorAll('.flight-tab'),
  serverStatus: document.querySelector('#server-status'),
  telemetry: document.querySelector('#telemetry-details'),
  commandForm: document.querySelector('#command-form'),
  commandType: document.querySelector('#command-type'),
  commandValue: document.querySelector('#command-value'),
  commandSubmit: document.querySelector('#command-submit'),
  commandMessage: document.querySelector('#command-message'),
  history: document.querySelector('#command-history')
};

function setCommandMessage(text, type = '') {
  elements.commandMessage.textContent = text;
  elements.commandMessage.className = 'command-message';
  if (type) {
    elements.commandMessage.classList.add(type);
  }
}

function setServerStatus(online, label) {
  elements.serverStatus.textContent = label;
  elements.serverStatus.classList.toggle('online', online);
  elements.serverStatus.classList.toggle('offline', !online);
}

function formatDate(dateValue) {
  if (!dateValue) {
    return '—';
  }

  const date = new Date(dateValue);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }

  return date.toLocaleString('uk-UA', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
}

function renderFlightList() {
  const visibleFlights = getVisibleFlights();
  if (!visibleFlights.length) {
    elements.flightList.innerHTML = state.activeTab === 'history'
      ? '<li class="empty-state">Історія рейсів порожня.</li>'
      : '<li class="empty-state">Немає поточних рейсів.</li>';
    return;
  }

  const selectedId = state.selectedFlightId;

  elements.flightList.innerHTML = visibleFlights
    .map((flight) => {
      const statusClass = String(flight.status || '').trim().toLowerCase() || 'unknown';
      const isSelected = Number(flight.id) === Number(selectedId);
      const flightLabel = getFlightStatusLabel(flight.status);
      return `
        <li class="flight-item ${isSelected ? 'active' : ''}" data-flight-id="${flight.id}">
          <div class="flight-summary">
            <span class="flight-call-sign">${flight.callsign}</span>
            <span class="flight-meta">Рейс #${flight.id} • ${flight.aircraftId ?? '—'}</span>
          </div>
          <span class="flight-status ${statusClass}">${flightLabel}</span>
        </li>
      `;
    })
    .join('');

  elements.flightList.querySelectorAll('.flight-item').forEach((item) => {
    item.addEventListener('click', () => {
      const nextFlightId = Number(item.dataset.flightId);
      if (Number(state.selectedFlightId) === nextFlightId) {
        return;
      }

      setSelectedFlightId(nextFlightId);
      updateSelectedFlightState();
    });
  });
}

function renderTelemetry() {
  const selectedFlight = getSelectedFlight();
  if (!selectedFlight) {
    elements.telemetry.innerHTML = '<div class="empty-state">Виберіть рейс для перегляду.</div>';
    return;
  }

  const telemetryForFlight = state.aircraft
    .filter((snapshot) => Number(snapshot.flightId) === Number(selectedFlight.id))
    .sort((a, b) => new Date(b.timestampUtc) - new Date(a.timestampUtc));

  const liveTelemetry = telemetryForFlight.find((snapshot) => !snapshot.isHistorical && isTelemetryFresh(snapshot));
  const latest = telemetryForFlight[0] || null;
  const telemetryEntry = liveTelemetry || latest;

  if (!telemetryEntry) {
    const emptyMessage = isTerminalFlightStatus(selectedFlight.status)
      ? 'Для вибраного завершеного рейсу історична телеметрія відсутня або не прив’язана до рейсу.'
      : 'Для вибраного рейсу немає актуальних показів телеметрії.';
    elements.telemetry.innerHTML = `<div class="empty-state">${emptyMessage}</div>`;
    return;
  }

  const isHistorical = Boolean(telemetryEntry.isHistorical);
  const staleTelemetry = !isHistorical && !isTelemetryFresh(telemetryEntry);
  const historicFlightView = isTerminalFlightStatus(selectedFlight.status) && (isHistorical || staleTelemetry);
  const statusLabel = staleTelemetry ? 'Застаріла телеметрія' : getTelemetryStatusLabel(telemetryEntry.status, isHistorical);

  const coordinateCards = historicFlightView
    ? ''
    : `
      <div class="telemetry-card">
        <span class="label">Широта</span>
        <div class="value">${Number(telemetryEntry.latitude || 0).toFixed(5)}</div>
      </div>
      <div class="telemetry-card">
        <span class="label">Довгота</span>
        <div class="value">${Number(telemetryEntry.longitude || 0).toFixed(5)}</div>
      </div>
    `;

  elements.telemetry.innerHTML = `
    <div class="telemetry-grid">
      <div class="telemetry-card status ${isHistorical || staleTelemetry ? 'historial' : ''}">
        <span class="label">Статус</span>
        <div class="value">${statusLabel}</div>
      </div>
      <div class="telemetry-card">
        <span class="label">Швидкість</span>
        <div class="value">${Number(telemetryEntry.speedKmh || 0).toFixed(0)} км/год</div>
      </div>
      <div class="telemetry-card">
        <span class="label">Висота</span>
        <div class="value">${Number(telemetryEntry.altitudeMeters || 0).toFixed(0)} м</div>
      </div>
      <div class="telemetry-card">
        <span class="label">Курс</span>
        <div class="value">${Number(telemetryEntry.headingDegrees || 0).toFixed(0)}°</div>
      </div>
      ${coordinateCards}
      <div class="telemetry-card">
        <span class="label">Час</span>
        <div class="value">${formatDate(telemetryEntry.timestampUtc)}</div>
      </div>
      <div class="telemetry-card">
        <span class="label">Рейс</span>
        <div class="value">${selectedFlight.callsign}</div>
      </div>
    </div>
  `;
}

function renderCommandHistory() {
  if (!state.commandHistory.length) {
    elements.history.innerHTML = '<li class="empty-state">Команди для цього рейсу відсутні.</li>';
    return;
  }

  const sorted = [...state.commandHistory].sort((a, b) => new Date(b.issuedAtUtc) - new Date(a.issuedAtUtc));

  elements.history.innerHTML = sorted
    .map((command) => {
      const status = String(command.status || 'Pending').trim().toLowerCase();
      const statusClass = Object.prototype.hasOwnProperty.call(COMMAND_STATUS_LABELS, status) ? status : '';
      const message = localizeCommandMessage(command.resultMessage);
      const technicalDetails = message.isKnown || !message.original
        ? ''
        : `<details><summary>Технічне повідомлення</summary><small>${escapeHtml(message.original)}</small></details>`;

      return `
        <li>
          <div class="meta">
            <strong>${escapeHtml(localizeCommandType(command.type))}</strong>
            <small>${escapeHtml(command.commandId)}</small>
            <small title="${escapeHtml(message.original)}">${escapeHtml(message.text)}</small>
            ${technicalDetails}
          </div>
          <div class="command-status ${statusClass}">${escapeHtml(localizeCommandStatus(status))}</div>
        </li>
      `;
    })
    .join('');
}

function updateCommandAvailability() {
  const flight = getSelectedFlight();
  const canSend = String(flight?.status || '').trim().toLowerCase() === 'active';
  elements.commandType.disabled = !canSend;
  elements.commandValue.disabled = !canSend;
  elements.commandSubmit.disabled = !canSend;

  if (!flight) {
    setCommandMessage('Спочатку виберіть рейс.', 'error');
  } else if (canSend) {
    setCommandMessage('Команди можна надсилати для активного рейсу.', 'success');
  } else if (isTerminalFlightStatus(flight.status)) {
    setCommandMessage('Надсилання команд для завершеного рейсу недоступне.', 'error');
  } else {
    setCommandMessage('Керувати можна лише активним рейсом.', 'error');
  }
}

async function updateSelectedFlightState() {
  syncSelectedFlightWithTab();
  updateCommandAvailability();
  const flight = getSelectedFlight();
  const selectionVersion = state.selectedFlightVersion;
  if (!flight) {
    clearRoute();
    state.routeState = { flightId: null, routeId: null, signature: null, loaded: false };
    state.commandHistory = [];
    renderFlightList();
    renderTelemetry();
    renderCommandHistory();
    return;
  }

  if (state.routeState.flightId !== Number(flight.id)) {
    clearRoute();
    state.routeState = { flightId: null, routeId: null, signature: null, loaded: false };
  }
  state.commandHistory = [];
  renderCommandHistory();

  await ensureSelectedFlightRoute({ centerOnRoute: true, force: true, selectionVersion });
  if (!isCurrentSelection(flight.id, selectionVersion)) {
    return;
  }

  try {
    const commands = await getFlightCommands(flight.id);
    if (!isCurrentSelection(flight.id, selectionVersion)) {
      return;
    }
    state.commandHistory = Array.isArray(commands) ? commands : [];
  } catch (error) {
    console.error('DispatcherWeb: failed to load commands for selected flight.', error);
    if (!isCurrentSelection(flight.id, selectionVersion)) {
      return;
    }
    state.commandHistory = [];
  }

  renderFlightList();
  renderTelemetry();
  renderCommandHistory();
}

async function refreshDashboard() {
  let health = { ok: false, status: 'Недоступно', raw: 'Немає зв’язку' };

  try {
    health = await getHealth();
  } catch (error) {
    console.error('DispatcherWeb: health check threw an unexpected error.', error);
  }

  let flights = [];
  try {
    const rawFlights = await getFlights();
    flights = Array.isArray(rawFlights) ? rawFlights : [];
    if (!Array.isArray(rawFlights)) {
      console.error('DispatcherWeb: invalid flights payload from API.', rawFlights);
    }
  } catch (error) {
    console.error('DispatcherWeb: failed to load flights from API.', error);
  }

  let aircraft = [];
  try {
    const rawAircraft = await getAircraft();
    aircraft = Array.isArray(rawAircraft) ? rawAircraft : [];
    if (!Array.isArray(rawAircraft)) {
      console.error('DispatcherWeb: invalid aircraft payload from API.', rawAircraft);
    }
  } catch (error) {
    console.error('DispatcherWeb: failed to load aircraft telemetry from API.', error);
  }

  const previousSelectedFlightId = state.selectedFlightId;
  const previousSelectedFlight = getSelectedFlight();
  const previousSelectedFlightStatus = previousSelectedFlight?.status;
  state.flights = flights;
  state.aircraft = aircraft;
  syncSelectedFlightWithTab();
  const selectedFlight = getSelectedFlight();
  const selectionChanged = previousSelectedFlightId !== state.selectedFlightId;
  const selectedFlightStatusChanged = previousSelectedFlightStatus !== selectedFlight?.status;
  if (selectionChanged || selectedFlightStatusChanged) {
    updateCommandAvailability();
  }
  const selectionVersion = state.selectedFlightVersion;

  try {
    const flightsById = new Map(state.flights.map((flight) => [Number(flight.id), flight]));
    updateAircraftLayer(state.aircraft, flightsById, state.selectedFlightId);
    renderFlightList();
    renderTelemetry();
  } catch (error) {
    console.error('DispatcherWeb: map rendering failed while updating aircraft layer.', error);
  }

  if (state.selectedFlightId != null) {
    if (selectedFlight) {
      if (selectionChanged) {
        state.commandHistory = [];
        renderCommandHistory();
      }

      const selectedFlightChanged = selectionChanged;
      await ensureSelectedFlightRoute({
        centerOnRoute: selectedFlightChanged,
        force: selectedFlightChanged,
        selectionVersion
      });
      if (!isCurrentSelection(selectedFlight.id, selectionVersion)) {
        return;
      }

      try {
        const commands = await getFlightCommands(selectedFlight.id);
        if (!isCurrentSelection(selectedFlight.id, selectionVersion)) {
          return;
        }
        state.commandHistory = Array.isArray(commands) ? commands : [];
      } catch (error) {
        console.error('DispatcherWeb: failed to load commands for selected flight.', error);
        if (!isCurrentSelection(selectedFlight.id, selectionVersion)) {
          return;
        }
        state.commandHistory = [];
      }
    }
  } else {
    clearRoute();
    state.routeState = { flightId: null, routeId: null, signature: null, loaded: false };
    state.commandHistory = [];
  }

  renderCommandHistory();
  setServerStatus(health.ok, health.ok ? `Сервер доступний • ${health.raw}` : 'Сервер недоступний');
}

async function handleCommandSubmit(event) {
  event.preventDefault();

  const flight = getSelectedFlight();
  if (!flight) {
    setCommandMessage('Спочатку виберіть рейс.', 'error');
    return;
  }

  if (String(flight.status).trim().toLowerCase() !== 'active') {
    updateCommandAvailability();
    return;
  }

  const type = elements.commandType.value;
  const value = Number(elements.commandValue.value);

  if (!Number.isFinite(value)) {
    setCommandMessage('Введіть коректне значення.', 'error');
    return;
  }

  try {
    const response = await postFlightCommand(flight.id, type, value);
    const commandId = response?.commandId || response?.CommandId || 'не визначено';
    setCommandMessage(`Команда створена. CommandId: ${commandId}`, 'success');
    await refreshDashboard();
  } catch (error) {
    setCommandMessage(error?.message || 'Не вдалося відправити команду.', 'error');
  }
}

function bindFlightTabs() {
  elements.flightTabs.forEach((button) => {
    button.addEventListener('click', () => {
      const tab = button.dataset.flightTab;
      if (!tab || tab === state.activeTab) {
        return;
      }

      state.activeTab = tab;
      elements.flightTabs.forEach((tabButton) => {
        tabButton.classList.toggle('active', tabButton.dataset.flightTab === tab);
      });

      syncSelectedFlightWithTab();
      updateCommandAvailability();
      updateSelectedFlightState();
    });
  });
}

async function start() {
  initMap();
  bindFlightTabs();
  elements.commandForm.addEventListener('submit', handleCommandSubmit);
  await refreshDashboard();
  setInterval(() => {
    refreshDashboard();
  }, 2000);
}

document.addEventListener('DOMContentLoaded', start);
