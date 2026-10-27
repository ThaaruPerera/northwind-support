import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { agentsApi } from '../../api/agents';
import { categoriesApi } from '../../api/categories';
import { customersApi } from '../../api/customers';
import { ticketsApi } from '../../api/tickets';
import { page, ticketListItem } from '../../test/fixtures';
import { TicketListPage } from './TicketListPage';

vi.mock('../../api/tickets', () => ({ ticketsApi: { getTickets: vi.fn() } }));
vi.mock('../../api/agents', () => ({ agentsApi: { getAgents: vi.fn() } }));
vi.mock('../../api/categories', () => ({ categoriesApi: { getCategories: vi.fn() } }));
vi.mock('../../api/customers', () => ({ customersApi: { getCustomers: vi.fn() } }));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('TicketListPage SLA filter', () => {
  beforeEach(() => {
    vi.mocked(agentsApi.getAgents).mockResolvedValue([]);
    vi.mocked(categoriesApi.getCategories).mockResolvedValue([]);
    vi.mocked(customersApi.getCustomers).mockResolvedValue([]);
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('asks the API for the chosen SLA state straight away and shows what comes back', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <TicketListPage />
      </MemoryRouter>,
    );
    await screen.findByRole('link', { name: 'TCK-0012' });

    getTickets.mockResolvedValue(
      page([ticketListItem({ id: 99, reference: 'TCK-0099', slaStatus: 'AtRisk' })]),
    );

    await user.selectOptions(screen.getByLabelText('SLA'), 'At risk');

    await waitFor(() =>
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ slaStatus: 'AtRisk', page: 1 })),
    );
    expect(await screen.findByRole('link', { name: 'TCK-0099' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'TCK-0012' })).not.toBeInTheDocument();
  });

  it('goes back to every ticket when the filters are cleared', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <TicketListPage />
      </MemoryRouter>,
    );
    await screen.findByRole('link', { name: 'TCK-0012' });

    await user.selectOptions(screen.getByLabelText('SLA'), 'Breached');
    await waitFor(() =>
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ slaStatus: 'Breached' })),
    );

    await user.click(screen.getByRole('button', { name: 'Clear filters' }));

    await waitFor(() => expect(getTickets.mock.lastCall?.[0].slaStatus).toBeUndefined());
    expect(screen.getByLabelText('SLA')).toHaveValue('');
  });
});
