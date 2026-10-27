import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { emptyFilters } from '../hooks/useTicketList';
import { slaDisplay, slaStatuses } from '../lib/sla';
import { TicketFilters } from './TicketFilters';

function renderFilters(overrides: Partial<typeof emptyFilters> = {}) {
  const onChange = vi.fn();
  const onReset = vi.fn();

  render(
    <TicketFilters
      filters={{ ...emptyFilters, ...overrides }}
      agents={[]}
      categories={[]}
      customers={[]}
      onChange={onChange}
      onReset={onReset}
    />,
  );

  return { onChange, onReset };
}

describe('TicketFilters SLA filter', () => {
  it('offers every SLA state, labelled the way the SLA indicator labels it', () => {
    renderFilters();

    const options = within(screen.getByLabelText('SLA')).getAllByRole('option');

    expect(options.map((option) => option.textContent)).toEqual([
      'Any SLA state',
      ...slaStatuses.map((status) => slaDisplay[status].label),
    ]);
  });

  it('reports the chosen SLA state', async () => {
    const user = userEvent.setup();
    const { onChange } = renderFilters();

    await user.selectOptions(screen.getByLabelText('SLA'), 'At risk');

    expect(onChange).toHaveBeenCalledWith({ slaStatus: 'AtRisk' });
  });

  it('reports no SLA state when "Any SLA state" is chosen again', async () => {
    const user = userEvent.setup();
    const { onChange } = renderFilters({ slaStatus: 'Breached' });

    await user.selectOptions(screen.getByLabelText('SLA'), 'Any SLA state');

    expect(onChange).toHaveBeenCalledWith({ slaStatus: undefined });
  });

  it('shows the state that is currently chosen', () => {
    renderFilters({ slaStatus: 'Met' });

    expect(screen.getByLabelText('SLA')).toHaveValue('Met');
  });
});
