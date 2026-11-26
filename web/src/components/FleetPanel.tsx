import { useState } from 'react';
import { api, ApiError } from '../api';
import { countByStatus, formatKg, loadPercent } from '../lib/fleet';
import type { Vehicle, VehicleStatus } from '../types';
import { Notice } from './Notice';
import { StatusPill } from './StatusPill';

type Filter = VehicleStatus | 'All';
const FILTERS: { key: Filter; label: string }[] = [
  { key: 'All', label: 'All' },
  { key: 'Available', label: 'Available' },
  { key: 'OnRoute', label: 'On route' },
  { key: 'InMaintenance', label: 'Maintenance' },
];

interface Props {
  vehicles: Vehicle[];
  load: Map<number, number>;
  onChanged: () => void;
}

export function FleetPanel({ vehicles, load, onChanged }: Props) {
  const [filter, setFilter] = useState<Filter>('All');
  const [plate, setPlate] = useState('');
  const [capacity, setCapacity] = useState('');
  const [error, setError] = useState<string | null>(null);

  const counts = countByStatus(vehicles);
  const visible = filter === 'All' ? vehicles : vehicles.filter((v) => v.status === filter);

  async function run(action: () => Promise<unknown>) {
    setError(null);
    try {
      await action();
      onChanged();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Request failed.');
    }
  }

  return (
    <section className="card panel">
      <header className="panel-head">
        <div>
          <h2>Fleet</h2>
          <p className="muted">
            {vehicles.length} vehicles · {counts.OnRoute} on route
          </p>
        </div>
      </header>

      <div className="chips">
        {FILTERS.map((f) => (
          <button key={f.key} className={`chip ${filter === f.key ? 'active' : ''}`} onClick={() => setFilter(f.key)}>
            {f.label} <span className="count">{counts[f.key]}</span>
          </button>
        ))}
      </div>

      {error && <Notice kind="error" onClose={() => setError(null)}>{error}</Notice>}

      <div className="stack">
        {visible.map((v) => {
          const kg = load.get(v.id) ?? 0;
          const pct = loadPercent(kg, v.maxCapacity);
          return (
            <article key={v.id} className="item">
              <div className="item-head">
                <div>
                  <div className="plate">{v.plateNumber}</div>
                  <div className="muted small">Capacity {formatKg(v.maxCapacity)}</div>
                </div>
                <StatusPill status={v.status} />
              </div>
              <div className={`bar bar-${v.status}`} aria-label={`Load ${pct}%`}>
                <span style={{ width: `${pct}%` }} />
              </div>
              <div className="item-foot muted small">
                <span>Load {formatKg(kg)}</span>
                <span>{pct}% used</span>
              </div>
              {v.status !== 'OnRoute' && (
                <button
                  className="btn btn-ghost btn-sm"
                  onClick={() =>
                    run(() =>
                      api.updateVehicle({ ...v, status: v.status === 'Available' ? 'InMaintenance' : 'Available' }),
                    )
                  }
                >
                  {v.status === 'Available' ? 'Send to maintenance' : 'Mark available'}
                </button>
              )}
            </article>
          );
        })}
        {visible.length === 0 && <p className="empty">No vehicles in this group.</p>}
      </div>

      <form
        className="inline-form"
        onSubmit={(e) => {
          e.preventDefault();
          run(async () => {
            await api.addVehicle(plate.trim(), Number(capacity));
            setPlate('');
            setCapacity('');
          });
        }}
      >
        <input placeholder="Plate, e.g. 34 ABC 123" required value={plate} onChange={(e) => setPlate(e.target.value)} />
        <input
          placeholder="Capacity (kg)"
          type="number"
          min="0.01"
          step="0.01"
          required
          value={capacity}
          onChange={(e) => setCapacity(e.target.value)}
        />
        <button className="btn btn-ghost">Add vehicle</button>
      </form>
    </section>
  );
}
