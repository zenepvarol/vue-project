import { getDistance, calculateNextPosition, interpolateSlerp } from '@/utils/physics';

/**
 * Uçakların hareket fiziğini ve harita üzerindeki marker güncellemelerini yöneten composable.
 * 
 * @param {Ref} currentFlights - Tüm uçakların verilerini içeren reaktif nesne
 * @param {Ref} mapPlanes - MapPlanes bileşenine referans (marker'lara erişim için)
 * @param {Ref} mapRoutes - MapRoutes bileşenine referans (rota çizgilerini güncellemek için)
 * @param {Ref} isEmergency - Acil durum durumunu takip eden reaktif değişken
 */
export function useFlightPhysics(currentFlights, mapPlanes, mapRoutes, isEmergency) {

  /**  Verilen uçağı hedeflenen koordinata doğru ilerletir ve marker açısını günceller. */
  const movePlane = (icao, targetLat, targetLon, moveStep = 0) => {
    const plane = currentFlights.value[icao];
    const marker = mapPlanes.value?.markers[String(icao)];
    if (!plane) return false;

    let nextLat, nextLon, heading, hasArrived;

    // Eğer uçağın kavisli rota için başlangıç noktası ve mesafesi belliyse Slerp uygula
    if (moveStep > 0 && plane.startLat !== undefined && plane.startLon !== undefined && plane.trip_distance > 0) {
      const startPos = { lat: plane.startLat, lon: plane.startLon };
      const targetPos = { lat: targetLat, lon: targetLon };

      // Kümülatif mesafe üzerinden ilerleme oranını (t) hesapla
      const nextDist = plane.distance_from_dep + moveStep;
      const t = Math.min(1, nextDist / plane.trip_distance);

      // Slerp ile kavis üzerindeki gerçek noktayı bul
      const nextPoint = interpolateSlerp(startPos, targetPos, t);

      nextLat = nextPoint.lat;
      nextLon = nextPoint.lon;

      // Yönelim (heading) hesabı
      const dx = nextLat - plane.lat;
      const dy = nextLon - plane.lon;
      heading = (Math.atan2(dy, dx) * (180 / Math.PI));

      // Hedefe ulaşıldı mı kontrolü
      hasArrived = t >= 1 || getDistance({ lat: plane.lat, lon: plane.lon }, targetPos) <= moveStep;
    } else {
      // Slerp için veri yoksa doğrusal yöntemi kullan
      const result = calculateNextPosition(plane.lat, plane.lon, targetLat, targetLon, moveStep, plane.heading || 0);
      nextLat = result.nextLat;
      nextLon = result.nextLon;
      heading = result.heading;
      hasArrived = result.hasArrived;
    }

    if (hasArrived) {
      return true;
    }

    plane.lat = nextLat;
    plane.lon = nextLon;
    plane.heading = heading;
    const newPos = [nextLat, nextLon];

    // Marker varsa görseli güncelle
    if (marker) {
      if (moveStep === 0) {
        marker.slideTo(newPos, { duration: 100 });
      } else {
        marker.setLatLng(newPos);
      }
      marker.setRotationAngle(heading - 45);
    }

    // Rota çizgisini güncelle
    if (mapRoutes.value?.activeRoutes[icao] && !isEmergency.value) {
      mapRoutes.value.activeRoutes[icao].addLatLng(newPos);
    }
    return false;
  };

  /**  Uçağın hız ve irtifa fiziğini günceller, iniş/kalkış yumuşatmalarını yapar. */
  const updatePlanePhysics = (plane, icao, currentPos, targetPos, cruiseSpeed = 220, cruiseAlt = 10000, noDescent = false, descentDist = 20) => {
    const distToTarget = getDistance(currentPos, targetPos);
    const stepSize = Math.max(0.1, plane.velocity / 1500);
    const oldPos = { lat: plane.lat, lon: plane.lon };

    const arrived = movePlane(icao, targetPos.lat, targetPos.lon, stepSize);
    plane.distance_from_dep += getDistance(oldPos, { lat: plane.lat, lon: plane.lon });

    if (!noDescent && distToTarget < descentDist) {
      // İniş mantığı: Orantılı azalma
      const ratio = Math.max(0, distToTarget / descentDist);
      const targetVel = cruiseSpeed * ratio;
      const targetAlt = cruiseAlt * ratio;

      plane.velocity += (targetVel - plane.velocity) * 0.1;
      plane.baroaltitude += (targetAlt - plane.baroaltitude) * 0.1;
    } else {
      // Kalkış ve Seyir
      plane.velocity += (cruiseSpeed - plane.velocity) * 0.01;
      plane.baroaltitude += (cruiseAlt - plane.baroaltitude) * 0.01;
    }
    return { arrived, distToTarget };
  };

  return {
    movePlane,
    updatePlanePhysics
  };
}
