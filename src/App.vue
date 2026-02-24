<template>
  <div class="app-container">

    <!-- SOL PANEL -->
    <div class="sidebar left" :class="{ dark: darkMode, collapsed: !sidebarOpen }">
      <div class="sidebar-header" :class="{ dark: darkMode }">
        <div class="header-top">
          <h3>Uçuş Listesi</h3>
          <button class="dark-toggle" @click="toggleDarkMode" :title="darkMode ? 'Aydinlik Mod' : 'Karanlik Mod'">
            {{ darkMode ? '☀️' : '🌙' }}
          </button>
        </div>
        <input v-model="searchQuery" placeholder="Uçuş (Callsign) ara..." class="search-input"
          :class="{ dark: darkMode }" />
      </div>

      <!-- UCUS LISTESI -->
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

    <!-- SAĞ PANEL -->
    <div v-if="selectedFlight" class="sidebar right" :class="{ dark: darkMode }">
      <div class="sidebar-header" :class="{ dark: darkMode }">
        <div class="header-top">
          <h3>Uçuş Detayları</h3>
          <button class="close-button" @click="activeIcao = null; clearAllRoutes()" :title="'Kapat'">✕</button>
        </div>
      </div>

      <div class="flight-details" :class="{ dark: darkMode }">
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
          <button class="locate-button" @click="recenterMap" :class="{ dark: darkMode }"> Uçağa Odaklan</button>
          <button class="returnhome-button" @click="returnhome" :class="{ dark: darkMode }"> Ana Merkeze Dön </button>
          <button class="pause-button" @click="togglePause" :class="{ dark: darkMode, paused: isPaused }">
            {{ isPaused ? 'Hareketi Başlat' : 'Hareketi Durdur' }}
          </button>
          <div v-if="nearestAirport" class="nearest-airport-info"> En Yakın Acil İniş: <strong>{{ nearestAirport.name }}
              ({{ nearestAirport.id }})</strong>
          </div>
        </div>
      </div>
    </div>
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
const isPaused = ref(false);  // hareket halindeyken duraklatma icin
const sidebarOpen = ref(true);
const isReturning = ref(false);
const airports = ref([]); // Havalimanları icin 

// en yakin havalimanı hesaplama
const getNearestAirport = (planeLat, planeLon) => {
  if (!airports.value.length) return null;

  return airports.value.reduce((nearest, airport) => {
    // oklid mesafesi hesaplama
    const distToNearest = Math.sqrt(Math.pow(planeLat - nearest.lat, 2) + Math.pow(planeLon - nearest.lon, 2));
    const distToCurr = Math.sqrt(Math.pow(planeLat - airport.lat, 2) + Math.pow(planeLon - airport.lon, 2));
    return distToCurr < distToNearest ? airport : nearest;
  });
};

// Seçili ucagin her hareketinde en yakın havalimanını hesaplayama
const nearestAirport = computed(() => {
  if (selectedFlight.value && airports.value.length > 0) {
    return getNearestAirport(selectedFlight.value.lat, selectedFlight.value.lon);
  }
  return null;
});

const toggleDarkMode = () => { darkMode.value = !darkMode.value; };
const toggleSidebar = () => { sidebarOpen.value = !sidebarOpen.value; };

const togglePause = () => {
  if (isPaused.value && activeIcao.value) {
    const path = flightPaths.value[activeIcao.value];
    const step = animationSteps.value[activeIcao.value];
    if (path && step + 1 >= path.length) {
      animationSteps.value[activeIcao.value] = 0;
      clearAllRoutes();
      drawFullRoute(activeIcao.value);
    }
  }
  isPaused.value = !isPaused.value;
};

const recenterMap = () => {
  if (selectedFlight.value) {
    map.setView([selectedFlight.value.lat, selectedFlight.value.lon], 8, {
      animate: true, // harita kayarak gidiyor
      duration: 1 // kayma islemi 1 saniye 
    });
  }
};

const returnhome = () => {
  if (activeIcao.value) {
    isPaused.value = false;
    isReturning.value = true;
  }
};

// GUZERGAH GORSELI
const staticRoutes = {};
const activeRoutes = {};
const terminalMarkers = {};

let map = null;

