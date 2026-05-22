<script setup>
import { defineModel } from 'vue'; import L from 'leaflet';
import { SIM_SETTINGS } from '@/constants/flightConstants';
import { getDistance } from '@/utils/physics';

const props = defineProps({
  selectedFlight: Object,
  nearestAirport: Object,
  map: Object,
  mapRoutes: Object,
  airports: Array
});

const isEmergencySimulated = defineModel('isEmergencySimulated');
const isEmergency = defineModel('isEmergency');
const emergencyCountdown = defineModel('emergencyCountdown');
const activeFailure = defineModel('activeFailure');
const isPaused = defineModel('isPaused');
const failureTypes = { LOW_BATTERY: { label: 'DÜŞÜK YAKIT', color: '#e74c3c' }, LINK_LOSS: { label: 'SİNYAL KAYBI', color: '#f39c12' } };

// En yakın havalimanını bulma yardımcısı
const findNearestAirport = (planeLat, planeLon) => {
  if (props.nearestAirport && props.selectedFlight && 
      props.selectedFlight.lat === planeLat && props.selectedFlight.lon === planeLon) {
    return props.nearestAirport;
  }
  if (!props.airports || !props.airports.length) return null;
  const planePos = { lat: planeLat, lon: planeLon };
  return props.airports.reduce((nearest, airport) => {
    const distToCurrent = getDistance(planePos, airport);
    const distToNearest = getDistance(planePos, nearest);
    return distToCurrent < distToNearest ? airport : nearest;
  });
};

// Acil iniş sürecini başlatır
const startEmergencyLanding = (targetPlane = null) => {
  const plane = targetPlane || props.selectedFlight;
  if (!plane) return;

  const nearest = findNearestAirport(plane.lat, plane.lon);
  if (nearest) {
    plane.isEmergency = true;
    plane.isPaused = false;
    plane.isReturningToStart = false;
    plane.isManualRouting = false;

    if (plane === props.selectedFlight) {
      isEmergency.value = true;
      isPaused.value = false;
    }

    const start = [plane.lat, plane.lon];
    const end = [nearest.lat, nearest.lon];

    // Slerp uçuşu için başlangıç konumunu ve toplam mesafeyi kaydet
    plane.startLat = plane.lat;
    plane.startLon = plane.lon;
    plane.distance_from_dep = 0;
    plane.trip_distance = getDistance(
      { lat: plane.lat, lon: plane.lon },
      { lat: nearest.lat, lon: nearest.lon }
    );

    props.mapRoutes?.setEmergencyRoute(start, end);     // Rota çizimi

    if (props.map && plane === props.selectedFlight) {
      props.map.fitBounds(L.latLngBounds([start, end]), { padding: [50, 50], maxZoom: 8 });     // Haritayı odakla
    }
  }
};

// Simüle edilmiş bir arıza/acil durum başlatır
const triggerSimulatedFailure = (targetPlane = null) => {
  const plane = targetPlane || props.selectedFlight;
  if (!plane || plane.isEmergencySimulated) return;

  plane.isEmergencySimulated = true;
  if (plane === props.selectedFlight) {
    activeFailure.value = plane.energy < SIM_SETTINGS.LOW_FUEL_THRESHOLD ? failureTypes.LOW_BATTERY : failureTypes.LINK_LOSS;
    isEmergencySimulated.value = true;
    emergencyCountdown.value = 10;
  }

  let countdown = 10;
  const interval = setInterval(() => {
    if (countdown > 0 && plane.isEmergencySimulated) {
      countdown--;
      if (plane === props.selectedFlight) {
        emergencyCountdown.value = countdown;
      }
    } else {
      clearInterval(interval);
      if (plane.isEmergencySimulated && !plane.isEmergency) {
        startEmergencyLanding(plane);
        plane.isEmergencySimulated = false;
        if (plane === props.selectedFlight) {
          isEmergencySimulated.value = false;
        }
      }
    }
  }, 1000);
};

// Kullanıcı tarafından tetiklenen manuel acil durum
const handleManualEmergency = (targetPlane = null) => {
  const plane = targetPlane || props.selectedFlight;
  if (!plane) return;
  plane.isEmergencySimulated = false;
  if (plane === props.selectedFlight) {
    isEmergencySimulated.value = false;
  }
  startEmergencyLanding(plane);
};

defineExpose({
  triggerSimulatedFailure,
  handleManualEmergency,
  startEmergencyLanding
});
</script>

<template>
  <!-- Mantıksal bir bileşen olduğu için UI elementi barındırmaz -->
</template>