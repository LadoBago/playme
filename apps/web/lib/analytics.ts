// PostHog wrapper. CLAUDE.md §4.2 & §5.8 in two lines:
//   - cookieless, no autocapture, no IP capture
//   - only explicit events; the catalog lives in `track()` below
//
// Featureless on purpose: features import `track(event)`, never posthog-js
// directly, so swapping the analytics SDK later is one file.
//
// posthog-js is ~60 KB gzipped — loading it eagerly put it on the critical
// path of every page and cost real LCP on the room page. `initAnalytics`
// therefore schedules a dynamic import for browser idle time instead of
// importing at module scope; events fired before the SDK lands queue in
// memory and flush on init. Analytics is best-effort by definition, so a
// dropped queue on a failed load is acceptable.

import type { PostHog } from 'posthog-js';

type AnalyticsState = 'idle' | 'loading' | 'ready' | 'disabled';

let state: AnalyticsState = 'idle';
let client: PostHog | null = null;

// Events captured between page load and the deferred SDK init. Capped so a
// disabled/never-initialised session can't grow it unbounded.
const PENDING_CAP = 20;
const pending: AnalyticsEvent[] = [];

/**
 * Run `start` when the browser is idle so the SDK download/parse never
 * competes with hydration or the SignalR handshake. Safari has no
 * requestIdleCallback — fall back to a flat post-load delay there.
 */
function whenIdle(start: () => void): void {
  if (typeof window.requestIdleCallback === 'function') {
    window.requestIdleCallback(() => start(), { timeout: 5000 });
  } else {
    window.setTimeout(start, 2000);
  }
}

export function initAnalytics(): void {
  if (state !== 'idle') return;
  if (typeof window === 'undefined') return;
  const key = process.env.NEXT_PUBLIC_POSTHOG_KEY;
  const host = process.env.NEXT_PUBLIC_POSTHOG_HOST ?? 'https://eu.i.posthog.com';
  if (!key) {
    state = 'disabled';
    pending.length = 0;
    return;
  }

  state = 'loading';
  whenIdle(() => {
    // Floating by design: nothing downstream awaits analytics readiness,
    // and the catch path below resolves every outcome.
    void import('posthog-js')
      .then(({ default: posthog }) => {
        posthog.init(key, {
          api_host: host,
          autocapture: false,
          capture_pageview: false,
          capture_pageleave: false,
          disable_session_recording: true,
          disable_surveys: true,
          persistence: 'memory', // no cookies, no localStorage
          person_profiles: 'never',
          ip: false,
        });
        client = posthog;
        state = 'ready';
        for (const event of pending.splice(0)) {
          client.capture(event.name, { ...event.props, source: 'web' });
        }
      })
      .catch(() => {
        // Chunk failed to load (offline, blocked, deploy skew) — analytics
        // is best-effort, so disable for this session rather than retry.
        state = 'disabled';
        pending.length = 0;
      });
  });
}

// Event catalog — web-side events only. User actions fire from here;
// authoritative outcomes (match_ended, room_expired) fire from the API
// per docs/observability-and-i18n.md §1.2 so the catalog stays accurate
// when a client disconnects before it can report.
export type AnalyticsEvent =
  | { name: 'room_created'; props: { gameId: string } }
  | { name: 'room_joined'; props: { gameId: string } }
  | { name: 'match_started'; props: { gameId: string } }
  | { name: 'move_made'; props: { gameId: string } }
  | { name: 'rematch_offered'; props: { gameId: string } }
  | { name: 'rematch_accepted'; props: { gameId: string } }
  | { name: 'rematch_rejected'; props: { gameId: string } };

export function track(event: AnalyticsEvent): void {
  if (state === 'ready' && client) {
    client.capture(event.name, { ...event.props, source: 'web' });
    return;
  }
  if (state === 'disabled') return;
  // 'idle' or 'loading' — hold the event for the deferred init.
  if (pending.length < PENDING_CAP) pending.push(event);
}
