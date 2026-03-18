<template>
  <div id="map"></div>
</template>

<script setup>
import { onMounted, ref, computed } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-rotatedmarker';
import 'leaflet.marker.slideto';
import Swal from 'sweetalert2';

const props = defineProps({
  myFleetIcaos: Array,
  selectedFlight: Object
});

const currentFlights = defineModel('currentFlights');
const activeIcao = defineModel('activeIcao');
const isPaused = defineModel('isPaused');
const animationSteps = defineModel('animationSteps');
const airports = defineModel('airports');
const isEmergency = defineModel('isEmergency');
const isReturningToStart = defineModel('isReturningToStart');
const isEmergencySimulated = defineModel('isEmergencySimulated');
const emergencyCountdown = defineModel('emergencyCountdown');
const manualLat = defineModel('manualLat');
const manualLon = defineModel('manualLon');
const manualAirportId = defineModel('manualAirportId');
const destinationAirportId = defineModel('destinationAirportId');
const activeFailure = defineModel('activeFailure');

const markers = {};
const flightPaths = ref({});
const emergencyRoute = ref(null);
const activeRoutes = {};
let map = null;
const routeLayer = L.layerGroup();
const manualTarget = ref(null);
const isManualRouting = ref(false);
const missionPathLayer = ref(null);

const failureTypes = {
  LOW_BATTERY: {
    label: 'DÜŞÜK YAKIT',
    message: 'Enerji seviyesi %20 altına düştü! Güvenli iniş gerekli.',
    color: '#e74c3c'
  },
  LINK_LOSS: {
    label: 'SİNYAL KAYBI',
    message: 'Bağlantı kesildi! 10sn içinde otonom iniş yapılacak.',
    color: '#f39c12'
  }
};

const getDistance = (p1, p2) => {
  const R = 6371;
  const dLat = (p2.lat - p1.lat) * Math.PI / 180;
  const dLon = (p2.lon - p1.lon) * Math.PI / 180;
  const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) + Math.cos(p1.lat * Math.PI / 180) * Math.cos(p2.lat * Math.PI / 180) * Math.sin(dLon / 2) * Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
};

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
      return true;
    }
  }

  plane.lat = nextLat;
  plane.lon = nextLon;
  const newPos = [nextLat, nextLon];

  if (moveStep === 0) {
    marker.slideTo(newPos, { duration: 100 });
  } else {
    marker.setLatLng(newPos);
  }

  marker.setRotationAngle(heading - 45);
  if (activeRoutes[icao]) activeRoutes[icao].addLatLng(newPos);
  return false;
};

const triggerExplosion = (lat, lon) => {
  const explosionIcon = L.divIcon({
    html: `<div class="explosion-container">
      <div class="explosion-core"></div>
      <div class="explosion-ring ring1"></div>
      <div class="explosion-ring ring2"></div>
      <div class="explosion-ring ring3"></div>
      <div class="explosion-spark spark1"></div>
      <div class="explosion-spark spark2"></div>
      <div class="explosion-spark spark3"></div>
      <div class="explosion-spark spark4"></div>
      <div class="explosion-spark spark5"></div>
      <div class="explosion-spark spark6"></div>
    </div>`,
    className: '',
    iconSize: [120, 120],
    iconAnchor: [60, 60]
  });

  const explosionMarker = L.marker([lat, lon], { icon: explosionIcon, zIndexOffset: 9999 }).addTo(map);
  setTimeout(() => map.removeLayer(explosionMarker), 2500);
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

const nearestAirport = computed(() => {
  if (props.selectedFlight && airports.value.length > 0) {
    return getNearestAirport(props.selectedFlight.lat, props.selectedFlight.lon);
  }
  return null;
});

const returnToStart = () => {
  if (!activeIcao.value) return;
  const icao = activeIcao.value;
  const plane = currentFlights.value[icao];

  if (plane && props.myFleetIcaos.includes(String(icao))) {
    plane.status = 'RETURNING';
  }

  const path = flightPaths.value[icao];
  if (path && path.length > 0) {
    const targetPos = { lat: path[0].lat, lon: path[0].lon };
    plane.trip_distance = getDistance({ lat: plane.lat, lon: plane.lon }, targetPos);
    plane.distance_from_dep = 0;
  }

  resetActivePath(icao);
  isReturningToStart.value = true;
  isEmergency.value = false;
  isPaused.value = false;
  isManualRouting.value = false;

  if (missionPathLayer.value && map.hasLayer(missionPathLayer.value)) {
    map.removeLayer(missionPathLayer.value);
    missionPathLayer.value = null;
  }
  if (emergencyRoute.value) {
    map.removeLayer(emergencyRoute.value);
    emergencyRoute.value = null;
  }
};

const zoomToPlane = (lat, lon) => {
  if (lat && lon && map) map.setView([lat, lon], 8, { animate: true, duration: 1 });
};

const recenterMap = () => {
  if (props.selectedFlight) zoomToPlane(props.selectedFlight.lat, props.selectedFlight.lon);
};

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

    Swal.fire({
      icon: 'success', title: 'Rota Hazır', text: `Mesafe: ${Math.round(dist)} km`,
      timer: 1500, toast: true, position: 'top-end', showConfirmButton: false
    });
  }
};

