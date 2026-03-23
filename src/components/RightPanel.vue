<template>
  <div class="sidebar right" style="overflow-y: auto; max-height: 100vh;">
    <TargetSelection
      v-if="!selectedFlight"
      v-model:destinationAirportId="destinationAirportId"
      :airports="airports"
      @assign-mission="$emit('assign-mission')"
    />

    <FlightDetails
      v-else
      v-model:activeIcao="activeIcao"
      v-model:manualAirportId="manualAirportId"
      v-model:manualLat="manualLat"
      v-model:manualLon="manualLon"
      :selectedFlight="selectedFlight"
      :airports="airports"
      :activeFailure="activeFailure"
      :isEmergencySimulated="isEmergencySimulated"
      :isEmergency="isEmergency"
      :isReturningToStart="isReturningToStart"
      :isPaused="isPaused"
      :myFleetIcaos="myFleetIcaos"
      :animationSteps="animationSteps"
      :emergencyCountdown="emergencyCountdown"
      @recenter-map="$emit('recenter-map')"
      @return-to-start="$emit('return-to-start')"
      @toggle-pause="$emit('toggle-pause')"
      @trigger-simulated-failure="$emit('trigger-simulated-failure')"
      @handle-manual-emergency="$emit('handle-manual-emergency')"
      @set-manual-target="$emit('set-manual-target')"
    />
  </div>
</template>

<script setup>
import TargetSelection from './RightPanel/TargetSelection.vue';
import FlightDetails from './RightPanel/FlightDetails.vue';

defineProps({
  selectedFlight: Object,
  airports: Array,
  activeFailure: Object,
  isEmergencySimulated: Boolean,
  isEmergency: Boolean,
  isReturningToStart: Boolean,
  isPaused: Boolean,
  myFleetIcaos: Array,
  animationSteps: Object,
  emergencyCountdown: Number
});

const activeIcao = defineModel('activeIcao');
const destinationAirportId = defineModel('destinationAirportId', { type: String });
const manualAirportId = defineModel('manualAirportId', { type: String });
const manualLat = defineModel('manualLat');
const manualLon = defineModel('manualLon');

defineEmits([
  'assign-mission',
  'recenter-map',
  'return-to-start',
  'toggle-pause',
  'trigger-simulated-failure',
  'handle-manual-emergency',
  'set-manual-target'
]);
</script>
