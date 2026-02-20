<template>
  <div class="app-container">
    <div class="sidebar" :class="{ dark: darkMode, collapsed: !sidebarOpen }">
      <div class="sidebar-header" :class="{ dark: darkMode }">
        <div class="header-top">
          <h3>{{ selectedFlight ? 'Uçuş Detayları' : 'Uçuş Listesi' }}</h3>
          <button class="dark-toggle" @click="toggleDarkMode" :title="darkMode ? 'Aydınlık Mod' : 'Karanlık Mod'">
            {{ darkMode ? '☀️' : '🌙' }}
          </button>
        </div>
        <input v-if="!selectedFlight" v-model="searchQuery" placeholder="Uçuş (Callsign) ara..." class="search-input"
          :class="{ dark: darkMode }" />
      </div>

      <div v-if="selectedFlight" class="flight-details" :class="{ dark: darkMode }">
        <button class="back-button" @click="activeIcao = null; clearAllRoutes()" :class="{ dark: darkMode }">
          ◀ Listeye Dön
        </button>
        <div class="details-card" :class="{ dark: darkMode }">
          <div class="details-header">
            <span class="plane-icon-large">✈</span>
            <h2>{{ selectedFlight.callsign || 'Bilinmiyor' }}</h2>
          </div>
          <div class="details-grid">
            <div class="detail-item">
              <label>Model</label>
              <span>{{ selectedFlight.modeltype || 'N/A' }}</span>
            </div>
            <div class="detail-item">
              <label>Hız</label>
              <span>{{ Math.round(selectedFlight.velocity) }} kt</span>
            </div>
            <div class="detail-item">
              <label>Rakım</label>
              <span>{{ Math.round(selectedFlight.baroaltitude) }} ft</span>
            </div>
            <div class="detail-item">
              <label>Mesafe (Gidilen / Toplam)</label>
              <span>{{ Math.round(selectedFlight.distance_from_dep) }} / {{ Math.round(selectedFlight.trip_distance) }}
                km</span>
            </div>
            <div v-if="selectedFlight.trip_distance" class="detail-item progress-container">
              <label>Yolun %{{ Math.round((selectedFlight.distance_from_dep / selectedFlight.trip_distance) * 100) }}
                tamamlandı</label>
              <div class="progress-bar">
                <div class="progress-fill"
                  :style="{ width: (selectedFlight.distance_from_dep / selectedFlight.trip_distance) * 100 + '%' }">
                </div>
              </div>
            </div>
          </div>
          <button class="pause-button" @click="isPaused = !isPaused" :class="{ dark: darkMode, paused: isPaused }">
            {{ isPaused ? 'Hareketi Başlat' : 'Hareketi Durdur' }}
          </button>
        </div>
      </div>

      <ul v-else class="flight-list">
        <li v-for="f in filteredFlights" :key="f.icao24" @click="focusFlight(f)">
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
  </div>
</template>

<script setup>
import { onMounted, ref, computed } from 'vue';
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
const isPaused = ref(false); // hareket halindeyken duraklatma icin
const sidebarOpen = ref(true);

const toggleDarkMode = () => {
  darkMode.value = !darkMode.value;
};

const toggleSidebar = () => {
  sidebarOpen.value = !sidebarOpen.value;
};

// GUZERGAH GORSELI
const staticRoutes = {};
const activeRoutes = {};
const terminalMarkers = {};

let map = null;

// COMPUTED: Liste aramaya göre filtrelenir
const filteredFlights = computed(() => {
  const query = searchQuery.value.toLowerCase();
  return Object.values(currentFlights.value).filter(f =>
    f.callsign?.toString().toLowerCase().includes(query)
  );
});

// COMPUTED: Secilen uçuş verisi
const selectedFlight = computed(() => activeIcao.value ? currentFlights.value[activeIcao.value] : null);

// Haritadaki tüm eski rotaları siler
const clearAllRoutes = () => {
  Object.keys(staticRoutes).forEach(key => {
    if (staticRoutes[key]) map.removeLayer(staticRoutes[key]);
    delete staticRoutes[key];
  });
  Object.keys(activeRoutes).forEach(key => {
    if (activeRoutes[key]) map.removeLayer(activeRoutes[key]);
    delete activeRoutes[key];
  });
  Object.keys(terminalMarkers).forEach(key => {
    if (terminalMarkers[key]) {
      terminalMarkers[key].forEach(layer => map.removeLayer(layer));
    }
    delete terminalMarkers[key];
  });
};

