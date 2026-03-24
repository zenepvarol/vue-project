/**
 * Pinia kütüphanesi kullanılarak oluşturulan bu depo (store), uçuş verilerinin, kullanıcı
 * tercihlerinin ve global arayüz durumlarının tüm bileşenler arasında senkronize kalmasını sağlar. 
 */
import { defineStore } from 'pinia';

export const useFlightStore = defineStore('flight', {
  /**
   * STATE: Uygulamanın belleğinde tutulan reaktif veri tabanıdır.
   * Bu veriler değiştiğinde, onlara bağlı olan tüm bileşenler (UI) otomatik güncellenir. 
   */
  state: () => ({
    searchQuery: '', // Sol paneldeki uçuş arama filtresi
    darkMode: false, // Uygulamanın gece/gündüz teması tercihi
    sidebarOpen: true, // Sol panelin (Flight List) açık/kapalı olma durumu
    activeIcao: null, // Haritada ve sağ panelde odaklanılan uçağın ICAO kodu
    currentFlights: {} // Havada olan tüm aktif İHA/uçak verilerinin tutulduğu obje
  }),

  /**
   * GETTERS: Mevcut veriden (state) türetilen hesaplanmış (computed) değerlerdir.
   * Orijinal veriyi bozmadan, ihtiyaca göre filtrelenmiş veya işlenmiş veri döndürür.
   */
  getters: {
    //'searchQuery' değerine göre uçuşları 'callsign' üzerinden filtreler.
    filteredFlights: (state) => {
      const query = state.searchQuery.toLowerCase();
      return Object.values(state.currentFlights).filter(f =>
        f.callsign?.toString().toLowerCase().includes(query)
      );
    }
  },

  /** ACTIONS: State üzerindeki verileri güncellemek için kullanılan metodlardır. Uygulama içindeki iş mantığını (logic) barındırır. */
  actions: {
    toggleDarkMode() {
      this.darkMode = !this.darkMode;
    },
    toggleSidebar() {
      this.sidebarOpen = !this.sidebarOpen; //Sol panelin ekran üzerindeki görünürlüğünü (toggle) kontrol eder.
    }
  }
});
