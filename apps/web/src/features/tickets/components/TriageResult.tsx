import { formatDateTime } from '../../../lib/format';
import type { TicketDetail } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface TriageResultProps {
  ticket: TicketDetail;
}

/**
 * What the system decided for a ticket that has just been raised, and why: its priority, when it
 * is due and who has it, or that nobody does and the reason.
 */
export function TriageResult({ ticket }: TriageResultProps) {
  const { triage } = ticket;

  return (
    <dl className="summary triage">
      <div>
        <dt>Priority</dt>
        <dd>
          <PriorityBadge priority={ticket.priority} />
          {triage && <span className="summary__note">{triage.priorityReason}</span>}
        </dd>
      </div>

      <div>
        <dt>Due</dt>
        <dd>
          {formatDateTime(ticket.dueAtUtc)}
          {triage && <span className="summary__note">{triage.slaReason}</span>}
        </dd>
      </div>

      <div>
        <dt>Assigned to</dt>
        <dd>
          {ticket.assignedAgent ? (
            <>
              {ticket.assignedAgent.fullName}
              {triage && <span className="summary__note">{triage.assignmentReason}</span>}
            </>
          ) : (
            // Nobody could take it: say so, and why, in the same words the ticket page uses.
            ticket.unassignedReason ? `Unassigned: ${ticket.unassignedReason}` : 'Unassigned'
          )}
        </dd>
      </div>
    </dl>
  );
}
