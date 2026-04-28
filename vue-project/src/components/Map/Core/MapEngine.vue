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
  <MapManualControl
    ref="mapManualControl"
    :airports="airports"
    :currentFlights="currentFlights"
    :mapRoutes="mapRoutes"
    v-model:activeIcao="activeIcao"
    v-model:manualLat="manualLat"
    v-model:manualLon="manualLon"
    v-model:manualAirportId="manualAirportId"
    v-model:isManualRouting="isManualRouting"
    v-model:manualTarget="manualTarget"
    v-model:isPaused="isPaused"
  />
  <MapNavigator
    ref="mapNavigator"
    :map="mapObject"
    :mapRoutes="mapRoutes"
    :selectedFlight="selectedFlight"
    v-model:activeIcao="activeIcao"
    v-model:isPaused="isPaused"
  />
</template>

<script setup>
import { onMounted, ref, computed } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-rotatedmarker';
import 'leaflet.marker.slideto';
import Swal from 'sweetalert2';
import { useMissionLogger } from '@/composables/useMissionLogger';
import { useEmergencySystem } from '@/composables/useEmergencySystem';
import { useMissionControl } from '@/composables/useMissionControl';
import { getDistance, calculateNextPosition, interpolateSlerp } from '@/utils/physics';
import { getPlaneIcon } from '@/utils/mapVisuals';
import { FLIGHT_STATUS, SIM_SETTINGS } from '@/constants/flightConstants';
import { ucakService } from '@/api/ucakService';
import MapTanker from '../Features/MapTanker.vue';
import MapLayers from './MapLayers.vue';
import MapPlanes from '../Visuals/MapPlanes.vue';
import MapRoutes from '../Visuals/MapRoutes.vue';
import MapEmergency from '../Features/MapEmergency.vue';
import MapMission from '../Features/MapMission.vue';
import MapManualControl from '../Features/MapManualControl.vue';
import MapNavigator from './MapNavigator.vue';

const props = defineProps({ myFleetIcaos: Array, selectedFlight: Object });
const currentFlights = defineModel('currentFlights'), activeIcao = defineModel('activeIcao'), isPaused = defineModel('isPaused');
const animationSteps = defineModel('animationSteps'), airports = defineModel('airports'), isEmergency = defineModel('isEmergency');
const isReturningToStart = defineModel('isReturningToStart'), isEmergencySimulated = defineModel('isEmergencySimulated'), emergencyCountdown = defineModel('emergencyCountdown');
const manualLat = defineModel('manualLat'), manualLon = defineModel('manualLon'), manualAirportId = defineModel('manualAirportId');
const destinationAirportId = defineModel('destinationAirportId'), destLat = defineModel('destLat'), destLon = defineModel('destLon'), activeFailure = defineModel('activeFailure');

const flightPaths = ref({}), manualTarget = ref(null);
let map = null; const isManualRouting = ref(false), mapTanker = ref(null), mapPlanes = ref(null), mapRoutes = ref(null), mapEmergency = ref(null), mapMission = ref(null), mapManualControl = ref(null), mapNavigator = ref(null);
const mapObject = ref(null);

const { logFlightRecord } = useMissionLogger();
const { nearestAirport, startEmergencyLanding, triggerSimulatedFailure, handleManualEmergency } = useEmergencySystem(props, airports, mapEmergency);
const { resetActivePath, drawMissionRoute, returnToStart, assignMission, triggerExplosion, setManualTarget } = useMissionControl({
  activeIcao, currentFlights, props, flightPaths, isReturningToStart, isEmergency, isPaused, isManualRouting, mapRoutes, mapMission, mapManualControl
});


