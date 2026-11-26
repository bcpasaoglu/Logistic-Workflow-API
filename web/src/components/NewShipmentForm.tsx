import { useState } from 'react';
import { api, ApiError } from '../api';
import { formatKg } from '../lib/fleet';
import type { Shipment, Vehicle } from '../types';
import { Notice } from './Notice';

export function NewShipmentForm({ vehicles, onCreated }: { vehicles: Vehicle[]; onCreated: (s: Shipment) => void }) {
  const empty = { trackingNumber: '', weight: '', receiverAddress: '', vehicleId: '' };
  const [form, setForm] = useState(empty);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm({ ...form, [key]: e.target.value });

  const available = vehicles.filter((v) => v.status === 'Available');

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setSuccess(null);
    try {
      const created = await api.createShipment({
        trackingNumber: form.trackingNumber.trim() || undefined,
        weight: Number(form.weight),
        receiverAddress: form.receiverAddress.trim(),
        vehicleId: form.vehicleId ? Number(form.vehicleId) : undefined,
      });
      setSuccess(
        created.vehicleId
          ? `${created.trackingNumber} loaded and on its way.`
          : `${created.trackingNumber} stored in the warehouse.`,
      );
      setForm(empty);
      onCreated(created);
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
          <h2>New shipment</h2>
          <p className="muted">Store it in the warehouse or load it straight onto a vehicle.</p>
        </div>
      </header>
      <form className="form" onSubmit={submit}>
        <div className="row">
          <label>
            Tracking number
            <input placeholder="Generated if empty" maxLength={32} value={form.trackingNumber} onChange={set('trackingNumber')} />
          </label>
          <label>
            Weight (kg)
            <input type="number" min="0.01" step="0.01" required value={form.weight} onChange={set('weight')} />
          </label>
        </div>
        <label>
          Receiver address
          <input required maxLength={500} value={form.receiverAddress} onChange={set('receiverAddress')} />
        </label>
        <label>
          Vehicle
          <select value={form.vehicleId} onChange={set('vehicleId')}>
            <option value="">Keep in warehouse</option>
            {available.map((v) => (
              <option key={v.id} value={v.id}>
                {v.plateNumber} · max {formatKg(v.maxCapacity)}
              </option>
            ))}
          </select>
        </label>
        {error && (
          <Notice kind="error" onClose={() => setError(null)}>
            {error}
          </Notice>
        )}
        {success && <Notice kind="success">{success}</Notice>}
        <button className="btn btn-dark" disabled={busy}>
          {form.vehicleId ? 'Create and load' : 'Create shipment'}
        </button>
      </form>
    </section>
  );
}
