<template>
  <!-- v-sheet: Sol panelin ana çerçevesi. 
       'sidebar left' sınıflarıyla mevcut uçuş stillerini (arka plan, geçişler) korur.
       :class: Store'daki sidebarOpen durumuna göre paneli gizleyip açar. -->
  <v-sheet component="aside" class="sidebar left" :class="{ collapsed: !store.sidebarOpen }"
    style="display: flex; flex-direction: column; max-height: 100vh;">
    <FleetSearch /> <!-- Üst Başlık ve Arama Kısmı -->

    <!-- @focus-flight: Listeden gelen tıklama olayını üst bileşene (App.vue) aktarır. -->
    <FleetList @focus-flight="$emit('focus-flight', $event)" />
  </v-sheet>
</template>

<script setup>
/** FleetPanel.vue - Sol Navigasyon ve Filtreleme Paneli
 * Bu bileşen, uygulamanın uçuş arama ve liste görüntüleme fonksiyonlarını modüler bir yapıda birleştirir. */
import { useFlightStore } from '@/stores/flightStore';
import FleetSearch from './FleetSearch.vue';
import FleetList from './FleetList.vue';

/** STORE ERİŞİMİ: Yan panelin açık/kapalı (collapsed) durumunu global store üzerinden kontrol eder.*/
const store = useFlightStore();

/** OLAY YÖNETİMİ (EMITS): FleetList'ten gelen 'focus-flight' sinyalini yakalayıp harita kontrolü için App.vue'ya paslar. */
defineEmits(['focus-flight']);
</script>
