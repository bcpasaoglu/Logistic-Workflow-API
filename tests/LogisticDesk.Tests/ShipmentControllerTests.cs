using LogisticDesk.Controllers;
using LogisticDesk.Data;
using LogisticDesk.DTOs;
using LogisticDesk.Enums;
using LogisticDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogisticDesk.Tests;

public class ShipmentControllerTests
{
    private static LogiDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<LogiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LogiDbContext(options);
    }

    private static async Task<Vehicle> SeedVehicle(LogiDbContext db, decimal capacity = 1000m,
        VehicleStatus status = VehicleStatus.Available)
    {
        var vehicle = new Vehicle { PlateNumber = "06 XYZ 42", MaxCapacity = capacity, Status = status };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle;
    }

    private static ShipmentAddingDto Dto(decimal weight, int? vehicleId = null) => new()
    {
        Weight = weight,
        ReceiverAddress = "Kadikoy, Istanbul",
        VehicleId = vehicleId,
    };

    [Fact]
    public async Task Warehouse_shipment_is_pending_with_generated_tracking_number()
    {
        await using var db = NewContext();
        var controller = new ShipmentController(db);

        var result = await controller.AddShipmentToWarehouse(Dto(25m));

        var shipment = Assert.IsType<Shipment>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Null(shipment.VehicleId);
        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
        Assert.Equal(8, shipment.TrackingNumber.Length);
    }

    [Fact]
    public async Task Direct_assignment_puts_vehicle_on_route()
    {
        await using var db = NewContext();
        var vehicle = await SeedVehicle(db);
        var controller = new ShipmentController(db);

        var result = await controller.AssignShipmentDirectly(Dto(300m, vehicle.Id));

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.Vehicles.FindAsync(vehicle.Id);
        Assert.Equal(VehicleStatus.OnRoute, saved!.Status);
    }

    [Fact]
    public async Task Direct_assignment_over_capacity_is_rejected_and_nothing_is_saved()
    {
        await using var db = NewContext();
        var vehicle = await SeedVehicle(db, capacity: 100m);
        var controller = new ShipmentController(db);

        var result = await controller.AssignShipmentDirectly(Dto(150m, vehicle.Id));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.Shipments);
        Assert.Equal(VehicleStatus.Available, (await db.Vehicles.FindAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task Direct_assignment_to_missing_vehicle_returns_404()
    {
        await using var db = NewContext();
        var result = await new ShipmentController(db).AssignShipmentDirectly(Dto(10m, 999));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Loading_from_warehouse_to_busy_vehicle_is_rejected()
    {
        await using var db = NewContext();
        var vehicle = await SeedVehicle(db, status: VehicleStatus.InMaintenance);
        var controller = new ShipmentController(db);
        var shipment = (Shipment)((OkObjectResult)await controller.AddShipmentToWarehouse(Dto(10m))).Value!;

        var result = await controller.LoadFromWarehouseToVehicle(shipment.Id, vehicle.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Loading_from_warehouse_assigns_shipment()
    {
        await using var db = NewContext();
        var vehicle = await SeedVehicle(db);
        var controller = new ShipmentController(db);
        var shipment = (Shipment)((OkObjectResult)await controller.AddShipmentToWarehouse(Dto(10m))).Value!;

        var result = await controller.LoadFromWarehouseToVehicle(shipment.Id, vehicle.Id);

        var loaded = Assert.IsType<Shipment>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(vehicle.Id, loaded.VehicleId);
        Assert.Equal(ShipmentStatus.InTransit, loaded.Status);
    }

    [Fact]
    public async Task Update_cannot_bypass_capacity_checks_by_changing_vehicle()
    {
        await using var db = NewContext();
        var small = await SeedVehicle(db, capacity: 5m);
        var controller = new ShipmentController(db);
        var shipment = (Shipment)((OkObjectResult)await controller.AddShipmentToWarehouse(Dto(50m))).Value!;

        var result = await controller.UpdateShipmentById(shipment.Id, Dto(50m, small.Id));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null((await db.Shipments.FindAsync(shipment.Id))!.VehicleId);
    }

    [Fact]
    public async Task Update_cannot_raise_weight_above_assigned_vehicle_capacity()
    {
        await using var db = NewContext();
        var vehicle = await SeedVehicle(db, capacity: 100m);
        var controller = new ShipmentController(db);
        var shipment = (Shipment)((OkObjectResult)await controller.AssignShipmentDirectly(Dto(80m, vehicle.Id))).Value!;

        var result = await controller.UpdateShipmentById(shipment.Id, Dto(120m, vehicle.Id));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task List_is_paged_and_can_filter_by_status()
    {
        await using var db = NewContext();
        var vehicle = await SeedVehicle(db, capacity: 10_000m);
        var controller = new ShipmentController(db);
        for (var i = 0; i < 3; i++) await controller.AddShipmentToWarehouse(Dto(10m));
        await controller.AssignShipmentDirectly(Dto(10m, vehicle.Id));

        var all = (PagedResult<Shipment>)((OkObjectResult)await controller.GetShipments(new PaginationQuery { PageSize = 2 })).Value!;
        var pending = (PagedResult<Shipment>)((OkObjectResult)await controller.GetShipments(new PaginationQuery(), ShipmentStatus.Pending)).Value!;

        Assert.Equal(4, all.TotalCount);
        Assert.Equal(2, all.Items.Count);
        Assert.Equal(2, all.TotalPages);
        Assert.Equal(3, pending.TotalCount);
        Assert.All(pending.Items, s => Assert.Equal(ShipmentStatus.Pending, s.Status));
    }

    [Fact]
    public async Task Shipment_in_transit_cannot_be_deleted()
    {
        await using var db = NewContext();
        var vehicle = await SeedVehicle(db);
        var controller = new ShipmentController(db);
        var shipment = (Shipment)((OkObjectResult)await controller.AssignShipmentDirectly(Dto(10m, vehicle.Id))).Value!;

        Assert.IsType<ConflictObjectResult>(await controller.DeleteShipmentById(shipment.Id));
        Assert.Single(db.Shipments);
    }

    [Fact]
    public async Task Warehouse_shipment_can_be_deleted()
    {
        await using var db = NewContext();
        var controller = new ShipmentController(db);
        var shipment = (Shipment)((OkObjectResult)await controller.AddShipmentToWarehouse(Dto(10m))).Value!;

        Assert.IsType<NoContentResult>(await controller.DeleteShipmentById(shipment.Id));
        Assert.Empty(db.Shipments);
    }

    [Fact]
    public async Task Duplicate_tracking_number_is_rejected()
    {
        await using var db = NewContext();
        var controller = new ShipmentController(db);
        var dto = Dto(10m);
        dto.TrackingNumber = "TR-1001";
        await controller.AddShipmentToWarehouse(dto);

        var result = await controller.AddShipmentToWarehouse(dto);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Shipments);
    }
}
