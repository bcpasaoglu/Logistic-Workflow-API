using LogisticDesk.Controllers;
using LogisticDesk.Data;
using LogisticDesk.DTOs;
using LogisticDesk.Enums;
using LogisticDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogisticDesk.Tests;

public class VehicleControllerTests
{
    private static LogiDbContext NewContext() => new(new DbContextOptionsBuilder<LogiDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static async Task SeedVehicles(LogiDbContext db, int count, VehicleStatus status = VehicleStatus.Available)
    {
        for (var i = 0; i < count; i++)
        {
            db.Vehicles.Add(new Vehicle { PlateNumber = $"34 AB {i:000}", MaxCapacity = 1000m, Status = status });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_returns_requested_page_with_metadata()
    {
        await using var db = NewContext();
        await SeedVehicles(db, 25);

        var result = await new VehicleController(db).GetVehicles(new PaginationQuery { Page = 2, PageSize = 10 });

        var page = Assert.IsType<PagedResult<Vehicle>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNextPage);
        Assert.True(page.HasPreviousPage);
        Assert.Equal("34 AB 010", page.Items[0].PlateNumber);
    }

    [Fact]
    public async Task Last_page_is_partial_and_has_no_next_page()
    {
        await using var db = NewContext();
        await SeedVehicles(db, 25);

        var result = await new VehicleController(db).GetVehicles(new PaginationQuery { Page = 3, PageSize = 10 });

        var page = Assert.IsType<PagedResult<Vehicle>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(5, page.Items.Count);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task Page_size_is_clamped_to_the_maximum()
    {
        await using var db = NewContext();
        await SeedVehicles(db, 120);

        var result = await new VehicleController(db).GetVehicles(new PaginationQuery { Page = 1, PageSize = 500 });

        var page = Assert.IsType<PagedResult<Vehicle>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(PaginationQuery.MaxPageSize, page.PageSize);
        Assert.Equal(PaginationQuery.MaxPageSize, page.Items.Count);
    }

    [Fact]
    public async Task Status_filter_only_returns_matching_vehicles()
    {
        await using var db = NewContext();
        await SeedVehicles(db, 3);
        await SeedVehicles(db, 2, VehicleStatus.InMaintenance);

        var result = await new VehicleController(db).GetVehicleByStatus(VehicleStatus.InMaintenance, new PaginationQuery());

        var page = Assert.IsType<PagedResult<Vehicle>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, v => Assert.Equal(VehicleStatus.InMaintenance, v.Status));
    }

    [Fact]
    public async Task Vehicle_with_assigned_shipments_cannot_be_deleted()
    {
        await using var db = NewContext();
        var vehicle = new Vehicle { PlateNumber = "06 XYZ 42", MaxCapacity = 100m };
        db.Vehicles.Add(vehicle);
        db.Shipments.Add(new Shipment { TrackingNumber = "T1", ReceiverAddress = "Ankara", Weight = 10m, Vehicle = vehicle });
        await db.SaveChangesAsync();

        var result = await new VehicleController(db).DeleteVehicleById(vehicle.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Vehicles);
    }

    [Fact]
    public async Task Unused_vehicle_is_deleted()
    {
        await using var db = NewContext();
        await SeedVehicles(db, 1);
        var id = db.Vehicles.Single().Id;

        var result = await new VehicleController(db).DeleteVehicleById(id);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.Vehicles);
    }

    [Fact]
    public async Task Deleting_missing_vehicle_returns_404()
    {
        await using var db = NewContext();
        Assert.IsType<NotFoundResult>(await new VehicleController(db).DeleteVehicleById(42));
    }
}
