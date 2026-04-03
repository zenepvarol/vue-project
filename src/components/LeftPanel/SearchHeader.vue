<template>
  <v-sheet class="sidebar-header px-4 py-2" color="transparent">
    <div class="d-flex justify-space-between align-center mb-1">
      <div class="text-subtitle-1 font-weight-bold" style="font-size: 15px; margin-left: 15px;">Uçuş Listesi</div>
      <v-btn icon variant="text" size="small" @click="store.toggleDarkMode" id="dark-mode-toggle">
        <v-icon :color="store.darkMode ? 'yellow-darken-2' : 'indigo-darken-1'"
          :icon="store.darkMode ? 'mdi-weather-sunny' : 'mdi-moon-waxing-crescent'" />
        <v-tooltip activator="parent" location="bottom">{{ store.darkMode ? 'Aydınlık Mod' : 'Karanlık Mod'
        }}</v-tooltip>
      </v-btn>
    </div>
    <v-text-field v-model="store.searchQuery" placeholder="Uçuş ara (Callsign)..." prepend-inner-icon="mdi-magnify"
      variant="outlined" density="compact" hide-details color="primary" class="mb-0" bg-color="surface-light"
      style="max-width: 280px; margin-left: 10px;" />
  </v-sheet>
  <v-divider />
</template>

<script setup>
import { useFlightStore } from '@/stores/flightStore';

const store = useFlightStore();
</script>
