import { PlaymeClient } from '@playme/shared';
import { ssrApiBase } from './api-base';

/**
 * Server-derived game capability (Sprint 12 seam D): the ids of every
 * catalog game whose module offers a bot opponent, as reported by
 * `GET /api/games`. Shared by the landing page (bot badge on the card)
 * and the configure page (the "play with a bot" tab).
 *
 * Cached for five minutes — the set only changes with a deploy — and
 * degraded to the empty set when the API is unreachable, so both pages
 * still render (as human-only) during a build or an API outage.
 */
export async function fetchBotCapableGames(): Promise<ReadonlySet<string>> {
  try {
    const client = new PlaymeClient({ baseUrl: ssrApiBase });
    const res = await client.getGames({ next: { revalidate: 300 } });
    if (!res.ok) return new Set();
    return new Set(res.value.filter((g) => g.supportsBot).map((g) => g.id));
  } catch {
    return new Set();
  }
}
