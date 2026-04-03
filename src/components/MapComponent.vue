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
import { triggerExplosion, getPlaneIcon, getAirportIcon, getTankerIcon } from '../utils/mapVisuals';

const props = defineProps({
  myFleetIcaos: Array,
  selectedFlight: Object
});

const currentFlights = defineModel('currentFlights'), activeIcao = defineModel('activeIcao'), isPaused = defineModel('isPaused');
const animationSteps = defineModel('animationSteps'), airports = defineModel('airports'), isEmergency = defineModel('isEmergency');
const isReturningToStart = defineModel('isReturningToStart'), isEmergencySimulated = defineModel('isEmergencySimulated'), emergencyCountdown = defineModel('emergencyCountdown');
const manualLat = defineModel('manualLat'), manualLon = defineModel('manualLon'), manualAirportId = defineModel('manualAirportId');
const destinationAirportId = defineModel('destinationAirportId'), destLat = defineModel('destLat'), destLon = defineModel('destLon'), activeFailure = defineModel('activeFailure');

const markers = {}, flightPaths = ref({}), emergencyRoute = ref(null), activeRoutes = {};
let map = null; const routeLayer = L.layerGroup(), manualTarget = ref(null);
const isManualRouting = ref(false), missionPathLayer = ref(null), tankerFlight = ref(null), tankerMarker = ref(null);

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

