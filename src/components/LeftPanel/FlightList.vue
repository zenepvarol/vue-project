<template>
  <div class="fleet-status-section"
    style="padding: 0 10px 10px 10px; flex: 1; overflow-y: auto; scrollbar-width: thin;">
    <div class="fleet-grid">
      <v-card v-for="f in store.filteredFlights" :key="f.icao24" class="fleet-mini-card mb-0" density="compact"
        :variant="store.activeIcao === f.icao24 ? 'tonal' : 'flat'"
        :color="store.activeIcao === f.icao24 ? 'primary' : ''" @click="$emit('focus-flight', f)"
        style="padding: 4px 6px; cursor: pointer; transition: 0.2s;">
        <div class="mini-card-header d-flex justify-space-between align-center mb-0">
          <span class="mini-callsign font-weight-bold" style="font-size: 11px;">{{ f.callsign || 'İHA' }}</span>
          <v-icon :color="f.energy < 20 ? 'error' : 'success'" size="8">mdi-circle</v-icon>
        </div>

        <div class="mini-stats d-flex justify-space-between mb-1" style="font-size: 10px; opacity: 0.8;">
          <span><v-icon icon="mdi-gauge" size="12" class="mr-1" />{{ Math.round(f.velocity) }} kt</span>
          <span><v-icon icon="mdi-image-filter-hdr" size="12" class="mr-1" />{{ Math.round(f.baroaltitude) }} ft</span>
        </div>

        <v-progress-linear :model-value="f.energy" :color="f.energy < 20 ? 'error' : 'success'" height="3" rounded />
      </v-card>
    </div>
  </div>
</template>

<script setup>
import { useFlightStore } from '@/stores/flightStore';

const store = useFlightStore();
defineEmits(['focus-flight']);
</script>