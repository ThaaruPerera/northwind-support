import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalationResult, TicketDetail } from '../../../types/api';
import {
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  escalationBlockedReason,
  nextPriority,
  validateEscalation,
  type EscalationErrors,
} from '../lib/escalation';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (result: EscalationResult) => void;
}

/**
 * Raises a ticket's priority one level, with a reason on record. Checks the same rules as the API
 * before calling it, and still shows whatever the API says if it refuses anyway. Who escalated is
 * the signed in agent, which the API knows from the token.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<EscalationErrors>({});
  const [isSaving, setIsSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  const blockedReason = escalationBlockedReason(ticket);
  const target = nextPriority(ticket.priority);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validateEscalation(reason);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      const result = await ticketsApi.escalate(ticket.id, { reason: reason.trim() });

      setReason('');
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError) {
        setErrors({ reason: caught.fieldErrors.Reason?.[0] });
      }

      setSubmitError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <form className="card form" onSubmit={submit} noValidate>
      <h2>Escalate ticket</h2>

      {blockedReason && (
        <p className="field__hint" role="status">
          {blockedReason}
        </p>
      )}

      <fieldset className="form__fieldset" disabled={blockedReason !== undefined || isSaving}>
        <Field
          id="escalation-reason"
          label="Reason"
          error={errors.reason}
          hint={`${reason.trim().length}/${REASON_MAX_LENGTH} characters, at least ${REASON_MIN_LENGTH}.`}
        >
          {(fieldProps) => (
            <textarea
              {...fieldProps}
              rows={3}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          )}
        </Field>

        <div className="button-row">
          <button type="submit" className="button button--primary">
            {isSaving ? 'Escalating...' : target && !blockedReason ? `Escalate to ${target}` : 'Escalate'}
          </button>
        </div>
      </fieldset>

      {submitError && (
        <p className="field__error" role="alert">
          {submitError}
        </p>
      )}
    </form>
  );
}
