import { afterEach, describe, expect, it, vi } from 'vitest';
import { emptyFilters } from '../features/tickets/hooks/useTicketList';
import { request } from './client';
import { ticketsApi } from './tickets';

vi.mock('./client', () => ({ request: vi.fn() }));

const send = vi.mocked(request);

/** The query string of the one request that was made. */
function queryOfRequest(): URLSearchParams {
  const path = send.mock.lastCall?.[0] ?? '';

  return new URLSearchParams(path.split('?')[1]);
}

describe('ticketsApi.getTickets', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('sends the SLA status when one is chosen', async () => {
    await ticketsApi.getTickets({ ...emptyFilters, slaStatus: 'AtRisk' });

    expect(queryOfRequest().get('slaStatus')).toBe('AtRisk');
  });

  it('leaves the SLA status out when none is chosen', async () => {
    await ticketsApi.getTickets(emptyFilters);

    expect(queryOfRequest().has('slaStatus')).toBe(false);
  });

  it('sends the SLA status together with the other filters', async () => {
    await ticketsApi.getTickets({ ...emptyFilters, slaStatus: 'Breached', priority: 'Critical', search: ' disk ' });

    const query = queryOfRequest();

    expect(query.get('slaStatus')).toBe('Breached');
    expect(query.get('priority')).toBe('Critical');
    expect(query.get('search')).toBe('disk');
  });
});
