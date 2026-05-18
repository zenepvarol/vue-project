<!-- MapEngine.vue - Merkezi Harita ve Simülasyon Motoru
     Bu bileşen, İHA uçuş fiziği, dinamik rota hesaplamaları ve gerçek zamanlı telemetri senkronizasyonunu yönetir.
     Dinamik Hedef Takip Sistemi (DHTS) kapsamında, aktif görev rotaları ve uçuş niyetleri harita üzerinde 
     hem pilot hem de izleyiciler için eşzamanlı olarak görselleştirilir. -->
<template>
  <div id="map"></div>
  <MapLayers :map="mapObject" @airports-loaded="data => airports = data" />
  <MapPlanes ref="mapPlanes" :map="mapObject" :currentFlights="currentFlights" :activeIcao="activeIcao"
    :myFleetIcaos="myFleetIcaos" @marker-click="focusFlight" />
  <MapRoutes ref="mapRoutes" :map="mapObject" :currentFlights="currentFlights" :activeIcao="activeIcao"
    :flightPaths="flightPaths" :animationSteps="animationSteps" />
  <MapTanker ref="mapTanker" :map="mapObject" :currentFlights="currentFlights" :airports="airports" />
  <MapEmergency ref="mapEmergency" v-model:isEmergencySimulated="isEmergencySimulated" v-model:isEmergency="isEmergency"
    v-model:emergencyCountdown="emergencyCountdown" v-model:activeFailure="activeFailure" v-model:isPaused="isPaused"
    :selectedFlight="selectedFlight" :nearestAirport="nearestAirport" :map="mapObject" :mapRoutes="mapRoutes" />
  <MapMission ref="mapMission" v-model:destinationAirportId="destinationAirportId" v-model:destLat="destLat"
    v-model:destLon="destLon" v-model:activeIcao="activeIcao" v-model:isPaused="isPaused"
    :currentFlights="currentFlights" :airports="airports" :myFleetIcaos="myFleetIcaos" :map="mapObject"
    :mapRoutes="mapRoutes" />
  <MapManualControl ref="mapManualControl" :airports="airports" :currentFlights="currentFlights" :mapRoutes="mapRoutes"
    v-model:activeIcao="activeIcao" v-model:manualLat="manualLat" v-model:manualLon="manualLon"
    v-model:manualAirportId="manualAirportId" v-model:isManualRouting="isManualRouting"
    v-model:manualTarget="manualTarget" v-model:isPaused="isPaused" />
  <MapNavigator ref="mapNavigator" :map="mapObject" :mapRoutes="mapRoutes" :selectedFlight="selectedFlight"
    v-model:activeIcao="activeIcao" v-model:isPaused="isPaused" />
</template>

<script setup>
import { onMounted, ref, computed, watch } from 'vue';
import { useFlightStore } from '@/stores/flightStore';
import { useAuthStore } from '@/stores/authStore';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-rotatedmarker';
import 'leaflet.marker.slideto';
import Swal from 'sweetalert2';
import { useMissionLogger } from '@/composables/useMissionLogger';
import { useEmergencySystem } from '@/composables/useEmergencySystem';
import { useMissionControl } from '@/composables/useMissionControl';
import { useFlightPhysics } from '@/composables/useFlightPhysics';
import { getDistance } from '@/utils/physics';
import { getPlaneIcon } from '@/utils/mapVisuals';
import { FLIGHT_STATUS, SIM_SETTINGS } from '@/constants/flightConstants';
import MapTanker from '../Features/MapTanker.vue';
import MapLayers from './MapLayers.vue';
import MapPlanes from '../Visuals/MapPlanes.vue';
import MapRoutes from '../Visuals/MapRoutes.vue';
import MapEmergency from '../Features/MapEmergency.vue';
import MapMission from '../Features/MapMission.vue';
import MapManualControl from '../Features/MapManualControl.vue';
import MapNavigator from './MapNavigator.vue';
import { telemetryService } from '@/api/telemetryService';

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
const { movePlane, updatePlanePhysics } = useFlightPhysics(currentFlights, mapPlanes, mapRoutes, isEmergency);
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

const store = useFlightStore();
const authStore = useAuthStore();

