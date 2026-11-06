import { BotIcon } from './bot-icon';

interface BotBadgeProps {
  /** Localized meaning of the badge, e.g. "Bot opponent available". */
  label: string;
}

/**
 * Corner marker on a landing-page game card: the game's module offers a
 * bot opponent. Informative only (the card is already one link, so the
 * badge must not be a second one); the icon is decorative and the
 * meaning lives in the visually-hidden text plus a native tooltip.
 */
export function BotBadge({ label }: BotBadgeProps) {
  return (
    <span className="game-card__bot" title={label}>
      <BotIcon />
      <span className="visually-hidden">{label}</span>
    </span>
  );
}
