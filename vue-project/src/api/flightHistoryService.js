import axios from 'axios';

const API_URL = 'http://localhost:5143/api/FlightHistory';

export const flightHistoryService = {
  getAllHistory() {
    return axios.get(API_URL);
  },
  getHistoryByIcao(icao) {
    return axios.get(`${API_URL}/${icao}`);
  },
  saveFlight(flightData) {
    return axios.post(API_URL, flightData);
  },
  deleteHistory(icao) {
    return axios.delete(`${API_URL}/${icao}`);
  }
};
