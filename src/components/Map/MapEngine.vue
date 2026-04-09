<template>
  <div id="map"></div>
  <MapLayers :map="mapObject" @airports-loaded="data => airports = data" />
  <MapPlanes ref="mapPlanes" :map="mapObject" :currentFlights="currentFlights" :activeIcao="activeIcao"
    :myFleetIcaos="myFleetIcaos" @marker-click="focusFlight" />
  <MapRoutes ref="mapRoutes" :map="mapObject" :currentFlights="currentFlights" :activeIcao="activeIcao"
    :flightPaths="flightPaths" :animationSteps="animationSteps" />
  <MapTanker ref="mapTanker" :map="mapObject" :currentFlights="currentFlights" :airports="airports" />
  <MapEmergency 
    ref="mapEmergency"
    v-model:isEmergencySimulated="isEmergencySimulated"
    v-model:isEmergency="isEmergency"
    v-model:emergencyCountdown="emergencyCountdown"
    v-model:activeFailure="activeFailure"
    v-model:isPaused="isPaused"
    :selectedFlight="selectedFlight"
    :nearestAirport="nearestAirport"
    :map="mapObject"
    :mapRoutes="mapRoutes"
  />
  <MapMission
    ref="mapMission"
    v-model:destinationAirportId="destinationAirportId"
    v-model:destLat="destLat"
    v-model:destLon="destLon"
    v-model:activeIcao="activeIcao"
    v-model:isPaused="isPaused"
    :currentFlights="currentFlights"
    :airports="airports"
    :myFleetIcaos="myFleetIcaos"
    :map="mapObject"
    :mapRoutes="mapRoutes"
  />
</template>

<script setup>
import { onMounted, ref, computed } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-rotatedmarker';
import 'leaflet.marker.slideto';
import Swal from 'sweetalert2';
import { getDistance, calculateNextPosition } from '@/utils/physics';
import { getPlaneIcon } from '@/utils/mapVisuals';
import MapTanker from './MapTanker.vue';
import MapLayers from './MapLayers.vue';
import MapPlanes from './MapPlanes.vue';
import MapRoutes from './MapRoutes.vue';
import MapEmergency from './MapEmergency.vue';
import MapMission from './MapMission.vue';

const props = defineProps({ myFleetIcaos: Array, selectedFlight: Object });
const currentFlights = defineModel('currentFlights'), activeIcao = defineModel('activeIcao'), isPaused = defineModel('isPaused');
const animationSteps = defineModel('animationSteps'), airports = defineModel('airports'), isEmergency = defineModel('isEmergency');
const isReturningToStart = defineModel('isReturningToStart'), isEmergencySimulated = defineModel('isEmergencySimulated'), emergencyCountdown = defineModel('emergencyCountdown');
const manualLat = defineModel('manualLat'), manualLon = defineModel('manualLon'), manualAirportId = defineModel('manualAirportId');
const destinationAirportId = defineModel('destinationAirportId'), destLat = defineModel('destLat'), destLon = defineModel('destLon'), activeFailure = defineModel('activeFailure');

const flightPaths = ref({}), manualTarget = ref(null);
let map = null; const isManualRouting = ref(false), mapTanker = ref(null), mapPlanes = ref(null), mapRoutes = ref(null), mapEmergency = ref(null), mapMission = ref(null);
const mapObject = ref(null);


// Verilen uçağı hedeflenen koordinata doğru ilerletir ve dönüş açısını ayarlar
const movePlane = (icao, targetLat, targetLon, moveStep = 0) => {
  const plane = currentFlights.value[icao], marker = mapPlanes.value?.markers[icao];
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
  if (mapRoutes.value?.activeRoutes[icao] && !isEmergency.value) {
    mapRoutes.value.activeRoutes[icao].addLatLng(newPos);
  }
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
  mapRoutes.value?.resetActivePath(icao);
};

// Aktif bir görevi başlattığımızda veya güncellediğimizde rotayı çizer
const drawMissionRoute = (plane, targetPos) => {
  return mapRoutes.value?.drawMissionRoute(plane, targetPos);
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

  mapRoutes.value?.clearAllRoutes();
};

const zoomToPlane = (lat, lon) => {
  if (lat && lon && map) map.setView([lat, lon], 8, { animate: true, duration: 1 });
};

const recenterMap = () => {
  if (props.selectedFlight) zoomToPlane(props.selectedFlight.lat, props.selectedFlight.lon);
};

const togglePause = () => {
  if (!activeIcao.value) return;
  const plane = currentFlights.value[activeIcao.value];
  if (isPaused.value) {
    isPaused.value = false;
    isReturningToStart.value = false;
    isEmergency.value = false;
    if (plane && (plane.status === 'STANDBY' || plane.status === 'COMPLETED' || plane.status === 'ARRIVED' || plane.status === 'EMERGENCY_LANDED')) {
      plane.status = 'ACTIVE';
    }
  }
};

