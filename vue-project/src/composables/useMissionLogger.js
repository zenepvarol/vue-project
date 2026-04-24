import { useFlightStore } from '@/stores/flightStore';

/**
 * Uçuş kayıtlarını (log) yöneten ve backend'e gönderen composable.
 * Bu yapı, MapEngine içindeki karmaşıklığı azaltmak ve kayıt mantığını merkezi bir yerde toplamak için kullanılır.
 */
export function useMissionLogger() {
  const store = useFlightStore();

  /**
   * Uçağın bir noktaya vardığında veya görevini tamamladığında uçuş verilerini veritabanına kaydeder.
   * @param {Object} plane - Aktif uçak objesi
   * @param {string} arrivalName - Varış noktasının adı (Havalimanı veya Hedef)
   */
  const logFlightRecord = (plane, arrivalName = "Hedef") => {
    if (!plane) return;

    // ADIM 1: Uçağın vardığı anı yakala ve rapor verilerini (icao, nereden, nereye, mesafe) hazırla
    const departureName = plane.lastDepartureName || "Ana Merkez";
    const distance = Math.round(plane.distance_from_dep || 0);
    
    const flightData = {
      icao: String(plane.icao24), // ICAO'yu yazı formatına çevirir
      departure: departureName,
      arrival: arrivalName,
      distance: Number(distance)  // Mesafeyi sayı formatına çevirir
    };

    console.log(">> Uçuş Kaydı Hazırlandı:", flightData);

    // ADIM 2: Hazırlanan raporu Pinia Store üzerinden backend'e gönderilmek üzere ilet
    store.saveHistory(flightData);
    
    // Sonraki uçuş bacağı için mesafe sayacı sıfırlanır ve yeni kalkış noktası mevcut varış noktası olarak atanır
    plane.distance_from_dep = 0;
    plane.lastDepartureName = arrivalName;
  };

  return {
    logFlightRecord
  };
}
