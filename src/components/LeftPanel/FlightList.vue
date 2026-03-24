<template>
  <div class="fleet-status-section"
    style="padding: 10px; border-top: none; flex: 1; overflow-y: auto; scrollbar-width: thin;">
    <div class="fleet-grid">
      <div v-for="f in store.filteredFlights" :key="f.icao24" class="fleet-mini-card"
        :class="{ 'is-active': store.activeIcao === f.icao24 }" @click="$emit('focus-flight', f)">
        <div class="mini-card-header">
          <span class="mini-callsign">{{ f.callsign || 'İHA' }}</span>
          <span class="mini-status-dot" :style="{ backgroundColor: f.energy < 20 ? '#e74c3c' : '#2ecc71' }"></span>
        </div>
        <div class="mini-stats">
          <span>
            <i class="mdi mdi-gauge" style="font-size: 12px;"></i> {{ Math.round(f.velocity) }} kt
          </span>
          <span>
            <i class="mdi mdi-image-filter-hdr" style="font-size: 12px;"></i> {{ Math.round(f.baroaltitude) }} ft
          </span>
        </div>
        <div class="mini-energy-bar">
          <div class="mini-energy-fill"
            :style="{ width: f.energy + '%', backgroundColor: f.energy < 20 ? '#e74c3c' : '#2ecc71' }"></div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>

import { useFlightStore } from '@/stores/flightStore';

const store = useFlightStore();
defineEmits(['focus-flight']);
</script>
