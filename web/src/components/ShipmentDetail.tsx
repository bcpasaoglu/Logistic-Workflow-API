import { useEffect, useState } from 'react';
import { api, ApiError } from '../api';
import { eligibleVehicles, formatKg } from '../lib/fleet';
import type { Shipment, Vehicle } from '../types';
import { Notice } from './Notice';
import { StatusPill } from './StatusPill';

interface Props {
  shipment: Shipment | null;
  vehicles: Vehicle[];
  onChanged: (updated?: Shipment) => void;
  onDeleted: () => void;
}

const STEPS = [
  { key: 'Pending', label: 'Received in warehouse' },
  { key: 'InTransit', label: 'Loaded on vehicle' },
  { key: 'Delivered', label: 'Delivered' },
] as const;

export function ShipmentDetail({ shipment, vehicles, onChanged, onDeleted }: Props) {
  const [vehicleId, setVehicleId] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setVehicleId('');
    setError(null);
  }, [shipment?.id]);

  if (!shipment) {
    return (
      <section className="card panel">
        <h2>Shipment details</h2>
        <p className="empty">Select a shipment from the list.</p>
      </section>
    );
  }

  const vehicle = vehicles.find((v) => v.id === shipment.vehicleId);
  const stepIndex = STEPS.findIndex((s) => s.key === shipment.status);
  // Offer every available vehicle; the API decides about capacity so its
  // error message is shown as is.
  const available = vehicles.filter((v) => v.status === 'Available');
  const fitting = new Set(eligibleVehicles(vehicles, shipment.weight).map((v) => v.id));

  async function run(action: () => Promise<Shipment | void>, deleted = false) {
    setBusy(true);
    setError(null);
    try {
      const result = await action();
      if (deleted) onDeleted();
      else onChanged(result || undefined);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Request failed.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card panel">
      <header className="panel-head">
        <div>
          <h2>Shipment details</h2>
          <p className="muted">
            {shipment.trackingNumber} · created {new Date(shipment.createdAt + (shipment.createdAt.endsWith('Z') ? '' : 'Z')).toLocaleDateString('en-GB')}
          </p>
        </div>
        <StatusPill status={shipment.status} />
      </header>

      <ol className="timeline">
        {STEPS.map((step, i) => (
          <li key={step.key} className={i <= stepIndex ? 'done' : ''}>
            <span className="node" />
            <span>{step.label}</span>
            {step.key === 'InTransit' && vehicle && i <= stepIndex && <span className="muted small">{vehicle.plateNumber}</span>}
          </li>
        ))}
      </ol>

      <dl className="facts">
        <div>
          <dt>Receiver</dt>
          <dd>{shipment.receiverAddress}</dd>
        </div>
        <div>
          <dt>Weight</dt>
          <dd>{formatKg(shipment.weight)}</dd>
        </div>
        <div>
          <dt>Vehicle</dt>
          <dd>{vehicle ? `${vehicle.plateNumber} (${formatKg(vehicle.maxCapacity)})` : 'Not assigned'}</dd>
        </div>
      </dl>

      {error && <Notice kind="error" onClose={() => setError(null)}>{error}</Notice>}

      <div className="actions">
        {shipment.status === 'Pending' && (
          <>
            <select value={vehicleId} onChange={(e) => setVehicleId(e.target.value)} aria-label="Vehicle">
              <option value="">Choose an available vehicle</option>
              {available.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.plateNumber} · {formatKg(v.maxCapacity)}
                  {fitting.has(v.id) ? '' : ' (too small)'}
                </option>
              ))}
            </select>
            <button
              className="btn btn-dark"
              disabled={!vehicleId || busy}
              onClick={() => run(() => api.loadToVehicle(shipment.id, Number(vehicleId)))}
            >
              Load to vehicle
            </button>
            <button className="btn btn-ghost" disabled={busy} onClick={() => run(() => api.deleteShipment(shipment.id), true)}>
              Delete
            </button>
          </>
        )}
        {shipment.status === 'InTransit' && (
          <button className="btn btn-dark" disabled={busy} onClick={() => run(() => api.deliver(shipment.id))}>
            Mark delivered
          </button>
        )}
        {shipment.status === 'Delivered' && <p className="muted small">Delivered. The vehicle is free for the next load.</p>}
      </div>
    </section>
  );
}
