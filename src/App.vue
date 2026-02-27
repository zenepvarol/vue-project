<template>
  <div class="app-container">

    <div class="sidebar left" :class="{ dark: darkMode, collapsed: !sidebarOpen }">
      <div class="sidebar-header" :class="{ dark: darkMode }">
        <div class="header-top">
          <h3>Uçuş Listesi</h3>
          <button class="dark-toggle" @click="toggleDarkMode" title="Modu Değiştir">
            {{ darkMode ? '☀️' : '🌙' }}
          </button>
        </div>
        <input v-model="searchQuery" placeholder="Uçuş (Callsign) ara..." class="search-input"
          :class="{ dark: darkMode }" />
      </div>

      <ul class="flight-list">
        <li v-for="f in filteredFlights" :key="f.icao24" @click="focusFlight(f)"
          :class="{ active: activeIcao === f.icao24 }">
          <div class="flight-info">
            <span class="callsign">{{ f.callsign || 'Bilinmiyor' }}</span>
          </div>
          <div class="arrow" :style="{ transform: `rotate(${(f.heading || 0) - 80}deg)` }">✈</div>
        </li>
      </ul>
    </div>

    <button class="sidebar-toggle" :class="{ dark: darkMode, open: sidebarOpen }" @click.stop="toggleSidebar">
      {{ sidebarOpen ? '◀' : '▶' }}
    </button>

    <div id="map"></div>

    <div v-if="selectedFlight" class="sidebar right" :class="{ dark: darkMode }">
      <div class="sidebar-header" :class="{ dark: darkMode }">
        <div class="header-top">
          <h3>Uçuş Detayları</h3>
          <button class="close-button" @click="activeIcao = null" title="Kapat">✕</button>
        </div>
      </div>

      <div class="flight-details" :class="{ dark: darkMode }">
        <div class="details-card" :class="{ dark: darkMode }">
          <div class="details-header">
            <span class="plane-icon-large">✈</span>
            <div class="header-text">
              <h2>{{ selectedFlight.callsign || 'Bilinmiyor' }}</h2>
              <span class="model-subtitle">{{ selectedFlight.modeltype || 'İnsansız Hava Aracı' }}</span>
            </div>
            <button class="locate-mini-button" @click="recenterMap" title="Uçağa Odaklan">➣</button>
          </div>

          <div class="details-grid">
            <div class="detail-item">
              <label>Durum</label>
              <span :style="{
                color: isEmergency ? '#e74c3c' : (isReturningToStart ? '#3498db' : (isPaused ? '#f39c12' : '#2ecc71')),
                fontWeight: 'bold'
              }">
                {{
                  isEmergency ? 'ACİL İNİŞTE' :
                    (isReturningToStart ? 'ANA MERKEZE DÖNÜLÜYOR' :
                      (isPaused ? 'DURDURULDU / BEKLEMEDE' : 'GÖREVDE'))
                }}
              </span>
            </div>

            <div class="details-row-inline">
              <div class="detail-item">
                <label>Hız</label>
                <span>{{ Math.round(selectedFlight.velocity) }} kt</span>
              </div>
              <div class="detail-item">
                <label>Rakım</label>
                <span>{{ Math.round(selectedFlight.baroaltitude) }} ft</span>
              </div>
            </div>

            <div class="detail-item">
              <label>Mesafe (Gidilen / Toplam)</label>
              <span>{{ Math.round(selectedFlight.distance_from_dep) }} / {{ Math.round(selectedFlight.trip_distance) }}
                km</span>
            </div>

            <div v-if="selectedFlight.trip_distance > 0" class="detail-item progress-container">
              <label>Yolun %{{ Math.round((selectedFlight.distance_from_dep / selectedFlight.trip_distance) * 100) }}
                tamamlandı</label>
              <div class="progress-bar">
                <div class="progress-fill"
                  :style="{ width: (selectedFlight.distance_from_dep / selectedFlight.trip_distance) * 100 + '%' }">
                </div>
              </div>
            </div>
          </div>

          <div class="action-section">
            <button v-if="animationSteps[activeIcao] > 0 && !isReturningToStart && !isEmergency"
              class="returnhome-button" @click="returnToStart" :class="{ dark: darkMode }">
              ANA MERKEZE DÖN
            </button>

            <button v-if="isPaused && animationSteps[activeIcao] === 0" class="pause-button paused"
              :class="{ dark: darkMode }" @click="togglePause">
              KALKIŞ ONAYI VER
            </button>

            <div v-if="!isPaused && !isReturningToStart && !isEmergency && nearestAirport" class="emergency-section">
              <div class="nearest-airport-info">
                En Yakın Güvenli Nokta: <strong>{{ nearestAirport.name }}</strong>
              </div>
              <button class="emergency-button" @click="startEmergencyLanding">
                ACİL İNİŞ YAP
              </button>
            </div>
          </div>

          <div v-if="isEmergency" class="emergency-status-note" style="color: #e74c3c;">
            Acil iniş noktasına gidiliyor.
          </div>
          <div v-if="isReturningToStart" class="emergency-status-note" style="color: #3498db;">
            Ana merkeze dönüş yapılıyor.
          </div>

        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref, computed } from 'vue';
