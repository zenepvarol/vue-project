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
    :selectedFlight="selectedFlight" :nearestAirport="nearestAirport" :map="mapObject" :mapRoutes="mapRoutes"
    :airports="airports" />
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
import { signalRService } from '@/api/signalRService';

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
const { nearestAirport, getNearestAirport, startEmergencyLanding, triggerSimulatedFailure, handleManualEmergency } = useEmergencySystem(props, airports, mapEmergency);
const { resetActivePath, drawMissionRoute, returnToStart, assignMission, triggerExplosion, setManualTarget } = useMissionControl({
  activeIcao, currentFlights, props, flightPaths, isReturningToStart, isEmergency, isPaused, isManualRouting, mapRoutes, mapMission, mapManualControl
});
const { movePlane, updatePlanePhysics } = useFlightPhysics(currentFlights, mapPlanes, mapRoutes, isEmergency);
const zoomToPlane = (lat, lon) => mapNavigator.value?.zoomToPlane(lat, lon);
const recenterMap = () => mapNavigator.value?.recenterMap();

// Seçili uçak değiştikçe global ref/model durumlarını uçağın kendi durumlarıyla senkronize et
watch(activeIcao, (newIcao) => {
  if (newIcao && currentFlights.value[newIcao]) {
    const plane = currentFlights.value[newIcao];
    isPaused.value = plane.isPaused !== undefined ? plane.isPaused : (plane.status === FLIGHT_STATUS.STANDBY);
    isEmergency.value = plane.isEmergency !== undefined ? plane.isEmergency : false;
    isReturningToStart.value = plane.isReturningToStart !== undefined ? plane.isReturningToStart : false;
    isManualRouting.value = plane.isManualRouting !== undefined ? plane.isManualRouting : false;
    manualTarget.value = plane.manualTarget || null;
  } else {
    isPaused.value = false;
    isEmergency.value = false;
    isReturningToStart.value = false;
    isManualRouting.value = false;
    manualTarget.value = null;
  }
});

// Global ref/model değişikliklerini o anki aktif uçağa yansıt
watch(isPaused, (val) => {
  if (activeIcao.value && currentFlights.value[activeIcao.value]) {
    currentFlights.value[activeIcao.value].isPaused = val;
  }
});
watch(isEmergency, (val) => {
  if (activeIcao.value && currentFlights.value[activeIcao.value]) {
    currentFlights.value[activeIcao.value].isEmergency = val;
  }
});
watch(isReturningToStart, (val) => {
  if (activeIcao.value && currentFlights.value[activeIcao.value]) {
    currentFlights.value[activeIcao.value].isReturningToStart = val;
  }
});
watch(isManualRouting, (val) => {
  if (activeIcao.value && currentFlights.value[activeIcao.value]) {
    currentFlights.value[activeIcao.value].isManualRouting = val;
  }
});
watch(manualTarget, (val) => {
  if (activeIcao.value && currentFlights.value[activeIcao.value]) {
    currentFlights.value[activeIcao.value].manualTarget = val;
  }
});

const togglePause = () => {
  if (!activeIcao.value) return;
  const plane = currentFlights.value[activeIcao.value];
  const currentPauseState = plane ? (plane.isPaused !== undefined ? plane.isPaused : isPaused.value) : isPaused.value;
  
  if (currentPauseState) {
    isPaused.value = false;
    isReturningToStart.value = false;
    isEmergency.value = false;
    if (plane) {
      plane.isPaused = false;
      plane.isReturningToStart = false;
      plane.isEmergency = false;
      if (plane.status === FLIGHT_STATUS.STANDBY || plane.status === FLIGHT_STATUS.COMPLETED || plane.status === FLIGHT_STATUS.ARRIVED || plane.status === FLIGHT_STATUS.EMERGENCY_LANDED) {
        plane.status = FLIGHT_STATUS.ACTIVE;
      }
    }
  } else {
    isPaused.value = true;
    if (plane) {
      plane.isPaused = true;
    }
  }
};

