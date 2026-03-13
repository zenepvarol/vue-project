<template>
  <div class="app-container" :class="{ 'dark-mode-active': darkMode }">
    <div class="sidebar left" :class="{ collapsed: !sidebarOpen }">
      <div class="sidebar-header">
        <div class="header-top">
          <h3>Uçuş Listesi</h3>
          <button class="dark-toggle" @click="toggleDarkMode" :title="darkMode ? 'Aydınlık Mod' : 'Karanlık Mod'">
            <Sun v-if="darkMode" :size="20" color="#f1c40f" />
            <Moon v-else :size="20" />
          </button>
        </div>
        <div class="search-container">
          <input v-model="searchQuery" placeholder="Uçuş (Callsign) ara: " class="search-input" />
        </div>
      </div>

      <div class="fleet-status-section" style="padding: 10px; border-top: none;">
        <div class="fleet-grid">
          <div v-for="f in filteredFlights" :key="f.icao24" class="fleet-mini-card"
            :class="{ 'is-active': activeIcao === f.icao24 }" @click="focusFlight(f)">
            <div class="mini-card-header">
              <span class="mini-callsign">{{ f.callsign || 'İHA' }}</span>
              <span class="mini-status-dot" :style="{ backgroundColor: f.energy < 20 ? '#e74c3c' : '#2ecc71' }"></span>
            </div>
            <div class="mini-stats">
              <span>
                <Gauge :size="12" /> {{ Math.round(f.velocity) }} kt
              </span>
              <span>
                <Mountain :size="12" /> {{ Math.round(f.baroaltitude) }} ft
              </span>
            </div>
            <div class="mini-energy-bar">
              <div class="mini-energy-fill"
                :style="{ width: f.energy + '%', backgroundColor: f.energy < 20 ? '#e74c3c' : '#2ecc71' }"></div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <button class="sidebar-toggle" :class="{ open: sidebarOpen }" @click.stop="toggleSidebar">
      <ChevronLeft v-if="sidebarOpen" />
      <ChevronRight v-else />
    </button>

    <div id="map"></div>
    <div class="sidebar right">
      <div v-if="!selectedFlight" class="flight-details">
        <div class="sidebar-header" style="padding: 0 0 15px 0;">
          <h3>Görev Kontrol Merkezi</h3>
        </div>
        <div class="details-card active-focus">
          <div class="manual-target-input" style="border-top: none; padding-top: 0;">
            <h4>Yeni Görev Atama</h4>
            <p style="font-size: 11px; opacity: 0.7; margin-bottom: 15px;">Hangardaki İHA'lar için rota belirleyin.</p>

            <label style="font-size: 11px; font-weight: bold; margin-bottom: 5px; display: block;">KALKIŞ
              MERKEZİ</label>
            <input v-model="departureAirportId" type="text" placeholder="Örn: LTBI" class="full-width-input"
              list="airport-list" style="margin-bottom: 15px;">

            <label style="font-size: 11px; font-weight: bold; margin-bottom: 5px; display: block;">VARILACAK
              HEDEF</label>
            <input v-model="destinationAirportId" type="text" placeholder="Örn: LTFO" class="full-width-input"
              list="airport-list">

            <datalist id="airport-list">
              <option v-for="ap in airports" :key="ap.id" :value="ap.id">{{ ap.name }} ({{ ap.city }})</option>
            </datalist>

            <button class="apply-target-btn" @click="assignMission"
              style="margin-top: 20px; background: #2ecc71; width: 100%; border: none; font-weight: bold;">
              UYGUN İHA'YI GÖREVE GÖNDER
            </button>
          </div>
        </div>
      </div>

      <div v-else class="flight-details">
        <div class="sidebar-header">
          <div class="header-top">
            <h3>Uçuş Detayları</h3>
            <button class="close-button" @click="activeIcao = null" title="Kapat">
              <X :size="20" />
            </button>
          </div>
        </div>

        <div class="details-card active-focus">
          <div class="details-header">
            <div class="header-text">
              <h2>{{ selectedFlight.callsign || 'Bilinmiyor' }}</h2>
              <span class="model-subtitle">{{ selectedFlight.modeltype || 'İnsansız Hava Aracı' }}</span>
            </div>
            <button class="locate-mini-button" @click="recenterMap" title="Uçağa Odaklan">
              <Navigation :size="18" />
            </button>
          </div>

          <div class="details-grid"
            :class="{ 'link-loss-blur': activeFailure?.label.includes('SİNYAL') && isEmergencySimulated }">
            <div class="detail-item full-width">
              <label>
                <Activity :size="14" /> Durum
              </label>
              <span
                :style="{ color: isEmergency ? '#e74c3c' : (isReturningToStart ? '#3498db' : (isPaused ? '#f39c12' : '#2ecc71')), fontWeight: 'bold' }">
                {{ isEmergency ? 'ACİL İNİŞTE' : (isReturningToStart ? 'ANA MERKEZE DÖNÜLÜYOR' : (isPaused ? 'DURDURULDU / BEKLEMEDE' : 'GÖREVDE')) }}
              </span>
            </div>

            <div class="detail-item full-width">
              <label>
                <Battery :size="14" /> YAKIT (%{{ Math.round(selectedFlight.energy) }})
              </label>
              <div class="progress-bar energy-bar">
                <div class="progress-fill"
                  :style="{ width: selectedFlight.energy + '%', backgroundColor: selectedFlight.energy < 20 ? '#e74c3c' : '#2ecc71' }">
                </div>
              </div>
            </div>

            <div class="details-row-inline">
              <div class="detail-item"><label>
                  <Gauge :size="14" /> Hız
                </label><span>{{ Math.round(selectedFlight.velocity) }} kt</span></div>
              <div class="detail-item"><label>
                  <Mountain :size="14" /> Rakım
                </label><span>{{ Math.round(selectedFlight.baroaltitude) }} ft</span></div>
            </div>

            <div class="detail-item">
              <label>
                <MapPin :size="14" /> Mesafe (Gidilen / Toplam)
              </label>
              <span>{{ Math.round(selectedFlight.distance_from_dep) }} / {{ Math.round(selectedFlight.trip_distance) }}
                km</span>
            </div>

            <div v-if="selectedFlight.trip_distance > 0" class="detail-item progress-container full-width">
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
            <button
              v-if="animationSteps[activeIcao] > 0 && !isReturningToStart && !isEmergency && !isEmergencySimulated"
              class="returnhome-button" @click="returnToStart">
              <RotateCcw :size="16" /> ANA MERKEZE DÖN
            </button>
            <button v-if="isPaused && animationSteps[activeIcao] === 0" class="pause-button paused"
              @click="togglePause">
              <Play :size="16" /> KALKIŞ ONAYI VER
            </button>
            <button v-if="!isPaused && !isEmergencySimulated && !isEmergency && !isReturningToStart"
              class="simulate-btn" @click="triggerSimulatedFailure">
              <AlertTriangle :size="16" /> ARIZA SİMÜLE ET
            </button>

            <div v-if="isEmergencySimulated" class="emergency-decision-box">
              <div class="emergency-warning-text" :style="{ color: activeFailure?.color || '#e74c3c' }">
                <AlertOctagon :size="18" class="pulse-icon" />
                {{ activeFailure?.label || 'SİSTEM ARIZASI!' }} ({{ emergencyCountdown }}s)
              </div>
              <button class="emergency-button" @click="handleManualEmergency">ACİL İNİŞ YAP</button>
            </div>
          </div>

          <div class="manual-target-input">
            <h4>Manuel Hedef Belirleme</h4>
            <input v-model="manualAirportId" type="text" placeholder="Havalimanı Seçin veya Yazın"
              class="full-width-input" list="airport-list-manual">
            <datalist id="airport-list-manual">
              <option value="MANUAL_COORD"> Manuel Koordinat Girişi</option>
              <option v-for="ap in airports" :key="ap.id" :value="ap.id">{{ ap.name }} ({{ ap.id }})</option>
            </datalist>

            <div v-if="manualAirportId === 'MANUAL_COORD'" class="input-row"
              style="margin-top: 10px; display: flex; gap: 5px;">
              <input v-model="manualLat" type="number" placeholder="Enlem (Lat)" class="coord-input" step="0.000001"
                style="flex: 1; padding: 8px; font-size: 12px; border-radius: 4px; border: 1px solid #ddd;">
              <input v-model="manualLon" type="number" placeholder="Boylam (Lon)" class="coord-input" step="0.000001"
                style="flex: 1; padding: 8px; font-size: 12px; border-radius: 4px; border: 1px solid #ddd;">
           </div>
            <button class="apply-target-btn" @click="setManualTarget" style="margin-top: 10px; width: 100%;">HEDEFE
              YÖNLENDİR </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
