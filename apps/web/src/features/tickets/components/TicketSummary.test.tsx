import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ticketDetail } from '../../../test/fixtures';
import { TicketSummary } from './TicketSummary';

describe('TicketSummary', () => {
  it('shows the agent when the ticket has one', () => {
    render(<TicketSummary ticket={ticketDetail()} />);

    expect(screen.getByText('Sara Lindqvist')).toBeInTheDocument();
    expect(screen.queryByText(/Unassigned/)).not.toBeInTheDocument();
  });

  it('shows why a ticket is unassigned, next to the agent field', () => {
    render(
      <TicketSummary
        ticket={ticketDetail({
          assignedAgent: null,
          unassignedReason: 'No active agent specialises in Security.',
        })}
      />,
    );

    expect(
      screen.getByText('Unassigned: No active agent specialises in Security.'),
    ).toBeInTheDocument();
  });

  it('says only "Unassigned" when no reason was recorded', () => {
    render(<TicketSummary ticket={ticketDetail({ assignedAgent: null, unassignedReason: null })} />);

    expect(screen.getByText('Unassigned')).toBeInTheDocument();
  });

  it('shows the SLA state with the shared indicator, including At risk', () => {
    render(<TicketSummary ticket={ticketDetail({ slaStatus: 'AtRisk' })} />);

    expect(screen.getByText('At risk')).toBeInTheDocument();
  });
});
