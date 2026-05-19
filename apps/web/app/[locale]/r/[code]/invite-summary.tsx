'use client';

import { type GameCatalogEntry } from '@playme/shared';
import { useTranslator } from '@/lib/use-locale';

interface InviteSummaryProps {
  hostDisplayName: string;
  game: GameCatalogEntry;
}

export function InviteSummary({ hostDisplayName, game }: InviteSummaryProps) {
  const { t } = useTranslator();
  return (
    <div className="card stack invite-summary">
      <span className="label">{t('join.invite.headline')}</span>

      <dl className="invite-summary__facts">
        <div>
          <dt>{t('join.invite.host')}</dt>
          <dd>{hostDisplayName}</dd>
        </div>
        <div>
          <dt>{t('join.invite.game')}</dt>
          <dd>{t(game.nameKey)}</dd>
        </div>
      </dl>

      <details className="invite-summary__rules">
        <summary>{t('join.invite.rules')}</summary>
        <p className="rules-panel">{t(game.rulesKey)}</p>
      </details>
    </div>
  );
}