const startEmergencyLanding = () => {
  if (props.selectedFlight && nearestAirport.value) {
    const icao = activeIcao.value;
    resetActivePath(icao);
    isEmergency.value = true;
    isPaused.value = false;

    const start = [props.selectedFlight.lat, props.selectedFlight.lon];
    const end = [nearestAirport.value.lat, nearestAirport.value.lon];

    if (emergencyRoute.value) map.removeLayer(emergencyRoute.value);
    emergencyRoute.value = L.polyline([start, end], {
      color: 'red', weight: 4, dashArray: '10, 10', opacity: 0.8
    }).addTo(map);
    map.fitBounds(L.latLngBounds([start, end]), { padding: [50, 50], maxZoom: 8 });
  }
};

const triggerSimulatedFailure = () => {
  if (isEmergencySimulated.value || !props.selectedFlight) return;
  const plane = props.selectedFlight;
  if (plane.energy < 20) {
    activeFailure.value = failureTypes.LOW_BATTERY;
  } else {
    activeFailure.value = failureTypes.LINK_LOSS;
  }

  isEmergencySimulated.value = true;
  emergencyCountdown.value = 10;

  const interval = setInterval(() => {
    if (emergencyCountdown.value > 0 && isEmergencySimulated.value) {
      emergencyCountdown.value--;
    } else {
      clearInterval(interval);
      if (isEmergencySimulated.value && !isEmergency.value) {
        startEmergencyLanding();
        isEmergencySimulated.value = false;
      }
    }
  }, 1000);
};

const handleManualEmergency = () => {
  isEmergencySimulated.value = false;
  startEmergencyLanding();
};

const focusFlight = (f) => {
  if (f.lat && f.lon) {
    if (activeIcao.value !== f.icao24) {
      activeIcao.value = f.icao24;
      isPaused.value = true;
      drawFullRoute(f.icao24);
    }
    zoomToPlane(f.lat, f.lon);
  }
};

const assignMission = () => {
  const arr = destinationAirportId.value.toUpperCase().trim();
  const arrAp = airports.value.find(ap => ap.id === arr);
  if (!arrAp) {
    Swal.fire('Hata', 'Hedef havalimanı bulunamadı!', 'error');
    return;
  }
  const targetPos = { lat: arrAp.lat, lon: arrAp.lon };

  const availableIcaos = props.myFleetIcaos.filter(icao =>
    currentFlights.value[icao] && currentFlights.value[icao].status === 'STANDBY'
  );
  if (availableIcaos.length === 0) {
    Swal.fire('Hangar Boş', 'Şu an tüm üslerdeki İHA’lar görevde!', 'info');
    return;
  }

  let closestIcao = null;
  let minDistance = Infinity;

  availableIcaos.forEach(icao => {
    const plane = currentFlights.value[icao];
    const dist = getDistance({ lat: plane.lat, lon: plane.lon }, targetPos);
    if (dist < minDistance) {
      minDistance = dist;
      closestIcao = icao;
    }
  });

  const plane = currentFlights.value[closestIcao];
  plane.missionDest = targetPos;
  plane.trip_distance = minDistance;
  plane.distance_from_dep = 0;
  plane.status = 'GOING_TO_DEST';

  if (missionPathLayer.value) map.removeLayer(missionPathLayer.value);
  missionPathLayer.value = L.polyline([], {
    color: '#e74c3c', weight: 4, dashArray: '10, 5'
  }).addTo(map);

  activeIcao.value = closestIcao;
  isPaused.value = false;
  map.setView([plane.lat, plane.lon], 7);

  Swal.fire({
    title: 'Operasyon Başladı',
    html: `<b>${plane.callsign}</b> seçildi.<br>Mesafe: <b>${Math.round(minDistance)} km</b>`,
    icon: 'warning', toast: true, position: 'top-end',
    timer: 4000, showConfirmButton: false, timerProgressBar: true
  });
};