const getHomeAirportName = (plane) => {
  if (!plane) return "Ana Merkez";
  const homeLat = plane.homeLat || plane.lat;
  const homeLon = plane.homeLon || plane.lon;
  
  if (airports.value) {
    const list = Array.isArray(airports.value) ? airports.value : Object.values(airports.value);
    let nearest = null;
    let minDist = Infinity;
    
    list.forEach(apt => {
      if (apt.type === 'Base') return; // Sadece gerçek havalimanı kodlarını eşleştir
      const dist = getDistance({ lat: homeLat, lon: homeLon }, { lat: apt.lat, lon: apt.lon });
      if (dist < minDist) {
        minDist = dist;
        nearest = apt;
      }
    });
    
    if (nearest && minDist < 2.0) {
      return nearest.id || nearest.name;
    }
  }
  return "Ana Merkez";
};

// Uçaklar yüklendiğinde ya da yeni bir uçak eklendiğinde başlangıç havalimanı adını otomatik ata
watch(() => currentFlights.value, (flights) => {
  if (!flights || !airports.value) return;
  Object.values(flights).forEach(plane => {
    if (!plane.homeAirportName) {
      const homeAirport = getHomeAirportName(plane);
      plane.homeAirportName = homeAirport;
      if (!plane.lastDepartureName) {
        plane.lastDepartureName = homeAirport;
      }
    }
  });
}, { deep: true, immediate: true });

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
    const username = authStore.user?.username;
    const isAdmin = authStore.user?.role?.toLowerCase() === 'admin';
    
    // Uçağın bizim tarafımızdan kontrol edilip edilmediğini kontrol et
    const isControlledByMe = f.controlledBy === username ||
                             (!f.controlledBy && isAdmin && (props.myFleetIcaos.map(String).includes(icao) || f.isSiha || f.IsSiha));

    const existingPlane = currentFlights.value[icao];

    // Eğer bu uçağı yerel olarak simüle ediyorsak ve uçak havadaysa telemetry'den gelen koordinatların
    // yerel koordinatları ezmesini engelliyoruz (initial load/standby hariç).
    if (isControlledByMe && existingPlane) {
      const isSimulatingActive = existingPlane.status !== FLIGHT_STATUS.STANDBY &&
                                 existingPlane.status !== FLIGHT_STATUS.COMPLETED &&
                                 existingPlane.status !== FLIGHT_STATUS.EMERGENCY_LANDED &&
                                 existingPlane.status !== FLIGHT_STATUS.ARRIVED &&
                                 !(existingPlane.isPaused !== undefined ? existingPlane.isPaused : (existingPlane.status === FLIGHT_STATUS.STANDBY));

      if (isSimulatingActive) {
        existingPlane.energy = f.energy;
        existingPlane.controlledBy = f.controlledBy;
        existingPlane.modelType = f.modelType;
        return;
      }
    }

    const newPos = [f.lat, f.lon];

    if (!existingPlane) {
      // Radar ekranına yeni giren uçağı ekle
      const planeData = {
        ...f,
        icao24: icao,
        isRemote: !isControlledByMe,
        status: f.status || 'ACTIVE',
        controlledBy: f.controlledBy
      };

      // Eğer sayfa yenilendiyse ve uçak bizim kontrolümüz altındaysa, durumunu ve rotasını restore et
      if (isControlledByMe) {
        planeData.isRemote = false;
        planeData.isPaused = false; // uçuşu devam ettirmek için aktifleşmeli

        if (f.destLat && f.destLon) {
          const destLatLng = { lat: f.destLat, lon: f.destLon };
          if (f.status === 'GOING_TO_DEP') {
            planeData.missionDep = destLatLng;
          } else if (f.status === 'GOING_TO_DEST') {
            planeData.missionDest = destLatLng;
          } else if (f.status === 'RETURNING') {
            planeData.isReturningToStart = true;
          } else if (f.status === 'EMERGENCY') {
            planeData.isEmergency = true;
          } else {
            planeData.isManualRouting = true;
            planeData.manualTarget = destLatLng;
          }
          planeData.missionDestName = f.destName;

          // Yeniden yüklemede harita üzerinde hedef çizgisi çizelim
          setTimeout(() => {
            const updatedPlane = currentFlights.value[icao];
            if (updatedPlane) {
              if (updatedPlane.isEmergency) {
                const nearest = getNearestAirport(updatedPlane.lat, updatedPlane.lon);
                if (nearest) {
                  mapRoutes.value?.setEmergencyRoute([updatedPlane.lat, updatedPlane.lon], [nearest.lat, nearest.lon]);
                }
              } else if (!updatedPlane.isReturningToStart) {
                mapRoutes.value?.drawMissionRoute(updatedPlane, destLatLng);
              }
            }
          }, 100);
        }
      }

      currentFlights.value[icao] = planeData;
    } else {
      const isPositionSame = existingPlane.lat === f.lat && existingPlane.lon === f.lon;
      
      existingPlane.lat = f.lat;
      existingPlane.lon = f.lon;
      existingPlane.velocity = f.velocity;
      existingPlane.baroaltitude = f.altitude;
      existingPlane.heading = f.heading;
      existingPlane.energy = f.energy;
      existingPlane.status = f.status;
      existingPlane.isRemote = !isControlledByMe;
      existingPlane.missionDestName = f.destName;
      existingPlane.modelType = f.modelType;
      existingPlane.controlledBy = f.controlledBy;

      // HARİTADA CANLI ROTA ÇİZİMİ (Hedef Çizgisi)
      if (f.destLat && f.destLon) {
        mapRoutes.value?.updateRemoteMissionRoute(icao, f.lat, f.lon, f.destLat, f.destLon);
      }

      // HARİTADA AKICI HAREKET: Sadece konum değiştiğinde kaydır
      const marker = mapPlanes.value?.markers[icao];
      if (marker && !isPositionSame) {
        marker.slideTo(newPos, { duration: 600 }); 
        marker.setRotationAngle(f.heading - 45);
      }
    }
  });

  // Telemetriden silinen (inen) uçakları haritadan kaldır
  Object.keys(currentFlights.value).forEach(icao => {
    if (currentFlights.value[icao].isRemote && !newTelemetry[icao]) {
      delete currentFlights.value[icao];
      mapRoutes.value?.updateRemoteMissionRoute(icao, null, null, null, null);
    }
  });
}, { deep: true });

