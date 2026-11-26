using Microsoft.AspNetCore.Authorization;
using LogisticDesk.Data;
using LogisticDesk.DTOs;
using LogisticDesk.Enums;
using LogisticDesk.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace LogisticDesk.Controllers
{

    /// <summary>Manages the vehicle fleet.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class VehicleController : ControllerBase
    {
        private readonly LogiDbContext _context;
        public VehicleController(LogiDbContext context)
        {
            _context = context;
        }
        /// <summary>Lists vehicles ordered by id, one page at a time.</summary>
        /// <param name="query">Page number and page size (max 100).</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<Vehicle>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetVehicles([FromQuery] PaginationQuery query, CancellationToken cancellationToken = default)
        {
            var page = await _context.Vehicles.AsNoTracking()
                .OrderBy(v => v.Id)
                .ToPagedResultAsync(query, cancellationToken);
            return Ok(page);
        }

        /// <summary>Gets a single vehicle.</summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Vehicle), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVehicleById(int id)
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }
            return Ok(vehicle);
        }
        /// <summary>Lists vehicles with the given status, one page at a time.</summary>
        /// <param name="status">Available, OnRoute or InMaintenance.</param>
        /// <param name="query">Page number and page size (max 100).</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpGet("status/{status}")]
        [ProducesResponseType(typeof(PagedResult<Vehicle>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetVehicleByStatus(VehicleStatus status, [FromQuery] PaginationQuery query, CancellationToken cancellationToken = default)
        {
            var page = await _context.Vehicles.AsNoTracking()
                .Where(v => v.Status == status)
                .OrderBy(v => v.Id)
                .ToPagedResultAsync(query, cancellationToken);
            return Ok(page);
        }

        /// <summary>Registers a new vehicle. New vehicles start as Available.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(Vehicle), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddVehicle([FromBody] VehicleAddingDto vehicleDto)
        {
            if (vehicleDto == null)
            {
                return BadRequest("Vehicle data is required.");
            }
            Vehicle newVehicle = new Vehicle
            {
                PlateNumber = vehicleDto.PlateNumber,
                MaxCapacity = Math.Round(vehicleDto.MaxCapacity,2),
                Status = VehicleStatus.Available,
                CreatedAt = DateTime.UtcNow,
                IsActive = vehicleDto.IsActive
            };
            _context.Vehicles.Add(newVehicle);
            await _context.SaveChangesAsync();
            return Ok(newVehicle);
        }
        /// <summary>Updates plate number, capacity, status and active flag of a vehicle.</summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(Vehicle), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateVehicleById(int id, [FromBody] VehicleAddingDto vehicleDto)
        {
            if (id <= 0)
            {
                return BadRequest("Invalid vehicle ID.");
            }
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }
            vehicle.PlateNumber = vehicleDto.PlateNumber;
            vehicle.MaxCapacity = Math.Round(vehicleDto.MaxCapacity, 2);
            vehicle.Status = vehicleDto.VehicleStatus;
            vehicle.IsActive = vehicleDto.IsActive;
            await _context.SaveChangesAsync();
            return Ok(vehicle);
        }

        /// <summary>Deletes a vehicle that has no shipments assigned to it.</summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteVehicleById(int id)
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }
            if (await _context.Shipments.AnyAsync(s => s.VehicleId == id))
            {
                return Conflict("This vehicle still has shipments assigned to it.");
            }

            _context.Vehicles.Remove(vehicle);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