onMounted(async () => {
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180));
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);
  routeLayer.addTo(map);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap', noWrap: true
  }).addTo(map);

  const getPlaneIcon = (icao) => {
    const isEnvanter = props.myFleetIcaos.includes(icao.toString());
    const iconColor = isEnvanter ? '#e74c3c' : '#9381ff';
    return L.divIcon({
      html: `
      <div class="moving-plane">
        <svg xmlns="http://www.w3.org/2000/svg" width="36" height="36" viewBox="0 0 24 24" fill="${iconColor}" stroke="#ffffff" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M17.8 19.2 16 11l3.5-3.5C21 6 21.5 4 21 3c-1-.5-3 0-4.5 1.5L13 8 4.8 6.2c-.5-.1-.9.1-1.1.5l-.3.5c-.2.5-.1 1 .3 1.3L9 12l-2 3H4l-1 1 3 2 2 3 1-1v-3l3-2 3.5 5.3c.3.4.8.5 1.3.3l.5-.2c.4-.3.6-.7.5-1.2z"/>
        </svg>
      </div>`,
      className: 'plane-icon', iconSize: [40, 40], iconAnchor: [20, 20]
    });
  };

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
        className: 'custom-airport', iconSize: [40, 40]
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

    Object.keys(grouped).forEach(icao => {
      const firstPoint = grouped[icao][0];
      const isEnvanter = props.myFleetIcaos.includes(icao.toString());
      currentFlights.value[icao] = {
        ...firstPoint, velocity: 0, baroaltitude: 0, energy: 100, ammo: isEnvanter ? 2 : 0, status: isEnvanter ? 'STANDBY' : 'ON_MISSION'
      };
      animationSteps.value[icao] = 0;

      if (firstPoint.lat && firstPoint.lon) {
        const marker = L.marker([firstPoint.lat, firstPoint.lon], {
          icon: getPlaneIcon(icao), rotationAngle: (firstPoint.heading || 0) - 45
        }).addTo(map);

        marker.on('click', (e) => {
          L.DomEvent.stopPropagation(e);
          // Instead of sidebarOpen.value = true directly, we let App.vue handle it, BUT MapComponent doesn't have sidebarOpen ref! We can emit or manage it. 
          // Wait! Let's emit it so App.vue handles it! Wait, we'll just update activeIcao and the map handles the rest.
          // App.vue watch(activeIcao) can open sidebar.
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

      if (plane.energy > 0 && plane.velocity > 0) plane.energy = Math.max(0, plane.energy - 0.01);
      if (plane.energy < 20 && !isEmergencySimulated.value && !isEmergency.value && !isReturningToStart.value) triggerSimulatedFailure();

      if (plane.status === 'GOING_TO_DEP' || plane.status === 'GOING_TO_DEST') {
        const targetPos = plane.status === 'GOING_TO_DEP' ? plane.missionDep : plane.missionDest;
        const distToTarget = getDistance(currentPos, targetPos);
        const stepSize = Math.max(0.1, plane.velocity / 1500);
        const oldPos = { lat: plane.lat, lon: plane.lon };
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
        plane.distance_from_dep += getDistance(oldPos, { lat: plane.lat, lon: plane.lon });

        if (missionPathLayer.value) missionPathLayer.value.addLatLng([plane.lat, plane.lon]);
        map.panTo([plane.lat, plane.lon]);

        const cruiseAlt = 10000; const cruiseSpeed = 220;
        if (distToTarget > 20) {
          if (plane.velocity < cruiseSpeed) plane.velocity += 0.8;
          if (plane.baroaltitude < cruiseAlt) plane.baroaltitude += 15;
        } else {
          const ratio = Math.max(0.01, distToTarget / 20);
          plane.velocity -= (plane.velocity - (cruiseSpeed * ratio)) * 0.05;
          plane.baroaltitude -= (plane.baroaltitude - (cruiseAlt * ratio)) * 0.05;
        }

        if (arrived || distToTarget < 0.1) {
          if (plane.status === 'GOING_TO_DEP') {
            plane.status = 'GOING_TO_DEST';
            if (missionPathLayer.value) missionPathLayer.value.setStyle({ color: '#2ecc71', dashArray: null });
          } else {
            triggerExplosion(plane.lat, plane.lon);
            if (plane.ammo > 0) plane.ammo--;
            plane.velocity = 0; plane.baroaltitude = 0; plane.distance_from_dep = plane.trip_distance;
            isPaused.value = true;
            plane.status = plane.ammo <= 0 ? 'MISSION_COMPLETE' : 'STANDBY';
            Swal.fire({
              title: 'HEDEF İMHA EDİLDİ', html: `Birim: <b>${plane.callsign}</b><br>Kalan Mühimmat: <b>${plane.ammo}</b>`,
              icon: 'success', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false, timerProgressBar: true
            });
          }
        }
      } else if (isEmergency.value && nearestAirport.value) {
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
      } else if (isReturningToStart.value) {
        const targetPos = { lat: path[0].lat, lon: path[0].lon };
        const distToTarget = getDistance(currentPos, targetPos);
        const stepSize = Math.max(0.1, plane.velocity / 1500);
        const oldPos = { lat: plane.lat, lon: plane.lon };
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
        plane.distance_from_dep += getDistance(oldPos, { lat: plane.lat, lon: plane.lon });

        const cruiseAlt = 10000; const cruiseSpeed = 220;
        if (distToTarget > 20) {
          if (plane.velocity < cruiseSpeed) plane.velocity += 0.8;
          if (plane.baroaltitude < cruiseAlt) plane.baroaltitude += 15;
        } else {
          const ratio = Math.max(0.01, distToTarget / 20);
          plane.velocity -= (plane.velocity - (cruiseSpeed * ratio)) * 0.05;
          plane.baroaltitude -= (plane.baroaltitude - (cruiseAlt * ratio)) * 0.05;
        }

        if (arrived || distToTarget < 0.1) {
          plane.velocity = 0; plane.baroaltitude = 0; plane.status = 'STANDBY'; plane.energy = 100; plane.ammo = 2;
          isReturningToStart.value = false; isPaused.value = true; animationSteps.value[icao] = 0; plane.distance_from_dep = 0;
          drawFullRoute(icao);
          Swal.fire({ title: 'Üsse Dönüldü', text: 'İkmal tamamlandı.', icon: 'info', toast: true, position: 'top-end', timer: 3000, showConfirmButton: false });
        }
      } else if (isManualRouting.value && manualTarget.value) {
        const targetPos = { lat: manualTarget.value.lat, lon: manualTarget.value.lon };
        const remainingDist = getDistance(currentPos, targetPos);
        const stepSize = Math.max(0.05, (plane.velocity / 2000));
        const oldPos = { lat: plane.lat, lon: plane.lon };
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
        plane.distance_from_dep += getDistance(oldPos, { lat: plane.lat, lon: plane.lon });

        let progressPercent = (plane.distance_from_dep / plane.trip_distance) * 100;
        const maxAlt = 1500; const maxSpeed = 160;

        if (progressPercent < 70) {
          if (plane.velocity < maxSpeed) plane.velocity += 0.4;
          if (plane.baroaltitude < maxAlt) plane.baroaltitude += 2;
        } else {
          const factor = Math.max(0, (100 - progressPercent) / 30);
          plane.velocity -= (plane.velocity - (maxSpeed * factor)) * 0.02;
          plane.baroaltitude -= (plane.baroaltitude - (maxAlt * factor)) * 0.02;
        }

        if (arrived || remainingDist < 0.1) {
          triggerExplosion(plane.lat, plane.lon);
          if (plane.ammo > 0) plane.ammo--;
          plane.velocity = 0; plane.baroaltitude = 0; plane.distance_from_dep = plane.trip_distance;
          isPaused.value = true; isManualRouting.value = false; manualTarget.value = null;
          plane.status = plane.ammo <= 0 ? 'MISSION_COMPLETE' : 'STANDBY';
          if (emergencyRoute.value) { map.removeLayer(emergencyRoute.value); emergencyRoute.value = null; }
          Swal.fire({
            title: 'HEDEF İMHA EDİLDİ', html: `Kalan Mühimmat: <b>${plane.ammo}</b>`,
            icon: 'success', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false, timerProgressBar: true
          });
        }
      } else if (path && path.length > 0) {
        const step = animationSteps.value[icao] || 0;
        if (step + 1 >= path.length) { plane.velocity = 0; plane.baroaltitude = 0; return; }
        const nextPoint = path[step + 1];
        const arrived = movePlane(icao, nextPoint.lat, nextPoint.lon, Math.max(plane.velocity, 20) / 2000);
        plane.distance_from_dep = (path[step].distance_from_dep || 0) + getDistance({ lat: path[step].lat, lon: path[step].lon }, currentPos);
        if (plane.velocity < nextPoint.velocity) plane.velocity += 3; else if (plane.velocity > nextPoint.velocity) plane.velocity -= 1;
        if (plane.baroaltitude < nextPoint.baroaltitude) plane.baroaltitude += 15; else if (plane.baroaltitude > nextPoint.baroaltitude) plane.baroaltitude -= 10;
        if (arrived) animationSteps.value[icao] = step + 1;
      }
    }, 10);
  } catch (error) {
    console.error("Hata:", error);
  }
});

defineExpose({
  assignMission,
  recenterMap,
  returnToStart,
  triggerSimulatedFailure,
  handleManualEmergency,
  setManualTarget,
  focusFlight,
  togglePause
});
</script>
