import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  describe('DEFECT-117: every filter change reaches the API', () => {
    /** Renders the hook and lets the first load settle, so each test starts from a clean call log. */
    const renderSettled = async () => {
      const hook = renderHook(() => useTicketList());

      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      getTickets.mockClear();

      return hook;
    };

    /** Lets already-queued work run without moving the debounce clock forward. */
    const flushWithoutWaiting = () =>
      act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

    it('requests immediately when the status changes', async () => {
      const { result } = await renderSettled();

      act(() => {
        result.current.updateFilters({ status: 'InProgress' });
      });
      await flushWithoutWaiting();

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'InProgress', search: '', page: 1 }),
      );
    });

    it.each([
      ['priority', { priority: 'High' }],
      ['category', { categoryId: 2 }],
      ['customer', { customerId: 3 }],
      ['assigned agent', { assignedAgentId: 4 }],
      ['unassigned only', { unassignedOnly: true }],
      ['sort', { sortBy: 'dueAtUtc', sortDirection: 'asc' }],
    ] as const)('requests immediately when the %s changes', async (_name, patch) => {
      const { result } = await renderSettled();

      act(() => {
        result.current.updateFilters(patch);
      });
      await flushWithoutWaiting();

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining(patch));
    });

    it('keeps the chosen status when the search text is typed and then cleared', async () => {
      const { result } = await renderSettled();

      act(() => {
        result.current.updateFilters({ status: 'InProgress' });
      });
      await flushWithoutWaiting();

      act(() => {
        result.current.updateFilters({ search: 'invoice' });
      });
      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'InProgress', search: 'invoice' }),
      );

      act(() => {
        result.current.updateFilters({ search: '' });
      });
      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'InProgress', search: '' }),
      );
    });

    it('sends one request for a burst of keystrokes, not one per keystroke', async () => {
      const { result } = await renderSettled();

      for (const text of ['i', 'in', 'inv', 'invo']) {
        act(() => {
          result.current.updateFilters({ search: text });
        });
        await act(async () => {
          await vi.advanceTimersByTimeAsync(100);
        });
      }

      expect(getTickets).not.toHaveBeenCalled();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'invo' }));
    });

    it('ignores a slow response that arrives after a newer request was answered', async () => {
      const { result } = await renderSettled();

      let resolveOlder: (value: ReturnType<typeof page>) => void = () => undefined;
      getTickets.mockImplementationOnce(
        () => new Promise((resolve) => (resolveOlder = resolve)),
      );
      getTickets.mockResolvedValueOnce(page([ticketListItem({ id: 2, reference: 'TCK-0002' })]));

      act(() => {
        result.current.updateFilters({ status: 'Open' });
      });
      act(() => {
        result.current.updateFilters({ status: 'Closed' });
      });
      await flushWithoutWaiting();

      expect(result.current.data?.items[0].reference).toBe('TCK-0002');

      await act(async () => {
        resolveOlder(page([ticketListItem({ id: 1, reference: 'TCK-0001' })]));
      });

      expect(result.current.data?.items[0].reference).toBe('TCK-0002');
    });

    it('does not request again for an unchanged filter value', async () => {
      const { result } = await renderSettled();

      act(() => {
        result.current.updateFilters({ status: 'Open' });
      });
      await flushWithoutWaiting();
      act(() => {
        result.current.updateFilters({ status: 'Open' });
      });
      await flushWithoutWaiting();

      expect(getTickets).toHaveBeenCalledTimes(1);
    });
  });
});
