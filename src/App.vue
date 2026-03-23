<template>
  <div class="app-container" :class="{ 'dark-mode-active': darkMode }">
    <LeftPanel @focus-flight="(f) => mapRef?.focusFlight(f)" />

    <button class="sidebar-toggle" :class="{ open: sidebarOpen }" @click.stop="toggleSidebar">
      <ChevronLeft v-if="sidebarOpen" />
      <ChevronRight v-else />
    </button>

    <MapComponent
      ref="mapRef"
      v-model:currentFlights="currentFlights"
      v-model:activeIcao="activeIcao"
      v-model:isPaused="isPaused"
      v-model:animationSteps="animationSteps"
      v-model:airports="airports"
      v-model:isEmergency="isEmergency"
      v-model:isReturningToStart="isReturningToStart"
      v-model:isEmergencySimulated="isEmergencySimulated"
      v-model:emergencyCountdown="emergencyCountdown"
      v-model:manualLat="manualLat"
      v-model:manualLon="manualLon"
      v-model:manualAirportId="manualAirportId"
      v-model:destinationAirportId="destinationAirportId"
      v-model:activeFailure="activeFailure"
      :myFleetIcaos="myFleetIcaos"
      :selectedFlight="selectedFlight"
    />

    <RightPanel
      v-model:activeIcao="activeIcao"
      v-model:destinationAirportId="destinationAirportId"
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
      @assign-mission="() => mapRef?.assignMission()"
      @recenter-map="() => mapRef?.recenterMap()"
      @return-to-start="() => mapRef?.returnToStart()"
      @toggle-pause="() => mapRef?.togglePause()"
      @trigger-simulated-failure="() => mapRef?.triggerSimulatedFailure()"
      @handle-manual-emergency="() => mapRef?.handleManualEmergency()"
      @set-manual-target="() => mapRef?.setManualTarget()"
    />
  </div>
</template>

<script setup>
import { ChevronLeft, ChevronRight } from 'lucide-vue-next';
import { ref, computed } from 'vue';
import { storeToRefs } from 'pinia';
import { useFlightStore } from '@/stores/flightStore';
import LeftPanel from '@/components/LeftPanel.vue';
import RightPanel from '@/components/RightPanel.vue';
import MapComponent from '@/components/MapComponent.vue';
import './assets/flight-style.css';

const store = useFlightStore();
const { searchQuery, currentFlights, activeIcao, darkMode, sidebarOpen } = storeToRefs(store);

const mapRef = ref(null);
const animationSteps = ref({});
const isPaused = ref(true);
const airports = ref([]);
const isEmergency = ref(false);
const isReturningToStart = ref(false);
const isEmergencySimulated = ref(false);
const emergencyCountdown = ref(10);
const manualLat = ref(null);
const manualLon = ref(null);
const manualAirportId = ref('');
const destinationAirportId = ref('');
const activeFailure = ref(null);
const myFleetIcaos = ["9005", "9501", "9802", "7001", "7002", "7003", "7004", "7005"];

const toggleDarkMode = () => store.toggleDarkMode();
const toggleSidebar = () => store.toggleSidebar();

const selectedFlight = computed(() => activeIcao.value ? currentFlights.value[activeIcao.value] : null);
</script>