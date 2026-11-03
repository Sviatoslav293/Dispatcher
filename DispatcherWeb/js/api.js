async function readErrorMessage(response) {
  const contentType = response.headers.get('content-type') || '';

  try {
    if (contentType.includes('application/json')) {
      const payload = await response.json();
      return payload?.message || payload?.error || JSON.stringify(payload);
    }

    const text = await response.text();
    return text || `HTTP ${response.status}`;
  } catch {
    return `HTTP ${response.status}`;
  }
}

async function requestJson(url, options = {}) {
  try {
    const response = await fetch(url, {
      cache: 'no-store',
      headers: {
        Accept: 'application/json',
        ...(options.headers || {})
      },
      ...options
    });

    if (!response.ok) {
      const message = await readErrorMessage(response);
      throw new Error(`${response.status} ${message}`);
    }

    if (response.status === 204) {
      return null;
    }

    const contentType = response.headers.get('content-type') || '';
    if (!contentType.includes('application/json')) {
      return response.text();
    }

    return response.json();
  } catch (error) {
    const detailedError = error instanceof Error ? error : new Error(String(error));
    console.error(`DispatcherWeb: API request failed for ${url}`, detailedError);
    throw detailedError;
  }
}

export async function getHealth() {
  try {
    const response = await fetch('/health', { cache: 'no-store' });
    const text = await response.text();
    return {
      ok: response.ok,
      status: response.ok ? (text.trim() || 'Healthy') : 'Недоступно',
      raw: text.trim() || 'Healthy'
    };
  } catch (error) {
    const detailedError = error instanceof Error ? error : new Error(String(error));
    console.error('DispatcherWeb: health check failed.', detailedError);
    return {
      ok: false,
      status: 'Недоступно',
      raw: 'Немає зв’язку' 
    };
  }
}

export async function getFlights() {
  return requestJson('/api/flights');
}

export async function getAircraft() {
  return requestJson('/api/aircraft');
}

export async function getFlightById(flightId) {
  return requestJson(`/api/flights/${flightId}`);
}

export async function getRoute(routeId) {
  return requestJson(`/api/routes/${routeId}`);
}

export async function getFlightCommands(flightId) {
  return requestJson(`/api/flights/${flightId}/commands`);
}

export async function getCommandById(commandId) {
  return requestJson(`/api/commands/${commandId}`);
}

export async function postFlightCommand(flightId, type, value) {
  return requestJson(`/api/flights/${flightId}/commands`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ type, value })
  });
}
