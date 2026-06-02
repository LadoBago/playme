import { createTranslator, findGame } from '@playme/shared';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// Pins the shape of the Schema.org JSON-LD we emit on the indexable
// pages. A careless edit to a builder (wrong @type, dropped field,
// non-absolute URL) is the kind of thing that silently disqualifies a
// page from rich results, so we lock the contract here.
//
// `structured-data.ts` reads SITE_URL (from @/lib/site) at module-load
// time, so we set the env and reset the module cache between cases.
describe('structured-data', () => {
  const originalEnv = { ...process.env };

  beforeEach(() => {
    vi.resetModules();
    process.env.NEXT_PUBLIC_SITE_URL = 'https://playme.ge';
  });

  afterEach(() => {
    process.env = { ...originalEnv };
  });

  it('buildWebSiteSchema emits a WebSite node with the canonical apex URL', async () => {
    const { buildWebSiteSchema } = await import('./structured-data');
    const { t } = createTranslator('ka');

    expect(buildWebSiteSchema(t, 'ka')).toMatchObject({
      '@context': 'https://schema.org',
      '@type': 'WebSite',
      name: 'PlayMe',
      url: 'https://playme.ge/',
      inLanguage: 'ka-GE',
    });
  });

  it('buildVideoGameSchema marks a two-player multiplayer browser game', async () => {
    const { buildVideoGameSchema } = await import('./structured-data');
    const { t } = createTranslator('en');
    const game = findGame('reversi');
    if (!game) throw new Error('reversi missing from catalog');

    expect(buildVideoGameSchema(game, t, 'en')).toMatchObject({
      '@type': 'VideoGame',
      name: 'Reversi',
      // en is locale-prefixed; ka would be unprefixed.
      url: 'https://playme.ge/en/play/reversi',
      inLanguage: 'en-US',
      playMode: 'MultiPlayer',
      numberOfPlayers: { '@type': 'QuantitativeValue', value: 2 },
    });
  });

  it('buildBreadcrumbSchema trails Home → Game with absolute, locale-aware URLs', async () => {
    const { buildBreadcrumbSchema } = await import('./structured-data');
    const { t } = createTranslator('ka');
    const game = findGame('connect4');
    if (!game) throw new Error('connect4 missing from catalog');

    const schema = buildBreadcrumbSchema(game, t, 'ka');
    expect(schema['@type']).toBe('BreadcrumbList');
    expect(schema.itemListElement).toMatchObject([
      { position: 1, name: 'PlayMe', item: 'https://playme.ge/' },
      { position: 2, item: 'https://playme.ge/play/connect4' },
    ]);
  });
});
