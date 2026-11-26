using LogisticDesk.Enums;
using LogisticDesk.Models;

namespace LogisticDesk.Services
{
    /// <summary>
    /// Business rules for loading a shipment onto a vehicle. Kept free of
    /// EF Core and HTTP concerns so the rules can be unit tested directly.
    /// </summary>
    public static class ShipmentAssignmentRules
    {
        /// <summary>
        /// Returns null when the shipment can be loaded onto the vehicle,
        /// otherwise a human-readable reason why it cannot.
        /// </summary>
        public static string? Validate(Vehicle vehicle, decimal shipmentWeight)
        {
            if (vehicle.Status != VehicleStatus.Available)
            {
                return "This vehicle is currently not available (On Route or Under Maintenance).";
            }

            if (shipmentWeight > vehicle.MaxCapacity)
            {
                return $"Capacity exceeded! The vehicle can carry a maximum of {vehicle.MaxCapacity}, but the shipment weighs {shipmentWeight}.";
            }

            return null;
        }

        /// <summary>
        /// Applies the state changes of a successful assignment.
        /// </summary>
        public static void Assign(Shipment shipment, Vehicle vehicle)
        {
            shipment.VehicleId = vehicle.Id;
            shipment.Vehicle = vehicle;
            shipment.Status = ShipmentStatus.InTransit;
            vehicle.Status = VehicleStatus.OnRoute;
        }

        /// <summary>
        /// Returns null when the shipment can be marked as delivered, otherwise the reason it cannot.
        /// </summary>
        public static string? ValidateDelivery(Shipment shipment) => shipment.Status switch
        {
            ShipmentStatus.InTransit => null,
            ShipmentStatus.Delivered => "This shipment has already been delivered.",
            _ => "Only shipments that are in transit can be delivered.",
        };

        /// <summary>
        /// Marks the shipment as delivered. The vehicle becomes available again once it
        /// carries no other shipment that is still in transit.
        /// </summary>
        public static void Deliver(Shipment shipment, Vehicle? vehicle, bool vehicleHasOtherShipmentsInTransit)
        {
            shipment.Status = ShipmentStatus.Delivered;
            if (vehicle != null && !vehicleHasOtherShipmentsInTransit && vehicle.Status == VehicleStatus.OnRoute)
            {
                vehicle.Status = VehicleStatus.Available;
            }
        }
    }
}
