import type { Escalation, PagedResult, TicketDetail, TicketListItem } from '../types/api';

/** A list row with sensible defaults, so tests only state what they care about. */
export function ticketListItem(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    status: 'InProgress',
    priority: 'High',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard' },
    category: { id: 2, name: 'Billing' },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'Breached',
    ...overrides,
  };
}

export function page(items: TicketListItem[]): PagedResult<TicketListItem> {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
  };
}

/** A ticket page with sensible defaults: High, in progress, owned, not yet escalated. */
export function ticketDetail(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    description: 'Invoice 5512 is EUR 240 higher than the confirmed order.',
    status: 'InProgress',
    priority: 'High',
    customer: {
      id: 2,
      name: 'Fabrikam Inc',
      tier: 'Standard',
      email: 'helpdesk@fabrikam.example',
      phone: null,
    },
    category: { id: 2, name: 'Billing', requiresSpecialist: true },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'WithinSla',
    ...overrides,
  };
}

/** One history row: Medium to High, same agent, with a reason and a person. */
export function escalation(overrides: Partial<Escalation> = {}): Escalation {
  return {
    id: 1,
    ticketId: 12,
    fromPriority: 'Medium',
    toPriority: 'High',
    fromAgent: { id: 4, fullName: 'Sara Lindqvist' },
    toAgent: { id: 4, fullName: 'Sara Lindqvist' },
    fromDueAtUtc: '2026-09-25T08:00:00Z',
    toDueAtUtc: '2026-09-24T16:12:00Z',
    reason: 'Customer is waiting on this',
    escalatedBy: 'Dana',
    escalatedAtUtc: '2026-09-24T08:12:00Z',
    ...overrides,
  };
}