<script setup>
import {
  Plane, Gauge, Mountain, MapPin, Navigation, AlertOctagon, AlertCircle, Play, RotateCcw,
  Sun, Moon, X, ChevronLeft, ChevronRight, Activity, Info, AlertTriangle
} from 'lucide-vue-next';

import { onMounted, ref, computed } from 'vue';
import './assets/flight-style.css';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-rotatedmarker'; // marker döndürme
import 'leaflet.marker.slideto'; // akıcı geçiş (animation)
import Swal from 'sweetalert2';

//  REAKTİF DEĞİŞKENLER 
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
const activeRoutes = {};
let map = null;
const routeLayer = L.layerGroup();
const isEmergencySimulated = ref(false);
const emergencyCountdown = ref(10); // ARIZA SAYACI 10 sn simdilik
const manualTarget = ref(null);
const isManualRouting = ref(false);
const manualLat = ref(null);
const manualLon = ref(null);
const manualAirportId = ref(''); // havalimanı kodu ile arama icin
const departureAirportId = ref('');
const destinationAirportId = ref('');
const missionPathLayer = ref(null);
const myFleetIcaos = ["9005", "9501", "9802"];

const getDistance = (p1, p2) => {
  const R = 6371; // dunyanin yaricapi - km
  const dLat = (p2.lat - p1.lat) * Math.PI / 180; // iki nokta arasi enlem ve boylam farki dereceden radyana
  const dLon = (p2.lon - p1.lon) * Math.PI / 180;

  const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(p1.lat * Math.PI / 180) * Math.cos(p2.lat * Math.PI / 180) * Math.sin(dLon / 2) * Math.sin(dLon / 2); // Haversine formülü
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));   // iki nokta arası merkez açı
  return R * c; // Sonuç km
};

