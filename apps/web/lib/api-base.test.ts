import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// Smoke tests for the API base / hub URL helpers. Their job is mainly
// to prove the Vitest toolchain is wired for apps/web (test runner +
// Node env + CI). They also pin the URL shape the SignalR client uses,
// so a careless rename in api-base.ts shows up at test time.
//
// `api-base.ts` reads `process.env` at module-load time, so we have to
// reset the module cache between cases that change the environment.
describe('api-base', () => {
  const originalEnv = { ...process.env };

  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    process.env = { ...originalEnv };
  });

  it('hubUrl appends /hubs/room to the browser API base', async () => {
    process.env.NEXT_PUBLIC_API_URL = 'https://api.playme.ge';
    const mod = await import('./api-base');
    expect(mod.hubUrl()).toBe('https://api.playme.ge/hubs/room');
  });

  it('falls back to localhost:5080 when no env var is set', async () => {
    delete process.env.NEXT_PUBLIC_API_URL;
    delete process.env.PLAYME_API_URL;
    const mod = await import('./api-base');
    expect(mod.browserApiBase).toBe('http://localhost:5080');
    expect(mod.ssrApiBase).toBe('http://localhost:5080');
  });

  it('strips a trailing slash from the configured base', async () => {
    process.env.NEXT_PUBLIC_API_URL = 'https://api.playme.ge/';
    const mod = await import('./api-base');
    expect(mod.browserApiBase).toBe('https://api.playme.ge');
    expect(mod.hubUrl()).toBe('https://api.playme.ge/hubs/room');
  });
});
