import { formatKg } from '../lib/fleet';
import type { PagedResult, Shipment, ShipmentStatus } from '../types';
import { StatusPill } from './StatusPill';

type Filter = ShipmentStatus | undefined;
const FILTERS: { key: Filter; label: string }[] = [
  { key: undefined, label: 'All' },
  { key: 'Pending', label: 'Warehouse' },
  { key: 'InTransit', label: 'In transit' },
  { key: 'Delivered', label: 'Delivered' },
];

interface Props {
  page: PagedResult<Shipment> | null;
  filter: Filter;
  selectedId: number | null;
  onFilter: (f: Filter) => void;
  onPage: (page: number) => void;
  onSelect: (s: Shipment) => void;
}

export function ShipmentsPanel({ page, filter, selectedId, onFilter, onPage, onSelect }: Props) {
  return (
    <section className="card panel">
      <header className="panel-head">
        <div>
          <h2>Shipments</h2>
          <p className="muted">{page ? `${page.totalCount} total` : 'Loading…'}</p>
        </div>
      </header>

      <div className="chips">
        {FILTERS.map((f) => (
          <button key={f.label} className={`chip ${filter === f.key ? 'active' : ''}`} onClick={() => onFilter(f.key)}>
            {f.label}
          </button>
        ))}
      </div>

      <ul className="stack list">
        {page?.items.map((s) => (
          <li key={s.id}>
            <button className={`item item-button ${selectedId === s.id ? 'selected' : ''}`} onClick={() => onSelect(s)}>
              <div className="item-head">
                <div>
                  <div className="plate">{s.trackingNumber}</div>
                  <div className="muted small">{s.receiverAddress}</div>
                </div>
                <span className="weight">{formatKg(s.weight)}</span>
              </div>
              <StatusPill status={s.status} />
            </button>
          </li>
        ))}
        {page && page.items.length === 0 && <li className="empty">No shipments here yet.</li>}
      </ul>

      {page && page.totalPages > 1 && (
        <nav className="pager" aria-label="Pages">
          <button className="btn btn-ghost btn-sm" disabled={!page.hasPreviousPage} onClick={() => onPage(page.page - 1)}>
            Previous
          </button>
          <span className="muted small">
            Page {page.page} of {page.totalPages}
          </span>
          <button className="btn btn-ghost btn-sm" disabled={!page.hasNextPage} onClick={() => onPage(page.page + 1)}>
            Next
          </button>
        </nav>
      )}
    </section>
  );
}
