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
    }
}