import './assets/flight-style.css';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-rotatedmarker';
import 'leaflet.marker.slideto';
// REAKTİF DEGİSKENLER 
const searchQuery = ref('');
const currentFlights = ref({});
const markers = {};
const flightPaths = ref({});
const animationSteps = ref({});
const activeIcao = ref(null);
const darkMode = ref(false);
const isPaused = ref(true);
const sidebarOpen = ref(true);
const airports = ref([]);
const isEmergency = ref(false);
const emergencyRoute = ref(null);
const isReturningToStart = ref(false);

const resetActivePath = (icao) => {
  if (activeRoutes[icao]) {
    map.removeLayer(activeRoutes[icao]);
    const currentPos = [currentFlights.value[icao].lat, currentFlights.value[icao].lon];
    activeRoutes[icao] = L.polyline([currentPos], { color: '#9381ff', weight: 4, opacity: 1 }).addTo(map);
  }
};

const getNearestAirport = (planeLat, planeLon) => {
  if (!airports.value.length) return null;
  return airports.value.reduce((nearest, airport) => {
    const distToNearest = Math.sqrt(Math.pow(planeLat - nearest.lat, 2) + Math.pow(planeLon - nearest.lon, 2));
    const distToCurr = Math.sqrt(Math.pow(planeLat - airport.lat, 2) + Math.pow(planeLon - airport.lon, 2));
    return distToCurr < distToNearest ? airport : nearest;
  });
};

const returnToStart = () => {
  if (!activeIcao.value) return;

  const icao = activeIcao.value;
  resetActivePath(icao);

  isReturningToStart.value = true;
  isEmergency.value = false;
  isPaused.value = false;

  if (emergencyRoute.value) {
    map.removeLayer(emergencyRoute.value);
    emergencyRoute.value = null;
  }
};

const nearestAirport = computed(() => {
  if (selectedFlight.value && airports.value.length > 0) {
    return getNearestAirport(selectedFlight.value.lat, selectedFlight.value.lon);
  }
  return null;
});

const toggleDarkMode = () => { darkMode.value = !darkMode.value; };
const toggleSidebar = () => { sidebarOpen.value = !sidebarOpen.value; };

const togglePause = () => {
  if (!activeIcao.value) return;
  if (isPaused.value) {
    isPaused.value = false;
    isReturningToStart.value = false;
    isEmergency.value = false;
  }
};

const recenterMap = () => {
  if (selectedFlight.value) {
    map.setView([selectedFlight.value.lat, selectedFlight.value.lon], 8, {
      animate: true,
      duration: 1
    });
  }
};

const staticRoutes = {};
const activeRoutes = {};
const terminalMarkers = {};
let map = null;

const filteredFlights = computed(() => {
  const query = searchQuery.value.toLowerCase();
  return Object.values(currentFlights.value).filter(f =>
    f.callsign?.toString().toLowerCase().includes(query)
  );
});

const selectedFlight = computed(() => activeIcao.value ? currentFlights.value[activeIcao.value] : null);

const clearAllRoutes = () => {
  Object.keys(staticRoutes).forEach(key => { if (staticRoutes[key]) map.removeLayer(staticRoutes[key]); delete staticRoutes[key]; });
  Object.keys(activeRoutes).forEach(key => { if (activeRoutes[key]) map.removeLayer(activeRoutes[key]); delete activeRoutes[key]; });
  if (emergencyRoute.value) {
    map.removeLayer(emergencyRoute.value);
    emergencyRoute.value = null;
  }
  Object.keys(terminalMarkers).forEach(key => {
    if (terminalMarkers[key]) terminalMarkers[key].forEach(layer => map.removeLayer(layer));
    delete terminalMarkers[key];
  });
};

