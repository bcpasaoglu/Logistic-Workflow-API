export type VehicleStatus = 'Available' | 'OnRoute' | 'InMaintenance';
export type ShipmentStatus = 'Pending' | 'InTransit' | 'Delivered';

export interface Vehicle {
  id: number;
  plateNumber: string;
  maxCapacity: number;
  status: VehicleStatus;
  isActive: boolean;
  createdAt: string;
}

export interface Shipment {
  id: number;
  trackingNumber: string;
  weight: number;
  receiverAddress: string;
  status: ShipmentStatus;
  vehicleId: number | null;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface CurrentUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  roles: string[];
}

export interface NewShipment {
  trackingNumber?: string;
  weight: number;
  receiverAddress: string;
  vehicleId?: number;
}
