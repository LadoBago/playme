import { afterEach, describe, expect, it, vi } from 'vitest';
import { fetchBotCapableGames } from './bot-support';

// The helper is the single source for "which games have a bot" on the
// public pages, so pin its two contracts: the id set mirrors the API's
// `supportsBot` flags, and any failure degrades to "no bots" rather than
// throwing (the pages must render during an API outage).
describe('fetchBotCapableGames', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns the ids of the games the API flags as bot-capable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          Response.json([
            { id: 'tictactoe', supportsBot: true },
            { id: 'reversi', supportsBot: false },
            { id: 'connect4', supportsBot: true },
          ]),
        ),
      ),
    );
    const ids = await fetchBotCapableGames();
    expect([...ids].sort()).toEqual(['connect4', 'tictactoe']);
  });

  it('degrades to an empty set when the API is unreachable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new Error('ECONNREFUSED'))),
    );
    expect((await fetchBotCapableGames()).size).toBe(0);
  });

  it('degrades to an empty set on a non-2xx response', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response('nope', { status: 503 }))),
    );
    expect((await fetchBotCapableGames()).size).toBe(0);
  });
});