const movePlane = (icao, targetLat, targetLon, moveStep = 0) => { // Ortak Hareket Fonksiyonu
  const plane = currentFlights.value[icao];
  const marker = markers[icao];
  if (!plane || !marker) return false;

  let nextLat = targetLat;
  let nextLon = targetLon;
  let heading = plane.heading || 0;

  if (moveStep > 0) { // hedefle aradaki dikey (dx) ve yatay (dy) fark hesabı
    const dx = targetLat - plane.lat;
    const dy = targetLon - plane.lon;

    // Mevcut konumla hedef arasındaki mesafe
    const dist = getDistance({ lat: plane.lat, lon: plane.lon }, { lat: targetLat, lon: targetLon });

    if (dist > moveStep) {
      nextLat = plane.lat + (dx / dist) * moveStep; // ucak hedefe moveStep kadar kayar
      nextLon = plane.lon + (dy / dist) * moveStep;
      heading = (Math.atan2(dy, dx) * (180 / Math.PI));  // aci hesaplama
    } else {
      return true; // varış
    }
  }

  plane.lat = nextLat; // ucagin yeni enlemi
  plane.lon = nextLon; // boylmaı

  const newPos = [nextLat, nextLon];
  if (moveStep === 0) { // normal ucusta
    marker.slideTo(newPos, { duration: 100 });
  } else {
    marker.setLatLng(newPos);
  }

  marker.setRotationAngle(heading - 45); // -45 derece iconun tasarımı geregi
  if (activeRoutes[icao]) activeRoutes[icao].addLatLng(newPos);
  return false; // Henüz hedefe varılmadı hareket devaö ediyor
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
  // Havalimanı listesini karşılaştırarak en yakın olanı seçer
  return airports.value.reduce((nearest, airport) => {
    return getDistance(planePos, airport) < getDistance(planePos, nearest) ? airport : nearest;
  });
};

