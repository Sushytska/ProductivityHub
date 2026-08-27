export const environment = {
  // realtime-service's CORS defaults to "*", so the dev server can connect
  // directly without needing the ng serve proxy (which only covers /api).
  realtimeUrl: 'http://localhost:4000',
};
