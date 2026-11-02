import { describe, expect, it } from 'vitest';
import type { I18nKey } from '@playme/shared';
import { botLevelKey, playerLabel } from './player-label';

const t = (key: I18nKey): string => `[${key}]`;

describe('playerLabel', () => {
  it('uses the display name for a human', () => {
    expect(playerLabel({ displayName: 'Lado', side: 'x', kind: 'human' }, undefined, t)).toBe('Lado');
  });

  it('treats a missing kind as human', () => {
    expect(playerLabel({ displayName: 'Nika', side: 'o' }, 'hard', t)).toBe('Nika');
  });

  it('renders a bot from i18n, never from the stored name', () => {
    expect(playerLabel({ displayName: 'Bot', side: 'o', kind: 'bot' }, undefined, t)).toBe(
      '[match.bot]',
    );
  });

  it('appends the room bot level when known', () => {
    expect(playerLabel({ displayName: 'Bot', side: 'o', kind: 'bot' }, 'hard', t)).toBe(
      '[match.bot] · [configure.botLevel.hard]',
    );
  });

  it('returns null for an empty seat', () => {
    expect(playerLabel(undefined, undefined, t)).toBeNull();
  });

  it('maps every level to a configure key', () => {
    expect(botLevelKey('easy')).toBe('configure.botLevel.easy');
    expect(botLevelKey('medium')).toBe('configure.botLevel.medium');
    expect(botLevelKey('hard')).toBe('configure.botLevel.hard');
  });
});
