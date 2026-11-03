let mapInstance = null;
let aircraftMarkers = new Map();
let routeLayer = null;
let waypointMarkers = [];

export function initMap() {
  if (mapInstance) {
    return mapInstance;
  }

  mapInstance = L.map('map', {
    zoomControl: true,
    attributionControl: true
  });

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 18,
    attribution: '&copy; OpenStreetMap'
  }).addTo(mapInstance);

  mapInstance.setView([50.45, 30.52], 5);
  return mapInstance;
}

export function clearRoute() {
  if (routeLayer) {
    mapInstance.removeLayer(routeLayer);
    routeLayer = null;
  }

  waypointMarkers.forEach((marker) => mapInstance.removeLayer(marker));
  waypointMarkers = [];
}

function isTelemetryFresh(snapshot) {
  const timestamp = Date.parse(snapshot?.timestampUtc ?? '');
  return Number.isFinite(timestamp) && Date.now() - timestamp <= 90_000;
}

export function showRoute(route, options = {}) {
  const { centerOnRoute = false } = options;

  if (!mapInstance || !route || !route.waypoints || !route.waypoints.length) {
    clearRoute();
    return;
  }

  clearRoute();

  const points = [...route.waypoints]
    .sort((a, b) => a.sequenceNumber - b.sequenceNumber)
    .map((waypoint) => [Number(waypoint.latitude), Number(waypoint.longitude)]);

  routeLayer = L.polyline(points, {
    color: '#38bdf8',
    weight: 3,
    opacity: 0.85
  }).addTo(mapInstance);

  points.forEach(([lat, lng], index) => {
    const marker = L.circleMarker([lat, lng], {
      radius: 4,
      color: '#dbeafe',
      fillColor: index === 0 || index === points.length - 1 ? '#fbbf24' : '#38bdf8',
      fillOpacity: 0.9,
      weight: 1
    }).addTo(mapInstance);

    const waypoint = route.waypoints[index];
    marker.bindPopup(`<strong>${waypoint.name}</strong><br>Порядок: ${waypoint.sequenceNumber}`);
    waypointMarkers.push(marker);
  });

  if (centerOnRoute) {
    mapInstance.fitBounds(points.length > 1 ? points : [[points[0][0], points[0][1]]]);
  }
}

export function updateAircraftLayer(aircraftList, flightsById, selectedFlightId) {
  if (!mapInstance) {
    return;
  }

  if (!Array.isArray(aircraftList)) {
    throw new TypeError('DispatcherWeb: aircraftList is not an array.');
  }

  const validAircraftIds = new Set();

  aircraftList.forEach((item) => {
    if (!item || typeof item !== 'object') {
      console.warn('DispatcherWeb: skipping invalid aircraft telemetry item:', item);
      return;
    }

    const flightId = Number(item.flightId);
    const aircraftId = Number(item.aircraftId);
    const lat = Number(item.latitude);
    const lon = Number(item.longitude);
    const flight = flightsById.get(flightId);
    const isSelected = flightId === Number(selectedFlightId);
    const isHistorical = Boolean(item.isHistorical);
    const isLiveAndFresh = !isHistorical && flightId > 0 && isTelemetryFresh(item);

    if (!Number.isFinite(aircraftId) || !Number.isFinite(lat) || !Number.isFinite(lon) || !isLiveAndFresh) {
      if (Number.isFinite(aircraftId) && aircraftMarkers.has(aircraftId) && !isLiveAndFresh) {
        const marker = aircraftMarkers.get(aircraftId);
        if (marker) {
          mapInstance.removeLayer(marker);
          aircraftMarkers.delete(aircraftId);
        }
      }
      return;
    }

    validAircraftIds.add(aircraftId);

    let marker = aircraftMarkers.get(aircraftId);
    if (!marker) {
      marker = L.circleMarker([lat, lon], {
        radius: 8,
        color: '#02131a',
        weight: 2,
        fillColor: isSelected ? '#38bdf8' : '#34d399',
        fillOpacity: 0.9,
        opacity: 1
      }).addTo(mapInstance);
      aircraftMarkers.set(aircraftId, marker);
    }

    marker.setLatLng([lat, lon]);
    marker.setStyle({
      radius: isSelected ? 10 : 7,
      fillColor: isSelected ? '#38bdf8' : '#34d399',
      opacity: 1,
      fillOpacity: 0.9,
      weight: 2
    });
    marker.bindPopup(`
      <strong>${flight?.callsign || 'Літак'} </strong><br>
      Швидкість: ${Number(item.speedKmh).toFixed(0)} км/год<br>
      Висота: ${Number(item.altitudeMeters).toFixed(0)} м<br>
      Курс: ${Number(item.headingDegrees).toFixed(0)}°
    `);
  });

  Array.from(aircraftMarkers.entries()).forEach(([aircraftId, marker]) => {
    if (!validAircraftIds.has(aircraftId)) {
      mapInstance.removeLayer(marker);
      aircraftMarkers.delete(aircraftId);
    }
  });
}
