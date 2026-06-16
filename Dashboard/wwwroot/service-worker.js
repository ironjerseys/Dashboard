const CACHE_NAME = 'dashboard-cache-v2';
const ASSETS = [
  '/',
  '/css/site.css',
  '/Dashboard.styles.css',
  '/lib/bootstrap/dist/css/bootstrap.min.css',
  '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
  '/lib/jquery/dist/jquery.min.js',
  '/js/site.js',
  '/icon.png'
];

self.addEventListener('install', (event) => {
  // Active immédiatement la nouvelle version sans attendre la fermeture des onglets.
  self.skipWaiting();
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(ASSETS))
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
    if (req.method !== 'GET') return;

  //event.respondWith(
  //  caches.match(req).then((cached) => {
  //    const fetchPromise = fetch(req).then((networkResp) => {
  //      if (networkResp && networkResp.status === 200 && networkResp.type === 'basic') {
  //        const copy = networkResp.clone();
  //        caches.open(CACHE_NAME).then((cache) => cache.put(req, copy));
  //      }
  //      return networkResp;
  //    }).catch(() => cached);
  //    return cached || fetchPromise;
  //  })
    //  );


    const url = new URL(req.url);

    // Ignore tout ce qui n'est pas votre site (extensions, CDN, etc.)
    if (url.origin !== self.location.origin) return;
    if (url.protocol !== 'http:' && url.protocol !== 'https:') return;

    // Ne pas intercepter les navigations (pages HTML)
    if (req.mode === 'navigate') return;

    // Cache uniquement les fichiers connus (assets)
    if (!ASSETS.includes(url.pathname)) return;

    // Network-first : on récupère toujours la dernière version quand le réseau est
    // disponible (et le cache ne sert que de repli hors-ligne). Évite que le CSS/JS
    // reste figé après un déploiement.
    event.respondWith(
        caches.open(CACHE_NAME).then(async (cache) => {
            try {
                const resp = await fetch(req);
                if (resp.ok) cache.put(req, resp.clone());
                return resp;
            } catch {
                const cached = await cache.match(req, { ignoreSearch: true });
                if (cached) return cached;
                throw new Error('Network error and no cached response for ' + url.pathname);
            }
        })
    );

});
