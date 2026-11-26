import { describe, expect, it } from 'vitest';
import { countByStatus, eligibleVehicles, loadByVehicle, loadPercent } from './fleet';
import { describeProblem } from './problem';
import type { Shipment, Vehicle } from '../types';

const vehicle = (id: number, status: Vehicle['status'], maxCapacity: number): Vehicle => ({
  id, status, maxCapacity, plateNumber: `P${id}`, isActive: true, createdAt: '',
});
const shipment = (id: number, vehicleId: number | null, status: Shipment['status'], weight: number): Shipment => ({
  id, vehicleId, status, weight, trackingNumber: `T${id}`, receiverAddress: '', createdAt: '',
});

describe('fleet helpers', () => {
  it('sums only in-transit shipments per vehicle', () => {
    const load = loadByVehicle([shipment(1, 1, 'InTransit', 100), shipment(2, 1, 'Delivered', 50), shipment(3, null, 'Pending', 10)]);
    expect(load.get(1)).toBe(100);
    expect(load.size).toBe(1);
  });

  it('clamps load percentage', () => {
    expect(loadPercent(50, 200)).toBe(25);
    expect(loadPercent(500, 200)).toBe(100);
    expect(loadPercent(10, 0)).toBe(0);
  });

  it('counts vehicles by status', () => {
    const counts = countByStatus([vehicle(1, 'Available', 1), vehicle(2, 'OnRoute', 1), vehicle(3, 'OnRoute', 1)]);
    expect(counts).toEqual({ All: 3, Available: 1, OnRoute: 2, InMaintenance: 0 });
  });

  it('offers only available vehicles that are big enough', () => {
    const list = eligibleVehicles([vehicle(1, 'Available', 100), vehicle(2, 'Available', 10), vehicle(3, 'OnRoute', 500)], 50);
    expect(list.map((v) => v.id)).toEqual([1]);
  });
});

describe('describeProblem', () => {
  it('uses ProblemDetails detail', () => {
    expect(describeProblem({ title: 'Bad Request', detail: 'Capacity exceeded!' }, 400)).toBe('Capacity exceeded!');
  });

  it('flattens validation errors', () => {
    expect(describeProblem({ title: 'x', errors: { Weight: ['Weight must be between 0.01 and 100000.'] } }, 400)).toBe(
      'Weight must be between 0.01 and 100000.',
    );
  });

  it('reads Identity errors', () => {
    expect(describeProblem({ message: 'User creation failed', errors: [{ description: 'Email taken.' }] }, 400)).toBe('Email taken.');
  });

  it('falls back to the status code', () => {
    expect(describeProblem(undefined, 500)).toBe('Request failed (500).');
  });
});