const updatePlanePhysics = (plane, icao, currentPos, targetPos, cruiseSpeed = 220, cruiseAlt = 10000, noDescent = false, descentDist = 20) => {
  const distToTarget = getDistance(currentPos, targetPos);
  const stepSize = Math.max(0.1, plane.velocity / 1500);
  const oldPos = { lat: plane.lat, lon: plane.lon };

  const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
  plane.distance_from_dep += getDistance(oldPos, { lat: plane.lat, lon: plane.lon });

  if (!noDescent && distToTarget < descentDist) {
    // İniş mantığı: Belirlenen alçalma mesafesine (descentDist) girildiğinde orantılı azalma başlar.
    const ratio = Math.max(0, distToTarget / descentDist);
    const targetVel = cruiseSpeed * ratio;
    const targetAlt = cruiseAlt * ratio;

    plane.velocity += (targetVel - plane.velocity) * 0.1;
    plane.baroaltitude += (targetAlt - plane.baroaltitude) * 0.1;
  } else {
    // Kalkış ve Seyir: Hedef değerlere yumuşak geçiş
    plane.velocity += (cruiseSpeed - plane.velocity) * 0.01;
    plane.baroaltitude += (cruiseAlt - plane.baroaltitude) * 0.01;
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

      // TB3 gerçekçiliği için yakıt tüketimi (100% = 5700 km menzil) (0.01 di guncellendi)
      if (plane.energy > 0 && plane.velocity > 0) plane.energy = Math.max(0, plane.energy - 0.0025);
      if (plane.energy < 20 && !isEmergencySimulated.value && !isEmergency.value && !isReturningToStart.value) triggerSimulatedFailure();

      /** Uçağın o anki uçuş moduna göre (Acil iniş, görev, manuel rota) gitmek istediği asıl hedef noktasını tespit eder. 
       * Bu hedef, yakıtın yetip yetmeyeceğini hesaplamak için kullanılır.*/
      const getActivePlaneTarget = () => {
        if (isEmergency.value && nearestAirport.value) return nearestAirport.value; // Acil iniş durumunda hedef: en yakın havalimanı
        if (plane.status === 'GOING_TO_DEP') return plane.missionDep; // Görev kalkış noktasına gidiliyorsa hedef odur
        if (plane.status === 'GOING_TO_DEST' || plane.status === 'MISSION_COMPLETE') return plane.status === 'GOING_TO_DEST' ? plane.missionDest : { lat: path[0].lat, lon: path[0].lon }; // üsse dönüş hedefleri
        if (isReturningToStart.value) return { lat: path[0].lat, lon: path[0].lon }; // Manuel üsse dön butonuyla başlangıç hedefleri
        if (isManualRouting.value && manualTarget.value) return manualTarget.value; // Manuel tıklanan hedefler
        if (path && path.length > 0) return path[path.length - 1]; // Sabit rotalar için son rota noktası
        return null;
      };

      const finalTarget = getActivePlaneTarget(); // Uçağın o an gitmek istediği asıl hedefi bulur
      if (finalTarget) {
        const distToFinal = getDistance(currentPos, finalTarget); // Mevcut konumdan hedefe olan mesafe

        /** Yakıtın yarısı tükendiyse, mevcut yakıt hedefe ulaşmaya yetmiyorsa (TB3 5700km menzil baz), bir tanker uçak zaten yolda değilse */
        if (plane.energy < 50 && plane.energy < (distToFinal / 57) && !tankerFlight.value) {
          const tankerBase = getNearestAirport(plane.lat, plane.lon); // Tankerin kalkacağı en yakın üs
          if (tankerBase) {
            // Tanker objesi ve verileri başlatılır
            tankerFlight.value = {
              targetIcao: icao, // Hangi uçağa ikmal yapılacağı
              lat: tankerBase.lat, lon: tankerBase.lon, heading: 0,
              velocity: 350, energy: 100, startBase: tankerBase, status: 'APPROACHING'
            };
            tankerMarker.value = L.marker([tankerBase.lat, tankerBase.lon], { icon: getTankerIcon(), zIndexOffset: 1000 }).addTo(map); // tanker marker - turuncu

            Swal.fire({
              title: 'İKMAL TALEBİ', text: 'Yakıt yetersiz! Tanker uçağı sevk edildi.', icon: 'warning',
              toast: true, position: 'top-end', timer: 3000, showConfirmButton: false
            });
          }
        }
      }

      // Tanker havadaysa her animasyon adımında çalışır 
      if (tankerFlight.value) {
        const tanker = tankerFlight.value; // Tanker verileri
        const tMarker = tankerMarker.value; // Tankerin haritadaki simgesi
        const targetPlane = currentFlights.value[tanker.targetIcao]; // Tanker tarafından takip edilen uçak

        if (!targetPlane) {
          tanker.status = 'RETURNING';
        }
        tanker.energy = Math.max(0, tanker.energy - 0.0008); // Tanker yakıtı uçaklardan daha yavaş azalıyor

        // Tanker 1. Adım: Tanker uçağa doğru yüksek hızla ilerliyor
        if (tanker.status === 'APPROACHING') {
          // calculateNextPosition ile uçağın her an güncellenen konumunu takip eder (hız yaklaşık 3 kat hızlı)
          const { nextLat, nextLon, heading } = calculateNextPosition(tanker.lat, tanker.lon, targetPlane.lat, targetPlane.lon, 0.4);
          tanker.lat = nextLat; tanker.lon = nextLon; tanker.heading = heading;
          tMarker.setLatLng([nextLat, nextLon]);
          tMarker.setRotationAngle(heading - 45); // Tanker, uçağa doğru bakarak uçar

          // ikisi arası mesafe 0.5km altına düşerse ikmal
          if (getDistance({ lat: tanker.lat, lon: tanker.lon }, { lat: targetPlane.lat, lon: targetPlane.lon }) < 0.5) {
            tanker.status = 'REFUELING';
            Swal.fire({
              title: 'İKMAL BAŞLADI', text: 'Depo dolduruluyor...', icon: 'info',
              toast: true, position: 'top-end', timer: 3000, showConfirmButton: false
            });
          }
        }
        // Tanker 2. Adım: Depo fulllenene kadar beraber uçuyorlar
        else if (tanker.status === 'REFUELING') {
          tanker.lat = targetPlane.lat; tanker.lon = targetPlane.lon; // Tanker konumu her animasyon adımında hedef uçağın konumuyla eşitlenir
          tMarker.setLatLng([targetPlane.lat, targetPlane.lon]);
          tMarker.setRotationAngle((targetPlane.heading || 0) - 45); // Uçakla aynı açıda durur

          targetPlane.energy = Math.min(100, targetPlane.energy + 0.02); // saniyede %2 artış
          if (targetPlane.energy >= 100) {
            tanker.status = 'RETURNING';
            Swal.fire({
              title: 'İKMAL TAMAMLANDI', text: 'Tanker üsse geri dönüyor.', icon: 'success',
              toast: true, position: 'top-end', timer: 3000, showConfirmButton: false
            });
          }
        }
        // Tanker 3. Adım: İkmal bitti, tanker kalktığı üsse geri dönüyor
        else if (tanker.status === 'RETURNING') {
          // calculateNextPosition ile tanker ana merkezine (startBase) yönlenir
          const { nextLat, nextLon, heading, hasArrived } = calculateNextPosition(tanker.lat, tanker.lon, tanker.startBase.lat, tanker.startBase.lon, 0.4);
          tanker.lat = nextLat; tanker.lon = nextLon; tanker.heading = heading;
          tMarker.setLatLng([nextLat, nextLon]);
          tMarker.setRotationAngle(heading - 45);

          if (hasArrived) {
            map.removeLayer(tMarker); // marker silinir
            tankerFlight.value = null;
            tankerMarker.value = null;
          }
        }
      }

      // 1. Durum: Acil durum aktif (En yakın havalimanına acil iniş)
      if (isEmergency.value && nearestAirport.value) {
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
          plane.status = 'EMERGENCY_LANDED'; plane.energy = 100; plane.ammo = 2;
          if (emergencyRoute.value) { map.removeLayer(emergencyRoute.value); emergencyRoute.value = null; }
          Swal.fire({
            title: 'ACİL İNİŞ YAPILDI',
            text: 'İniş başarılı, ikmal tamamlandı.',
            icon: 'info', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false
          });
        }
        // 2. Durum: Hedef havalimanına veya görev sahasına gidiş işlemi
      } else if (plane.status === 'GOING_TO_DEP' || plane.status === 'GOING_TO_DEST' || plane.status === 'MISSION_COMPLETE') {
        const targetPos = plane.status === 'GOING_TO_DEP' ? plane.missionDep : plane.missionDest;
        const dynamicCruiseAlt = Math.min(10000, Math.max(1000, (plane.trip_distance || 100) * 100));  // Yolun uzunluğuna göre rakım belirleme

        // Hedefe yaklaşıldığında veya bekleme modundayken rakım ve hız korunur
        const keepFlightEnv = plane.status === 'GOING_TO_DEST' || plane.status === 'MISSION_COMPLETE';
        const { arrived, distToTarget } = updatePlanePhysics(plane, icao, currentPos, targetPos, 220, dynamicCruiseAlt, keepFlightEnv);

        if (missionPathLayer.value) {
          const progressLine = missionPathLayer.value.getLayers().find(l => l instanceof L.Polyline && !l.options.dashArray);
          if (progressLine) progressLine.addLatLng([plane.lat, plane.lon]);
        }
        map.panTo([plane.lat, plane.lon]);

        // Hedefe gidiş kontrolü
        if (plane.status === 'GOING_TO_DEP') {
          if (arrived || distToTarget < 0.1) {
            plane.status = 'GOING_TO_DEST';
            if (missionPathLayer.value) {
              const progressLine = missionPathLayer.value.getLayers().find(l => l instanceof L.Polyline && !l.options.dashArray);
              if (progressLine) progressLine.setStyle({ color: '#2ecc71', dashArray: null });
            }
          }
        } else if (plane.status === 'GOING_TO_DEST') {
          // Hedefe 1.0 birim (1km) kala mühimmatı bırak ve patlat
          if (distToTarget < 1.0) {
            triggerExplosion(plane.lat, plane.lon, map);
            if (plane.ammo > 0) plane.ammo--;
            plane.status = 'MISSION_COMPLETE';

            Swal.fire({
              title: 'HEDEF İMHA EDİLDİ', html: `Birim: <b>${plane.callsign}</b><br>Görev Tamamlandı, Üsse Dönülüyor!`,
              icon: 'success', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false, timerProgressBar: true
            });

            // 3 saniye sonra dönüş  
            setTimeout(() => {
              if (currentFlights.value[icao] && currentFlights.value[icao].status === 'MISSION_COMPLETE') {
                returnToStart();
              }
            }, 3000);
          }
        }
        // 3. Durum: Görev bitti (imha) / iptal edildi, ana üsse (başlangıç koordinatlarına) geri dönüş
      } else if (isReturningToStart.value) {
        const targetPos = { lat: path[0].lat, lon: path[0].lon };
        // Dönüş yolu uzunluğuna göre dinamik irtifa hesapla
        const dynamicReturnAlt = Math.min(10000, Math.max(1000, (plane.trip_distance || 100) * 100));

        // Yolun %70'i tamamlandığında veya kalan mesafe 40 km'nin altına düştüğünde alçalmaya başla
        const descentDist = Math.max(40, (plane.trip_distance || 0) * 0.3);
        const { arrived, distToTarget } = updatePlanePhysics(plane, icao, currentPos, targetPos, 220, dynamicReturnAlt, false, descentDist);

        if (arrived || distToTarget < 0.1) {
          plane.velocity = 0; plane.baroaltitude = 0; plane.status = 'STANDBY'; plane.energy = 100; plane.ammo = 2;
          isReturningToStart.value = false; isPaused.value = true; animationSteps.value[icao] = 0; plane.distance_from_dep = 0;
          drawFullRoute(icao);
          Swal.fire({ title: 'Üsse Dönüldü', text: 'İkmal tamamlandı.', icon: 'info', toast: true, position: 'top-end', timer: 3000, showConfirmButton: false });
        }
        // 4. Durum: Haritada manuel olarak tanımlanan özel bir rotaya/koordinata uçuş
      } else if (isManualRouting.value && manualTarget.value) {
        const targetPos = { lat: manualTarget.value.lat, lon: manualTarget.value.lon };
        // Manuel rotada da mesafeye göre irtifayı dinamik ve orantılı belirle
        const dynamicManualAlt = Math.min(1500, Math.max(500, (plane.total_manual_dist || 15) * 100));
        // Manuel rotada da ortak fizik ve yumuşak alçalma/yükselme mantığını kullan
        const { arrived, distToTarget: remainingDist } = updatePlanePhysics(plane, icao, currentPos, targetPos, 160, dynamicManualAlt);

        if (missionPathLayer.value) {
          const progressLine = missionPathLayer.value.getLayers().find(l => l instanceof L.Polyline && !l.options.dashArray);
          if (progressLine) progressLine.addLatLng([plane.lat, plane.lon]);
        }

        if (arrived || remainingDist < 0.1) {
          plane.velocity = 0; plane.baroaltitude = 0; plane.distance_from_dep = plane.trip_distance;
          isPaused.value = true; isManualRouting.value = false; manualTarget.value = null;
          plane.status = 'ARRIVED';
          plane.energy = 100; plane.ammo = 2;
          if (missionPathLayer.value) { map.removeLayer(missionPathLayer.value); missionPathLayer.value = null; }
          Swal.fire({
            title: 'HEDEFE VARILDI', html: `Birim: <b>${plane.callsign || 'Bilinmeyen'}</b><br>Manuel Rota ve ikmal tamamlandı.`,
            icon: 'success', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false, timerProgressBar: true
          });
        }
        // 5. Durum: Tanımlanmış rota noktaları olan standart JSON uçuş rotasında ilerleme
      } else if (path && path.length > 0) {
        const step = animationSteps.value[icao] || 0;
        if (step + 1 >= path.length) {
          plane.velocity = 0;
          plane.baroaltitude = 0;
          plane.status = 'COMPLETED';
          plane.energy = 100;
          plane.ammo = 2;
          Swal.fire({ title: 'GÖREV TAMAMLANDI', text: 'İkmal yapıldı.', icon: 'info', toast: true, position: 'top-end', timer: 3000, showConfirmButton: false });
          return;
        }
        const nextPoint = path[step + 1];
        const arrived = movePlane(icao, nextPoint.lat, nextPoint.lon, Math.max(plane.velocity, 20) / 2000);
        plane.distance_from_dep = (path[step].distance_from_dep || 0) + getDistance({ lat: path[step].lat, lon: path[step].lon }, currentPos);

        // JSON rotasında hedef noktanın hız ve rakımına yumuşak geçiş
        plane.velocity += (nextPoint.velocity - plane.velocity) * 0.05;
        plane.baroaltitude += (nextPoint.baroaltitude - plane.baroaltitude) * 0.05;

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
