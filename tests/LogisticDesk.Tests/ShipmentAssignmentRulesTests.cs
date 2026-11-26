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
}
