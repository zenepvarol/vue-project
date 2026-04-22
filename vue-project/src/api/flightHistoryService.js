import axios from 'axios';

const API_URL = 'https://localhost:7089/api/FlightHistory';

export const flightHistoryService = {
  getAllHistory() {
    return axios.get(API_URL);
  },
  getHistoryByIcao(icao) {
    return axios.get(`${API_URL}/${icao}`);
  },
  // Veri HTTP POST isteği ile backend portuna (7089) gider
  saveFlight(flightData) {
    return axios.post(API_URL, flightData);
  },
  deleteHistory(icao) {
    return axios.delete(`${API_URL}/${icao}`);
  }
};
