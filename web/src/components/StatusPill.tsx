import type { ShipmentStatus, VehicleStatus } from '../types';

const LABELS: Record<ShipmentStatus | VehicleStatus, string> = {
  Pending: 'In warehouse',
  InTransit: 'In transit',
  Delivered: 'Delivered',
  Available: 'Available',
  OnRoute: 'On route',
  InMaintenance: 'Maintenance',
};

const TONES: Record<ShipmentStatus | VehicleStatus, string> = {
  Pending: 'amber',
  InTransit: 'blue',
  Delivered: 'green',
  Available: 'green',
  OnRoute: 'blue',
  InMaintenance: 'gray',
};

export function statusLabel(status: ShipmentStatus | VehicleStatus) {
  return LABELS[status];
}

export function StatusPill({ status }: { status: ShipmentStatus | VehicleStatus }) {
  return (
    <span className={`pill pill-${TONES[status]}`}>
      <span className="dot" />
      {LABELS[status]}
    </span>
  );
}
