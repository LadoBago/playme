'use client';

import { useEffect, useState } from 'react';
import * as Sentry from '@sentry/nextjs';
import { track } from '@/lib/analytics';

/**
 * One-off dev page to verify Sentry + PostHog wiring. Hit it once after
 * configuring DSNs, confirm the error appears in Sentry and the event
 * in PostHog, then delete this file.
 *
 * Not for production — drop before Sprint 7.
 */
export default function SentryTestPage() {
  const [sent, setSent] = useState(false);

  useEffect(() => {
    track({ name: 'room_created', props: { gameId: 'sentry-test', timeLimit: 'ThreeMin' } });
  }, []);

  return (
    <main className="container stack">
      <h1>Sentry + PostHog smoke test</h1>
      <p>On mount: fires a <code>room_created</code> PostHog event.</p>
      <p>Click below: throws an uncaught client error captured by Sentry.</p>
      <button
        type="button"
        className="button"
        onClick={() => {
          Sentry.captureException(new Error('PlayMe Sentry smoke test (client)'));
          setSent(true);
        }}
      >
        Trigger Sentry test error
      </button>
      {sent ? <p>Sent. Check sentry.io.</p> : null}
    </main>
  );
}
