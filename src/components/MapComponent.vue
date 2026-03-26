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
import { getDistance, calculateNextPosition } from '../utils/physics';
import { triggerExplosion, getPlaneIcon, getAirportIcon } from '../utils/mapVisuals';

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
const destLat = defineModel('destLat');
const destLon = defineModel('destLon');
const activeFailure = defineModel('activeFailure');

const markers = {}; // Harita üzerindeki marker objelerini (uçak ikonlarını) tutar
const flightPaths = ref({}); // Uçuşların tüm rota noktalarını tutar
const emergencyRoute = ref(null); // Acil durum iniş rotası katmanı
const activeRoutes = {}; // Uçuşların haritada çizilmiş yol rotalarını tutar
let map = null; // Leaflet harita nesnesi referansı
const routeLayer = L.layerGroup(); // Rotaların eklendiği harita katmanı grubu
const manualTarget = ref(null); // Manuel hedefin (koordinat veya havalimanı) bilgilerini tutar
const isManualRouting = ref(false); // Manuel olarak hedefe yönlendirme durumunu belirtir
const missionPathLayer = ref(null); // Aktif görev gidiş-dönüş rotası katmanı

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

// Verilen uçağı hedeflenen koordinata doğru ilerletir ve dönüş açısını ayarlar
const movePlane = (icao, targetLat, targetLon, moveStep = 0) => {
  const plane = currentFlights.value[icao];
  const marker = markers[icao];
  if (!plane || !marker) return false;

  const { nextLat, nextLon, heading, hasArrived } = calculateNextPosition(
    plane.lat, plane.lon, targetLat, targetLon, moveStep, plane.heading || 0
  );

  if (hasArrived) {
    return true;
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

const updatePlanePhysics = (plane, icao, currentPos, targetPos, cruiseSpeed = 220, cruiseAlt = 10000) => {
  const distToTarget = getDistance(currentPos, targetPos);
  const stepSize = Math.max(0.1, plane.velocity / 1500);
  const oldPos = { lat: plane.lat, lon: plane.lon };
  
  const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
  plane.distance_from_dep += getDistance(oldPos, { lat: plane.lat, lon: plane.lon });

  if (distToTarget > 20) {
    if (plane.velocity < cruiseSpeed) plane.velocity += 0.8;
    if (plane.baroaltitude < cruiseAlt) plane.baroaltitude += 15;
  } else {
    const ratio = Math.max(0.01, distToTarget / 20);
    plane.velocity -= (plane.velocity - (cruiseSpeed * ratio)) * 0.05;
    plane.baroaltitude -= (plane.baroaltitude - (cruiseAlt * ratio)) * 0.05;
  }

  return { arrived, distToTarget };
};

const resetActivePath = (icao) => {
  if (activeRoutes[icao]) {
    routeLayer.removeLayer(activeRoutes[icao]);
    const currentPos = [currentFlights.value[icao].lat, currentFlights.value[icao].lon];
    activeRoutes[icao] = L.polyline([currentPos], { color: '#9381ff', weight: 4, opacity: 1 }).addTo(routeLayer);
  }
};

// Aktif bir görevi başlattığımızda veya güncellediğimizde rotayı çizer
const drawMissionRoute = (plane, targetPos) => {
  if (missionPathLayer.value) map.removeLayer(missionPathLayer.value);
  
  const startPos = [plane.lat, plane.lon];
  const targetCoord = [targetPos.lat, targetPos.lon];

  const previewLine = L.polyline([startPos, targetCoord], {
    color: '#e74c3c', weight: 4, dashArray: '10, 5', opacity: 0.7
  });
  
  const targetCircle = L.circleMarker(targetCoord, {
    radius: 8, color: '#e74c3c', fillOpacity: 1, weight: 2
  }).bindPopup('<b>BOMBALAMA HEDEFİ</b>');

  const progressLine = L.polyline([startPos], {
    color: '#e74c3c', weight: 4, opacity: 1
  });

  missionPathLayer.value = L.layerGroup([previewLine, targetCircle, progressLine]).addTo(map);
  return progressLine;
};

// Uçağın o anki konumuna en yakın müttefik havalimanını (üs noktasını) bulur
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

// Görevi iptal ederek aktif uçağın başlangıç koordinatlarına (üssüne) dönmesini tetikler
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

    drawMissionRoute(plane, target); // Manuel güncellenen rotanın çizimi

    Swal.fire({
      icon: 'success', title: 'Rota Hazır', text: `Mesafe: ${Math.round(dist)} km`,
      timer: 1500, toast: true, position: 'top-end', showConfirmButton: false
    });
  }
};

