<template>
  <!-- v-sheet: Sol panelin ana çerçevesi. 
       'sidebar left' sınıflarıyla mevcut uçuş stillerini (arka plan, geçişler) korur.
       :class: Store'daki sidebarOpen durumuna göre paneli gizleyip açar. -->
  <v-sheet component="aside" class="sidebar left" :class="{ collapsed: !store.sidebarOpen }"
    style="display: flex; flex-direction: column; max-height: 100vh;">
    <SearchHeader /> <!-- Üst Başlık ve Arama Kısmı -->

    <!-- @focus-flight: Listeden gelen tıklama olayını üst bileşene (App.vue) aktarır. -->
    <FlightList @focus-flight="$emit('focus-flight', $event)" />
  </v-sheet>
</template>

<script setup>
/** LeftPanel.vue - Sol Navigasyon ve Filtreleme Paneli
 * Bu bileşen, uygulamanın uçuş arama ve liste görüntüleme fonksiyonlarını modüler bir yapıda birleştirir. */
import { useFlightStore } from '@/stores/flightStore';
import SearchHeader from './LeftPanel/SearchHeader.vue';
import FlightList from './LeftPanel/FlightList.vue';

/** STORE ERİŞİMİ: Yan panelin açık/kapalı (collapsed) durumunu global store üzerinden kontrol eder.*/
const store = useFlightStore();

/** OLAY YÖNETİMİ (EMITS): FlightList'ten gelen 'focus-flight' sinyalini yakalayıp harita kontrolü için App.vue'ya paslar. */
defineEmits(['focus-flight']);
</script>
