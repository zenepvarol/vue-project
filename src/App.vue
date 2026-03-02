<template>
  <div class="app-container" :class="{ 'dark-mode-active': darkMode }">

    <div class="sidebar left" :class="{ collapsed: !sidebarOpen }">
      <div class="sidebar-header">
        <div class="header-top">
          <h3>Uçuş Listesi</h3>
          <button class="dark-toggle" @click="toggleDarkMode" :title="darkMode ? 'Aydınlık Mod' : 'Karanlık Mod'">
            <Sun v-if="darkMode" :size="20" />
            <Moon v-else :size="20" />
          </button>
        </div>
        <div class="search-container">
          <Search class="search-icon" :size="16" />
          <input v-model="searchQuery" placeholder="Uçuş (Callsign) ara..." class="search-input" />
        </div>
      </div>

      <ul class="flight-list">
        <li v-for="f in filteredFlights" :key="f.icao24" @click="focusFlight(f)"
          :class="{ active: activeIcao === f.icao24 }">
          <div class="flight-info">
            <span class="callsign">{{ f.callsign || 'Bilinmiyor' }}</span>
          </div>
          <div class="arrow" :style="{ transform: `rotate(${(f.heading || 0) - 80}deg)` }">
            <Plane :size="18" />
          </div>
        </li>
      </ul>
    </div>

    <button class="sidebar-toggle" :class="{ open: sidebarOpen }" @click.stop="toggleSidebar">
      <ChevronLeft v-if="sidebarOpen" />
      <ChevronRight v-else />
    </button>

    <div id="map"></div>

    <div v-if="selectedFlight" class="sidebar right">
      <div class="sidebar-header">
        <div class="header-top">
          <h3>Uçuş Detayları</h3>
          <button class="close-button" @click="activeIcao = null" title="Kapat">
            <X :size="20" />
          </button>
        </div>
      </div>

      <div class="flight-details">
        <div class="details-card">
          <div class="details-header">
            <div class="plane-icon-wrapper">
              <Plane :size="32" class="plane-icon-large" />
            </div>
            <div class="header-text">
              <h2>{{ selectedFlight.callsign || 'Bilinmiyor' }}</h2>
              <span class="model-subtitle">{{ selectedFlight.modeltype || 'İnsansız Hava Aracı' }}</span>
            </div>
            <button class="locate-mini-button" @click="recenterMap" title="Uçağa Odaklan">
              <Navigation :size="18" />
            </button>
          </div>

          <div class="details-grid">
            <div class="detail-item full-width">
              <label>
                <Activity :size="14" /> Durum
              </label>
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
                <label>
                  <Gauge :size="14" /> Hız
                </label>
                <span>{{ Math.round(selectedFlight.velocity) }} kt</span>
              </div>
              <div class="detail-item">
                <label>
                  <Mountain :size="14" /> Rakım
                </label>
                <span>{{ Math.round(selectedFlight.baroaltitude) }} ft</span>
              </div>
            </div>

            <div class="detail-item">
              <label>
                <MapPin :size="14" /> Mesafe (Gidilen / Toplam)
              </label>
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
              class="returnhome-button" @click="returnToStart">
              <RotateCcw :size="16" /> ANA MERKEZE DÖN
            </button>

            <button v-if="isPaused && animationSteps[activeIcao] === 0" class="pause-button paused"
              @click="togglePause">
              <Play :size="16" /> KALKIŞ ONAYI VER
            </button>

            <div v-if="!isPaused && !isReturningToStart && !isEmergency && nearestAirport" class="emergency-section">
              <div class="nearest-airport-info">
                <MapPin :size="14" /> En Yakın Güvenli Nokta: <strong>{{ nearestAirport.name }}</strong>
              </div>
              <button class="emergency-button" @click="startEmergencyLanding">
                <AlertOctagon :size="16" /> ACİL İNİŞ YAP
              </button>
            </div>
          </div>

          <div v-if="isEmergency" class="emergency-status-note" style="color: #e74c3c;">
            <AlertCircle :size="14" /> Acil iniş noktasına gidiliyor.
          </div>
          <div v-if="isReturningToStart" class="emergency-status-note" style="color: #3498db;">
            <Info :size="14" /> Ana merkeze dönüş yapılıyor.
          </div>

        </div>
      </div>
    </div>
  </div>
</template>
<script setup>
import {
  Plane, Gauge, Mountain, MapPin, Navigation,
  AlertOctagon, AlertCircle, Play, RotateCcw,
  Search, Sun, Moon, X, ChevronLeft, ChevronRight,
  Activity, Info
} from 'lucide-vue-next';

import { onMounted, ref, computed } from 'vue';
import './assets/flight-style.css';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-rotatedmarker';
import 'leaflet.marker.slideto';

