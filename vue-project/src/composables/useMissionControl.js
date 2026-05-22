import { getDistance } from '@/utils/physics';
import { FLIGHT_STATUS } from '@/constants/flightConstants';

/**
 * Görev yönetimi, üsse dönüş ve operasyonel komutlar için kullanılan composable.
 * Bu yapı, uçağın görev hedeflerini, manuel yönlendirmelerini ve üsse dönüş mantığını yönetir.
 */
export function useMissionControl(context) {
  const { 
    activeIcao, 
    currentFlights, 
    props, 
    flightPaths, 
    isReturningToStart, 
    isEmergency, 
    isPaused, 
    isManualRouting, 
    mapRoutes, 
    mapMission, 
    mapManualControl 
  } = context;

  /**
   * Belirli bir uçağın aktif rotasını haritadan temizler.
   */
  const resetActivePath = (icao) => {
    mapRoutes.value?.resetActivePath(icao);
  };

  /**
   * Uçak için görev rotasını çizer.
   */
  const drawMissionRoute = (plane, targetPos) => {
    return mapRoutes.value?.drawMissionRoute(plane, targetPos);
  };

  /**
   * Mevcut görevi iptal eder ve uçağı başlangıç koordinatlarına (ana merkeze) geri döndürür.
   */
  const returnToStart = (targetIcao = null) => {
    const icao = targetIcao || activeIcao.value;
    if (!icao) return;
    const plane = currentFlights.value[icao];

    if (plane && (props.myFleetIcaos.includes(String(icao)) || plane.isSiha || plane.IsSiha)) {
      // Eğer uçuş tamamlanıp (MISSION_COMPLETE) üsse dönülüyorsa kalkış noktasını hedef olarak kaydet.
      // Yol ortasında (görev tamamlanmadan) ana merkeze dönülüyorsa gerçek kalkış noktasını bozma.
      if (plane.status === FLIGHT_STATUS.MISSION_COMPLETE) {
        plane.lastDepartureName = plane.missionDestName || "Görev Sahası";
      }
      plane.status = FLIGHT_STATUS.RETURNING;
      plane.isReturningToStart = true;
      plane.isEmergency = false;
      plane.isPaused = false;
      plane.isManualRouting = false;
    }

    const path = flightPaths.value[icao];
    const targetPos = path && path.length > 0 
      ? { lat: path[0].lat, lon: path[0].lon } 
      : { lat: plane.homeLat, lon: plane.homeLon };

    if (targetPos && targetPos.lat !== undefined) {
      plane.trip_distance = getDistance({ lat: plane.lat, lon: plane.lon }, targetPos);
      plane.distance_from_dep = 0;
      
      // Slerp algoritmasının kullanabilmesi için başlangıç konumunu kaydet
      plane.startLat = parseFloat(plane.lat);
      plane.startLon = parseFloat(plane.lon);
      
      // Uçağa ilk hareketi ver
      if (plane.velocity === 0) plane.velocity = 10;
    }

    resetActivePath(icao);

    if (icao === activeIcao.value) {
      isReturningToStart.value = true;
      isEmergency.value = false;
      isPaused.value = false;
      isManualRouting.value = false;
      mapRoutes.value?.clearAllRoutes();
    } else {
      if (mapRoutes.value?.missionPaths[icao] && props.map) {
        props.map.removeLayer(mapRoutes.value.missionPaths[icao]);
        delete mapRoutes.value.missionPaths[icao];
      }
    }
  };

  // Alt bileşenler üzerindeki görev komutlarını tetikleyen fonksiyonlar
  const assignMission = () => mapMission.value?.assignMission();
  const triggerExplosion = (lat, lon) => mapMission.value?.triggerExplosion(lat, lon);
  const setManualTarget = () => mapManualControl.value?.setManualTarget();

  return {
    resetActivePath,
    drawMissionRoute,
    returnToStart,
    assignMission,
    triggerExplosion,
    setManualTarget
  };
}
