using LogisticDesk.Enums;
using LogisticDesk.Models;
using LogisticDesk.Services;
using Xunit;

namespace LogisticDesk.Tests;

public class ShipmentAssignmentRulesTests
{
    private static Vehicle AvailableVehicle(decimal capacity = 1000m) => new()
    {
        Id = 1,
        PlateNumber = "34 ABC 123",
        MaxCapacity = capacity,
        Status = VehicleStatus.Available,
    };

    [Fact]
    public void Validate_allows_shipment_within_capacity()
    {
        Assert.Null(ShipmentAssignmentRules.Validate(AvailableVehicle(), 400m));
    }

    [Fact]
    public void Validate_allows_shipment_exactly_at_capacity()
    {
        Assert.Null(ShipmentAssignmentRules.Validate(AvailableVehicle(500m), 500m));
    }

    [Fact]
    public void Validate_rejects_shipment_over_capacity()
    {
        var error = ShipmentAssignmentRules.Validate(AvailableVehicle(500m), 500.01m);
        Assert.NotNull(error);
        Assert.Contains("Capacity exceeded", error);
    }

    [Theory]
    [InlineData(VehicleStatus.OnRoute)]
    [InlineData(VehicleStatus.InMaintenance)]
    public void Validate_rejects_unavailable_vehicle(VehicleStatus status)
    {
        var vehicle = AvailableVehicle();
        vehicle.Status = status;
        Assert.NotNull(ShipmentAssignmentRules.Validate(vehicle, 10m));
    }

    [Fact]
    public void Assign_links_shipment_and_updates_statuses()
    {
        var vehicle = AvailableVehicle();
        var shipment = new Shipment { Weight = 10m, ReceiverAddress = "Istanbul", TrackingNumber = "T1" };

        ShipmentAssignmentRules.Assign(shipment, vehicle);

        Assert.Equal(vehicle.Id, shipment.VehicleId);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
        Assert.Equal(VehicleStatus.OnRoute, vehicle.Status);
    }

    [Fact]
    public void Delivery_frees_the_vehicle_when_nothing_else_is_in_transit()
    {
        var vehicle = new Vehicle { Status = VehicleStatus.OnRoute };
        var shipment = new Shipment { Status = ShipmentStatus.InTransit };

        ShipmentAssignmentRules.Deliver(shipment, vehicle, vehicleHasOtherShipmentsInTransit: false);

        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    [Fact]
    public void Delivery_keeps_vehicle_on_route_while_other_shipments_are_in_transit()
    {
        var vehicle = new Vehicle { Status = VehicleStatus.OnRoute };

        ShipmentAssignmentRules.Deliver(new Shipment { Status = ShipmentStatus.InTransit }, vehicle, vehicleHasOtherShipmentsInTransit: true);

        Assert.Equal(VehicleStatus.OnRoute, vehicle.Status);
    }

    [Theory]
    [InlineData(ShipmentStatus.Pending)]
    [InlineData(ShipmentStatus.Delivered)]
    public void Only_in_transit_shipments_can_be_delivered(ShipmentStatus status)
    {
        Assert.NotNull(ShipmentAssignmentRules.ValidateDelivery(new Shipment { Status = status }));
    }
}