// --- REAKTİF DEĞİŞKENLER ---
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

const staticRoutes = {};
const activeRoutes = {};
const terminalMarkers = {};
let map = null;
const routeLayer = L.layerGroup();


const getDistance = (p1, p2) => {
  return Math.sqrt(Math.pow(p1.lat - p2.lat, 2) + Math.pow(p1.lon - p2.lon, 2));
};

// Ortak Hareket Fonksiyonu
const movePlane = (icao, targetLat, targetLon, moveStep = 0) => {
  const plane = currentFlights.value[icao];
  const marker = markers[icao];
  if (!plane || !marker) return false;

  let nextLat = targetLat;
  let nextLon = targetLon;
  let heading = plane.heading || 0;

  if (moveStep > 0) {
    const dx = targetLat - plane.lat;
    const dy = targetLon - plane.lon;
    const dist = getDistance({ lat: plane.lat, lon: plane.lon }, { lat: targetLat, lon: targetLon });

    if (dist > moveStep) {
      nextLat = plane.lat + (dx / dist) * moveStep;
      nextLon = plane.lon + (dy / dist) * moveStep;
      heading = (Math.atan2(dy, dx) * (180 / Math.PI));
    } else {
      return true; // Hedefe ulaşıldı
    }
  }

  // Veri Güncellenir
  plane.lat = nextLat;
  plane.lon = nextLon;

  const newPos = [nextLat, nextLon];
  if (moveStep === 0) {
    marker.slideTo(newPos, { duration: 100 });
  } else {
    marker.setLatLng(newPos);
  }

  marker.setRotationAngle(heading - 80);
  if (activeRoutes[icao]) activeRoutes[icao].addLatLng(newPos);

  return false; // Henüz hedefe varılmadı
};

const resetActivePath = (icao) => {
  if (activeRoutes[icao]) {
    routeLayer.removeLayer(activeRoutes[icao]);
    const currentPos = [currentFlights.value[icao].lat, currentFlights.value[icao].lon];
    activeRoutes[icao] = L.polyline([currentPos], { color: '#9381ff', weight: 4, opacity: 1 }).addTo(routeLayer);
  }
};

