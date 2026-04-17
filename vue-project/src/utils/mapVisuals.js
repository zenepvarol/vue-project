import L from 'leaflet';

// Hedefe ulaşıldığında haritada kısa süreli bir patlama/imha efekti çıkartır
export const triggerExplosion = (lat, lon, map) => {
  if (!map) return;
  const explosionIcon = L.divIcon({
    html: `<div class="explosion-container">
      <div class="explosion-core"></div>
      <div class="explosion-ring ring1"></div>
      <div class="explosion-ring ring2"></div>
      <div class="explosion-ring ring3"></div>
      <div class="explosion-spark spark1"></div>
      <div class="explosion-spark spark2"></div>
      <div class="explosion-spark spark3"></div>
      <div class="explosion-spark spark4"></div>
      <div class="explosion-spark spark5"></div>
      <div class="explosion-spark spark6"></div>
    </div>`,
    className: '', iconSize: [120, 120], iconAnchor: [60, 60]
  });

  const explosionMarker = L.marker([lat, lon], { icon: explosionIcon, zIndexOffset: 9999 }).addTo(map);
  setTimeout(() => map.removeLayer(explosionMarker), 2500);
};

export const getPlaneIcon = (isEnvanter, isApi) => {
  let iconColor = '#9381ff'; // Varsayılan: Mavi/Mor
  if (isEnvanter) iconColor = '#e74c3c'; // Envanter: Kırmızı
  if (isApi) iconColor = '#ff9800';      // API: Turuncu

  return L.divIcon({
    html: `
    <div class="moving-plane">
      <i class="mdi mdi-airplane" style="font-size: 36px; color: ${iconColor}; -webkit-text-stroke: 1px #ffffff; filter: drop-shadow(0 0 4px rgba(0, 0, 0, 0.4)); transition: transform 0.1s linear;"></i>
    </div>`,
    className: 'plane-icon', iconSize: [40, 40], iconAnchor: [22, 22]
  });
};

export const getAirportIcon = (airportId) => {
  return L.divIcon({
    html: `
    <div class="airport-marker">
      <i class="mdi mdi-map-marker" style="font-size: 24px; color: #2ecc71; -webkit-text-stroke: 1px #ffffff; filter: drop-shadow(0 0 5px rgba(46, 204, 113, 0.5));"></i>
      <span class="airport-label">${airportId}</span>
    </div>`,
    className: 'custom-airport', iconSize: [40, 40]
  });
};

export const getTankerIcon = () => {
  return L.divIcon({
    html: `
    <div class="moving-plane">
       <i class="mdi mdi-airplane" style="font-size: 36px; color: #f39c12; -webkit-text-stroke: 1px #ffffff; filter: drop-shadow(0 0 6px rgba(243, 156, 18, 0.6)); transition: transform 0.1s linear;"></i>
    </div>`,
    className: 'tanker-icon', iconSize: [45, 45], iconAnchor: [22, 22]
  });
};
