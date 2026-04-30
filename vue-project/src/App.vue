<template>
  <v-app :theme="darkMode ? 'dark' : 'light'">
    <router-view />
  </v-app>
</template>

<script setup>
import { storeToRefs } from 'pinia';
import { useFlightStore } from '@/stores/flightStore';

const store = useFlightStore();
const { darkMode } = storeToRefs(store);

/** App.vue - Uygulama Kök Katmanı
 * Bu bileşen ana yönlendirici (Router) ve global tema (Karanlık/Aydınlık Mod) yönetimi için kabuk görevi görür. */
</script>

<style>
html,
body {
  margin: 0;
  padding: 0;
  overflow: hidden;
}
</style>