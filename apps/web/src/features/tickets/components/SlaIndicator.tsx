import { Badge, type BadgeTone } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';

interface SlaDisplay {
  label: string;
  /** A shape that differs for every state, so the state never depends on colour alone. */
  symbol: string;
  tone: BadgeTone;
}

// A Record over every status: a state added to the API will not compile until it is shown here.
const display: Record<SlaStatus, SlaDisplay> = {
  WithinSla: { label: 'Within SLA', symbol: '●', tone: 'info' },
  AtRisk: { label: 'At risk', symbol: '▲', tone: 'warning' },
  Breached: { label: 'Breached', symbol: '✕', tone: 'danger' },
  Met: { label: 'Met', symbol: '✔', tone: 'success' },
  NotApplicable: { label: 'No SLA', symbol: '–', tone: 'neutral' },
};

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
  const { label, symbol, tone } = display[status];

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
