<template>
  <div class="sidebar left" :class="{ collapsed: !store.sidebarOpen }"
    style="display: flex; flex-direction: column; max-height: 100vh;">
    <div class="sidebar-header">
      <div class="header-top">
        <h3>Uçuş Listesi</h3>
        <button class="dark-toggle" @click="store.toggleDarkMode" :title="store.darkMode ? 'Aydınlık Mod' : 'Karanlık Mod'">
          <Sun v-if="store.darkMode" :size="20" color="#f1c40f" />
          <Moon v-else :size="20" />
        </button>
      </div>
      <div class="search-container">
        <input v-model="store.searchQuery" placeholder="Uçuş (Callsign) ara: " class="search-input" />
      </div>
    </div>

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
              <Gauge :size="12" /> {{ Math.round(f.velocity) }} kt
            </span>
            <span>
              <Mountain :size="12" /> {{ Math.round(f.baroaltitude) }} ft
            </span>
          </div>
          <div class="mini-energy-bar">
            <div class="mini-energy-fill"
              :style="{ width: f.energy + '%', backgroundColor: f.energy < 20 ? '#e74c3c' : '#2ecc71' }"></div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { Sun, Moon, Gauge, Mountain } from 'lucide-vue-next';
import { useFlightStore } from '@/stores/flightStore';

const store = useFlightStore();
defineEmits(['focus-flight']);
</script>
