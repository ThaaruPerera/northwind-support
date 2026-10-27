import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { agentsApi } from '../../api/agents';
import { categoriesApi } from '../../api/categories';
import { ApiError } from '../../api/client';
import { customersApi } from '../../api/customers';
import { ticketsApi } from '../../api/tickets';
import { escalation, ticketDetail } from '../../test/fixtures';
import { TicketDetailPage } from './TicketDetailPage';

vi.mock('../../api/tickets', () => ({
  ticketsApi: {
    getTicket: vi.fn(),
    getEscalations: vi.fn(),
    escalate: vi.fn(),
  },
}));
vi.mock('../../api/agents', () => ({ agentsApi: { getAgents: vi.fn() } }));
vi.mock('../../api/categories', () => ({ categoriesApi: { getCategories: vi.fn() } }));
vi.mock('../../api/customers', () => ({ customersApi: { getCustomers: vi.fn() } }));

const getTicket = vi.mocked(ticketsApi.getTicket);
const getEscalations = vi.mocked(ticketsApi.getEscalations);
const escalate = vi.mocked(ticketsApi.escalate);

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/tickets/12']}>
      <Routes>
        <Route path="/tickets/:id" element={<TicketDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

async function submitEscalation(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Reason'), 'Customer is waiting on this');
  await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));
}

describe('TicketDetailPage escalation', () => {
  beforeEach(() => {
    vi.mocked(agentsApi.getAgents).mockResolvedValue([]);
    vi.mocked(categoriesApi.getCategories).mockResolvedValue([]);
    vi.mocked(customersApi.getCustomers).mockResolvedValue([]);
    getTicket.mockResolvedValue(ticketDetail({ priority: 'High' }));
    getEscalations.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('refreshes the ticket and the history after escalating, without reloading the page', async () => {
    const user = userEvent.setup();
    const recorded = escalation({
      id: 7,
      fromPriority: 'High',
      toPriority: 'Critical',
      reason: 'Customer is waiting on this',
    });
    escalate.mockResolvedValue({ ticket: ticketDetail({ priority: 'Critical' }), escalation: recorded });
    getEscalations.mockResolvedValueOnce([]).mockResolvedValueOnce([recorded]);

    renderPage();
    expect(await screen.findByText('This ticket has not been escalated.')).toBeInTheDocument();

    await submitEscalation(user);

    // The history now lists the row, and the ticket can no longer be escalated.
    const row = await screen.findByRole('listitem');
    expect(within(row).getByText('Customer is waiting on this')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('is already Critical');
    expect(screen.getByRole('button', { name: 'Escalate' })).toBeDisabled();

    expect(getEscalations).toHaveBeenCalledTimes(2);
    expect(getTicket).toHaveBeenCalledTimes(1);
  });

  it('shows a 409 and leaves the ticket and the history as they were', async () => {
    const user = userEvent.setup();
    escalate.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0012 is already Critical and cannot be escalated.'),
    );

    renderPage();
    await screen.findByText('This ticket has not been escalated.');

    await submitEscalation(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('already Critical');

    // Nothing changed: still High, still escalatable by the form, history not reloaded.
    expect(screen.getByRole('button', { name: 'Escalate to Critical' })).toBeEnabled();
    expect(screen.getByText('This ticket has not been escalated.')).toBeInTheDocument();
    await waitFor(() => expect(getEscalations).toHaveBeenCalledTimes(1));
    expect(getTicket).toHaveBeenCalledTimes(1);
  });

  it('does not call the API when the reason is too short', async () => {
    const user = userEvent.setup();

    renderPage();
    await screen.findByText('This ticket has not been escalated.');

    await user.type(screen.getByLabelText('Reason'), 'no');
    await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(screen.getByText('The reason must be at least 5 characters.')).toBeInTheDocument();
    expect(escalate).not.toHaveBeenCalled();
  });
});