// Verilen uçağı hedeflenen koordinata doğru ilerletir ve dönüş açısını ayarlar
const movePlane = (icao, targetLat, targetLon, moveStep = 0) => {
  const plane = currentFlights.value[icao];
  const marker = mapPlanes.value?.markers[String(icao)]; 
  if (!plane) return false;

  let nextLat, nextLon, heading, hasArrived;

  // Eğer uçağın kavisli rota için başlangıç noktası (startLat/startLon) ve mesafesi belliyse Slerp uygula
  if (moveStep > 0 && plane.startLat !== undefined && plane.startLon !== undefined && plane.trip_distance > 0) {
    const startPos = { lat: plane.startLat, lon: plane.startLon };
    const targetPos = { lat: targetLat, lon: targetLon };
    
    // Kümülatif mesafe üzerinden ilerleme oranını (t) hesapla
    const nextDist = plane.distance_from_dep + moveStep;
    const t = Math.min(1, nextDist / plane.trip_distance);
    
    // Slerp ile kavis üzerindeki gerçek noktayı bul
    const nextPoint = interpolateSlerp(startPos, targetPos, t);
    
    nextLat = nextPoint.lat;
    nextLon = nextPoint.lon;
    
    // Yönelim (heading) hesabı
    const dx = nextLat - plane.lat;
    const dy = nextLon - plane.lon;
    heading = (Math.atan2(dy, dx) * (180 / Math.PI));
    
    // Hedefe ulaşıldı mı kontrolü
    hasArrived = t >= 1 || getDistance({ lat: plane.lat, lon: plane.lon }, targetPos) <= moveStep;
  } else {
    // Slerp için veri yoksa doğrusal (eski) yöntemi kullan
    const result = calculateNextPosition(plane.lat, plane.lon, targetLat, targetLon, moveStep, plane.heading || 0);
    nextLat = result.nextLat;
    nextLon = result.nextLon;
    heading = result.heading;
    hasArrived = result.hasArrived;
  }

  if (hasArrived) {
    return true;
  }

  plane.lat = nextLat;
  plane.lon = nextLon;
  plane.heading = heading;
  const newPos = [nextLat, nextLon];


  // Marker varsa görseli güncelle, yoksa sadece veri güncellenmiş olur
  if (marker) {
    if (moveStep === 0) {
      marker.slideTo(newPos, { duration: 100 });
    } else {
      marker.setLatLng(newPos);
    }
    marker.setRotationAngle(heading - 45);
  }
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


const zoomToPlane = (lat, lon) => mapNavigator.value?.zoomToPlane(lat, lon);
const recenterMap = () => mapNavigator.value?.recenterMap();

const togglePause = () => {
  if (!activeIcao.value) return;
  const plane = currentFlights.value[activeIcao.value];
  if (isPaused.value) {
    isPaused.value = false;
    isReturningToStart.value = false;
    isEmergency.value = false;
    if (plane && (plane.status === FLIGHT_STATUS.STANDBY || plane.status === FLIGHT_STATUS.COMPLETED || plane.status === FLIGHT_STATUS.ARRIVED || plane.status === FLIGHT_STATUS.EMERGENCY_LANDED)) {
      plane.status = FLIGHT_STATUS.ACTIVE;
    }
  }
};

const clearAllRoutes = () => {
  mapRoutes.value?.clearAllRoutes();
};

const drawFullRoute = (icao) => {
  mapRoutes.value?.drawFullRoute(icao);
};

const focusFlight = (f) => mapNavigator.value?.focusFlight(f);


// Bileşen ayağa kalktığında Leaflet haritasını oluşturur ve JSON verilerini yükler
onMounted(async () => {
  // Dünya sınırlarını sabitleyip temel haritayı çizer
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180));
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);
  mapObject.value = map;

  try {
    // ADIM 1: Veri Kaynağı Entegrasyonu
    // Tüm uçak verileri (konum, hız, yakıt vb.) backend servisinden (ucakService) canlı olarak çekilir.
    const ucakResponse = await ucakService.getUcaklar();
    const ucaklar = ucakResponse.data;
    const grouped = {};
    
    ucaklar.forEach(ucak => {
      const icao = String(ucak.icao24).toUpperCase();
      
      // ADIM 2: İlk Konumlandırma
      // Uçaklar simülasyon başlangıcında API'den gelen güncel koordinatlarına yerleştirilir.
      const startPos = { 
        lat: ucak.latitude, 
        lon: ucak.longitude,
        velocity: ucak.speed || 0,
        baroaltitude: ucak.baroaltitude || 0
      };
      
      // Her uçak için başlangıçta mevcut konumunu içeren bir rota listesi oluşturulur.
      // Bu liste, uçuş görevi atandığında kavisli rota noktalarıyla doldurulacaktır.
      grouped[icao] = [startPos];

      // ADIM 3: Operasyonel Verilerin Başlatılması
      // Harita üzerindeki her bir birimin durumu (Yakıt, mühimmat, uçuş statüsü) reaktif olarak tanımlanır.
      const isEnvanter = props.myFleetIcaos.includes(icao);
      currentFlights.value[icao] = {
        ...startPos,
        energy: ucak.fuel || 100,
        total_mission_dist: 0,
        trip_distance: 0,
        distance_from_dep: 0,
        ammo: (isEnvanter || ucak.isSiha) ? 2 : 0,
        isSiha: ucak.isSiha,
        status: isEnvanter ? FLIGHT_STATUS.STANDBY : FLIGHT_STATUS.ON_MISSION
      };
      // Animasyonun mevcut adım takibi 0dan baslar
      animationSteps.value[icao] = 0;
    });
    // Gruplanan rota verileri harita katmanına aktarılır.
    flightPaths.value = grouped;

    // Simülasyon döngüsü: Uçakların hareketini, hızlanmasını ve görev kontrollerini periyodik denetler
    setInterval(() => {
      const icao = activeIcao.value;
      if (!icao || isPaused.value || !currentFlights.value[icao]) return;

      const plane = currentFlights.value[icao];
      const path = flightPaths.value[icao];
      const currentPos = { lat: plane.lat, lon: plane.lon };

      // TB3 gerçekçiliği için yakıt tüketimi (100% = 5700 km menzil) (0.01 di guncellendi)
      if (plane.energy > 0 && plane.velocity > 0) plane.energy = Math.max(0, plane.energy - SIM_SETTINGS.FUEL_CONSUMPTION_RATE);
      if (plane.energy < SIM_SETTINGS.LOW_FUEL_THRESHOLD && !isEmergencySimulated.value && !isEmergency.value && !isReturningToStart.value) triggerSimulatedFailure();

      /** Uçağın o anki uçuş moduna göre (Acil iniş, görev, manuel rota) gitmek istediği asıl hedef noktasını tespit eder. 
       * Bu hedef, yakıtın yetip yetmeyeceğini hesaplamak için kullanılır.*/
      const getActivePlaneTarget = () => {
        if (isEmergency.value && nearestAirport.value) return nearestAirport.value; // Acil iniş durumunda hedef: en yakın havalimanı
        if (plane.status === FLIGHT_STATUS.GOING_TO_DEP) return plane.missionDep; // Görev kalkış noktasına gidiliyorsa hedef odur
        if (plane.status === FLIGHT_STATUS.GOING_TO_DEST || plane.status === FLIGHT_STATUS.MISSION_COMPLETE) {
          return plane.status === FLIGHT_STATUS.GOING_TO_DEST ? plane.missionDest : (path && path.length > 0 ? { lat: path[0].lat, lon: path[0].lon } : { lat: plane.homeLat, lon: plane.homeLon });
        }
        if (isReturningToStart.value) return path && path.length > 0 ? { lat: path[0].lat, lon: path[0].lon } : { lat: plane.homeLat, lon: plane.homeLon };
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
        const oldPos = { lat: plane.lat, lon: plane.lon };
        const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
        plane.distance_from_dep += getDistance(oldPos, { lat: plane.lat, lon: plane.lon });

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
          const homePos = path && path.length > 0 ? { lat: path[0].lat, lon: path[0].lon } : { lat: plane.homeLat, lon: plane.homeLon };
          const distToHome = getDistance({ lat: plane.lat, lon: plane.lon }, homePos);
          if (distToHome < 0.5) {
            plane.status = FLIGHT_STATUS.STANDBY;
            animationSteps.value[icao] = 0;
            logFlightRecord(plane, "Ana Merkez (Acil)");
          } else {
            plane.status = FLIGHT_STATUS.EMERGENCY_LANDED;
            logFlightRecord(plane, nearestAirport.value?.name || "Acil İniş Noktası");
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
        const keepFlightEnv = plane.status === FLIGHT_STATUS.GOING_TO_DEST || plane.status === FLIGHT_STATUS.MISSION_COMPLETE;
        const { arrived, distToTarget } = updatePlanePhysics(plane, icao, currentPos, targetPos, SIM_SETTINGS.DEFAULT_CRUISE_SPEED, dynamicCruiseAlt, keepFlightEnv);

        mapRoutes.value?.updateMissionProgress(plane);
        map.panTo([plane.lat, plane.lon]);

        // Hedefe gidiş kontrolü
        if (plane.status === FLIGHT_STATUS.GOING_TO_DEP) {
          if (arrived || distToTarget < 0.1) {
            plane.status = FLIGHT_STATUS.GOING_TO_DEST;
            mapRoutes.value?.setMissionSuccess();
            logFlightRecord(plane, plane.missionDest?.name || "Görev Sahası");
          }
        } else if (plane.status === FLIGHT_STATUS.GOING_TO_DEST) {
          // Hedefe 1.0 birim (1km) kala mühimmatı bırak ve patlat
          if (distToTarget < SIM_SETTINGS.EXPLOSION_THRESHOLD_KM) {
            triggerExplosion(plane.lat, plane.lon);
            if (plane.ammo > 0) plane.ammo--;
            plane.status = FLIGHT_STATUS.MISSION_COMPLETE;

            Swal.fire({
              title: 'HEDEF İMHA EDİLDİ', html: `Birim: <b>${plane.callsign}</b><br>Görev Tamamlandı, Üsse Dönülüyor!`,
              icon: 'success', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false, timerProgressBar: true
            });

            // 3 saniye sonra dönüş  
            setTimeout(() => {
              if (currentFlights.value[icao] && currentFlights.value[icao].status === FLIGHT_STATUS.MISSION_COMPLETE) {
                returnToStart();
              }
            }, 3000);
          }
        }
        // 3. Durum: Görev bitti (imha) / iptal edildi, ana üsse (başlangıç koordinatlarına) geri dönüş
      } else if (isReturningToStart.value) {
        // Rota yoksa (API uçağı), uçağın ilk eklendiği koordinata (homeLat) dön
        const targetPos = path && path.length > 0 
          ? { lat: path[0].lat, lon: path[0].lon } 
          : { lat: plane.homeLat, lon: plane.homeLon };
        // Dönüş yolu uzunluğuna göre dinamik irtifa hesapla
        const dynamicReturnAlt = Math.min(10000, Math.max(1000, (plane.trip_distance || 100) * 100));

        // Yolun %70'i tamamlandığında veya kalan mesafe 40 km'nin altına düştüğünde alçalmaya başla
        const descentDist = Math.max(40, (plane.trip_distance || 0) * 0.3);
        const { arrived, distToTarget } = updatePlanePhysics(plane, icao, currentPos, targetPos, SIM_SETTINGS.DEFAULT_CRUISE_SPEED, dynamicReturnAlt, false, descentDist);

        if (arrived || distToTarget < 0.1) {
          plane.velocity = 0; plane.baroaltitude = 0; plane.status = FLIGHT_STATUS.STANDBY; plane.energy = 100; plane.ammo = 2;
          isReturningToStart.value = false; isPaused.value = true; animationSteps.value[icao] = 0;
          logFlightRecord(plane, "Ana Merkez");
          drawFullRoute(icao);
          Swal.fire({ title: 'Üsse Dönüldü', text: 'İkmal tamamlandı.', icon: 'info', toast: true, position: 'top-end', timer: 3000, showConfirmButton: false });
        }
        // 4. Durum: Haritada manuel olarak tanımlanan özel bir rotaya/koordinata uçuş
      } else if (isManualRouting.value && manualTarget.value) {
        const targetPos = { lat: manualTarget.value.lat, lon: manualTarget.value.lon };
        // Manuel rotada da mesafeye göre irtifayı dinamik ve orantılı belirle
        const dynamicManualAlt = Math.min(1500, Math.max(500, (plane.total_manual_dist || 15) * 100));
        // Manuel rotada da ortak fizik ve yumuşak alçalma/yükselme mantığını kullan
        const { arrived, distToTarget: remainingDist } = updatePlanePhysics(plane, icao, currentPos, targetPos, SIM_SETTINGS.MANUAL_CRUISE_SPEED, dynamicManualAlt);

        mapRoutes.value?.updateMissionProgress(plane);

        if (arrived || remainingDist < 0.1) {
          plane.velocity = 0; plane.baroaltitude = 0;
          isPaused.value = true; isManualRouting.value = false; manualTarget.value = null;
          plane.status = FLIGHT_STATUS.ARRIVED;
          plane.energy = 100; plane.ammo = 2;
          logFlightRecord(plane, manualTarget.value?.name || "Manuel Hedef");
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
          plane.status = FLIGHT_STATUS.COMPLETED;
          plane.energy = 100;
          plane.ammo = 2;
          isPaused.value = true;
          animationSteps.value[icao] = 0;
          logFlightRecord(plane, "Rota Sonu");
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
    }, SIM_SETTINGS.UPDATE_INTERVAL_MS);
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