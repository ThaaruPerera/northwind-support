import { EmptyState } from '../../../components/EmptyState';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import type { AsyncData } from '../../../hooks/useAsyncData';
import { formatDateTime } from '../../../lib/format';
import type { Escalation } from '../../../types/api';
import { describeAgentChange } from '../lib/escalation';
import { PriorityBadge } from './TicketBadges';

interface EscalationHistoryProps {
  escalations: AsyncData<Escalation[]>;
}

/** Every time the ticket was escalated, newest first: what changed, who asked and why. */
export function EscalationHistory({ escalations }: EscalationHistoryProps) {
  const { data, error, isLoading, reload } = escalations;

  return (
    <div className="card">
      <h2>Escalation history</h2>

      {error && <ErrorMessage message={error} onRetry={() => void reload()} />}

      {isLoading && !data && <Spinner label="Loading escalation history" />}

      {data && data.length === 0 && (
        <EmptyState title="This ticket has not been escalated." />
      )}

      {data && data.length > 0 && (
        <ol className="history" aria-busy={isLoading}>
          {data.map((escalation) => (
            <li key={escalation.id} className="history__item">
              <p className="history__change">
                <PriorityBadge priority={escalation.fromPriority} />
                <span aria-hidden="true"> → </span>
                <span className="visually-hidden"> raised to </span>
                <PriorityBadge priority={escalation.toPriority} />
              </p>

              <dl className="history__details">
                <div>
                  <dt>Agent</dt>
                  <dd>{describeAgentChange(escalation.fromAgent, escalation.toAgent)}</dd>
                </div>
                <div>
                  <dt>Due</dt>
                  <dd>
                    {formatDateTime(escalation.fromDueAtUtc)} → {formatDateTime(escalation.toDueAtUtc)}
                  </dd>
                </div>
              </dl>

              <p className="history__reason">{escalation.reason}</p>

              <p className="history__meta">
                By {escalation.escalatedBy} on{' '}
                <time dateTime={escalation.escalatedAtUtc}>
                  {formatDateTime(escalation.escalatedAtUtc)}
                </time>
              </p>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
