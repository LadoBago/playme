import type { BotLevel, I18nKey, PlayerDto } from '@playme/shared';

/** i18n key for a bot strength, shared by the configure form and the HUD. */
export function botLevelKey(level: BotLevel): I18nKey {
  switch (level) {
    case 'easy':
      return 'configure.botLevel.easy';
    case 'medium':
      return 'configure.botLevel.medium';
    case 'hard':
      return 'configure.botLevel.hard';
  }
}

/**
 * What to call a seat in the HUD (Sprint 12 seam D). A human is their
 * display name. A bot is rendered from its `kind` through i18n — the
 * stored `displayName` is a placeholder the server never intends to be
 * shown — with the room's bot level appended when known, e.g. "Bot · Hard".
 */
export function playerLabel(
  player: PlayerDto | null | undefined,
  botLevel: BotLevel | undefined,
  t: (key: I18nKey) => string,
): string | null {
  if (!player) return null;
  if (player.kind !== 'bot') return player.displayName;
  const name = t('match.bot');
  return botLevel ? `${name} · ${t(botLevelKey(botLevel))}` : name;
}