const clearAllRoutes = () => {
  mapRoutes.value?.clearAllRoutes();
};

const drawFullRoute = (icao) => {
  mapRoutes.value?.drawFullRoute(icao);
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
    plane.total_mission_dist = dist; // Orijinal görev mesafesini korumak için
    plane.distance_from_dep = 0;
    plane.total_manual_dist = dist;
    plane.status = 'ACTIVE'; // Operasyon güncellendiğinde durumu anında 'GÖREVDE' yapar
    manualTarget.value = target;
    isManualRouting.value = true;
    isPaused.value = false;

    mapRoutes.value?.drawMissionRoute(plane, target); // Manuel güncellenen rotanın çizimi

    Swal.fire({
      icon: 'success', title: 'Rota Hazır', text: `Mesafe: ${Math.round(dist)} km`,
      timer: 1500, toast: true, position: 'top-end', showConfirmButton: false
    });
  }
};

const startEmergencyLanding = () => mapEmergency.value?.startEmergencyLanding();

const triggerSimulatedFailure = () => mapEmergency.value?.triggerSimulatedFailure();

const handleManualEmergency = () => mapEmergency.value?.handleManualEmergency();

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

const assignMission = () => mapMission.value?.assignMission();
const triggerExplosion = (lat, lon) => mapMission.value?.triggerExplosion(lat, lon);

// Bileşen ayağa kalktığında Leaflet haritasını oluşturur ve JSON verilerini yükler
onMounted(async () => {
  // Dünya sınırlarını sabitleyip temel haritayı çizer
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180));
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);
  mapObject.value = map;

  try {
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
      const lastPoint = grouped[icao][grouped[icao].length - 1];
      const totalPathDist = lastPoint.distance_from_dep || 0;
      const isEnvanter = props.myFleetIcaos.includes(icao.toString());
      
      currentFlights.value[icao] = {
        ...firstPoint,
        velocity: 0,
        baroaltitude: 0,
        energy: 100,
        total_mission_dist: totalPathDist,
        trip_distance: totalPathDist,
        ammo: isEnvanter ? 2 : 0,
        status: isEnvanter ? 'STANDBY' : 'ON_MISSION'
      };
      animationSteps.value[icao] = 0;
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
      if (plane.energy < 20 && !isEmergencySimulated.value && !isEmergency.value && !isReturningToStart.value) mapEmergency.value?.triggerSimulatedFailure();

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
      
      // Tanker mantığını yeni bileşen üzerinden yönet
      if (mapTanker.value) {
        mapTanker.value.update(icao, isPaused.value, finalTarget);
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
          
          // Eğer iniş yapılan nokta uçağın ana merkeziyse (path[0]), direkt hangara (STANDBY) gir
          const distToHome = getDistance({ lat: plane.lat, lon: plane.lon }, { lat: path[0].lat, lon: path[0].lon });
          if (distToHome < 0.5) {
            plane.status = 'STANDBY';
            animationSteps.value[icao] = 0;
            plane.distance_from_dep = 0;
          } else {
            plane.status = 'EMERGENCY_LANDED';
          }

          plane.energy = 100; plane.ammo = 2;
          mapRoutes.value?.clearAllRoutes();
          Swal.fire({
            title: distToHome < 0.5 ? 'ÜSSE ACİL İNİŞ YAPILDI' : 'ACİL İNİŞ YAPILDI',
            text: distToHome < 0.5 ? 'Ana merkeze güvenli iniş yapıldı.' : 'En yakın noktaya güvenli iniş yapıldı.',
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

        mapRoutes.value?.updateMissionProgress(plane);
        map.panTo([plane.lat, plane.lon]);

        // Hedefe gidiş kontrolü
        if (plane.status === 'GOING_TO_DEP') {
          if (arrived || distToTarget < 0.1) {
            plane.status = 'GOING_TO_DEST';
            mapRoutes.value?.setMissionSuccess();
          }
        } else if (plane.status === 'GOING_TO_DEST') {
          // Hedefe 1.0 birim (1km) kala mühimmatı bırak ve patlat
          if (distToTarget < 1.0) {
            mapMission.value?.triggerExplosion(plane.lat, plane.lon);
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

        mapRoutes.value?.updateMissionProgress(plane);

        if (arrived || remainingDist < 0.1) {
          plane.velocity = 0; plane.baroaltitude = 0; plane.distance_from_dep = plane.trip_distance;
          isPaused.value = true; isManualRouting.value = false; manualTarget.value = null;
          plane.status = 'ARRIVED';
          plane.energy = 100; plane.ammo = 2;
          mapRoutes.value?.clearAllRoutes();
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
