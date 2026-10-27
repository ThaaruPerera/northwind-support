import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ticketDetail } from '../../../test/fixtures';
import { TriageResult } from './TriageResult';

const triage = {
  priorityReason: 'Critical, because Security tickets are always Critical.',
  slaReason: 'Critical priority has a base window of 4h; a Premium customer gets 2h.',
  assignmentReason: 'Priya Nair has the fewest open tickets (4) of 1 eligible Security specialist.',
};

describe('TriageResult', () => {
  it('shows the applied priority, the due date and the agent, each with its reason', () => {
    render(
      <TriageResult
        ticket={ticketDetail({
          priority: 'Critical',
          assignedAgent: { id: 3, fullName: 'Priya Nair' },
          triage,
        })}
      />,
    );

    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText(triage.priorityReason)).toBeInTheDocument();
    expect(screen.getByText(triage.slaReason)).toBeInTheDocument();
    expect(screen.getByText('Priya Nair')).toBeInTheDocument();
    expect(screen.getByText(triage.assignmentReason)).toBeInTheDocument();
  });

  it('says why when nobody could take the ticket', () => {
    const reason = 'Every active Billing specialist is at their open ticket limit.';

    render(
      <TriageResult
        ticket={ticketDetail({
          assignedAgent: null,
          unassignedReason: reason,
          triage: { ...triage, assignmentReason: reason },
        })}
      />,
    );

    expect(screen.getByText(`Unassigned: ${reason}`)).toBeInTheDocument();
    // Said once, not repeated as a second note.
    expect(screen.getAllByText(new RegExp(reason))).toHaveLength(1);
  });

  it('still shows the facts when there are no reasons to show', () => {
    render(<TriageResult ticket={ticketDetail({ assignedAgent: null, triage: null })} />);

    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText('High')).toBeInTheDocument();
  });
});