const getNearestAirport = (planeLat, planeLon) => {
  if (!airports.value.length) return null;
  const planePos = { lat: planeLat, lon: planeLon };
  return airports.value.reduce((nearest, airport) => {
    return getDistance(planePos, airport) < getDistance(planePos, nearest) ? airport : nearest;
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

const zoomToPlane = (lat, lon) => {
  if (lat && lon && map) {
    map.setView([lat, lon], 8, { animate: true, duration: 1 });
  }
};

const recenterMap = () => {
  if (selectedFlight.value) {
    zoomToPlane(selectedFlight.value.lat, selectedFlight.value.lon);
  }
};

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

const clearAllRoutes = () => {
  routeLayer.clearLayers();
  if (emergencyRoute.value) {
    map.removeLayer(emergencyRoute.value);
    emergencyRoute.value = null;
  }
};

const drawFullRoute = (icao) => {
  clearAllRoutes();
  const path = flightPaths.value[icao];
  const allPoints = path.map(p => [p.lat, p.lon]);
  const currentStep = animationSteps.value[icao];

  const staticPath = L.polyline(allPoints, { color: '#0077b6', weight: 2, opacity: 0.5 });
  const pointsSoFar = allPoints.slice(0, currentStep + 1);
  activeRoutes[icao] = L.polyline(pointsSoFar, { color: '#9381ff', weight: 4, opacity: 1 });

  //Başlangıç ve Bitiş Noktaları
  const startCircle = L.circleMarker(allPoints[0], { radius: 6, color: '#2ecc71', fillOpacity: 1, weight: 2 });
  const endCircle = L.circleMarker(allPoints[allPoints.length - 1], { radius: 6, color: '#e74c3c', fillOpacity: 1, weight: 2 });

  staticPath.addTo(routeLayer);
  activeRoutes[icao].addTo(routeLayer);
  startCircle.addTo(routeLayer);
  endCircle.addTo(routeLayer);
  routeLayer.addTo(map);
};

const startEmergencyLanding = () => {
  if (selectedFlight.value && nearestAirport.value) {
    const icao = activeIcao.value;
    resetActivePath(icao);
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

const nearestAirport = computed(() => {
  if (selectedFlight.value && airports.value.length > 0) {
    return getNearestAirport(selectedFlight.value.lat, selectedFlight.value.lon);
  }
  return null;
});

const filteredFlights = computed(() => {
  const query = searchQuery.value.toLowerCase();
  return Object.values(currentFlights.value).filter(f =>
    f.callsign?.toString().toLowerCase().includes(query)
  );
});

const selectedFlight = computed(() => activeIcao.value ? currentFlights.value[activeIcao.value] : null);

onMounted(async () => {
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180));
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);
  routeLayer.addTo(map);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap',
    noWrap: true
  }).addTo(map);

  try {
    const airResponse = await fetch('/airports_library_v6.json');
    airports.value = await airResponse.json();

    airports.value.forEach(ap => {
      const airportIcon = L.divIcon({
        html: `
<div class="airport-marker">
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="#2ecc71" stroke="#ffffff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="3"/>
        </svg>
        <span class="airport-label">${ap.id}</span>
      </div>`,
        className: 'custom-airport',
        iconSize: [40, 40]
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
      html: `
    <div class="moving-plane">
      <svg xmlns="http://www.w3.org/2000/svg" width="36" height="36" viewBox="0 0 24 24" fill="#9381ff" stroke="#ffffff" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
        <path d="M17.8 19.2 16 11l3.5-3.5C21 6 21.5 4 21 3c-1-.5-3 0-4.5 1.5L13 8 4.8 6.2c-.5-.1-.9.1-1.1.5l-.3.5c-.2.5-.1 1 .3 1.3L9 12l-2 3H4l-1 1 3 2 2 3 1-1v-3l3-2 3.5 5.3c.3.4.8.5 1.3.3l.5-.2c.4-.3.6-.7.5-1.2z"/>
      </svg>
    </div>`,
      className: 'plane-icon',
      iconSize: [40, 40],
      iconAnchor: [20, 20]
    });

    Object.keys(grouped).forEach(icao => {
      const firstPoint = grouped[icao][0];
      currentFlights.value[icao] = firstPoint;

      currentFlights.value[icao] = {
        ...firstPoint,
        velocity: 0,
        baroaltitude: 0
      };

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
      const icao = activeIcao.value;
      if (!icao || isPaused.value || !flightPaths.value[icao]) return;

      const path = flightPaths.value[icao];
      const plane = currentFlights.value[icao];

      // ACİL İNİŞ (En yakın havaalanına)
      if (isEmergency.value && nearestAirport.value) {
        const arrived = movePlane(icao, nearestAirport.value.lat, nearestAirport.value.lon, 0.035);
        plane.baroaltitude = Math.max(0, plane.baroaltitude - 50); // İrtifa ve hız kademeli azalır
        plane.velocity = Math.max(0, plane.velocity - 4);
        if (arrived) {
          isPaused.value = true;
          isEmergency.value = false;
          if (emergencyRoute.value) { map.removeLayer(emergencyRoute.value); emergencyRoute.value = null; }
        }
      }

      // ANA MERKEZE DÖNÜŞ 
      else if (isReturningToStart.value) {
        const arrived = movePlane(icao, path[0].lat, path[0].lon, 0.03);
        plane.baroaltitude = Math.max(0, plane.baroaltitude - 40);
        plane.velocity = Math.max(0, plane.velocity - 3);
        if (arrived) {
          isReturningToStart.value = false;
          isPaused.value = true;
          animationSteps.value[icao] = 0;
          drawFullRoute(icao);
        }
      }
      // NORMAL İLERLEME 
      else {
        const step = animationSteps.value[icao];
        if (step + 1 >= path.length) {
          isPaused.value = true;
          return;
        }
        const nextStep = step + 1;
        const nextPoint = path[nextStep];
        const plane = currentFlights.value[icao];

        // Hedef değerler JSON'dan
        const targetVel = nextPoint.velocity || 250;
        const targetAlt = nextPoint.baroaltitude || 5000;

        // Değerler kademeli artır/azalt 
        if (plane.velocity < targetVel) plane.velocity = Math.min(targetVel, plane.velocity + 5);
        else if (plane.velocity > targetVel) plane.velocity = Math.max(targetVel, plane.velocity - 5);

        if (plane.baroaltitude < targetAlt) plane.baroaltitude = Math.min(targetAlt, plane.baroaltitude + 100);
        else if (plane.baroaltitude > targetAlt) plane.baroaltitude = Math.max(targetAlt, plane.baroaltitude - 100);

        plane.lat = nextPoint.lat;
        plane.lon = nextPoint.lon;
        plane.heading = nextPoint.heading;
        plane.distance_from_dep = nextPoint.distance_from_dep;
        plane.trip_distance = nextPoint.trip_distance;

        const currentPos = markers[icao].getLatLng();
        const distToNext = getDistance(currentPos, { lat: nextPoint.lat, lon: nextPoint.lon });

        animationSteps.value[icao] = nextStep;
        movePlane(icao, nextPoint.lat, nextPoint.lon, distToNext > 0.5 ? 0.01 : 0);
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
      activeIcao.value = f.icao24;
      isPaused.value = true;
      drawFullRoute(f.icao24);
    }
    zoomToPlane(f.lat, f.lon);
  }
};
</script>