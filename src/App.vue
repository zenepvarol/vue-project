<template>
  <v-app :theme="darkMode ? 'dark' : 'light'" style="display: block !important;">

    <div class="app-container" :class="{ 'dark-mode-active': darkMode }">

      <LeftPanel @focus-flight="(f) => mapRef?.focusFlight(f)" />

      <v-btn class="sidebar-toggle" :class="{ open: sidebarOpen }" @click.stop="toggleSidebar" color="surface"
        elevation="4" rounded="e-lg" width="30" height="50">
        <v-icon :icon="sidebarOpen ? 'mdi-chevron-left' : 'mdi-chevron-right'" />
      </v-btn>

      <MapComponent ref="mapRef" v-model:currentFlights="currentFlights" v-model:activeIcao="activeIcao"
        v-model:isPaused="isPaused" v-model:animationSteps="animationSteps" v-model:airports="airports"
        v-model:isEmergency="isEmergency" v-model:isReturningToStart="isReturningToStart"
        v-model:isEmergencySimulated="isEmergencySimulated" v-model:emergencyCountdown="emergencyCountdown"
        v-model:manualLat="manualLat" v-model:manualLon="manualLon" v-model:manualAirportId="manualAirportId"
        v-model:destinationAirportId="destinationAirportId" v-model:destLat="destLat" v-model:destLon="destLon"
        v-model:activeFailure="activeFailure" :myFleetIcaos="myFleetIcaos" :selectedFlight="selectedFlight" />

      <RightPanel v-model:activeIcao="activeIcao" v-model:destinationAirportId="destinationAirportId"
        v-model:destLat="destLat" v-model:destLon="destLon" v-model:manualAirportId="manualAirportId"
        v-model:manualLat="manualLat" v-model:manualLon="manualLon" :selectedFlight="selectedFlight"
        :airports="airports" :activeFailure="activeFailure" :isEmergencySimulated="isEmergencySimulated"
        :isEmergency="isEmergency" :isReturningToStart="isReturningToStart" :isPaused="isPaused"
        :myFleetIcaos="myFleetIcaos" :animationSteps="animationSteps" :emergencyCountdown="emergencyCountdown"
        @assign-mission="() => mapRef?.assignMission()" @recenter-map="() => mapRef?.recenterMap()"
        @return-to-start="() => mapRef?.returnToStart()" @toggle-pause="() => mapRef?.togglePause()"
        @trigger-simulated-failure="() => mapRef?.triggerSimulatedFailure()"
        @handle-manual-emergency="() => mapRef?.handleManualEmergency()"
        @set-manual-target="() => mapRef?.setManualTarget()" />
    </div>
  </v-app>
</template>
<script setup>

import { ref, computed } from 'vue';
import { storeToRefs } from 'pinia';
import { useFlightStore } from '@/stores/flightStore';
import LeftPanel from '@/components/LeftPanel.vue';
import RightPanel from '@/components/RightPanel.vue';
import MapComponent from '@/components/MapComponent.vue';
import './assets/flight-style.css';

const store = useFlightStore();
const { searchQuery, currentFlights, activeIcao, darkMode, sidebarOpen } = storeToRefs(store);

const mapRef = ref(null); // Harita bileşenine doğrudan erişim için
const animationSteps = ref({}); // Her uçağın animasyon adımları
const isPaused = ref(true); 
const airports = ref([]);
const isEmergency = ref(false);
const isReturningToStart = ref(false);
const isEmergencySimulated = ref(false);
const emergencyCountdown = ref(10); // Sinyal kaybı için geri sayım
const manualLat = ref(null); // Manuel hedef enlemi
const manualLon = ref(null); // Manuel hedef boylamı
const manualAirportId = ref(''); // Manuel seçilen havalimanı ID
const destinationAirportId = ref(''); // İlk görev ataması yapılan hedef ID
const destLat = ref(null);
const destLon = ref(null);
const activeFailure = ref(null); // Aktif arıza tipi (yakıt/sinyal)
const myFleetIcaos = ["9005", "9501", "9802", "7001", "7002", "7003", "7004", "7005"]; // Envanter tanımlanması

const toggleDarkMode = () => store.toggleDarkMode();
const toggleSidebar = () => store.toggleSidebar();

const selectedFlight = computed(() => activeIcao.value ? currentFlights.value[activeIcao.value] : null);
</script>