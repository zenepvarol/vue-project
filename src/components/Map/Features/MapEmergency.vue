<script setup>
import { defineModel } from 'vue'; import L from 'leaflet';
import { SIM_SETTINGS } from '@/constants/flightConstants';

const props = defineProps({
  selectedFlight: Object,
  nearestAirport: Object,
  map: Object,
  mapRoutes: Object
});

const isEmergencySimulated = defineModel('isEmergencySimulated');
const isEmergency = defineModel('isEmergency');
const emergencyCountdown = defineModel('emergencyCountdown');
const activeFailure = defineModel('activeFailure');
const isPaused = defineModel('isPaused');
const failureTypes = { LOW_BATTERY: { label: 'DÜŞÜK YAKIT', color: '#e74c3c' }, LINK_LOSS: { label: 'SİNYAL KAYBI', color: '#f39c12' } };

// Acil iniş sürecini başlatır
const startEmergencyLanding = () => {
  if (props.selectedFlight && props.nearestAirport) {
    isEmergency.value = true; isPaused.value = false;
    const start = [props.selectedFlight.lat, props.selectedFlight.lon];
    const end = [props.nearestAirport.lat, props.nearestAirport.lon];
    props.mapRoutes?.setEmergencyRoute(start, end);     // Rota çizimi

    if (props.map) {
      props.map.fitBounds(L.latLngBounds([start, end]), { padding: [50, 50], maxZoom: 8 });     // Haritayı odakla
    }
  }
};

// Simüle edilmiş bir arıza/acil durum başlatır
const triggerSimulatedFailure = () => {
  if (isEmergencySimulated.value || !props.selectedFlight) return;
  const p = props.selectedFlight; activeFailure.value = p.energy < SIM_SETTINGS.LOW_FUEL_THRESHOLD ? failureTypes.LOW_BATTERY : failureTypes.LINK_LOSS;
  isEmergencySimulated.value = true; emergencyCountdown.value = 10;
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

// Kullanıcı tarafından tetiklenen manuel acil durum
const handleManualEmergency = () => {
  isEmergencySimulated.value = false;
  startEmergencyLanding();
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