const returnToStart = () => {
  if (!activeIcao.value) return; // aktif uçuş seçili değilse işlem sonlanır
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
  const staticPath = L.polyline(allPoints, { color: '#0077b6', weight: 2, opacity: 0.5 }); // gitmesi gereken saydam mavi yol
  const pointsSoFar = allPoints.slice(0, currentStep + 1);
  activeRoutes[icao] = L.polyline(pointsSoFar, { color: '#9381ff', weight: 4, opacity: 1 });
  const startCircle = L.circleMarker(allPoints[0], { radius: 6, color: '#2ecc71', fillOpacity: 1, weight: 2 }); // baslangic noktasi
  const endCircle = L.circleMarker(allPoints[allPoints.length - 1], { radius: 6, color: '#e74c3c', fillOpacity: 1, weight: 2 }); // bitis noktasi

  staticPath.addTo(routeLayer);
  activeRoutes[icao].addTo(routeLayer);
  startCircle.addTo(routeLayer);
  endCircle.addTo(routeLayer);
  routeLayer.addTo(map);
};

const setManualTarget = () => {
  if (!activeIcao.value) return;
  let target = null;

  const targetAp = airports.value.find(ap => ap.id.toLowerCase() === manualAirportId.value.toLowerCase());

  if (manualAirportId.value === 'MANUAL_COORD' && manualLat.value && manualLon.value) {
    target = { lat: parseFloat(manualLat.value), lon: parseFloat(manualLon.value) };
  } else if (targetAp) {
    target = { lat: targetAp.lat, lon: targetAp.lon };
  }

  if (target) {
    const plane = currentFlights.value[activeIcao.value];
    const dist = getDistance({ lat: plane.lat, lon: plane.lon }, target);

    plane.trip_distance = dist;
    plane.distance_from_dep = 0;
    plane.total_manual_dist = dist;
    manualTarget.value = target;
    isManualRouting.value = true;
    isPaused.value = false;

    if (emergencyRoute.value) map.removeLayer(emergencyRoute.value);
    emergencyRoute.value = L.polyline([[plane.lat, plane.lon], [target.lat, target.lon]], {
      color: '#2ecc71', dashArray: '5, 10', weight: 3
    }).addTo(map);

    Swal.fire({ icon: 'success', title: 'Rota Hazır', text: `Mesafe: ${Math.round(dist)} km`, timer: 1500 });
  }
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
      dashArray: '10, 10', // Kesikli çizgi efekti
      opacity: 0.8
    }).addTo(map);
    map.fitBounds(L.latLngBounds([start, end]), { padding: [50, 50], maxZoom: 8 }); // Harita uçak ve iniş noktasını içine alacak şekilde odaklanır
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
  // TileLayer: Harita katmanı (OpenStreetMap)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap',
    noWrap: true
  }).addTo(map);

  try {
    const airResponse = await fetch('/airports_library_v6.json');
    airports.value = await airResponse.json();

    airports.value.forEach(ap => {
      const airportIcon = L.divIcon({ // SVG tabanlı özel icon tanımlama
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

    const planeIcon = L.divIcon({ // Uçak ikonu tasarımı (SVG)
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
      const firstPoint = grouped[icao][0]; // Her ucak başlangıç konumunda olusur
      const isEnvanter = myFleetIcaos.includes(icao.toString());
      currentFlights.value[icao] = { // Başlangıç değerleri
        ...firstPoint,
        velocity: 0,
        baroaltitude: 0,
        energy: 100,
        status: isEnvanter ? 'STANDBY' : 'ON_MISSION'
      };

      animationSteps.value[icao] = 0;

      if (firstPoint.lat && firstPoint.lon) {
        const marker = L.marker([firstPoint.lat, firstPoint.lon], {
          icon: planeIcon,
          rotationAngle: (firstPoint.heading || 0) - 45
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
      if (!icao || isPaused.value || !currentFlights.value[icao]) return;

      const plane = currentFlights.value[icao];
      const path = flightPaths.value[icao];
      const currentPos = { lat: plane.lat, lon: plane.lon };

      // Enerji Tüketimi
      if (plane.energy > 0 && plane.velocity > 0) {
        plane.energy = Math.max(0, plane.energy - 0.01);
      }

      // Kritik Enerji Kontrolü
      if (plane.energy < 20 && !isEmergencySimulated.value && !isEmergency.value && !isReturningToStart.value) {
        triggerSimulatedFailure();
      }

      // ENVANTER GÖREV MANTIĞI 9005 9501 9802 ŞİMDİLİK
      if (plane.status === 'GOING_TO_DEP' || plane.status === 'GOING_TO_DEST') {
        const targetPos = plane.status === 'GOING_TO_DEP' ? plane.missionDep : plane.missionDest;
        const distToTarget = getDistance(currentPos, targetPos);
        const stepSize = Math.max(0.1, plane.velocity / 1500);
        const oldPos = { lat: plane.lat, lon: plane.lon };
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
        plane.distance_from_dep += getDistance(oldPos, { lat: plane.lat, lon: plane.lon });
        if (missionPathLayer.value) missionPathLayer.value.addLatLng([plane.lat, plane.lon]);
        map.panTo([plane.lat, plane.lon]);

        // Tırmanış ve Alçalış
        const cruiseAlt = 10000;
        const cruiseSpeed = 220;

        if (distToTarget > 20) {
          if (plane.velocity < cruiseSpeed) plane.velocity += 0.8;
          if (plane.baroaltitude < cruiseAlt) plane.baroaltitude += 15;
        }
        else {
          const ratio = Math.max(0.01, distToTarget / 20);
          plane.velocity -= (plane.velocity - (cruiseSpeed * ratio)) * 0.05;
          plane.baroaltitude -= (plane.baroaltitude - (cruiseAlt * ratio)) * 0.05;
        }

        if (arrived || distToTarget < 0.1) {
          if (plane.status === 'GOING_TO_DEP') {
            plane.status = 'GOING_TO_DEST';
            if (missionPathLayer.value) missionPathLayer.value.setStyle({ color: '#2ecc71', dashArray: null });
          } else {
            plane.velocity = 0; plane.baroaltitude = 0;
            plane.distance_from_dep = plane.trip_distance;
            plane.status = 'STANDBY';
            isPaused.value = true;
          }
        }
      }

      // ACİL İNİŞ
      else if (isEmergency.value && nearestAirport.value) {
        const targetPos = { lat: nearestAirport.value.lat, lon: nearestAirport.value.lon };
        const dist = getDistance(currentPos, targetPos);
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, 5);

        if (!arrived && dist > 0) {
          const estimatedStepsLeft = dist / 5;
          if (estimatedStepsLeft > 1) {
            plane.velocity -= (plane.velocity / estimatedStepsLeft);
            plane.baroaltitude -= (plane.baroaltitude / estimatedStepsLeft);
          }
        }
        if (arrived) {
          plane.velocity = 0; plane.baroaltitude = 0; isPaused.value = true; isEmergency.value = false;
          if (emergencyRoute.value) { map.removeLayer(emergencyRoute.value); emergencyRoute.value = null; }
        }
      }

      // ANA MERKEZE DÖNÜŞ
      else if (isReturningToStart.value) {
        const targetPos = { lat: path[0].lat, lon: path[0].lon };
        const distToTarget = getDistance(currentPos, targetPos);
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, 5);

        if (!arrived && distToTarget > 0) {
          const estimatedStepsLeft = distToTarget / 5;
          if (estimatedStepsLeft > 1) {
            plane.velocity -= (plane.velocity / estimatedStepsLeft);
            plane.baroaltitude -= (plane.baroaltitude / estimatedStepsLeft);
          }
        }

        if (arrived) {
          plane.velocity = 0; plane.baroaltitude = 0;
          isReturningToStart.value = false; isPaused.value = true;
          animationSteps.value[icao] = 0; drawFullRoute(icao);
        }
      }

      // MANUEL YÖNLENDİRME
      else if (isManualRouting.value && manualTarget.value) {
        const targetPos = { lat: manualTarget.value.lat, lon: manualTarget.value.lon };
        const currentPos = { lat: plane.lat, lon: plane.lon };
        const remainingDist = getDistance(currentPos, targetPos);
        const stepSize = Math.max(0.05, (plane.velocity / 2000));
        const oldPos = { lat: plane.lat, lon: plane.lon };
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
        const movedDist = getDistance(oldPos, { lat: plane.lat, lon: plane.lon });
        plane.distance_from_dep += movedDist;
        let progressPercent = (plane.distance_from_dep / plane.trip_distance) * 100;
        progressPercent = Math.min(progressPercent, 99.9);
        const maxAlt = 1500;
        const maxSpeed = 160;

        if (progressPercent < 70) {
          if (plane.velocity < maxSpeed) plane.velocity += 0.4;
          if (plane.baroaltitude < maxAlt) plane.baroaltitude += 2;
        } else {
          const factor = Math.max(0, (100 - progressPercent) / 30);
          plane.velocity -= (plane.velocity - (maxSpeed * factor)) * 0.02;
          plane.baroaltitude -= (plane.baroaltitude - (maxAlt * factor)) * 0.02;
        }

        // VARIŞ KONTROLÜ
        if (arrived || remainingDist < 0.1) {
          plane.velocity = 0;
          plane.baroaltitude = 0;
          plane.distance_from_dep = plane.trip_distance;

          isPaused.value = true;
          isManualRouting.value = false;
          manualTarget.value = null;
          plane.total_manual_dist = 0;

          if (emergencyRoute.value) {
            map.removeLayer(emergencyRoute.value);
            emergencyRoute.value = null;
          }

          Swal.fire({ title: 'Hedefe Varıldı', icon: 'success', toast: true, position: 'top-end', timer: 3000 });
        }
      }

      // NORMAL ROTA
      else if (path && path.length > 0) {
        const step = animationSteps.value[icao] || 0;
        if (step + 1 >= path.length) {
          plane.velocity = 0; plane.baroaltitude = 0; return;
        }

        const nextPoint = path[step + 1];
        const targetPos = { lat: nextPoint.lat, lon: nextPoint.lon };
        const smoothStep = Math.max(plane.velocity, 20) / 2000;
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, smoothStep);
        const startPointOfStep = path[step];
        plane.distance_from_dep = (startPointOfStep.distance_from_dep || 0) + getDistance({ lat: startPointOfStep.lat, lon: startPointOfStep.lon }, currentPos);

        if (plane.velocity < nextPoint.velocity) plane.velocity += 3;
        else if (plane.velocity > nextPoint.velocity) plane.velocity -= 1;

        if (plane.baroaltitude < nextPoint.baroaltitude) plane.baroaltitude += 15;
        else if (plane.baroaltitude > nextPoint.baroaltitude) plane.baroaltitude -= 10;

        if (arrived || getDistance(currentPos, targetPos) < 0.2) {
          animationSteps.value[icao] = step + 1;
        }
      }
    }, 10);
  } catch (error) {
    console.error("Hata:", error);
  }
});

const assignMission = () => {
  const dep = departureAirportId.value.toUpperCase().trim();
  const arr = destinationAirportId.value.toUpperCase().trim();
  const depAp = airports.value.find(ap => ap.id === dep);
  const arrAp = airports.value.find(ap => ap.id === arr);

  if (!depAp || !arrAp) {
    Swal.fire('Hata', 'Havalimanı kodları bulunamadı (Örn: LTBI, LTFO)', 'error');
    return;
  }
  const availableIcao = myFleetIcaos.find(icao => currentFlights.value[icao]?.status === 'STANDBY');

  if (!availableIcao) {
    Swal.fire('Hangar Boş', 'Tüm İHA’lar şu an görevde!', 'info');
    return;
  }
  const plane = currentFlights.value[availableIcao];

  if (missionPathLayer.value) map.removeLayer(missionPathLayer.value); // Rota Çizgisi
  missionPathLayer.value = L.polyline([], { color: '#f1c40f', weight: 3, dashArray: '5, 10' }).addTo(map);

  plane.missionDep = { lat: depAp.lat, lon: depAp.lon };
  plane.missionDest = { lat: arrAp.lat, lon: arrAp.lon };

  const d1 = getDistance({ lat: plane.lat, lon: plane.lon }, plane.missionDep);
  const d2 = getDistance(plane.missionDep, plane.missionDest);
  plane.trip_distance = d1 + d2;
  plane.distance_from_dep = 0;
  plane.status = 'GOING_TO_DEP';
  activeIcao.value = availableIcao;
  isPaused.value = false;
  map.setView([plane.lat, plane.lon], 8);
  Swal.fire('Görev Başladı', `${plane.callsign} hangardan çıkış yaptı.`, 'success');
};

const failureTypes = {
  LOW_BATTERY: {
    label: 'DÜŞÜK YAKIT',
    message: 'Enerji seviyesi %20 altına düştü! Güvenli iniş gerekli.',
    color: '#e74c3c' // kırmızı hata
  },
  LINK_LOSS: {
    label: 'SİNYAL KAYBI',
    message: 'Bağlantı kesildi! 10sn içinde otonom iniş yapılacak.',
    color: '#f39c12' // turuncu uyarı
  }
};

const activeFailure = ref(null);

// ARIZA SİMÜLASYONU
const triggerSimulatedFailure = () => {
  if (isEmergencySimulated.value || !selectedFlight.value) return;

  const plane = selectedFlight.value;

  if (plane.energy < 20) {
    activeFailure.value = failureTypes.LOW_BATTERY;
  } else {
    activeFailure.value = failureTypes.LINK_LOSS;
  }

  isEmergencySimulated.value = true; // Simülasyon modunu aç
  emergencyCountdown.value = 10; // 10 saniyelik geri sayım

  const interval = setInterval(() => {   // Geri sayım sayacı
    if (emergencyCountdown.value > 0 && isEmergencySimulated.value) {
      emergencyCountdown.value--; // Her saniye sayacı bir azalt.
    } else {
      clearInterval(interval);
      if (isEmergencySimulated.value && !isEmergency.value) {
        startEmergencyLanding();
        isEmergencySimulated.value = false;
      }
    }
  }, 1000);
};

const handleManualEmergency = () => { // acil iniş butonuna basılınca
  isEmergencySimulated.value = false; // Sayacı durdur
  startEmergencyLanding();
};

// bir uçuş seçildiğinde haritayı uçağa odaklar ve detay panelini hazırlar.
const focusFlight = (f) => {
  if (f.lat && f.lon) {
    sidebarOpen.value = true; // Sağ paneli aç.
    if (activeIcao.value !== f.icao24) {
      activeIcao.value = f.icao24;
      isPaused.value = true;
      drawFullRoute(f.icao24); // Seçilen uçağın tüm rotasını haritaya çiz.
    }
    zoomToPlane(f.lat, f.lon); // Haritayı uçağın güncel koordinatlarına kaydır.
  }
};
</script>