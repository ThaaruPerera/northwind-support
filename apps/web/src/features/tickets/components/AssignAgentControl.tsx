import { useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { Agent, TicketDetail } from '../../../types/api';

interface AssignAgentControlProps {
  ticket: TicketDetail;
  agents: Agent[];
  onUpdated: (ticket: TicketDetail) => void;
}

/** Hands the ticket to an agent, or takes it back. */
export function AssignAgentControl({ ticket, agents, onUpdated }: AssignAgentControlProps) {
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string>();
  const [pending, setPending] = useState<Agent>();

  const assign = async (agentId: number | null) => {
    setIsSaving(true);
    setError(undefined);
    setPending(undefined);

    try {
      onUpdated(await ticketsApi.assignAgent(ticket.id, agentId));
    } catch (caught) {
      setError(toErrorMessage(caught, 'Could not assign the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  // Automatic triage never fills an agent past their limit, but a person may. Ask first, since the
  // agent's count is shown here and going over it is easy to do by accident.
  const choose = (agentId: number | null) => {
    const agent = agents.find((candidate) => candidate.id === agentId);

    if (
      agent &&
      agent.id !== ticket.assignedAgent?.id &&
      agent.openTicketCount >= agent.maxOpenTickets
    ) {
      setPending(agent);
      return;
    }

    void assign(agentId);
  };

  return (
    <div className="card">
      <h2>Assignment</h2>

      <Field id="assign-agent" label="Assigned agent" error={error}>
        {(fieldProps) => (
          <select
            {...fieldProps}
            value={ticket.assignedAgent?.id ?? ''}
            disabled={isSaving}
            onChange={(event) =>
              choose(event.target.value === '' ? null : Number(event.target.value))
            }
          >
            <option value="">Unassigned</option>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id} disabled={!agent.isActive}>
                {agent.fullName} ({agent.openTicketCount}/{agent.maxOpenTickets} open)
                {agent.isActive ? '' : ' - inactive'}
              </option>
            ))}
          </select>
        )}
      </Field>

      {pending && (
        <div role="alertdialog" aria-labelledby="assign-confirm-text" className="field__hint">
          <p id="assign-confirm-text">
            {pending.fullName} already has {pending.openTicketCount} of {pending.maxOpenTickets}{' '}
            open tickets. Assign this one to them anyway?
          </p>
          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void assign(pending.id)}
            >
              Yes, assign
            </button>
            <button type="button" className="button" onClick={() => setPending(undefined)}>
              No
            </button>
          </div>
        </div>
      )}

      {isSaving && <p className="field__hint">Saving...</p>}
    </div>
  );
}