// Aramaya göre uçuş filtreleme
const filteredFlights = computed(() => {
  const query = searchQuery.value.toLowerCase();
  return Object.values(currentFlights.value).filter(f =>
    f.callsign?.toString().toLowerCase().includes(query)
  );
});

// Seçili uçuş objesi
const selectedFlight = computed(() => activeIcao.value ? currentFlights.value[activeIcao.value] : null);

const clearAllRoutes = () => {
  Object.keys(staticRoutes).forEach(key => { if (staticRoutes[key]) map.removeLayer(staticRoutes[key]); delete staticRoutes[key]; });
  Object.keys(activeRoutes).forEach(key => { if (activeRoutes[key]) map.removeLayer(activeRoutes[key]); delete activeRoutes[key]; });
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

onMounted(async () => {
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180)); // Dünya sınırları

  // harita: sınırlar sabit, zoom seviyesi 2 
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap',
    noWrap: true // haritanin sonsuz tekrarlaması yatay
  }).addTo(map);

  try {
    const airResponse = await fetch('/airports_library_v6.json');     // Havalimanı verileri
    airports.value = await airResponse.json();

    // her havalimanı için özel icon
    airports.value.forEach(ap => {
      const airportIcon = L.divIcon({
        html: `
          <div class="airport-marker">
            <div class="airport-dot"></div>
            <span class="airport-label">${ap.id}</span>
          </div>`,
        className: 'custom-airport',
        iconSize: [30, 30]
      });

      // pop up
      L.marker([ap.lat, ap.lon], { icon: airportIcon })
        .addTo(map)
        .bindPopup(`<b>${ap.name}</b><br>Acil İniş Noktası`);
    });

    const response = await fetch('/ucus_final_v7.json'); //uçuş verileri
    const data = await response.json();

    // icao ya göre gruplama
    const grouped = data.reduce((acc, row) => {
      if (!acc[row.icao24]) acc[row.icao24] = [];
      acc[row.icao24].push(row);
      return acc;
    }, {});

    flightPaths.value = grouped; // tüm rotalar reaktif değişkene kaydediliyor

    // ucak iconu
    const planeIcon = L.divIcon({
      html: `<div class="moving-plane">✈</div>`,
      className: 'plane-icon',
      iconSize: [40, 40],
      iconAnchor: [20, 20]
    });

    // ucaklar ilk konumlarına yerlestirme
    Object.keys(grouped).forEach(icao => {
      const firstPoint = grouped[icao][0];
      currentFlights.value[icao] = firstPoint;
      animationSteps.value[icao] = 0; // animasyon 0. adımdan baslıyor

      if (firstPoint.lat && firstPoint.lon) {
        const marker = L.marker([firstPoint.lat, firstPoint.lon], {
          icon: planeIcon,
          rotationAngle: (firstPoint.heading || 0) - 80
        }).addTo(map);

        marker.on('click', (e) => {
          L.DomEvent.stopPropagation(e); // Tıklamanın haritaya geçmesini engelleme
          sidebarOpen.value = true;

          if (activeIcao.value === icao) {
            togglePause(); // Aynı uçaksa durdur/başlat
          } else {
            clearAllRoutes(); // Eski çizgileri sil
            animationSteps.value[icao] = 0; // Başa dön
            activeIcao.value = icao; // Yeni aktif uçağı belirle
            isPaused.value = false; // Hareket ettir
            drawFullRoute(icao); // Rotayı haritaya çiz
          }
        });
        markers[icao] = marker; // Marker'ı referans olarak sakla
      }
    });

    // ANİMSYON DÖNGÜSÜ
    setInterval(() => {
      if (activeIcao.value && flightPaths.value[activeIcao.value] && !isPaused.value) {
        const icao = activeIcao.value;
        const path = flightPaths.value[icao];
        const step = animationSteps.value[icao];

        // GERİ DÖNÜŞ
        if (isReturning.value) {
          if (step <= 0) { // Başlangıca geldiyse dur
            isReturning.value = false;
            isPaused.value = true; return;
          }
          const prevStep = step - 1; // Bir önceki adıma 
          const point = path[prevStep];
          animationSteps.value[icao] = prevStep;
          currentFlights.value[icao] = point;

          if (markers[icao]) {
            markers[icao].slideTo([point.lat, point.lon], { duration: 100 });
            markers[icao].setRotationAngle((point.heading || 0) + 100); // ilk aciya +180
            if (activeRoutes[icao]) {
              const currentLatLngs = activeRoutes[icao].getLatLngs(); // Mevcut çizgi
              currentLatLngs.pop(); // en sondaki koord silinir
              activeRoutes[icao].setLatLngs(currentLatLngs); // listenin kalanı haritada
            }
          }
        } else { // İLERİ GİDİŞ
          if (step + 1 >= path.length) { // sonraki adımda rota bitiyorsa ya da sonsa
            isPaused.value = true;
            return;
          }
          const nextStep = step + 1; // sonraki adımın numarası 
          const point = path[nextStep];

          // Uçağın mevcut adım numarası ve anlık veri güncellendi
          animationSteps.value[icao] = nextStep;
          currentFlights.value[icao] = point;
          if (markers[icao]) {
            const newPos = [point.lat, point.lon]; // Yeni koordinatlar

            markers[icao].slideTo(newPos, { duration: 100 });
            // Uçağı yeni konumuna 100 milisaniye sürecek şekilde kaydırarak hareket ettirme

            markers[icao].setRotationAngle((point.heading || 0) - 80); // Uçağı gidiş yönüne göre döndürme
            if (activeRoutes[icao]) activeRoutes[icao].addLatLng(newPos);
          }
        }
      }
    }, 100); // saniyede 10 kez
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
    drawFullRoute(f.icao24); // Uçağın gideceği tüm yol (mavi ince çizgi)
    map.setView([f.lat, f.lon], 8, {
      animate: true, // akıcı gecis
      duration: 1 // odaklanma: 1 saniye
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
  z-index: 1000;
  transition: width 0.3s ease, min-width 0.3s ease, background 0.3s, color 0.3s;
  overflow: hidden;
}

.sidebar.left {
  border-right: 1px solid #5a2121;
}

.sidebar.right {
  border-left: 1px solid #ccc;
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
}

.sidebar.dark.left {
  border-right: 1px solid #333;
}

.sidebar.dark.right {
  border-left: 1px solid #333;
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

.close-button {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
  color: #888;
  transition: color 0.2s;
  line-height: 1;
}

.close-button:hover {
  color: #f44336;
}

.sidebar.dark .close-button {
  color: #aaa;
}

.sidebar.dark .close-button:hover {
  color: #ff5252;
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

.flight-list li:hover,
.flight-list li.active {
  background: #fec3a6;
}

.flight-list li.active {
  border-left: 4px solid #9381ff;
}

.sidebar.dark .flight-list li:hover,
.sidebar.dark .flight-list li.active {
  background: #0f3460;
}

.sidebar.dark .flight-list li.active {
  border-left-color: #9381ff;
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
  padding: 16px;
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
  margin-top: -15px;
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

.pause-button.paused:hover {
  background: #13c35d;
}

.locate-button {
  margin-top: -15px;
  padding: 12px;
  border-radius: 8px;
  border: 1px solid #9381ff;
  background: white;
  color: #9381ff;
  font-weight: bold;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.locate-button:hover {
  background: #f0eeff;
}

.locate-button.dark {
  background: #1a1a2e;
  color: #9381ff;
  border-color: #9381ff;
}

.locate-button.dark:hover {
  background: #2a2a4a;
}

.returnhome-button {
  margin-top: -15px;
  padding: 12px;
  border-radius: 8px;
  border: 1px solid #ff0000;
  background: white;
  color: #ff0000;
  font-weight: bold;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.returnhome-button:hover {
  background: #e81111;
  color: #ffffff;
}

.returnhome-button.dark {
  background: #ff0000;
  color: #ffffff;
  border-color: #ff0000;
}

.returnhome-button.dark:hover {
  background: #ff7070;
}

.airport-marker {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.airport-dot {
  width: 8px;
  height: 8px;
  background: #f1c40f;
  border: 2px solid #2c3e50;
  border-radius: 50%;
}

.airport-label {
  font-size: 10px;
  font-weight: bold;
  color: #2c3e50;
  text-shadow: 1px 1px 0px white;
  margin-top: 2px;
}
</style>