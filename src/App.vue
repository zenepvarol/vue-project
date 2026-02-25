<template>
  <div class="app-container">

    <div class="sidebar left" :class="{ dark: darkMode, collapsed: !sidebarOpen }">
      <div class="sidebar-header" :class="{ dark: darkMode }">
        <div class="header-top">
          <h3>Uçuş Listesi</h3>
          <button class="dark-toggle" @click="toggleDarkMode" :title="darkMode ? 'Aydınlık Mod' : 'Karanlık Mod'">
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
          <button class="close-button" @click="closeDetails" :title="'Kapat'">✕</button>
        </div>
      </div>

      <div class="flight-details" :class="{ dark: darkMode }">
        <div class="details-card" :class="{ dark: darkMode }">
          <div class="details-header">
            <span class="plane-icon-large">✈</span>
            <div class="header-text">
              <h2>{{ selectedFlight.callsign || 'Bilinmiyor' }}</h2>
              <span class="model-subtitle">{{ selectedFlight.modeltype || 'N/A' }}</span>
            </div>
            <button class="locate-mini-button" @click="recenterMap" title="Uçağa Odaklan">
              ➣
            </button>
          </div>
          <div class="details-grid">
            <div class="detail-item">
              <label>Model</label>
              <span>{{ selectedFlight.modeltype || 'N/A' }}</span>
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

          <button v-if="isPaused && !isEmergency && !isReturningToStart" class="pause-button start" @click="togglePause"
            :class="{ dark: darkMode }">
            Harekete Başla
          </button>

          <button class="returnhome-button" @click="returnToStart" :class="{ dark: darkMode }">
            Ana Merkeze Dön
          </button>

          <div v-if="nearestAirport && !isReturningToStart && !isPaused" class="emergency-section">
            <div class="nearest-airport-info">
              En Yakın Acil İniş: <strong>{{ nearestAirport.name }} ({{ nearestAirport.id }})</strong>
            </div>
            <button v-if="!isEmergency" class="emergency-button" @click="startEmergencyLanding">
              ACİL İNİŞ YAP
            </button>
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
const isPaused = ref(false);  // hareket halindeyken duraklatma icin
const sidebarOpen = ref(true);
const isReturning = ref(false);
const airports = ref([]); // Havalimanları icin 
const isEmergency = ref(false); // acil iniş için
const emergencyRoute = ref(null);
const isReturningToStart = ref(false); // Ana merkeze uçuş durumunu tutar

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
const returnToStart = () => {
  if (!activeIcao.value) return;

  isReturningToStart.value = true; // dönüş modu 
  isEmergency.value = false;       // acil iniş modu 
  isPaused.value = false;

  if (emergencyRoute.value) {
    map.removeLayer(emergencyRoute.value);
    emergencyRoute.value = null;
  }
};
// Seçili ucagin her hareketinde en yakın havalimanını hesaplama
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

    // rota bittiyse ya da ucak acil indiyse
    if ((path && step + 1 >= path.length) || isEmergency.value === false) {
      animationSteps.value[activeIcao.value] = 0;
      clearAllRoutes(); // mor, mavi, kırmızı silinir
      drawFullRoute(activeIcao.value);

      const firstPoint = path[0];
      if (markers[activeIcao.value]) {
        markers[activeIcao.value].setLatLng([firstPoint.lat, firstPoint.lon]);
        currentFlights.value[activeIcao.value] = firstPoint;
      }
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

  staticRoutes[icao] = L.polyline(allPoints, { color: '#0077b6', weight: 2, opacity: 0.5 }).addTo(map); // MAvi

  const pointsSoFar = allPoints.slice(0, currentStep + 1);
  activeRoutes[icao] = L.polyline(pointsSoFar, { color: '#9381ff', weight: 4, opacity: 1 }).addTo(map); //Mor

  const startCircle = L.circleMarker(allPoints[0], { radius: 6, color: '#2ecc71', fillOpacity: 1, weight: 2 }); // Yeşil
  const endCircle = L.circleMarker(allPoints[allPoints.length - 1], { radius: 6, color: '#e74c3c', fillOpacity: 1, weight: 2 }); // Krımızı

  startCircle.addTo(map);
  endCircle.addTo(map);
  terminalMarkers[icao] = [startCircle, endCircle];
};

