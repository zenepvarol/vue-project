export const getDistance = (p1, p2) => {
  const R = 6371;
  const dLat = (p2.lat - p1.lat) * Math.PI / 180;
  const dLon = (p2.lon - p1.lon) * Math.PI / 180;
  const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) + Math.cos(p1.lat * Math.PI / 180) * Math.cos(p2.lat * Math.PI / 180) * Math.sin(dLon / 2) * Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
};

export const calculateNextPosition = (currentLat, currentLon, targetLat, targetLon, moveStep, currentHeading = 0) => {
  let nextLat = targetLat;
  let nextLon = targetLon;
  let heading = currentHeading;

  if (moveStep > 0) {
    const dx = targetLat - currentLat;
    const dy = targetLon - currentLon;
    const dist = getDistance({ lat: currentLat, lon: currentLon }, { lat: targetLat, lon: targetLon });

    if (dist > moveStep) {
      nextLat = currentLat + (dx / dist) * moveStep;
      nextLon = currentLon + (dy / dist) * moveStep;
      heading = (Math.atan2(dy, dx) * (180 / Math.PI));
      return { nextLat, nextLon, heading, hasArrived: false };
    } else {
      return { nextLat: targetLat, nextLon: targetLon, heading: currentHeading, hasArrived: true };
    }
  }

  return { nextLat, nextLon, heading, hasArrived: false };
};
