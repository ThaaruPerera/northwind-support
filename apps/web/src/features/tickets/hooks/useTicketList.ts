import { useEffect, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { useDebouncedValue } from '../../../hooks/useDebouncedValue';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  data?: PagedResult<TicketListItem>;
  error?: string;
  /** The attempt (request plus retry count) the data or error above answers. */
  settledKey?: string;
}

/**
 * Owns the ticket list's filters and the data they produce.
 *
 * The request is a function of the whole filter set, so any filter (including ones added later)
 * refetches without being listed anywhere. Only the search text is debounced: it changes on every
 * keystroke, while the other filters are single deliberate choices and should apply at once.
 */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [retries, setRetries] = useState(0);
  const [state, setState] = useState<State>({});

  const search = useDebouncedValue(filters.search.trim(), DEBOUNCE_MS);

  // A string, so the effect below re-runs when the request's content changes and not merely
  // because a new object was created. `undefined` fields drop out, which the query builder
  // treats the same as absent.
  const requestKey = JSON.stringify({ ...filters, search });

  // Identifies one attempt: a different request, or the same request retried.
  const attemptKey = `${requestKey}#${retries}`;

  useEffect(() => {
    // Set by the cleanup when a newer request replaces this one, so a slower, older response
    // cannot overwrite the result of the newer one.
    let superseded = false;
    const request = JSON.parse(requestKey) as TicketFilters;

    ticketsApi.getTickets(request).then(
      (data) => {
        if (!superseded) {
          setState({ data, settledKey: attemptKey });
        }
      },
      (error: unknown) => {
        if (!superseded) {
          const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
          setState((current) => ({ ...current, error: message, settledKey: attemptKey }));
        }
      },
    );

    return () => {
      superseded = true;
    };
  }, [requestKey, attemptKey]);

  const reload = () => setRetries((count) => count + 1);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => setFilters(emptyFilters);

  // Loading is derived, not stored: whatever is on screen is stale until it answers this request.
  const isLoading = state.settledKey !== attemptKey;

  return {
    filters,
    updateFilters,
    resetFilters,
    reload,
    isLoading,
    data: state.data,
    error: isLoading ? undefined : state.error,
  };
}
