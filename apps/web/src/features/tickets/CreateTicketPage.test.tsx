import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { agentsApi } from '../../api/agents';
import { categoriesApi } from '../../api/categories';
import { customersApi } from '../../api/customers';
import { ticketsApi } from '../../api/tickets';
import { ticketDetail } from '../../test/fixtures';
import { CreateTicketPage } from './CreateTicketPage';

vi.mock('../../api/tickets', () => ({ ticketsApi: { createTicket: vi.fn() } }));
vi.mock('../../api/agents', () => ({ agentsApi: { getAgents: vi.fn() } }));
vi.mock('../../api/categories', () => ({ categoriesApi: { getCategories: vi.fn() } }));
vi.mock('../../api/customers', () => ({ customersApi: { getCustomers: vi.fn() } }));

const createTicket = vi.mocked(ticketsApi.createTicket);

async function raiseATicket(user: ReturnType<typeof userEvent.setup>) {
  await screen.findByLabelText('Title');

  await user.type(screen.getByLabelText('Title'), 'Suspicious admin login');
  await user.type(screen.getByLabelText('Description'), 'Unknown admin logged in overnight.');
  await user.selectOptions(screen.getByLabelText('Customer'), '1');
  await user.selectOptions(screen.getByLabelText('Category'), '5');
  await user.click(screen.getByRole('button', { name: 'Raise ticket' }));
}

describe('CreateTicketPage', () => {
  beforeEach(() => {
    vi.mocked(agentsApi.getAgents).mockResolvedValue([]);
    vi.mocked(categoriesApi.getCategories).mockResolvedValue([
      { id: 5, name: 'Security', isActive: true, requiresSpecialist: true, forcesCriticalPriority: true },
    ]);
    vi.mocked(customersApi.getCustomers).mockResolvedValue([
      { id: 1, name: 'Contoso Ltd', email: 'a@b.test', phone: null, tier: 'Premium', openTicketCount: 0 },
    ]);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('shows what was decided for the new ticket, and why', async () => {
    const user = userEvent.setup();
    createTicket.mockResolvedValue(
      ticketDetail({
        reference: 'TCK-0041',
        priority: 'Critical',
        assignedAgent: { id: 3, fullName: 'Priya Nair' },
        triage: {
          priorityReason: 'Critical, because Security tickets are always Critical.',
          slaReason: 'Critical priority has a base window of 4h; a Premium customer gets 2h.',
          assignmentReason: 'Priya Nair has the fewest open tickets (4) of 1 eligible Security specialist.',
        },
      }),
    );

    render(
      <MemoryRouter>
        <CreateTicketPage />
      </MemoryRouter>,
    );
    await raiseATicket(user);

    expect(await screen.findByText(/Raised/)).toHaveTextContent('TCK-0041');
    expect(screen.getByText('Critical, because Security tickets are always Critical.')).toBeInTheDocument();
    expect(screen.getByText(/a Premium customer gets 2h/)).toBeInTheDocument();
    expect(screen.getByText('Priya Nair')).toBeInTheDocument();
  });

  it('says the ticket is unassigned and why, and still counts it as raised', async () => {
    const user = userEvent.setup();
    const reason = 'No active agent specialises in Security.';
    createTicket.mockResolvedValue(
      ticketDetail({
        reference: 'TCK-0042',
        assignedAgent: null,
        unassignedReason: reason,
        triage: {
          priorityReason: 'High, as requested.',
          slaReason: 'High priority has a base window of 8h.',
          assignmentReason: reason,
        },
      }),
    );

    render(
      <MemoryRouter>
        <CreateTicketPage />
      </MemoryRouter>,
    );
    await raiseATicket(user);

    expect(await screen.findByText(`Unassigned: ${reason}`)).toBeInTheDocument();
    expect(screen.getByText(/Raised/)).toHaveTextContent('TCK-0042');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
