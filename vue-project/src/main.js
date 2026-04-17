// Vue uygulamasının başlatıldığı, global eklentilerin ve stil dosyalarının sisteme dahil edildiği ana merkez

import './assets/main.css' // Uygulamanın temel CSS yapılandırması
import 'vuetify/styles' // Vuetify'ın temel stilleri

import { createApp } from 'vue' // Vue uygulama örneğini oluşturmak için gerekli metod
import { createPinia } from 'pinia' // Merkezi veri yönetimi (Store) için Pinia
import { createVuetify } from 'vuetify' // Vuetify eklentisinitanımlamak için
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'

import App from './App.vue' // Kök bileşen (Root Component)
import router from './router' // Sayfa yönlendirmelerini (Routing) yöneten yapı

// Görsel Efektler ve İkon Setleri
import '@mdi/font/css/materialdesignicons.css'; // MDI ikon seti

const vuetify = createVuetify({
  components,
  directives,
})

const app = createApp(App)

app.use(createPinia()) // Global State Management (Pinia) Entegrasyonu
app.use(router) // Router Entegrasyonu
app.use(vuetify) // Vuetify Entegrasyonu

// Hazırlanan Uygulama Yapısının DOM Üzerine Render Edilmesi (Mounting)
app.mount('#app')