const drawFullRoute = (icao) => {
  if (staticRoutes[icao]) map.removeLayer(staticRoutes[icao]);
  if (activeRoutes[icao]) map.removeLayer(activeRoutes[icao]);
  if (terminalMarkers[icao]) terminalMarkers[icao].forEach(m => map.removeLayer(m));

  const allPoints = flightPaths.value[icao].map(p => [p.lat, p.lon]);
  const currentStep = animationSteps.value[icao];

  staticRoutes[icao] = L.polyline(allPoints, { color: '#0077b6', weight: 2, opacity: 0.5 }).addTo(map);
  const pointsSoFar = allPoints.slice(0, currentStep + 1);
  activeRoutes[icao] = L.polyline(pointsSoFar, { color: '#9381ff', weight: 4, opacity: 1 }).addTo(map);

  const startCircle = L.circleMarker(allPoints[0], { radius: 6, color: '#2ecc71', fillOpacity: 1, weight: 2 });
  const endCircle = L.circleMarker(allPoints[allPoints.length - 1], { radius: 6, color: '#e74c3c', fillOpacity: 1, weight: 2 });

  startCircle.addTo(map);
  endCircle.addTo(map);
  terminalMarkers[icao] = [startCircle, endCircle];
};

const startEmergencyLanding = () => {
  if (selectedFlight.value && nearestAirport.value) {
    const icao = activeIcao.value;
    resetActivePath(icao); // ESKİ YOLU TEMİZLE

    isEmergency.value = true;
    isPaused.value = false;

    const start = [selectedFlight.value.lat, selectedFlight.value.lon];
    const end = [nearestAirport.value.lat, nearestAirport.value.lon];

    if (emergencyRoute.value) map.removeLayer(emergencyRoute.value);

    emergencyRoute.value = L.polyline([start, end], {
      color: 'red',
      weight: 4,
      dashArray: '10, 10',
      opacity: 0.8
    }).addTo(map);

    map.fitBounds(L.latLngBounds([start, end]), { padding: [50, 50], maxZoom: 8 });
  }
};

