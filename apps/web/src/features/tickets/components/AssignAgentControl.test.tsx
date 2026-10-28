import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import type { Agent } from '../../../types/api';
import { AssignAgentControl } from './AssignAgentControl';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: { assignAgent: vi.fn() },
}));

const assignAgent = vi.mocked(ticketsApi.assignAgent);

function agent(overrides: Partial<Agent>): Agent {
  return {
    id: 1,
    fullName: 'Alex Turner',
    email: 'alex.turner@northwind-support.example',
    isActive: true,
    maxOpenTickets: 6,
    openTicketCount: 2,
    specializations: [],
    ...overrides,
  };
}

const agents = [
  agent({ id: 1, fullName: 'Alex Turner', openTicketCount: 2 }),
  agent({ id: 5, fullName: 'Marcus Doyle', openTicketCount: 6 }),
];

function renderControl(onUpdated = vi.fn()) {
  render(
    <AssignAgentControl
      ticket={ticketDetail({ assignedAgent: null })}
      agents={agents}
      onUpdated={onUpdated}
    />,
  );
  return onUpdated;
}

describe('AssignAgentControl', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('assigns straight away when the agent has room', async () => {
    const user = userEvent.setup();
    assignAgent.mockResolvedValue(ticketDetail());
    const onUpdated = renderControl();

    await user.selectOptions(screen.getByLabelText('Assigned agent'), '1');

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(assignAgent).toHaveBeenCalledWith(12, 1);
    await vi.waitFor(() => expect(onUpdated).toHaveBeenCalled());
  });

  it('asks first when the agent is at their limit, and assigns on yes', async () => {
    const user = userEvent.setup();
    assignAgent.mockResolvedValue(ticketDetail());
    renderControl();

    await user.selectOptions(screen.getByLabelText('Assigned agent'), '5');

    expect(screen.getByRole('alertdialog')).toHaveTextContent('Marcus Doyle already has 6 of 6');
    expect(assignAgent).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Yes, assign' }));

    expect(assignAgent).toHaveBeenCalledWith(12, 5);
  });

  it('does nothing when the answer is no', async () => {
    const user = userEvent.setup();
    renderControl();

    await user.selectOptions(screen.getByLabelText('Assigned agent'), '5');
    await user.click(screen.getByRole('button', { name: 'No' }));

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(assignAgent).not.toHaveBeenCalled();
  });
});
