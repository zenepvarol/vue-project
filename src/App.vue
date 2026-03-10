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

            <div class="detail-item full-width">
              <label>
                <Battery :size="14" /> YAKIT (%{{ Math.round(selectedFlight.energy) }})
              </label>
              <div class="progress-bar energy-bar">
                <div class="progress-fill" :style="{
                  width: selectedFlight.energy + '%',
                  backgroundColor: selectedFlight.energy < 20 ? '#e74c3c' : '#2ecc71'
                }">
                </div>
              </div>
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
              <p class="decision-subtext" style="font-size: 11px; margin-bottom: 5px; font-weight: bold;">
                {{ activeFailure?.message || 'Bilinmeyen bir hata oluştu.' }}
              </p>
              <p class="decision-subtext">Operatör kararı bekleniyor...</p>

              <button class="emergency-button" @click="handleManualEmergency">
                ACİL İNİŞ YAP
              </button>
            </div>
          </div>

          <div v-if="isEmergency" class="emergency-status-note" style="color: #e74c3c; margin-top: 10px;">
            <AlertCircle :size="14" /> Acil iniş noktasına gidiliyor.
          </div>
          <div v-if="isReturningToStart" class="emergency-status-note" style="color: #3498db; margin-top: 10px;">
            <Info :size="14" /> Ana merkeze dönüş yapılıyor.
          </div>

          <div class="manual-target-input">
            <h4>Manuel Hedef Belirleme</h4>
            <div class="input-row">
              <input v-model="manualLat" type="number" placeholder="Enlem (Lat)" class="coord-input">
              <input v-model="manualLon" type="number" placeholder="Boylam (Lon)" class="coord-input">
            </div>

            <input v-model="manualAirportId" type="text" placeholder="Havalimanı Seçin veya Yazın"
              class="full-width-input" list="airport-list">

            <datalist id="airport-list">
              <option v-for="ap in airports" :key="ap.id" :value="ap.id">
                {{ ap.name }} ({{ ap.city }})
              </option>
            </datalist>

            <button class="apply-target-btn" @click="setManualTarget">
              HEDEFE YÖNLENDİR
            </button>
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
      // ucak hedefe moveStep kadar kayar
      nextLat = plane.lat + (dx / dist) * moveStep;
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

  const staticPath = L.polyline(allPoints, { color: '#0077b6', weight: 2, opacity: 0.5 }); // gitmesi gerekn saydam mavi yol
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

