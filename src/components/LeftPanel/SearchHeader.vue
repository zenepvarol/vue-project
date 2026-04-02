<template>
  <div class="sidebar-header" style="padding: 8px 15px;">
    <div class="header-top d-flex justify-space-between align-center mb-0">
      <h3 class="ma-0">Uçuş Listesi</h3>
      <v-btn icon variant="text" size="small" @click="store.toggleDarkMode"
        :title="store.darkMode ? 'Aydınlık Mod' : 'Karanlık Mod'">
        <v-icon :color="store.darkMode ? 'yellow-darken-2' : 'indigo-darken-1'"
          :icon="store.darkMode ? 'mdi-weather-sunny' : 'mdi-moon-waxing-crescent'" />
      </v-btn>
    </div>
    <v-text-field v-model="store.searchQuery" placeholder="Uçuş (Callsign) ara..." prepend-inner-icon="mdi-magnify"
      variant="outlined" density="compact" hide-details color="primary" class="mb-0"
      style="margin-bottom: 0px !important;" />
  </div>
</template>

<script setup>
import { useFlightStore } from '@/stores/flightStore';

const store = useFlightStore();
</script>
