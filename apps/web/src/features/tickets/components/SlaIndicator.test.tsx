import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

const allStatuses: { status: SlaStatus; label: string }[] = [
  { status: 'WithinSla', label: 'Within SLA' },
  { status: 'AtRisk', label: 'At risk' },
  { status: 'Breached', label: 'Breached' },
  { status: 'Met', label: 'Met' },
  { status: 'NotApplicable', label: 'No SLA' },
];

describe('SlaIndicator', () => {
  it.each(allStatuses)('shows "$label" in words for $status', ({ status, label }) => {
    render(<SlaIndicator status={status} />);

    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it('tells every state apart by its words, its symbol and its colour', () => {
    const seen = allStatuses.map(({ status }) => {
      const { container, unmount } = render(<SlaIndicator status={status} />);
      const badge = container.querySelector('.badge');
      const result = {
        words: badge?.textContent?.replace(/\s+/g, ' ').trim(),
        symbol: badge?.querySelector('[aria-hidden="true"]')?.textContent,
        tone: badge?.className,
      };
      unmount();

      return result;
    });

    expect(new Set(seen.map((s) => s.words)).size).toBe(allStatuses.length);
    expect(new Set(seen.map((s) => s.symbol)).size).toBe(allStatuses.length);
    expect(new Set(seen.map((s) => s.tone)).size).toBe(allStatuses.length);
  });

  it('keeps the meaning in text, so the symbol is hidden from screen readers', () => {
    render(<SlaIndicator status="AtRisk" />);

    expect(screen.getByText('▲')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.getByText('At risk')).not.toHaveAttribute('aria-hidden');
  });

  it('shows the due date under the state when it is given', () => {
    render(<SlaIndicator status="WithinSla" dueAtUtc="2026-03-02T17:00:00Z" />);

    expect(screen.getByText(/2026/)).toBeInTheDocument();
  });

  it('shows a dash for a ticket with no due date when asked for the date', () => {
    render(<SlaIndicator status="NotApplicable" dueAtUtc={null} />);

    expect(screen.getByText('-')).toBeInTheDocument();
  });

  it('shows no date at all when none is asked for', () => {
    render(<SlaIndicator status="Met" />);

    expect(screen.queryByText('-')).not.toBeInTheDocument();
  });
});