onMounted(async () => {
  const worldBounds = L.latLngBounds(L.latLng(-90, -180), L.latLng(90, 180));
  map = L.map('map', { maxBounds: worldBounds, maxBoundsViscosity: 1.0, minZoom: 2 }).setView([20, 0], 2);
  mapObject.value = map;

  signalRService.startConnection();
  signalRService.onNotificationReceived(({ type, title, text, icao }) => {
    const plane = currentFlights.value[icao];
    const isSimulatingHere = authStore.user?.role?.toLowerCase() === 'admin' && !isPaused.value && activeIcao.value === icao;
    const isRemote = plane ? plane.isRemote : true;

    if (isSimulatingHere && !isRemote) {
      return;
    }

    Swal.fire({
      title: title,
      html: text,
      icon: type,
      toast: true,
      position: 'top-end',
      timer: 3500,
      showConfirmButton: false,
      timerProgressBar: true
    });
  });

  // Simülasyon döngüsü: Tüm aktif uçakların hareketini, hızlanmasını ve görev kontrollerini periyodik denetler
  setInterval(() => {
    const now = Date.now();
    if (!window._lastTelemetryFetch || now - window._lastTelemetryFetch > 500) {
      window._lastTelemetryFetch = now;
      const store = useFlightStore();
      store.fetchActiveTelemetry();
    }

    if (authStore.user?.role?.toLowerCase() !== 'admin') return;

    Object.values(currentFlights.value).forEach(plane => {
      const icao = String(plane.icao || plane.icao24);
      if (!icao) return;

      const isControlledByMe = plane.controlledBy === authStore.user?.username || 
                               (!plane.controlledBy && (props.myFleetIcaos.map(String).includes(icao) || plane.isSiha || plane.IsSiha));
      
      if (!isControlledByMe) return;

      const planePaused = plane.isPaused !== undefined ? plane.isPaused : (plane.status === FLIGHT_STATUS.STANDBY);
      if (planePaused) return;

      if (plane.status === FLIGHT_STATUS.STANDBY || plane.status === FLIGHT_STATUS.COMPLETED || 
          plane.status === FLIGHT_STATUS.EMERGENCY_LANDED || plane.status === FLIGHT_STATUS.ARRIVED) {
        return;
      }

      if (!flightPaths.value[icao]) flightPaths.value[icao] = [{ lat: plane.lat, lon: plane.lon }];
      if (animationSteps.value[icao] === undefined) animationSteps.value[icao] = 0;

      const path = flightPaths.value[icao];
      const currentPos = { lat: plane.lat, lon: plane.lon };

      if (plane.energy > 0 && plane.velocity > 0) {
        plane.energy = Math.max(0, plane.energy - SIM_SETTINGS.FUEL_CONSUMPTION_RATE);
      }

      const planeEmergency = plane.isEmergency || false;
      const planeEmergencySimulated = plane.isEmergencySimulated || false;
      const planeReturningToStart = plane.isReturningToStart || false;
      const planeManualRouting = plane.isManualRouting || false;
      const planeManualTarget = plane.manualTarget || null;

      if (plane.energy < SIM_SETTINGS.LOW_FUEL_THRESHOLD && !planeEmergencySimulated && !planeEmergency && !planeReturningToStart) {
        mapEmergency.value?.triggerSimulatedFailure(plane);
      }

      const getActivePlaneTarget = () => {
        if (planeEmergency) {
          return getNearestAirport(plane.lat, plane.lon);
        }
        if (plane.status === FLIGHT_STATUS.GOING_TO_DEP) return plane.missionDep;
        if (plane.status === FLIGHT_STATUS.GOING_TO_DEST || plane.status === FLIGHT_STATUS.MISSION_COMPLETE) {
          return plane.status === FLIGHT_STATUS.GOING_TO_DEST 
            ? plane.missionDest 
            : (path && path.length > 0 ? { lat: path[0].lat, lon: path[0].lon } : { lat: plane.homeLat, lon: plane.homeLon });
        }
        if (planeReturningToStart) {
          return path && path.length > 0 ? { lat: path[0].lat, lon: path[0].lon } : { lat: plane.homeLat, lon: plane.homeLon };
        }
        if (planeManualRouting && planeManualTarget) return planeManualTarget;
        if (path && path.length > 0) return path[path.length - 1];
        return null;
      };

      const finalTarget = getActivePlaneTarget();

      if (mapTanker.value) {
        mapTanker.value.update(icao, planePaused, finalTarget);
      }

      if (!plane.lastTelemetryUpdate || Date.now() - plane.lastTelemetryUpdate > 500) {
        plane.lastTelemetryUpdate = Date.now();
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
            destName: plane.missionDestName || null,
            modelType: plane.modelType || null
          }).catch(err => console.error('Telemetri verisi iletilemedi:', err));
        }
      }

      // 1. Durum: Acil durum aktif (En yakın havalimanına acil iniş)
      if (planeEmergency) {
        const nearest = getNearestAirport(plane.lat, plane.lon);
        if (nearest) {
          const targetPos = { lat: nearest.lat, lon: nearest.lon };
          const dist = getDistance(currentPos, targetPos);

          const stepSize = Math.max(0.05, plane.velocity / 2000);
          const oldPos = { lat: plane.lat, lon: plane.lon };
          const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
          plane.distance_from_dep += getDistance(oldPos, { lat: plane.lat, lon: plane.lon });

          if (!arrived && dist > 0) {
            const stepsToTarget = dist / stepSize;
            if (stepsToTarget > 0) {
              plane.velocity -= (plane.velocity / stepsToTarget);
              plane.baroaltitude -= (plane.baroaltitude / stepsToTarget);
            }
          }
          if (arrived) {
            plane.velocity = 0;
            plane.baroaltitude = 0;
            plane.isEmergency = false;
            plane.isPaused = true;
            
            if (icao === activeIcao.value) {
              isEmergency.value = false;
              isPaused.value = true;
            }

            const homePos = path && path.length > 0 ? { lat: path[0].lat, lon: path[0].lon } : { lat: plane.homeLat, lon: plane.homeLon };
            const distToHome = getDistance({ lat: plane.lat, lon: plane.lon }, homePos);
            const homeName = getHomeAirportName(plane);
            const finalStatus = FLIGHT_STATUS.EMERGENCY_LANDED;
            if (distToHome < 0.5) {
              plane.status = FLIGHT_STATUS.STANDBY;
              animationSteps.value[icao] = 0;
              logFlightRecord(plane, `${homeName} (Acil)`);
            } else {
              plane.status = FLIGHT_STATUS.EMERGENCY_LANDED;
              const landingSpot = nearest.id || "ACIL";
              plane.missionDestName = landingSpot;
              logFlightRecord(plane, landingSpot);
            }

            telemetryService.updateTelemetry({
              icao: icao,
              lat: plane.lat,
              lon: plane.lon,
              velocity: 0,
              energy: 100,
              altitude: 0,
              heading: plane.heading,
              status: finalStatus,
              callsign: plane.callsign || 'Bilinmeyen',
              destLat: null,
              destLon: null,
              destName: plane.missionDestName || null,
              modelType: plane.modelType || null
            }).then(() => {
              telemetryService.endMission(icao).catch(()=>{});
            }).catch(() => {
              telemetryService.endMission(icao).catch(()=>{});
            });

            plane.energy = 100;
            plane.ammo = 2;

            if (icao === activeIcao.value) {
              mapRoutes.value?.clearAllRoutes();
            } else {
              if (mapRoutes.value?.missionPaths[icao] && map) {
                map.removeLayer(mapRoutes.value.missionPaths[icao]);
                delete mapRoutes.value.missionPaths[icao];
              }
            }

            Swal.fire({
              title: distToHome < 0.5 ? `${homeName.toUpperCase()} MEYDANINA ACİL İNİŞ YAPILDI` : 'ACİL İNİŞ YAPILDI',
              text: distToHome < 0.5 ? `${homeName} meydanına güvenli acil iniş yapıldı.` : 'En yakın noktaya güvenli iniş yapıldı.',
              icon: 'info', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false
            });
          }
        }
      }
      // 2. Durum: Hedef havalimanına veya görev sahasına gidiş işlemi
      else if (plane.status === 'GOING_TO_DEP' || plane.status === 'GOING_TO_DEST' || plane.status === 'MISSION_COMPLETE') {
        const targetPos = plane.status === 'GOING_TO_DEP' ? plane.missionDep : plane.missionDest;
        if (targetPos) {
          const dynamicCruiseAlt = Math.min(10000, Math.max(1000, (plane.trip_distance || 100) * 100));
          const keepFlightEnv = plane.status === FLIGHT_STATUS.GOING_TO_DEST || plane.status === FLIGHT_STATUS.MISSION_COMPLETE;
          const { arrived, distToTarget } = updatePlanePhysics(plane, icao, currentPos, targetPos, SIM_SETTINGS.DEFAULT_CRUISE_SPEED, dynamicCruiseAlt, keepFlightEnv);

          if (icao === activeIcao.value) {
            mapRoutes.value?.updateMissionProgress(plane);
            map.panTo([plane.lat, plane.lon]);
          }

          if (plane.status === FLIGHT_STATUS.GOING_TO_DEP) {
            if (arrived || distToTarget < 0.1) {
              plane.status = FLIGHT_STATUS.GOING_TO_DEST;
              if (icao === activeIcao.value) {
                mapRoutes.value?.setMissionSuccess(icao);
              }
              logFlightRecord(plane, plane.missionDestName || "Görev Sahası");
            }
          } else if (plane.status === FLIGHT_STATUS.GOING_TO_DEST) {
            if (distToTarget < SIM_SETTINGS.EXPLOSION_THRESHOLD_KM) {
              triggerExplosion(plane.lat, plane.lon);
              if (plane.ammo > 0) plane.ammo--;
              plane.status = FLIGHT_STATUS.MISSION_COMPLETE;
              logFlightRecord(plane, plane.missionDestName || "Hedef");

              Swal.fire({
                title: 'HEDEF İMHA EDİLDİ', html: `Birim: <b>${plane.callsign}</b><br>Görev Tamamlandı, Üsse Dönülüyor!`,
                icon: 'success', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false, timerProgressBar: true
              });

              setTimeout(() => {
                if (currentFlights.value[icao] && currentFlights.value[icao].status === FLIGHT_STATUS.MISSION_COMPLETE) {
                  returnToStart(icao);
                }
              }, 3000);
            }
          }
        }
      }
      // 3. Durum: Görev bitti (imha) / iptal edildi, ana üsse (başlangıç koordinatlarına) geri dönüş
      else if (planeReturningToStart) {
        const targetPos = path && path.length > 0
          ? { lat: path[0].lat, lon: path[0].lon }
          : { lat: plane.homeLat, lon: plane.homeLon };
        const dynamicReturnAlt = Math.min(10000, Math.max(1000, (plane.trip_distance || 100) * 100));
        const descentDist = Math.max(40, (plane.trip_distance || 0) * 0.3);
        const { arrived, distToTarget } = updatePlanePhysics(plane, icao, currentPos, targetPos, SIM_SETTINGS.DEFAULT_CRUISE_SPEED, dynamicReturnAlt, false, descentDist);

        if (arrived || distToTarget < 0.1) {
          plane.velocity = 0;
          plane.baroaltitude = 0;
          plane.status = FLIGHT_STATUS.STANDBY;
          plane.energy = 100;
          plane.ammo = 2;
          plane.isReturningToStart = false;
          plane.isPaused = true;
          animationSteps.value[icao] = 0;

          if (icao === activeIcao.value) {
            isReturningToStart.value = false;
            isPaused.value = true;
            drawFullRoute(icao);
          }

          const homeName = getHomeAirportName(plane);
          logFlightRecord(plane, homeName);

          telemetryService.updateTelemetry({
            icao: icao,
            lat: plane.lat,
            lon: plane.lon,
            velocity: 0,
            energy: 100,
            altitude: 0,
            heading: plane.heading,
            status: 'STANDBY',
            callsign: plane.callsign || 'Bilinmeyen',
            destLat: null,
            destLon: null,
            destName: plane.missionDestName || null,
            modelType: plane.modelType || null
          }).then(() => {
            telemetryService.endMission(icao).catch(()=>{});
          }).catch(() => {
            telemetryService.endMission(icao).catch(()=>{});
          });
          Swal.fire({ title: `${homeName} Meydanına Dönüldü`, text: 'İkmal tamamlandı.', icon: 'info', toast: true, position: 'top-end', timer: 3000, showConfirmButton: false });
        }
      }
      // 4. Durum: Haritada manuel olarak tanımlanan özel bir rotaya/koordinata uçuş
      else if (planeManualRouting && planeManualTarget) {
        const targetPos = { lat: planeManualTarget.lat, lon: planeManualTarget.lon };
        const dynamicManualAlt = Math.min(1500, Math.max(500, (plane.total_manual_dist || 15) * 100));
        const { arrived, distToTarget: remainingDist } = updatePlanePhysics(plane, icao, currentPos, targetPos, SIM_SETTINGS.MANUAL_CRUISE_SPEED, dynamicManualAlt);

        if (icao === activeIcao.value) {
          mapRoutes.value?.updateMissionProgress(plane);
        }

        if (arrived || remainingDist < 0.1) {
          plane.velocity = 0;
          plane.baroaltitude = 0;
          plane.status = FLIGHT_STATUS.ARRIVED;
          plane.energy = 100;
          plane.ammo = 2;
          plane.isManualRouting = false;
          plane.manualTarget = null;
          plane.isPaused = true;

          if (icao === activeIcao.value) {
            isPaused.value = true;
            isManualRouting.value = false;
            manualTarget.value = null;
            mapRoutes.value?.clearAllRoutes();
          }

          logFlightRecord(plane, plane.missionDestName || "Manuel Hedef");

          telemetryService.updateTelemetry({
            icao: icao,
            lat: plane.lat,
            lon: plane.lon,
            velocity: 0,
            energy: 100,
            altitude: 0,
            heading: plane.heading,
            status: FLIGHT_STATUS.ARRIVED,
            callsign: plane.callsign || 'Bilinmeyen',
            destLat: null,
            destLon: null,
            destName: plane.missionDestName || null,
            modelType: plane.modelType || null
          }).then(() => {
            telemetryService.endMission(icao).catch(()=>{});
          }).catch(() => {
            telemetryService.endMission(icao).catch(()=>{});
          });
          Swal.fire({
            title: 'HEDEFE VARILDI', html: `Birim: <b>${plane.callsign || 'Bilinmeyen'}</b><br>Manuel Rota ve ikmal tamamlandı.`,
            icon: 'success', toast: true, position: 'top-end', timer: 3500, showConfirmButton: false, timerProgressBar: true
          });
        }
      }
      // 5. Durum: Tanımlanmış rota noktaları olan standart JSON uçuş rotasında ilerleme
      else if (path && path.length > 0) {
        const step = animationSteps.value[icao] || 0;
        if (step + 1 >= path.length) {
          plane.velocity = 0;
          plane.baroaltitude = 0;
          plane.status = FLIGHT_STATUS.COMPLETED;
          plane.energy = 100;
          plane.ammo = 2;
          plane.isPaused = true;
          animationSteps.value[icao] = 0;

          if (icao === activeIcao.value) {
            isPaused.value = true;
          }

          logFlightRecord(plane, "Rota Sonu");

          telemetryService.updateTelemetry({
            icao: icao,
            lat: plane.lat,
            lon: plane.lon,
            velocity: 0,
            energy: 100,
            altitude: 0,
            heading: plane.heading,
            status: FLIGHT_STATUS.COMPLETED,
            callsign: plane.callsign || 'Bilinmeyen',
            destLat: null,
            destLon: null,
            destName: plane.missionDestName || null,
            modelType: plane.modelType || null
          }).then(() => {
            telemetryService.endMission(icao).catch(()=>{});
          }).catch(() => {
            telemetryService.endMission(icao).catch(()=>{});
          });
          Swal.fire({ title: 'GÖREV TAMAMLANDI', text: 'İkmal yapıldı.', icon: 'info', toast: true, position: 'top-end', timer: 3000, showConfirmButton: false });
          return;
        }
        const nextPoint = path[step + 1];
        const arrived = movePlane(icao, nextPoint.lat, nextPoint.lon, Math.max(plane.velocity, 20) / 2000);
        plane.distance_from_dep = (path[step].distance_from_dep || 0) + getDistance({ lat: path[step].lat, lon: path[step].lon }, currentPos);

        plane.velocity += (nextPoint.velocity - plane.velocity) * 0.05;
        plane.baroaltitude += (nextPoint.baroaltitude - plane.baroaltitude) * 0.05;

        if (arrived) animationSteps.value[icao] = step + 1;
      }
    });
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