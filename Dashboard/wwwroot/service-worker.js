const CACHE_NAME = 'dashboard-cache-v1';
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
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(ASSETS))
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k))))
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

    event.respondWith(
        caches.open(CACHE_NAME).then(async (cache) => {
            const cached = await cache.match(req);
            if (cached) return cached;

            const resp = await fetch(req);
            if (resp.ok) cache.put(req, resp.clone());
            return resp;
        })
    );

});
