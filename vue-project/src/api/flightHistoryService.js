import api from './index';

export const flightHistoryService = {
  getAllHistory() {
    return api.get('/FlightHistory');
  },
  getHistoryByIcao(icao) {
    return api.get(`/FlightHistory/${icao}`);
  },
  // Veri HTTP POST isteği ile backend portuna (7089) gider
  saveFlight(flightData) {
    return api.post('/FlightHistory', flightData);
  },
  deleteHistory(icao) {
    return api.delete(`/FlightHistory/${icao}`);
  }
};
