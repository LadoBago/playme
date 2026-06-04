// PlayMe service worker (Sprint 6 PWA). Hand-rolled — no Workbox.
//
// Three caching rules, narrow on purpose:
//   1. /api/* and /hubs/*  → network-only. Game state is real-time
//      and server-authoritative; a stale snapshot from cache would
//      contradict the live SignalR feed.
//   2. /_next/static/*, /icon-*, /screenshot-*, /manifest.webmanifest
//      → cache-first. These are content-hashed or static; immutable
//      for the life of a deploy.
//   3. Navigation requests (HTML) → network-first with the offline
//      fallback page when offline. We deliberately don't serve
//      cached HTML for previously-visited routes — room pages are
//      tied to live state and a stale snapshot would confuse players.
//
// Everything else falls through to the network with no SW
// involvement, so anything we forget to classify behaves exactly the
// way it did before the SW existed.

// Bump this whenever the SW's behaviour changes — the activate
// handler drops every cache name that doesn't match the current
// version, which is how rollouts invalidate stale entries.
const CACHE_VERSION = 'v2'; // v2: bust caches poisoned by pre-2026-06-04 dev sessions
const CACHE_NAME = `playme-${CACHE_VERSION}`;
const OFFLINE_URL = '/offline.html';

const PRECACHE_URLS = [OFFLINE_URL];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(PRECACHE_URLS)),
  );
  // skipWaiting + clients.claim (below) makes a fresh SW take over
  // immediately. Without it, the new SW would wait until every tab
  // closed before activating — fine in theory, infuriating in
  // practice for users who keep PlayMe pinned.
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    Promise.all([
      caches.keys().then((keys) =>
        Promise.all(
          keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k)),
        ),
      ),
      self.clients.claim(),
    ]),
  );
});

self.addEventListener('fetch', (event) => {
  const { request } = event;

  // Only GETs are cacheable — POST /api/rooms, the SignalR upgrade,
  // etc. always hit the network.
  if (request.method !== 'GET') return;

  const url = new URL(request.url);

  // Cross-origin (Sentry, PostHog) stays outside the SW's reach.
  if (url.origin !== self.location.origin) return;

  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
    return; // Rule 1: real-time, never cached.
  }

  if (
    url.pathname.startsWith('/_next/static/') ||
    url.pathname.startsWith('/icon-') ||
    url.pathname === '/icon.svg' ||
    url.pathname === '/apple-icon.png' ||
    url.pathname.startsWith('/screenshot-') ||
    url.pathname === '/manifest.webmanifest'
  ) {
    event.respondWith(cacheFirst(request));
    return;
  }

  if (request.mode === 'navigate') {
    event.respondWith(networkFirstWithOfflineFallback(request));
    return;
  }

  // Default: pass through.
});

async function cacheFirst(request) {
  const cache = await caches.open(CACHE_NAME);
  const hit = await cache.match(request);
  if (hit) return hit;
  const response = await fetch(request);
  if (response.ok) cache.put(request, response.clone());
  return response;
}

async function networkFirstWithOfflineFallback(request) {
  try {
    return await fetch(request);
  } catch {
    const cache = await caches.open(CACHE_NAME);
    const offline = await cache.match(OFFLINE_URL);
    return offline ?? new Response('Offline', { status: 503 });
  }
}
