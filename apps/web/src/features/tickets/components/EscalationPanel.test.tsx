import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { escalation, ticketDetail } from '../../../test/fixtures';
import type { EscalationResult } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
  },
}));

const escalate = vi.mocked(ticketsApi.escalate);

function result(): EscalationResult {
  return {
    ticket: ticketDetail({ priority: 'Critical' }),
    escalation: escalation({ fromPriority: 'High', toPriority: 'Critical' }),
  };
}

async function fill(user: ReturnType<typeof userEvent.setup>, reason: string, name = 'Dana') {
  const nameBox = screen.getByLabelText('Your name');
  await user.clear(nameBox);

  if (name) {
    await user.type(nameBox, name);
  }

  if (reason) {
    await user.type(screen.getByLabelText('Reason'), reason);
  }
}

describe('EscalationPanel', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('blocks a short reason and never calls the API', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fill(user, 'abc');
    await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(screen.getByText('The reason must be at least 5 characters.')).toBeInTheDocument();
    expect(screen.getByLabelText('Reason')).toHaveAttribute('aria-invalid', 'true');
    expect(escalate).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('blocks a reason that is too long and never calls the API', async () => {
    const user = userEvent.setup();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fill(user, '');
    await user.click(screen.getByLabelText('Reason'));
    await user.paste('a'.repeat(501));
    await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(screen.getByText('The reason must be at most 500 characters.')).toBeInTheDocument();
    expect(escalate).not.toHaveBeenCalled();
  });

  it('blocks a missing name and never calls the API', async () => {
    const user = userEvent.setup();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fill(user, 'Customer is waiting', '');
    await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(screen.getByText('Who is escalating the ticket is required.')).toBeInTheDocument();
    expect(escalate).not.toHaveBeenCalled();
  });

  it('sends the trimmed reason and name, and hands the result on', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn();
    escalate.mockResolvedValue(result());
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fill(user, '  Customer is waiting  ', ' Dana ');
    await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    await waitFor(() => expect(onEscalated).toHaveBeenCalledWith(result()));
    expect(escalate).toHaveBeenCalledWith(12, { reason: 'Customer is waiting', escalatedBy: 'Dana' });
    expect(screen.getByLabelText('Reason')).toHaveValue('');
  });

  it('shows a pending state and ignores a second click while the request runs', async () => {
    const user = userEvent.setup();
    let finish: (value: EscalationResult) => void = () => undefined;
    escalate.mockReturnValue(new Promise((resolve) => (finish = resolve)));
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fill(user, 'Customer is waiting');
    await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    const pending = screen.getByRole('button', { name: 'Escalating...' });
    expect(pending).toBeDisabled();
    expect(screen.getByLabelText('Reason')).toBeDisabled();

    await user.click(pending);
    expect(escalate).toHaveBeenCalledTimes(1);

    finish(result());
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Escalating...' })).toBeNull());
  });

  it('shows the API refusal and does not report an escalation', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn();
    escalate.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0012 is already Critical and cannot be escalated.'),
    );
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fill(user, 'Customer is waiting');
    await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ticket TCK-0012 is already Critical and cannot be escalated.',
    );
    expect(onEscalated).not.toHaveBeenCalled();
    expect(screen.getByLabelText('Reason')).toHaveValue('Customer is waiting');
  });

  it('puts a validation failure from the API on its field', async () => {
    const user = userEvent.setup();
    escalate.mockRejectedValue(
      new ApiError(400, 'One or more validation errors occurred.', {
        errors: { Reason: ['The reason must be at least 5 characters.'] },
      }),
    );
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fill(user, 'Customer is waiting');
    await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));

    expect(await screen.findByText('The reason must be at least 5 characters.')).toBeInTheDocument();
  });

  it.each([
    ['Critical', 'Open', 'is already Critical'],
    ['High', 'Resolved', 'is Resolved'],
    ['High', 'Closed', 'is Closed'],
  ] as const)(
    'is disabled with an explanation for a %s ticket that is %s',
    async (priority, status, explanation) => {
      render(
        <EscalationPanel ticket={ticketDetail({ priority, status })} onEscalated={vi.fn()} />,
      );

      expect(screen.getByRole('status')).toHaveTextContent(explanation);
      expect(screen.getByRole('button', { name: 'Escalate' })).toBeDisabled();
      expect(screen.getByLabelText('Reason')).toBeDisabled();
    },
  );

  it('offers the next priority on the button', () => {
    render(<EscalationPanel ticket={ticketDetail({ priority: 'Low' })} onEscalated={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Escalate to Medium' })).toBeEnabled();
  });

  it('remembers the name for next time', async () => {
    const user = userEvent.setup();
    escalate.mockResolvedValue(result());
    const { unmount } = render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fill(user, 'Customer is waiting', 'Dana');
    await user.click(screen.getByRole('button', { name: 'Escalate to Critical' }));
    await waitFor(() => expect(escalate).toHaveBeenCalled());
    unmount();

    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    expect(screen.getByLabelText('Your name')).toHaveValue('Dana');
  });

  it('counts the reason as it is typed', async () => {
    const user = userEvent.setup();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await user.type(screen.getByLabelText('Reason'), 'Hello');

    expect(screen.getByText('5/500 characters, at least 5.')).toBeInTheDocument();
  });
});
