// Vue uygulamasının başlatıldığı, global eklentilerin ve stil dosyalarının sisteme dahil edildiği ana merkez

import './assets/main.css' // Uygulamanın temel CSS yapılandırması

import { createApp } from 'vue' // Vue uygulama örneğini oluşturmak için gerekli metod
import { createPinia } from 'pinia' // Merkezi veri yönetimi (Store) için Pinia

import App from './App.vue' // Kök bileşen (Root Component)
import router from './router' // Sayfa yönlendirmelerini (Routing) yöneten yapı

// Görsel Efektler ve İkon Setleri
import '@mdi/font/css/materialdesignicons.css'; // MDI ikon seti

const app = createApp(App)

app.use(createPinia()) // Vue Uygulama Örneği (Instance) Tanımlama
app.use(router) // Global State Management (Pinia) Entegrasyonu

// Hazırlanan Uygulama Yapısının DOM Üzerine Render Edilmesi (Mounting)
app.mount('#app')
