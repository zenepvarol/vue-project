<template>
  <div class="sidebar-header">
    <div class="header-top">
      <h3>Uçuş Listesi</h3>
      <button class="dark-toggle" @click="store.toggleDarkMode" :title="store.darkMode ? 'Aydınlık Mod' : 'Karanlık Mod'">
        <i v-if="store.darkMode" class="mdi mdi-weather-sunny" style="font-size: 20px; color: #f1c40f;"></i>
        <i v-else class="mdi mdi-moon-waxing-crescent" style="font-size: 20px;"></i>
      </button>
    </div>
    <div class="search-container">
      <input v-model="store.searchQuery" placeholder="Uçuş (Callsign) ara: " class="search-input" />
    </div>
  </div>
</template>

<script setup>

import { useFlightStore } from '@/stores/flightStore';

const store = useFlightStore();
</script>
