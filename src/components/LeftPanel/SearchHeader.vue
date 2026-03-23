<template>
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
</template>

<script setup>
import { Sun, Moon } from 'lucide-vue-next';
import { useFlightStore } from '@/stores/flightStore';

const store = useFlightStore();
</script>
