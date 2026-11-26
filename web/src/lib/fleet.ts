import type { Shipment, Vehicle, VehicleStatus } from '../types';

/** Weight currently loaded on each vehicle (sum of its in-transit shipments). */
export function loadByVehicle(shipments: Shipment[]): Map<number, number> {
  const load = new Map<number, number>();
  for (const s of shipments) {
    if (s.vehicleId != null && s.status === 'InTransit') {
      load.set(s.vehicleId, (load.get(s.vehicleId) ?? 0) + s.weight);
    }
  }
  return load;
}

/** Load as a percentage of capacity, clamped to 0..100. */
export function loadPercent(load: number, capacity: number): number {
  if (capacity <= 0) return 0;
  return Math.min(100, Math.max(0, Math.round((load / capacity) * 100)));
}

export function countByStatus(vehicles: Vehicle[]): Record<VehicleStatus | 'All', number> {
  const counts = { All: vehicles.length, Available: 0, OnRoute: 0, InMaintenance: 0 };
  for (const v of vehicles) counts[v.status]++;
  return counts;
}

/** Vehicles that can take a shipment of the given weight right now. */
export function eligibleVehicles(vehicles: Vehicle[], weight: number): Vehicle[] {
  return vehicles.filter((v) => v.status === 'Available' && v.isActive && v.maxCapacity >= weight);
}

export function formatKg(value: number): string {
  return `${value.toLocaleString('en-US', { maximumFractionDigits: 2 })} kg`;
}
