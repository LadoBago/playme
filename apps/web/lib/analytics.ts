// PostHog wrapper. CLAUDE.md §4.2 & §5.8 in two lines:
//   - cookieless, no autocapture, no IP capture
//   - only explicit events; the catalog lives in `track()` below
//
// Featureless on purpose: features import `track(event)`, never posthog-js
// directly, so swapping the analytics SDK later is one file.

import posthog from 'posthog-js';

let initialized = false;

export function initAnalytics(): void {
  if (initialized) return;
  if (typeof window === 'undefined') return;
  const key = process.env.NEXT_PUBLIC_POSTHOG_KEY;
  const host = process.env.NEXT_PUBLIC_POSTHOG_HOST ?? 'https://eu.i.posthog.com';
  if (!key) return;

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

  initialized = true;
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
  if (!initialized) return;
  posthog.capture(event.name, { ...event.props, source: 'web' });
}
