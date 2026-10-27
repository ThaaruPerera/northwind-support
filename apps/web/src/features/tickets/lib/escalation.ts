import type { AgentSummary, TicketDetail, TicketPriority } from '../../../types/api';

/** The same limits the API enforces, so what the form accepts is what the server accepts. */
export const REASON_MIN_LENGTH = 5;
export const REASON_MAX_LENGTH = 500;
export const ACTOR_MAX_LENGTH = 100;

const priorityOrder: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

export interface EscalationErrors {
  reason?: string;
  escalatedBy?: string;
}

/** What is wrong with the input, judged on the trimmed text, as the server does. */
export function validateEscalation(reason: string, escalatedBy: string): EscalationErrors {
  const errors: EscalationErrors = {};
  const trimmedReason = reason.trim();

  if (trimmedReason.length === 0) {
    errors.reason = 'A reason is required.';
  } else if (trimmedReason.length < REASON_MIN_LENGTH) {
    errors.reason = `The reason must be at least ${REASON_MIN_LENGTH} characters.`;
  } else if (trimmedReason.length > REASON_MAX_LENGTH) {
    errors.reason = `The reason must be at most ${REASON_MAX_LENGTH} characters.`;
  }

  const trimmedName = escalatedBy.trim();

  if (trimmedName.length === 0) {
    errors.escalatedBy = 'Who is escalating the ticket is required.';
  } else if (trimmedName.length > ACTOR_MAX_LENGTH) {
    errors.escalatedBy = `The name must be at most ${ACTOR_MAX_LENGTH} characters.`;
  }

  return errors;
}

/** The priority one level up, or undefined when there is nowhere higher to go. */
export function nextPriority(priority: TicketPriority): TicketPriority | undefined {
  return priorityOrder[priorityOrder.indexOf(priority) + 1];
}

/**
 * Why a ticket cannot be escalated, or undefined when it can. The API makes the final call; this
 * lets the form say so up front instead of offering a button that is certain to fail.
 */
export function escalationBlockedReason(
  ticket: Pick<TicketDetail, 'reference' | 'priority' | 'status'>,
): string | undefined {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return `Ticket ${ticket.reference} is ${ticket.status} and cannot be escalated.`;
  }

  if (nextPriority(ticket.priority) === undefined) {
    return `Ticket ${ticket.reference} is already ${ticket.priority} and cannot be escalated.`;
  }

  return undefined;
}

/** "Alex Turner → Ben Osei", or that the owner stayed, or that nobody owned it. */
export function describeAgentChange(from: AgentSummary | null, to: AgentSummary | null): string {
  if (from === null && to === null) {
    return 'Unassigned';
  }

  if (from !== null && to !== null && from.id === to.id) {
    return `${to.fullName} (unchanged)`;
  }

  return `${from?.fullName ?? 'Unassigned'} → ${to?.fullName ?? 'Unassigned'}`;
}

const actorNameKey = 'northwind.escalatedBy';

/**
 * The name last used to escalate, so it need not be typed every time. Only a convenience while
 * there is no sign-in: it goes away when the name comes from the token.
 */
export function loadActorName(): string {
  try {
    return window.localStorage.getItem(actorNameKey) ?? '';
  } catch {
    return '';
  }
}

export function saveActorName(name: string): void {
  try {
    window.localStorage.setItem(actorNameKey, name);
  } catch {
    // Storage can be blocked or full. Not remembering the name is not worth failing for.
  }
}