const setManualTarget = () => {
  if (!activeIcao.value) return;

  let target = null;

  if (manualAirportId.value) {   // Havalimanı kodu girildiyse
    const targetAp = airports.value.find(ap => ap.id.toLowerCase() === manualAirportId.value.toLowerCase());
    if (targetAp) {
      target = { lat: targetAp.lat, lon: targetAp.lon };
    }
  }
  else if (manualLat.value !== null && manualLon.value !== null) { // Koordinat girildiyse
    const lat = parseFloat(manualLat.value);
    const lon = parseFloat(manualLon.value);

    if (lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180) {
      target = { lat, lon };
    } else {
      alert("Hatalı Koordinat! Enlem -90/+90, Boylam -180/+180 aralığında olmalıdır.");
      return;
    }
  }

  if (target) {
    manualTarget.value = target;
    isManualRouting.value = true;
    isPaused.value = false;

    // Görsel çizgiyi güncelle
    if (emergencyRoute.value) map.removeLayer(emergencyRoute.value);
    const currentPos = [currentFlights.value[activeIcao.value].lat, currentFlights.value[activeIcao.value].lon];
    emergencyRoute.value = L.polyline([currentPos, [target.lat, target.lon]], {
      color: '#2ecc71',
      dashArray: '5, 10',
      weight: 3
    }).addTo(map);
  } else {
    alert("Geçersiz koordinat veya havalimanı kodu girildi!");
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
    // Harita uçak ve iniş noktasını içine alacak şekilde odaklanır
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
      currentFlights.value[icao] = { // Başlangıç değerleri
        ...firstPoint,
        velocity: 0,
        baroaltitude: 0,
        energy: 100
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
      if (!icao || isPaused.value || !flightPaths.value[icao]) return;
      const path = flightPaths.value[icao];
      const plane = currentFlights.value[icao];

      // Uçak hareket ederken yakıt tüketimi
      if (plane.energy > 0) {
        plane.energy = Math.max(0, plane.energy - 0.02);  // 0.02 İDEAL 
      }

      if (plane.energy < 20 && !isEmergencySimulated.value && !isEmergency.value && !isReturningToStart.value) {
        console.warn("KRİTİK ENERJİ");
        triggerSimulatedFailure();
      }

      // ACİL İNİŞ (En yakın havaalanına)
      if (isEmergency.value && nearestAirport.value) {
        const targetPos = { lat: nearestAirport.value.lat, lon: nearestAirport.value.lon };
        const currentPos = { lat: plane.lat, lon: plane.lon };

        const dist = getDistance(currentPos, targetPos); // kalan mesafe
        const stepSize = 5; // iniş hızı
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);

        if (!arrived && dist > 0) { // kademeli sıfırlama hız ve rakımı
          const estimatedStepsLeft = dist / stepSize;
          if (estimatedStepsLeft > 1) {
            plane.velocity -= (plane.velocity / estimatedStepsLeft);
            plane.baroaltitude -= (plane.baroaltitude / estimatedStepsLeft);
          }
        }

        if (arrived) { // varış 
          plane.velocity = 0;
          plane.baroaltitude = 0;
          isPaused.value = true;
          isEmergency.value = false;

          // varılan yer ana merkezse sıfırlama
          const homePos = { lat: path[0].lat, lon: path[0].lon };
          const currentPos = { lat: plane.lat, lon: plane.lon };

          if (getDistance(currentPos, homePos) < 0.01) {
            animationSteps.value[icao] = 0; // Ana merkeze döndüğümüzü anla
            drawFullRoute(icao); // harita başlangıçta
          }

          if (emergencyRoute.value) {
            map.removeLayer(emergencyRoute.value);
            emergencyRoute.value = null;
          }
        }
      }

      // ANA MERKEZE DÖNÜŞ 
      else if (isReturningToStart.value) {
        const targetPos = { lat: path[0].lat, lon: path[0].lon };
        const currentPos = { lat: plane.lat, lon: plane.lon };
        const startPos = { lat: path[animationSteps.value[icao]].lat, lon: path[animationSteps.value[icao]].lon };

        const distToTarget = getDistance(currentPos, targetPos);
        const distFromStart = getDistance(currentPos, startPos);
        const stepSize = 5; // inis hizi 
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);

        if (!arrived && distToTarget > 0) {
          const estimatedStepsLeft = distToTarget / stepSize;
          // hedefe daha yakınken iniş
          if (distToTarget < distFromStart) {
            if (estimatedStepsLeft > 1) {
              plane.velocity -= (plane.velocity / estimatedStepsLeft);
              plane.baroaltitude -= (plane.baroaltitude / estimatedStepsLeft);
            }
          }
          // kalkısa daha yakınken yukselis
          else {
            if (plane.velocity < 180) plane.velocity += 1;
            let climbRate = 8; // Temel tırmanış hızı
            if (plane.baroaltitude > 2000) climbRate = 3;
            if (plane.baroaltitude > 3500) climbRate = 2;
            if (plane.baroaltitude > 4500) climbRate = 0.5;
            plane.baroaltitude += climbRate;
          }
        }

        if (arrived) {
          plane.velocity = 0;
          plane.baroaltitude = 0;
          isReturningToStart.value = false;
          isPaused.value = true;
          animationSteps.value[icao] = 0; // adım sıfırlanır
          drawFullRoute(icao);
        }
      }

      //YÖNLENDİRME
      else if (isManualRouting.value && manualTarget.value) {
        const currentPos = { lat: plane.lat, lon: plane.lon };
        const targetPos = { lat: manualTarget.value.lat, lon: manualTarget.value.lon };
        const remainingDist = getDistance(currentPos, targetPos); // Hedefe kalan gerçek mesafeyi Haversine formülüyle hesaplama

        if (!plane.total_manual_dist || plane.total_manual_dist === 0) {
          plane.total_manual_dist = remainingDist;
          plane.max_manual_alt = 800;
          plane.is_descending = false;
        }

        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, 1);
        const progressPercent = ((plane.total_manual_dist - remainingDist) / plane.total_manual_dist) * 100; // tamamlanma yüzdesi

        if (progressPercent < 70) { // %70 e kadar tırmanış
          if (plane.velocity < 150) plane.velocity += 1;
          if (plane.baroaltitude < plane.max_manual_alt) {
            plane.baroaltitude += 2.5; // saniyede 25 feet
          }
        } else {
          if (!plane.is_descending) {
            plane.descent_start_alt = plane.baroaltitude; // anlık rakım inis icin tutulur
            plane.is_descending = true;
          }

          const factor = Math.max(0, (100 - progressPercent) / 30); // %70 ile %100 arası süzülüş çarpanı
          plane.velocity = Math.max(15, 150 * factor);
          plane.baroaltitude = plane.descent_start_alt * factor;
        }

        plane.trip_distance = plane.total_manual_dist;
        plane.distance_from_dep = Math.max(0, plane.total_manual_dist - remainingDist);

        if (arrived || remainingDist < 0.1) {
          plane.velocity = 0;
          plane.baroaltitude = 0;
          plane.is_descending = false;
          plane.distance_from_dep = plane.trip_distance;
          isPaused.value = true;
          isManualRouting.value = false;
          manualTarget.value = null;
          plane.total_manual_dist = 0;
          animationSteps.value[icao] = 1;

          if (emergencyRoute.value) {
            map.removeLayer(emergencyRoute.value);
            emergencyRoute.value = null;
          }
        }
      }

      // NORMAL İLERLEME 
      else {
        const step = animationSteps.value[icao];
        if (step + 1 >= path.length) {
          isPaused.value = true; // rota bittiyse dur
          return;
        }
        const nextStep = step + 1;
        const nextPoint = path[nextStep];

        // Hedef değerler JSON'dan
        const targetVel = nextPoint.velocity;
        const targetAlt = nextPoint.baroaltitude;

        if (plane.velocity < targetVel) { // HIZ
          plane.velocity = Math.min(targetVel, plane.velocity + 2.5);
        } else if (plane.velocity > targetVel) {
          plane.velocity = Math.max(targetVel, plane.velocity - 2);
        }

        if (plane.baroaltitude < targetAlt) {  // RAKIM 
          plane.baroaltitude = Math.min(targetAlt, plane.baroaltitude + 10); // KALKIS
        } else if (plane.baroaltitude > targetAlt) {
          plane.baroaltitude = Math.max(targetAlt, plane.baroaltitude - 10); //İNİS
        }

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

// Sistemdeki olası acil durum senaryolarını, mesajlarını ve görsel renklerini tanımlar.
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

// ACİL İNİŞ BUTONUNA BASILDIĞINDA
const handleManualEmergency = () => {
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