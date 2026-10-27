import type { BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

export interface SlaDisplay {
  label: string;
  /** A shape that differs for every state, so the state never depends on colour alone. */
  symbol: string;
  tone: BadgeTone;
}

// A Record over every status: a state added to the API will not compile until it is shown here.
export const slaDisplay: Record<SlaStatus, SlaDisplay> = {
  WithinSla: { label: 'Within SLA', symbol: '●', tone: 'info' },
  AtRisk: { label: 'At risk', symbol: '▲', tone: 'warning' },
  Breached: { label: 'Breached', symbol: '✕', tone: 'danger' },
  Met: { label: 'Met', symbol: '✔', tone: 'success' },
  NotApplicable: { label: 'No SLA', symbol: '–', tone: 'neutral' },
};

/** The statuses in the order a person would want them listed. */
export const slaStatuses: SlaStatus[] = ['WithinSla', 'AtRisk', 'Breached', 'Met', 'NotApplicable'];
