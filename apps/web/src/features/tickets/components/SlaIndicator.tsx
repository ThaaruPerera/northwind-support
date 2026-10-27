import { Badge } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';
import { slaDisplay } from '../lib/sla';

interface SlaIndicatorProps {
  status: SlaStatus;
  /** When given, the due date is shown under the state; pass null for a ticket with no due date. */
  dueAtUtc?: string | null;
}

/**
 * Where a ticket stands against its SLA. The one place that decides how each state looks, used by
 * the ticket list and the ticket page alike. The words carry the meaning; the symbol and the
 * colour only reinforce them.
 */
export function SlaIndicator({ status, dueAtUtc }: SlaIndicatorProps) {
  const { label, symbol, tone } = slaDisplay[status];

  return (
    <div className="sla-indicator">
      <Badge tone={tone}>
        <span aria-hidden="true">{symbol}</span> <span>{label}</span>
      </Badge>
      {dueAtUtc !== undefined && (
        <span className="sla-indicator__due">{formatDateTime(dueAtUtc)}</span>
      )}
    </div>
  );
}
