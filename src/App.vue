<template>
  <div>
    <h1 class="title">Harita</h1>
    <div id="map"></div>
  </div>
</template>

<script setup>
import { onMounted } from 'vue'; 
import L from 'leaflet'; // harita kütüphanesi
import 'leaflet/dist/leaflet.css'; // haritanın görselleri

onMounted(() => {
  // 1. Harita İstanbul'un koordinatlarına (41.0049, 28.955) odaklı
  const map = L.map('map').setView([41.0049, 28.955], 10);

  // 2. Harita katmanı (OpenStreetMap. ((Bu  olmazsa harita gri bir ekran olarak kalır, yollar gözükmez.))
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap'
  }).addTo(map);
});
</script>

<style>
body {
  margin: 0;
  padding: 0;
  font-family: sans-serif;
}

.title {
  position: absolute;
  top: 10px;
  left: 50px;
  z-index: 1000;
  background: white;
  padding: 10px;
  border-radius: 8px;
  box-shadow: 0 2px 5px rgba(0, 0, 0, 0.2);
}

#map {
  height: 100vh;
  width: 100vw;
}
</style>