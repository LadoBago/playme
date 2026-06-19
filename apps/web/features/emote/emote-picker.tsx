'use client';

import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { EMOTE_IDS, type EmoteId } from '@playme/shared';
import { useTranslator } from '@/lib/use-locale';
import { EmoteIcon } from './emote-icon';

/**
 * Client-side cooldown after a send. The server enforces the authoritative
 * limit (3 / 6 s per session, dropped silently over-limit); this just keeps
 * the trigger from inviting a mash that the server would discard anyway.
 */
const COOLDOWN_MS = 2_500;

/**
 * How long the chosen emote stays highlighted before the tray closes — a
 * visible "got it" confirmation, since the send itself is instant and
 * otherwise invisible (especially on touch, where there's no hover).
 */
const PICK_FEEDBACK_MS = 180;

interface EmotePickerProps {
  onSend: (id: EmoteId) => void;
  /** Suppressed while the connection isn't live (the send would be lost). */
  disabled?: boolean;
}

/**
 * In-match emote trigger. A single button that opens an upward popover tray
 * of reactions; picking one sends it and closes the tray. Lives in the
 * controls band so it costs no board height (see the placement mockup). The
 * tray closes on outside-click, Escape, or selection.
 */
export function EmotePicker({ onSend, disabled = false }: EmotePickerProps) {
  const { t } = useTranslator();
  const [open, setOpen] = useState(false);
  const [coolingDown, setCoolingDown] = useState(false);
  // The emote currently flashing its "picked" confirmation, if any.
  const [picked, setPicked] = useState<EmoteId | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const cooldownTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const closeTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const trayId = useId();

  useEffect(
    () => () => {
      if (cooldownTimer.current) clearTimeout(cooldownTimer.current);
      if (closeTimer.current) clearTimeout(closeTimer.current);
    },
    [],
  );

  // Close the tray on any click outside it or an Escape press. Only bound
  // while open so there's no idle listener cost.
  useEffect(() => {
    if (!open) return undefined;
    const onPointerDown = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  const handlePick = useCallback(
    (id: EmoteId) => {
      // Send immediately; keep the tray open just long enough to flash the
      // chosen emote so the tap reads as registered, then close.
      onSend(id);
      setPicked(id);
      setCoolingDown(true);
      closeTimer.current = setTimeout(() => {
        setOpen(false);
        setPicked(null);
      }, PICK_FEEDBACK_MS);
      cooldownTimer.current = setTimeout(() => setCoolingDown(false), COOLDOWN_MS);
    },
    [onSend],
  );

  const triggerDisabled = disabled || coolingDown;

  return (
    <div className="emote-picker" ref={containerRef}>
      {open ? (
        <div className="emote-picker__tray" role="menu" id={trayId} aria-label={t('match.emote.tray')}>
          {EMOTE_IDS.map((id) => (
            <button
              key={id}
              type="button"
              role="menuitem"
              className={
                picked === id
                  ? 'emote-picker__option emote-picker__option--picked'
                  : 'emote-picker__option'
              }
              aria-label={t(`match.emote.${id}`)}
              onClick={() => handlePick(id)}
            >
              <EmoteIcon id={id} />
            </button>
          ))}
        </div>
      ) : null}
      <button
        type="button"
        className="emote-picker__trigger"
        aria-label={t('match.emote.open')}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? trayId : undefined}
        disabled={triggerDisabled}
        onClick={() => setOpen((v) => !v)}
      >
        <EmoteIcon id="smile" />
      </button>
    </div>
  );
}
