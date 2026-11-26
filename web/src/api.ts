import { describeProblem } from './lib/problem';
import type { CurrentUser, NewShipment, PagedResult, Shipment, ShipmentStatus, Vehicle } from './types';

const TOKEN_KEY = 'logisticdesk.token';

export class ApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
  }
}

// The token lives in sessionStorage so it is dropped when the tab closes.
export const tokenStore = {
  get: () => sessionStorage.getItem(TOKEN_KEY),
  set: (token: string) => sessionStorage.setItem(TOKEN_KEY, token),
  clear: () => sessionStorage.removeItem(TOKEN_KEY),
};

let onUnauthorized: () => void = () => {};
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (init.body) headers.set('Content-Type', 'application/json');
  const token = tokenStore.get();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const response = await fetch(path, { ...init, headers });
  const text = await response.text();
  let body: unknown = text;
  try {
    body = text ? JSON.parse(text) : undefined;
  } catch {
    // Non-JSON body: keep the raw text for the error message.
  }

  if (!response.ok) {
    if (response.status === 401 && token) onUnauthorized();
    throw new ApiError(describeProblem(body, response.status), response.status);
  }
  return body as T;
}

const json = (data: unknown) => JSON.stringify(data);

export const api = {
  login: (email: string, password: string) =>
    request<{ accessToken: string; expiresAtUtc: string }>('/api/auth/login', {
      method: 'POST',
      body: json({ email, password }),
    }),
  register: (data: { fullName: string; lastName: string; email: string; password: string; confirmPassword: string }) =>
    request<{ message: string }>('/api/auth/register', { method: 'POST', body: json(data) }),
  me: () => request<CurrentUser>('/api/auth/me'),

  vehicles: (page = 1, pageSize = 100) => request<PagedResult<Vehicle>>(`/api/Vehicle?page=${page}&pageSize=${pageSize}`),
  addVehicle: (plateNumber: string, maxCapacity: number) =>
    request<Vehicle>('/api/Vehicle', { method: 'POST', body: json({ plateNumber, maxCapacity }) }),
  updateVehicle: (v: Vehicle) =>
    request<Vehicle>(`/api/Vehicle/${v.id}`, {
      method: 'PUT',
      body: json({ plateNumber: v.plateNumber, maxCapacity: v.maxCapacity, vehicleStatus: v.status, isActive: v.isActive }),
    }),

  shipments: (page: number, pageSize: number, status?: ShipmentStatus) =>
    request<PagedResult<Shipment>>(
      `/api/Shipment?page=${page}&pageSize=${pageSize}${status ? `&status=${status}` : ''}`,
    ),
  createShipment: (data: NewShipment) =>
    data.vehicleId
      ? request<Shipment>('/api/Shipment/assign-direct', { method: 'POST', body: json(data) })
      : request<Shipment>('/api/Shipment/warehouse', { method: 'POST', body: json(data) }),
  loadToVehicle: (shipmentId: number, vehicleId: number) =>
    request<Shipment>(`/api/Shipment/${shipmentId}/load-to-vehicle/${vehicleId}`, { method: 'PUT' }),
  deliver: (shipmentId: number) => request<Shipment>(`/api/Shipment/${shipmentId}/deliver`, { method: 'PUT' }),
  deleteShipment: (shipmentId: number) => request<void>(`/api/Shipment/${shipmentId}`, { method: 'DELETE' }),
};