// En yakın havalimanına hesaplanan acil iniş rotasını çizer ve acil durumu başlatır
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

// Batarya seviyesine veya duruma göre manuel/suni arıza ve gerisayım (10sn) durumu başlatır
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

// Kullanıcının seçtiği hedef rotasına hangarda bekleyen en yakın İHA'yı gönderir
const assignMission = () => {
  const arr = destinationAirportId.value.toUpperCase().trim();
  const arrAp = airports.value.find(ap => ap.id === arr);
  let targetPos = null;

  if (destinationAirportId.value === 'MANUAL_COORD' && destLat.value && destLon.value) {
    targetPos = { lat: parseFloat(destLat.value), lon: parseFloat(destLon.value) };
  } else if (arrAp) {
    targetPos = { lat: arrAp.lat, lon: arrAp.lon };
  }

  if (!targetPos) {
    Swal.fire('Hata', 'Geçerli bir hedef (havalimanı veya koordinat) seçin!', 'error');
    return;
  }

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

  drawMissionRoute(plane, targetPos); // Görev rotasını çizimi

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

// Bileşen ayağa kalktığında Leaflet haritasını oluşturur ve JSON verilerini yükler
onMounted(async () => {
  // Dünya sınırlarını sabitleyip temel haritayı çizer
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180));
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);
  routeLayer.addTo(map);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap', noWrap: true
  }).addTo(map);

  try {
    const airResponse = await fetch('/airports_library_v6.json');
    const airData = await airResponse.json();
    airports.value = airData;

    airData.forEach(ap => {
      const airportIcon = getAirportIcon(ap.id);
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
          icon: getPlaneIcon(isEnvanter), rotationAngle: (firstPoint.heading || 0) - 45
        }).addTo(map);

        marker.on('click', (e) => {
          L.DomEvent.stopPropagation(e);
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

    // Simülasyon döngüsü: Uçakların hareketini, hızlanmasını ve görev kontrollerini periyodik denetler
    setInterval(() => {
      const icao = activeIcao.value;
      if (!icao || isPaused.value || !currentFlights.value[icao]) return;

      const plane = currentFlights.value[icao];
      const path = flightPaths.value[icao];
      const currentPos = { lat: plane.lat, lon: plane.lon };

      if (plane.energy > 0 && plane.velocity > 0) plane.energy = Math.max(0, plane.energy - 0.01);
      if (plane.energy < 20 && !isEmergencySimulated.value && !isEmergency.value && !isReturningToStart.value) triggerSimulatedFailure();
      
      // 1. Durum: Hedef havalimanına veya görev sahasına gidiş işlemi
      if (plane.status === 'GOING_TO_DEP' || plane.status === 'GOING_TO_DEST') {
        const targetPos = plane.status === 'GOING_TO_DEP' ? plane.missionDep : plane.missionDest;
        const { arrived, distToTarget } = updatePlanePhysics(plane, icao, currentPos, targetPos);

        if (missionPathLayer.value) {
          const progressLine = missionPathLayer.value.getLayers().find(l => l instanceof L.Polyline && !l.options.dashArray);
          if (progressLine) progressLine.addLatLng([plane.lat, plane.lon]);
        }
        map.panTo([plane.lat, plane.lon]);

        if (arrived || distToTarget < 0.1) {
          if (plane.status === 'GOING_TO_DEP') {
            plane.status = 'GOING_TO_DEST';
            if (missionPathLayer.value) {
               const progressLine = missionPathLayer.value.getLayers().find(l => l instanceof L.Polyline && !l.options.dashArray);
               if (progressLine) progressLine.setStyle({ color: '#2ecc71', dashArray: null });
            }
          } else {
            triggerExplosion(plane.lat, plane.lon, map);
            if (plane.ammo > 0) plane.ammo--;
            plane.velocity = 0; plane.baroaltitude = 0; plane.distance_from_dep = plane.trip_distance;
            isPaused.value = true;
            plane.status = 'MISSION_COMPLETE';
            Swal.fire({
              title: 'HEDEF İMHA EDİLDİ', html: `Birim: <b>${plane.callsign}</b><br>Görev Tamamlandı, Üsse Dönülüyor!`,
              icon: 'success', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false, timerProgressBar: true
            });
            setTimeout(() => {
              if (activeIcao.value !== icao) activeIcao.value = icao;
              returnToStart();
            }, 2000);
          }
        }
      // 2. Durum: Acil durum aktif (En yakın havalimanına acil iniş)
      } else if (isEmergency.value && nearestAirport.value) {
        const targetPos = { lat: nearestAirport.value.lat, lon: nearestAirport.value.lon };
        const dist = getDistance(currentPos, targetPos);
        
        // İniş sırasında hızı ve adımı kalan mesafeye göre orantılı düşürüyoruz
        const stepSize = Math.max(0.05, plane.velocity / 2000); 
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
        
        if (!arrived && dist > 0) {
          // Kalan mesafe üzerinden kaç adım kaldığını hesapla
          const stepsToTarget = dist / stepSize;
          
          if (stepsToTarget > 0) {
            // Hız ve irtifayı kalan adım sayısına göre azaltarak tam hedefte 0'a ulaşmasını sağla
            plane.velocity -= (plane.velocity / stepsToTarget);
            plane.baroaltitude -= (plane.baroaltitude / stepsToTarget);
          }
        }
        if (arrived) {
          plane.velocity = 0; plane.baroaltitude = 0; isPaused.value = true; isEmergency.value = false;
          if (emergencyRoute.value) { map.removeLayer(emergencyRoute.value); emergencyRoute.value = null; }
        }
      // 3. Durum: Görev bitti (imha) / iptal edildi, ana üsse (başlangıç koordinatlarına) geri dönüş
      } else if (isReturningToStart.value) {
        const targetPos = { lat: path[0].lat, lon: path[0].lon };
        const { arrived, distToTarget } = updatePlanePhysics(plane, icao, currentPos, targetPos);

        if (arrived || distToTarget < 0.1) {
          plane.velocity = 0; plane.baroaltitude = 0; plane.status = 'STANDBY'; plane.energy = 100; plane.ammo = 2;
          isReturningToStart.value = false; isPaused.value = true; animationSteps.value[icao] = 0; plane.distance_from_dep = 0;
          drawFullRoute(icao);
          Swal.fire({ title: 'Üsse Dönüldü', text: 'İkmal tamamlandı.', icon: 'info', toast: true, position: 'top-end', timer: 3000, showConfirmButton: false });
        }
      // 4. Durum: Haritada manuel olarak tanımlanan özel bir rotaya/koordinata uçuş
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

        if (missionPathLayer.value) {
          const progressLine = missionPathLayer.value.getLayers().find(l => l instanceof L.Polyline && !l.options.dashArray);
          if (progressLine) progressLine.addLatLng([plane.lat, plane.lon]);
        }

        if (arrived || remainingDist < 0.1) {
          plane.velocity = 0; plane.baroaltitude = 0; plane.distance_from_dep = plane.trip_distance;
          isPaused.value = true; isManualRouting.value = false; manualTarget.value = null;
          plane.status = 'ARRIVED';
          if (missionPathLayer.value) { map.removeLayer(missionPathLayer.value); missionPathLayer.value = null; }
          Swal.fire({
            title: 'HEDEFE VARILDI', html: `Birim: <b>${plane.callsign || 'Bilinmeyen'}</b><br>Manuel Rota Tamamlandı.`,
            icon: 'info', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false, timerProgressBar: true
          });
        }
      // 5. Durum: Tanımlanmış rota noktaları olan standart JSON uçuş rotasında ilerleme
      } else if (path && path.length > 0) {
        const step = animationSteps.value[icao] || 0;
        if (step + 1 >= path.length) { 
          plane.velocity = 0; 
          plane.baroaltitude = 0; 
          plane.status = 'COMPLETED';
          return; 
        }
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