onMounted(async () => {
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180));
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap',
    noWrap: true
  }).addTo(map);

  try {
    const airResponse = await fetch('/airports_library_v6.json');
    airports.value = await airResponse.json();

    airports.value.forEach(ap => {
      const airportIcon = L.divIcon({
        html: `<div class="airport-marker"><div class="airport-dot"></div><span class="airport-label">${ap.id}</span></div>`,
        className: 'custom-airport',
        iconSize: [30, 30]
      });
      L.marker([ap.lat, ap.lon], { icon: airportIcon }).addTo(map).bindPopup(`<b>${ap.name}</b><br>Acil İniş Noktası`);
    });

    const response = await fetch('/ucus_final_v7.json');
    const data = await response.json();
    const grouped = data.reduce((acc, row) => {
      if (!acc[row.icao24]) acc[row.icao24] = [];
      acc[row.icao24].push(row);
      return acc;
    }, {});

    flightPaths.value = grouped;

    const planeIcon = L.divIcon({
      html: `<div class="moving-plane">✈</div>`,
      className: 'plane-icon',
      iconSize: [40, 40],
      iconAnchor: [20, 20]
    });

    Object.keys(grouped).forEach(icao => {
      const firstPoint = grouped[icao][0];
      currentFlights.value[icao] = firstPoint;
      animationSteps.value[icao] = 0;

      if (firstPoint.lat && firstPoint.lon) {
        const marker = L.marker([firstPoint.lat, firstPoint.lon], {
          icon: planeIcon,
          rotationAngle: (firstPoint.heading || 0) - 80
        }).addTo(map);

        marker.on('click', (e) => {
          L.DomEvent.stopPropagation(e);
          sidebarOpen.value = true;
          if (activeIcao.value !== icao) {
            clearAllRoutes();
            activeIcao.value = icao;
            isPaused.value = true;
            drawFullRoute(icao);
            recenterMap();
          }
        });
        markers[icao] = marker;
      }
    });

    setInterval(() => {
      if (activeIcao.value && flightPaths.value[activeIcao.value] && !isPaused.value) {
        const icao = activeIcao.value;
        const path = flightPaths.value[icao];
        const step = animationSteps.value[icao];

        // ACİL İNİŞ 
        if (isEmergency.value && nearestAirport.value) {
          const plane = currentFlights.value[icao];
          const target = nearestAirport.value;
          const dx = target.lat - plane.lat;
          const dy = target.lon - plane.lon;
          const dist = Math.sqrt(dx * dx + dy * dy);
          const moveStep = 0.035;

          if (dist > moveStep) {
            const nextPos = [plane.lat + (dx / dist) * moveStep, plane.lon + (dy / dist) * moveStep];

            currentFlights.value[icao].baroaltitude = Math.max(10, plane.baroaltitude - 50);
            currentFlights.value[icao].velocity = Math.max(15, plane.velocity - 4);

            currentFlights.value[icao].lat = nextPos[0];
            currentFlights.value[icao].lon = nextPos[1];

            if (markers[icao]) {
              markers[icao].setLatLng(nextPos);
              markers[icao].setRotationAngle((Math.atan2(dy, dx) * (180 / Math.PI)) - 80);
              if (activeRoutes[icao]) activeRoutes[icao].addLatLng(nextPos);
            }
          } else {
            currentFlights.value[icao].baroaltitude = 0;
            currentFlights.value[icao].velocity = 0;
            isPaused.value = true;
            isEmergency.value = false;
            if (emergencyRoute.value) { map.removeLayer(emergencyRoute.value); emergencyRoute.value = null; }
          }
        }

        // ANA MERKEZE DÖNÜŞ
        else if (isReturningToStart.value) {
          const plane = currentFlights.value[icao];
          const target = path[0];
          const dx = target.lat - plane.lat;
          const dy = target.lon - plane.lon;
          const dist = Math.sqrt(dx * dx + dy * dy);
          const moveStep = 0.03;

          if (dist > moveStep) {
            const nextPos = [plane.lat + (dx / dist) * moveStep, plane.lon + (dy / dist) * moveStep];

            currentFlights.value[icao].baroaltitude = Math.max(10, plane.baroaltitude - 40);
            currentFlights.value[icao].velocity = Math.max(20, plane.velocity - 3);

            currentFlights.value[icao].lat = nextPos[0];
            currentFlights.value[icao].lon = nextPos[1];

            if (markers[icao]) {
              markers[icao].setLatLng(nextPos);
              markers[icao].setRotationAngle((Math.atan2(dy, dx) * (180 / Math.PI)) - 80);
              if (activeRoutes[icao]) activeRoutes[icao].addLatLng(nextPos);
            }
          } else {
            currentFlights.value[icao].baroaltitude = 0;
            currentFlights.value[icao].velocity = 0;
            isReturningToStart.value = false;
            isPaused.value = true;
            animationSteps.value[icao] = 0;
            clearAllRoutes();
            drawFullRoute(icao);
          }
        }

        // NORMAL İLERLEME
        else {
          if (step + 1 >= path.length) {
            isPaused.value = true;
            return;
          }

          const nextStep = step + 1;
          const point = path[nextStep];

          animationSteps.value[icao] = nextStep;
          currentFlights.value[icao] = { ...point };

          if (markers[icao]) {
            const newPos = [point.lat, point.lon];

            // Eğer uçak rotadan çok uzaktaysa
            const currentPos = markers[icao].getLatLng();
            const distCheck = Math.sqrt(Math.pow(newPos[0] - currentPos.lat, 2) + Math.pow(newPos[1] - currentPos.lng, 2));

            if (distCheck > 0.5) {
              markers[icao].setLatLng(newPos);
            } else {
              markers[icao].slideTo(newPos, { duration: 100 });
            }

            markers[icao].setRotationAngle((point.heading || 0) - 80);
            if (activeRoutes[icao]) activeRoutes[icao].addLatLng(newPos);
          }
        }
      }
    }, 100);
  } catch (error) {
    console.error("Hata:", error);
  }
});

const focusFlight = (f) => {
  if (f.lat && f.lon) {
    sidebarOpen.value = true;
    if (activeIcao.value !== f.icao24) {
      clearAllRoutes();
      isPaused.value = true;
      activeIcao.value = f.icao24;
      drawFullRoute(f.icao24);
    }
    map.setView([f.lat, f.lon], 8, { animate: true, duration: 1 });
  }
};
</script>