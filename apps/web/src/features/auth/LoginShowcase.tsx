import { useState } from 'react';

interface Pointer {
  x: number;
  y: number;
}

const still: Pointer = { x: 0, y: 0 };

/** The rows of the faint queue drawn behind the headline: a short id, a title bar and a status pill. */
const queueRows = [
  { title: 180, pill: 52, tone: 'danger' },
  { title: 140, pill: 56, tone: 'warning' },
  { title: 200, pill: 60, tone: 'success' },
  { title: 120, pill: 52, tone: 'info' },
] as const;

const sampleTickets = [
  { id: '#4821', title: 'Refund not received after 14 days', label: 'Urgent', tone: 'danger' },
  { id: '#4817', title: "Can't update billing address", label: 'Pending', tone: 'warning' },
  { id: '#4809', title: 'Order arrived damaged', label: 'Resolved', tone: 'success' },
] as const;

function QueueSketch({ className }: { className: string }) {
  return (
    <div className={`showcase__sketch ${className}`}>
      {queueRows.map((row) => (
        <div className="showcase__sketch-row" key={row.title}>
          <span className="showcase__bar" style={{ width: 36 }} />
          <span className="showcase__bar" style={{ width: row.title }} />
          <span className={`showcase__pill showcase__pill--${row.tone}`} style={{ width: row.pill }} />
        </div>
      ))}
    </div>
  );
}

/**
 * The decorative half of the login page. It carries no information, so it is hidden from assistive
 * technology, and its parts drift gently with the pointer unless the person has asked for less motion.
 */
export function LoginShowcase() {
  const [pointer, setPointer] = useState<Pointer>(still);

  const follow = (event: React.MouseEvent<HTMLElement>) => {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      return;
    }

    const box = event.currentTarget.getBoundingClientRect();

    setPointer({
      x: ((event.clientX - box.left) / box.width) * 2 - 1,
      y: ((event.clientY - box.top) / box.height) * 2 - 1,
    });
  };

  return (
    <aside
      className="showcase"
      aria-hidden="true"
      style={{ '--pointer-x': pointer.x, '--pointer-y': pointer.y } as React.CSSProperties}
      onMouseMove={follow}
      onMouseLeave={() => setPointer(still)}
    >
      <div className="showcase__backdrop">
        <div className="showcase__ring showcase__ring--large" />
        <div className="showcase__ring showcase__ring--small" />
        <QueueSketch className="showcase__sketch--far" />
        <QueueSketch className="showcase__sketch--near" />
      </div>

      <div className="showcase__copy">
        <span className="showcase__badge">
          <span className="showcase__dot" />
          Internal support desk
        </span>
        <div className="showcase__headline">
          Every Northwind
          <br />
          ticket <span>in one queue.</span>
        </div>
        <p>One shared queue to pick up, pass on and close every ticket.</p>
      </div>

      <div className="showcase__card-wrap">
        <div className="showcase__chip">
          <span className="showcase__avatar">MC</span>
          <span className="showcase__chip-text">
            <strong>#4821 assigned to Maya</strong>
            <span>Escalated · just now</span>
          </span>
        </div>

        <div className="showcase__card">
          <div className="showcase__card-head">
            <span>Open queue</span>
            <span>3 tickets</span>
          </div>
          {sampleTickets.map((ticket) => (
            <div className="showcase__ticket" key={ticket.id}>
              <span className="showcase__ticket-id">{ticket.id}</span>
              <span className="showcase__ticket-title">{ticket.title}</span>
              <span className={`showcase__label showcase__label--${ticket.tone}`}>{ticket.label}</span>
            </div>
          ))}
        </div>
      </div>
    </aside>
  );
}