const drawFullRoute = (icao) => {
  if (staticRoutes[icao]) map.removeLayer(staticRoutes[icao]);
  if (activeRoutes[icao]) map.removeLayer(activeRoutes[icao]);
  if (terminalMarkers[icao]) {
    terminalMarkers[icao].forEach(m => map.removeLayer(m));
  }

  const allPoints = flightPaths.value[icao].map(p => [p.lat, p.lon]);
  const currentStep = animationSteps.value[icao];

  staticRoutes[icao] = L.polyline(allPoints, {
    color: '#0077b6', //mavi 
    weight: 2,
    opacity: 0.5
  }).addTo(map);

  const pointsSoFar = allPoints.slice(0, currentStep + 1);
  activeRoutes[icao] = L.polyline(pointsSoFar, {
    color: '#9381ff', // violet
    weight: 4,
    opacity: 1
  }).addTo(map);

  const startCircle = L.circleMarker(allPoints[0], { radius: 6, color: '#2ecc71', fillOpacity: 1, weight: 2 });
  const endCircle = L.circleMarker(allPoints[allPoints.length - 1], { radius: 6, color: '#e74c3c', fillOpacity: 1, weight: 2 });

  startCircle.addTo(map);
  endCircle.addTo(map);
  terminalMarkers[icao] = [startCircle, endCircle];
};

