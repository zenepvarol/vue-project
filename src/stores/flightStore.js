import { defineStore } from 'pinia';

export const useFlightStore = defineStore('flight', {
  state: () => ({
    searchQuery: '',
    darkMode: false,
    sidebarOpen: true,
    activeIcao: null,
    currentFlights: {}
  }),
  getters: {
    filteredFlights: (state) => {
      const query = state.searchQuery.toLowerCase();
      return Object.values(state.currentFlights).filter(f =>
        f.callsign?.toString().toLowerCase().includes(query)
      );
    }
  },
  actions: {
    toggleDarkMode() {
      this.darkMode = !this.darkMode;
    },
    toggleSidebar() {
      this.sidebarOpen = !this.sidebarOpen;
    }
  }
});
