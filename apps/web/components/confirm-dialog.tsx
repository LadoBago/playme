'use client';

import { useEffect, useRef, type ReactElement } from 'react';

export interface ConfirmDialogProps {
  /** Whether the dialog is currently visible. */
  open: boolean;
  /** Heading text already translated by the caller. */
  title: string;
  /** Body copy already translated by the caller. */
  body: string;
  /** Label for the confirm button (already translated). */
  confirmLabel: string;
  /** Label for the cancel button (already translated). */
  cancelLabel: string;
  /** Visual tone of the confirm button. Default 'primary'. Use 'danger' for
   * destructive actions (resign, reject rematch) so the colour carries
   * the meaning, not just the copy. */
  tone?: 'primary' | 'danger';
  /** Fires when the user confirms the action. */
  onConfirm: () => void;
  /** Fires when the user cancels — clicks Cancel, presses Esc, or clicks
   * outside the dialog onto the backdrop. */
  onCancel: () => void;
}

/**
 * Shared confirmation modal. Wraps the native HTMLDialogElement so a11y
 * (focus trap, Esc-to-close, role="dialog", aria-modal) comes from the
 * browser. Used for resign confirmation and (PR-4) for reject-rematch
 * confirmation; the call site decides title/body/labels and the
 * destructive tone via {@link ConfirmDialogProps.tone}.
 *
 * The component is purely controlled — `open` flips the visibility and
 * the call site owns the boolean. We never resolve a promise or hold
 * internal state; the caller's `onConfirm` / `onCancel` callbacks are
 * the single source of truth.
 */
export function ConfirmDialog({
  open,
  title,
  body,
  confirmLabel,
  cancelLabel,
  tone = 'primary',
  onConfirm,
  onCancel,
}: ConfirmDialogProps): ReactElement {
  const ref = useRef<HTMLDialogElement | null>(null);

  // Mirror the `open` prop onto the native dialog. Calling showModal()
  // when already open throws, and close() when already closed is a
  // no-op — so we guard via the element's own `.open` getter.
  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  // Backdrop-click closes the dialog. The native ::backdrop pseudo isn't
  // a separate element — clicks on it bubble through the dialog itself,
  // so we detect them by checking that the click target is the dialog
  // (not any of its children).
  const handleBackdropClick = (e: React.MouseEvent<HTMLDialogElement>) => {
    if (e.target === ref.current) {
      onCancel();
    }
  };

  // Esc-to-close fires the native `cancel` event; intercept it so the
  // call site's onCancel runs and our controlled `open` state stays in
  // sync (otherwise the dialog closes natively but the prop stays
  // true → next render re-opens it).
  const handleCancelEvent = (e: React.SyntheticEvent<HTMLDialogElement>) => {
    e.preventDefault();
    onCancel();
  };

  const confirmClass = tone === 'danger' ? 'button-danger' : 'button-primary';

  return (
    <dialog
      ref={ref}
      className="confirm-dialog"
      onClick={handleBackdropClick}
      onCancel={handleCancelEvent}
    >
      <div className="confirm-dialog__body">
        <h2 className="confirm-dialog__title">{title}</h2>
        <p className="confirm-dialog__text">{body}</p>
        <div className="confirm-dialog__actions">
          <button type="button" className="button-ghost" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button type="button" className={confirmClass} onClick={onConfirm} autoFocus>
            {confirmLabel}
          </button>
        </div>
      </div>
    </dialog>
  );
}