onMounted(async () => {
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180));

  map = L.map('map', {
    maxBounds: worldBounds,
    maxBoundsViscosity: 1.0,
    minZoom: 2
  }).setView([20, 0], 2);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap',
    noWrap: true //sonsuz dongu kapandı
  }).addTo(map);

  try {
    const response = await fetch('/ucus_tamamlandi.json');
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

          if (activeIcao.value === icao) {
            isPaused.value = !isPaused.value;
          } else {
            clearAllRoutes();
            animationSteps.value[icao] = 0;
            activeIcao.value = icao;
            isPaused.value = false;
            drawFullRoute(icao);
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

        if (step + 1 >= path.length) {
          activeIcao.value = null;
          return;
        }

        const nextStep = step + 1;
        const point = path[nextStep];

        animationSteps.value[icao] = nextStep;
        currentFlights.value[icao] = point;

        if (markers[icao]) {
          const newPos = [point.lat, point.lon];

          markers[icao].slideTo(newPos, {
            duration: 100,
            keepAtCenter: false
          });

          markers[icao].setRotationAngle((point.heading || 0) - 80);

          if (activeRoutes[icao]) {
            activeRoutes[icao].addLatLng(newPos);
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

    clearAllRoutes();
    isPaused.value = false;
    animationSteps.value[f.icao24] = 0;
    activeIcao.value = f.icao24;
    drawFullRoute(f.icao24);

    map.setView([f.lat, f.lon], 12, {
      animate: true,
      duration: 1 // saniye cinsinden akıcı geçiş
    });
  }
};
</script>

<style>
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

html,
body {
  height: 100%;
  width: 100%;
  font-family: 'Segoe UI', sans-serif;
  background: #121212;
  overflow: hidden;
}

.app-container {
  display: flex;
  height: 100vh;
  width: 100vw;
  position: absolute;
  top: 0;
  left: 0;
}

.sidebar {
  width: 320px;
  min-width: 320px;
  background: #f7d9c4;
  color: #333;
  display: flex;
  flex-direction: column;
  border-right: 1px solid #ccc;
  z-index: 1000;
  transition: width 0.3s ease, min-width 0.3s ease, background 0.3s, color 0.3s;
  overflow: hidden;
}

.sidebar.collapsed {
  width: 0;
  min-width: 0;
  border-right: none;
}

.sidebar-toggle {
  position: absolute;
  left: 320px;
  top: 50%;
  transform: translateY(-50%);
  z-index: 1100;
  background: #f7d9c4;
  border: 1px solid #ccc;
  border-left: none;
  border-radius: 0 8px 8px 0;
  padding: 10px 6px;
  cursor: pointer;
  font-size: 14px;
  color: #333;
  transition: left 0.3s ease, background 0.3s, color 0.3s;
  box-shadow: 2px 0 6px rgba(0, 0, 0, 0.15);
  line-height: 1;
}

.sidebar-toggle:not(.open) {
  left: 0;
}

.sidebar-toggle.dark {
  background: #16213e;
  border-color: #333;
  color: #e0e0e0;
}

.sidebar-toggle:hover {
  background: #fec3a6;
}

.sidebar-toggle.dark:hover {
  background: #0f3460;
}

.sidebar.dark {
  background: #1a1a2e;
  color: #e0e0e0;
  border-right-color: #333;
}

.sidebar-header {
  padding: 20px;
  background: #e2cfc4;
  border-bottom: 1px solid #ccc;
  transition: background 0.3s;
}

.sidebar-header.dark {
  background: #16213e;
  border-bottom-color: #333;
}

.header-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.dark-toggle {
  background: none;
  border: 1px solid #aaa;
  border-radius: 20px;
  padding: 4px 10px;
  cursor: pointer;
  font-size: 16px;
}

.sidebar-header h3 {
  margin: 0;
  color: #000;
  transition: color 0.3s;
}

.sidebar-header.dark h3 {
  color: #e0e0e0;
}

.search-input {
  width: 100%;
  padding: 10px;
  border-radius: 4px;
  border: 1px solid #fff;
  background: #faedcb;
  color: #000;
  box-sizing: border-box;
}

.search-input.dark {
  background: #0f3460;
  color: #e0e0e0;
  border-color: #444;
}

.flight-list {
  flex: 1;
  overflow-y: auto;
  list-style: none;
  padding: 0;
}

.flight-list li {
  padding: 15px 20px;
  border-bottom: 1px solid #e2cfc4;
  cursor: pointer;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.sidebar.dark .flight-list li {
  border-bottom-color: #2a2a4a;
}

.flight-list li:hover {
  background: #fec3a6;
}

.sidebar.dark .flight-list li:hover {
  background: #0f3460;
}

.callsign {
  font-weight: bold;
  color: #282828;
  transition: color 0.3s;
}

.sidebar.dark .callsign {
  color: #e0e0e0;
}

.details {
  font-size: 0.8em;
  color: #666;
}

.sidebar.dark .details {
  color: #9a9ab0;
}

#map {
  flex-grow: 1;
  height: 100%;
  background: #aad3df;
}

.moving-plane {
  font-size: 40px;
  color: #9381ff;
  text-shadow: 1px 1px 2px black;
  transition: all 600ms linear;
}

.plane-icon {
  background: none !important;
  border: none !important;
  display: flex;
  align-items: center;
  justify-content: center;
}

.flight-details {
  padding: 20px;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 15px;
  overflow-y: auto;
}

.back-button {
  background: #faedcb;
  border: 1px solid #ccc;
  padding: 8px 12px;
  border-radius: 6px;
  cursor: pointer;
  align-self: flex-start;
  font-weight: bold;
  transition: all 0.2s;
}

.back-button.dark {
  background: #0f3460;
  color: #e0e0e0;
  border-color: #333;
}

.back-button:hover {
  background: #fec3a6;
}

.details-card {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.details-card.dark {
  background: #16213e;
  border: 1px solid #333;
}

.details-header {
  display: flex;
  align-items: center;
  gap: 15px;
  border-bottom: 2px solid #f7d9c4;
  padding-bottom: 10px;
}

.details-card.dark .details-header {
  border-bottom-color: #2a2a4a;
}

.plane-icon-large {
  font-size: 32px;
  color: #9381ff;
}

.details-grid {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.detail-item label {
  font-size: 11px;
  text-transform: uppercase;
  color: #888;
  font-weight: bold;
  letter-spacing: 0.5px;
}

.detail-item span {
  font-size: 16px;
  font-weight: 600;
  color: #333;
}

.details-card.dark .detail-item span {
  color: #e0e0e0;
}

.progress-container {
  margin-top: 5px;
}

.progress-bar {
  height: 8px;
  background: #eee;
  border-radius: 4px;
  overflow: hidden;
  margin-top: 5px;
}

.details-card.dark .progress-bar {
  background: #2a2a4a;
}

.progress-fill {
  height: 100%;
  background: #9381ff;
  transition: width 0.3s ease;
}

.pause-button {
  margin-top: 10px;
  padding: 12px;
  border-radius: 8px;
  border: none;
  background: #9381ff;
  color: white;
  font-weight: bold;
  cursor: pointer;
  transition: all 0.2s;
}

.pause-button:hover {
  background: #7a69e6;
}

.pause-button.paused {
  background: #2ecc71;
}

.pause-button.dark {
  opacity: 0.9;
}
</style>