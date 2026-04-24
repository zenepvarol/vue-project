import { computed } from 'vue';
import { getDistance } from '@/utils/physics';

/**
 * Acil durum yönetimi ve en yakın havalimanı tespiti için kullanılan composable.
 * Bu yapı, harita üzerindeki acil durum senaryolarını ve iniş noktası hesaplamalarını merkezi bir yerden yönetir.
 */
export function useEmergencySystem(props, airports, mapEmergency) {
  
  /**
   * Verilen koordinatlara en yakın müttefik havalimanını (üs noktasını) bulur.
   * @param {number} planeLat - Uçağın enlemi
   * @param {number} planeLon - Uçağın boylamı
   * @returns {Object|null} En yakın havalimanı objesi
   */
  const getNearestAirport = (planeLat, planeLon) => {
    if (!airports.value || !airports.value.length) return null;
    
    const planePos = { lat: planeLat, lon: planeLon };
    
    return airports.value.reduce((nearest, airport) => {
      const distToCurrent = getDistance(planePos, airport);
      const distToNearest = getDistance(planePos, nearest);
      return distToCurrent < distToNearest ? airport : nearest;
    });
  };

  /**
   * O an seçili olan uçağın konumuna göre en yakın havalimanını hesaplayan computed değer.
   */
  const nearestAirport = computed(() => {
    if (props.selectedFlight && airports.value && airports.value.length > 0) {
      return getNearestAirport(props.selectedFlight.lat, props.selectedFlight.lon);
    }
    return null;
  });

  // MapEmergency bileşeni üzerindeki aksiyonları tetikleyen köprü fonksiyonlar
  const startEmergencyLanding = () => mapEmergency.value?.startEmergencyLanding();
  const triggerSimulatedFailure = () => mapEmergency.value?.triggerSimulatedFailure();
  const handleManualEmergency = () => mapEmergency.value?.handleManualEmergency();

  return {
    nearestAirport,
    getNearestAirport,
    startEmergencyLanding,
    triggerSimulatedFailure,
    handleManualEmergency
  };
}