// CANLI İZLEME (Watcher)
// Backend'den gelen telemetri verileri değiştikçe haritadaki uçakları günceller.
watch(() => store.telemetryFlights, (newTelemetry) => {
  Object.values(newTelemetry).forEach(f => {
    const icao = String(f.icao);
    
    const isSimulatingHere = authStore.user?.role?.toLowerCase() === 'admin' && !isPaused.value && activeIcao.value === icao;
    if (isSimulatingHere) return;

    const existingPlane = currentFlights.value[icao];
    const newPos = [f.lat, f.lon];

    if (!existingPlane) {
      // Radar ekranına yeni giren uçağı ekle
      currentFlights.value[icao] = {
        ...f,
        icao24: icao,
        isRemote: true,
        status: f.status || 'ACTIVE'
      };
    } else {
      // Eğer konum değişmemişse gereksiz animasyon başlatma (Takılmayı önlemek iin)
      const isPositionSame = existingPlane.lat === f.lat && existingPlane.lon === f.lon;
      
      existingPlane.lat = f.lat;
      existingPlane.lon = f.lon;
      existingPlane.velocity = f.velocity;
      existingPlane.baroaltitude = f.altitude;
      existingPlane.heading = f.heading;
      existingPlane.energy = f.energy;
      existingPlane.status = f.status;
      existingPlane.isRemote = true; // Uzaktan takip edildiğini her güncellemede teyit et
      existingPlane.missionDestName = f.destName;
      existingPlane.modelType = f.modelType;

      // HARİTADA CANLI ROTA ÇİZİMİ (Hedef Çizgisi)
      mapRoutes.value?.updateRemoteMissionRoute(icao, f.lat, f.lon, f.destLat, f.destLon);

      // HARİTADA AKICI HAREKET: Sadece konum değiştiğinde kaydır
      const marker = mapPlanes.value?.markers[icao];
      if (marker && !isPositionSame) {
        // 500ms'lik veri periyoduna uygun 600ms'lik yumuşak geçiş
        marker.slideTo(newPos, { duration: 600 }); 
        marker.setRotationAngle(f.heading - 45);
      }
    }
  });

  // Telemetriden silinen (inen) uçakları haritadan kaldır
  Object.keys(currentFlights.value).forEach(icao => {
    if (currentFlights.value[icao].isRemote && !newTelemetry[icao]) {
      delete currentFlights.value[icao];
      mapRoutes.value?.updateRemoteMissionRoute(icao, null, null, null, null); // Rotayı temizle
    }
  });
}, { deep: true });

// Bileşen ayağa kalktığında Leaflet haritasını oluşturur ve JSON verilerini yükler
onMounted(async () => {
  // Dünya sınırlarını sabitleyip temel haritayı çizer
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180));
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);
  mapObject.value = map;

  // Simülasyon döngüsü: Uçakların hareketini, hızlanmasını ve görev kontrollerini periyodik denetler
  setInterval(() => {

    const icao = activeIcao.value;
    // --- CANLI RADAR SORGUSU (Her 500ms'de bir diğer uçakları kontrol et) ---
    const now = Date.now();
    if (!window._lastTelemetryFetch || now - window._lastTelemetryFetch > 500) {
      window._lastTelemetryFetch = now;
      const store = useFlightStore();
      store.fetchActiveTelemetry(); // Backend'deki tüm uçuşları çek
    }

    if (!icao || isPaused.value || !currentFlights.value[icao]) return;
    if (authStore.user?.role?.toLowerCase() !== 'admin') return;

    // Eksik olan rota veya adım verilerini çalışma anında tamamla (Lazy Initialization)
    if (!flightPaths.value[icao]) flightPaths.value[icao] = [{ lat: currentFlights.value[icao].lat, lon: currentFlights.value[icao].lon }];
    if (animationSteps.value[icao] === undefined) animationSteps.value[icao] = 0;

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

    // --- Canlı Telemetri Senkronizasyonu (500ms periyotlarla Backend'e veri iletimi) ---
    if (!plane.lastTelemetryUpdate || Date.now() - plane.lastTelemetryUpdate > 500) {
      plane.lastTelemetryUpdate = Date.now();
      // Standby haricindeki uçuş durumlarında güncel verileri ilet
      if (plane.status !== FLIGHT_STATUS.STANDBY && plane.status !== FLIGHT_STATUS.COMPLETED && 
          plane.status !== FLIGHT_STATUS.EMERGENCY_LANDED && plane.status !== FLIGHT_STATUS.ARRIVED) {
        telemetryService.updateTelemetry({
          icao: icao,
          lat: plane.lat,
          lon: plane.lon,
          velocity: plane.velocity,
          energy: plane.energy,
          altitude: plane.baroaltitude,
          heading: plane.heading,
          status: plane.status,
          callsign: plane.callsign || 'Bilinmeyen',
          destLat: finalTarget?.lat || null,
          destLon: finalTarget?.lon || null,
          destName: plane.missionDestName || null, // Hedef ismini gönder
          modelType: plane.modelType || null // Model bilgisini gönder
        }).catch(err => console.error('Telemetri verisi iletilemedi:', err));
      }
    }
    // -----------------------------------------------------------------------------------------

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
          telemetryService.endMission(icao).catch(()=>{}); // Telemetriden sil
        } else {
          plane.status = FLIGHT_STATUS.EMERGENCY_LANDED;
          const landingSpot = nearestAirport.value?.id || "ACIL";
          plane.missionDestName = landingSpot; // Sonraki uçuş için kalkış noktası ismi güncellenir
          logFlightRecord(plane, landingSpot);
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
          logFlightRecord(plane, plane.missionDestName || "Görev Sahası");
        }
      } else if (plane.status === FLIGHT_STATUS.GOING_TO_DEST) {
        // Hedefe 1.0 birim (1km) kala mühimmatı bırak ve patlat
        if (distToTarget < SIM_SETTINGS.EXPLOSION_THRESHOLD_KM) {
          triggerExplosion(plane.lat, plane.lon);
          if (plane.ammo > 0) plane.ammo--;
          plane.status = FLIGHT_STATUS.MISSION_COMPLETE;
          logFlightRecord(plane, plane.missionDestName || "Hedef");

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
        telemetryService.endMission(icao).catch(()=>{}); // Telemetriden sil
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
        logFlightRecord(plane, plane.missionDestName || "Manuel Hedef");
        mapRoutes.value?.clearAllRoutes();
        telemetryService.endMission(icao).catch(()=>{}); // Telemetriden sil
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
        telemetryService.endMission(icao).catch(()=>{}); // Telemetriden sil
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