const startEmergencyLanding = () => {
  if (selectedFlight.value && nearestAirport.value) {
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
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180)); // Dünya sınırları

  // harita: sınırlar sabit, zoom: 2 
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap',
    noWrap: true // haritanin sonsuz tekrarlaması
  }).addTo(map);

  try {
    const airResponse = await fetch('/airports_library_v6.json'); // Havalimanı verileri
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
            drawFullRoute(icao);
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

        // ACİL İNİŞ
        if (isEmergency.value && nearestAirport.value) {
          const plane = currentFlights.value[icao];
          const target = nearestAirport.value;

          const dx = target.lat - plane.lat;
          const dy = target.lon - plane.lon;
          const dist = Math.sqrt(dx * dx + dy * dy);
          const moveStep = 0.035;

          if (emergencyRoute.value) {
            emergencyRoute.value.setLatLngs([[plane.lat, plane.lon], [target.lat, target.lon]]); // kırmzı kesik cizgi
          } else {
            emergencyRoute.value = L.polyline([[plane.lat, plane.lon], [target.lat, target.lon]], {
              color: 'red', weight: 3, dashArray: '10, 10', opacity: 0.7
            }).addTo(map);
          }

          if (dist > moveStep) {
            const newLat = plane.lat + (dx / dist) * moveStep;
            const newLon = plane.lon + (dy / dist) * moveStep;
            const nextPos = [newLat, newLon];

            currentFlights.value[icao].lat = newLat;
            currentFlights.value[icao].lon = newLon;

            if (markers[icao]) {
              markers[icao].slideTo(nextPos, { duration: 100 });
              const angle = Math.atan2(dy, dx) * (180 / Math.PI);
              markers[icao].setRotationAngle(angle - 80);
            }
          } else {
            isPaused.value = true;
            isEmergency.value = false;

            if (emergencyRoute.value) {
              map.removeLayer(emergencyRoute.value);
              emergencyRoute.value = null;
            }
            if (activeRoutes[icao]) {
              map.removeLayer(activeRoutes[icao]);
              delete activeRoutes[icao];
            }
          }
        }

        // ANA MERKEZE DÖNÜŞ
        else if (isReturningToStart.value) {
          const plane = currentFlights.value[icao];
          const target = path[0];

          const dx = target.lat - plane.lat;
          const dy = target.lon - plane.lon;
          const dist = Math.sqrt(dx * dx + dy * dy);
          const angle = Math.atan2(dy, dx) * (180 / Math.PI);
          const moveStep = 0.03;

          if (emergencyRoute.value) {
            emergencyRoute.value.setLatLngs([[plane.lat, plane.lon], [target.lat, target.lon]]); // kırmzı kesik cizgi
          } else {
            emergencyRoute.value = L.polyline([[plane.lat, plane.lon], [target.lat, target.lon]], {
              color: 'red', weight: 3, dashArray: '10, 10', opacity: 0.7
            }).addTo(map);
          }

          if (dist > moveStep) {
            const newLat = plane.lat + (dx / dist) * moveStep;
            const newLon = plane.lon + (dy / dist) * moveStep;
            const nextPos = [newLat, newLon];

            currentFlights.value[icao].lat = newLat;
            currentFlights.value[icao].lon = newLon;

            if (markers[icao]) {
              markers[icao].slideTo(nextPos, { duration: 100 });
              markers[icao].setRotationAngle(angle - 80);
              if (activeRoutes[icao]) activeRoutes[icao].addLatLng(nextPos);
            }
          } else {
            isReturningToStart.value = false;
            isPaused.value = true;
            animationSteps.value[icao] = 0;

            if (emergencyRoute.value) {
              map.removeLayer(emergencyRoute.value);
              emergencyRoute.value = null;
            }

            clearAllRoutes();
            drawFullRoute(icao);

            currentFlights.value[icao] = { ...path[0] };
            if (markers[icao]) {
              markers[icao].setLatLng([path[0].lat, path[0].lon]);
            }
          }
        }
        else if (isReturning.value) { // geri
          if (step <= 0) {
            isReturning.value = false;
            isPaused.value = true; return;
          }
          const prevStep = step - 1;
          const point = path[prevStep];
          animationSteps.value[icao] = prevStep;
          currentFlights.value[icao] = point;

          if (markers[icao]) {
            markers[icao].slideTo([point.lat, point.lon], { duration: 100 });
            markers[icao].setRotationAngle((point.heading || 0) + 100);
            if (activeRoutes[icao]) {
              const currentLatLngs = activeRoutes[icao].getLatLngs();
              currentLatLngs.pop();
              activeRoutes[icao].setLatLngs(currentLatLngs);
            }
          }
        }
        else { //ileri
          if (step + 1 >= path.length) {
            isPaused.value = true;
            return;
          }
          const nextStep = step + 1;
          const point = path[nextStep];

          animationSteps.value[icao] = nextStep;
          currentFlights.value[icao] = point;
          if (markers[icao]) {
            const newPos = [point.lat, point.lon];
            markers[icao].slideTo(newPos, { duration: 100 });
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

