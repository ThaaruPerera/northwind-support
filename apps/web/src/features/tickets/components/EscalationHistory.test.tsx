import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { AsyncData } from '../../../hooks/useAsyncData';
import { escalation } from '../../../test/fixtures';
import type { Escalation } from '../../../types/api';
import { EscalationHistory } from './EscalationHistory';

function state(overrides: Partial<AsyncData<Escalation[]>>): AsyncData<Escalation[]> {
  return {
    isLoading: false,
    reload: vi.fn().mockResolvedValue(undefined),
    setData: vi.fn(),
    ...overrides,
  };
}

describe('EscalationHistory', () => {
  it('says so when the ticket has never been escalated', () => {
    render(<EscalationHistory escalations={state({ data: [] })} />);

    expect(screen.getByText('This ticket has not been escalated.')).toBeInTheDocument();
  });

  it('shows a loading state before there is anything to show', () => {
    render(<EscalationHistory escalations={state({ isLoading: true })} />);

    expect(screen.getByRole('status')).toHaveTextContent('Loading escalation history');
  });

  it('shows an error and lets the user try again', async () => {
    const user = userEvent.setup();
    const escalations = state({ error: 'Unable to load the escalation history.' });
    render(<EscalationHistory escalations={escalations} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Unable to load the escalation history.');

    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(escalations.reload).toHaveBeenCalledTimes(1);
  });

  it('shows the priority, agent and due date changes with the reason, person and time', () => {
    render(
      <EscalationHistory
        escalations={state({
          data: [
            escalation({
              fromPriority: 'Medium',
              toPriority: 'High',
              fromAgent: { id: 4, fullName: 'Sara Lindqvist' },
              toAgent: { id: 2, fullName: 'Ben Osei' },
              reason: 'Customer is waiting on this',
              escalatedBy: 'Dana',
            }),
          ],
        })}
      />,
    );

    const row = screen.getByRole('listitem');

    expect(within(row).getByText('Medium')).toBeInTheDocument();
    expect(within(row).getByText('High')).toBeInTheDocument();
    expect(within(row).getByText('Sara Lindqvist → Ben Osei')).toBeInTheDocument();
    expect(within(row).getByText('Customer is waiting on this')).toBeInTheDocument();
    expect(within(row).getByText(/By Dana on/)).toBeInTheDocument();
    // One arrow for the agent change and one for the due date change.
    expect(within(row).getAllByText(/→/, { selector: 'dd' })).toHaveLength(2);
  });

  it('keeps the order it is given, newest first', () => {
    render(
      <EscalationHistory
        escalations={state({
          data: [
            escalation({ id: 2, reason: 'Second, newer' }),
            escalation({ id: 1, reason: 'First, older' }),
          ],
        })}
      />,
    );

    const reasons = screen.getAllByRole('listitem').map((item) => item.textContent);

    expect(reasons[0]).toContain('Second, newer');
    expect(reasons[1]).toContain('First, older');
  });

  it('says when the agent stayed the same and when a ticket had no agent', () => {
    render(
      <EscalationHistory
        escalations={state({
          data: [
            escalation({ id: 2 }),
            escalation({ id: 1, fromAgent: null, toAgent: null }),
          ],
        })}
      />,
    );

    expect(screen.getByText('Sara Lindqvist (unchanged)')).toBeInTheDocument();
    expect(screen.getByText('Unassigned')).toBeInTheDocument();
  });
});
