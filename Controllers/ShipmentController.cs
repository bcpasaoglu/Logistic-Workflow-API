using Microsoft.AspNetCore.Authorization;
using LogisticDesk.Data;
using LogisticDesk.DTOs;
using LogisticDesk.Enums;
using LogisticDesk.Models;
using LogisticDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogisticDesk.Controllers
{
    /// <summary>Creates, assigns and tracks shipments.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class ShipmentController : ControllerBase
    {
        private readonly LogiDbContext _context;
        public ShipmentController(LogiDbContext context)
        {
            _context = context;
        }

        /// <summary>Lists shipments, newest first, one page at a time.</summary>
        /// <param name="query">Page number and page size (max 100).</param>
        /// <param name="status">Optional status filter.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<Shipment>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetShipments([FromQuery] PaginationQuery query, [FromQuery] ShipmentStatus? status = null, CancellationToken cancellationToken = default)
        {
            var shipments = _context.Shipments.AsNoTracking().AsQueryable();
            if (status != null)
            {
                shipments = shipments.Where(s => s.Status == status);
            }

            var page = await shipments
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .ToPagedResultAsync(query, cancellationToken);
            return Ok(page);
        }

        /// <summary>Gets a single shipment.</summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Shipment), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetShipmentById(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null)
            {
                return NotFound();
            }
            return Ok(shipment);
        }


        /// <summary>Stores a new shipment in the warehouse without assigning a vehicle.</summary>
        /// <remarks>A tracking number is generated when none is supplied.</remarks>
        [HttpPost("warehouse")] // URL: api/Shipment/warehouse
        [ProducesResponseType(typeof(Shipment), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AddShipmentToWarehouse([FromBody] ShipmentAddingDto shipmentDto)
        {
            if (shipmentDto == null)
            {
                return BadRequest("Shipment data cannot be null.");
            }

            var trackingNumber = NewTrackingNumberIfMissing(shipmentDto.TrackingNumber);
            if (await TrackingNumberTaken(trackingNumber))
            {
                return Conflict($"Tracking number '{trackingNumber}' is already in use.");
            }

            Shipment newShipment = new Shipment
            {
                TrackingNumber = trackingNumber,
                Weight = Math.Round(shipmentDto.Weight, 2),
                ReceiverAddress = shipmentDto.ReceiverAddress,
                Status = ShipmentStatus.Pending,
                VehicleId = null, // Not assigned to any vehicle yet
                CreatedAt = DateTime.UtcNow
            };

            _context.Shipments.Add(newShipment);
            await _context.SaveChangesAsync();

            return Ok(newShipment);
        }


        /// <summary>Creates a shipment and loads it onto a vehicle in one step.</summary>
        /// <remarks>The vehicle must be Available and its MaxCapacity must cover the shipment weight. On success the shipment becomes InTransit and the vehicle OnRoute.</remarks>
        [HttpPost("assign-direct")] // URL: api/Shipment/assign-direct
        [ProducesResponseType(typeof(Shipment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignShipmentDirectly([FromBody] ShipmentAddingDto shipmentDto)
        {
            // If VehicleId is not provided from the outside, throw an error
            if (shipmentDto.VehicleId == null || shipmentDto.VehicleId <= 0)
            {
                return BadRequest("A valid Vehicle ID (VehicleId) is required for direct assignment.");
            }

            var vehicle = await _context.Vehicles.FindAsync(shipmentDto.VehicleId);
            if (vehicle == null)
            {
                return NotFound("The vehicle intended for assignment was not found.");
            }

            var error = ShipmentAssignmentRules.Validate(vehicle, shipmentDto.Weight);
            if (error != null)
            {
                return BadRequest(error);
            }

            var trackingNumber = NewTrackingNumberIfMissing(shipmentDto.TrackingNumber);
            if (await TrackingNumberTaken(trackingNumber))
            {
                return Conflict($"Tracking number '{trackingNumber}' is already in use.");
            }

            Shipment newShipment = new Shipment
            {
                TrackingNumber = trackingNumber,
                ReceiverAddress = shipmentDto.ReceiverAddress,
                Weight = Math.Round(shipmentDto.Weight, 2),
                Status = ShipmentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            ShipmentAssignmentRules.Assign(newShipment, vehicle);

            _context.Shipments.Add(newShipment);
            await _context.SaveChangesAsync();

            return Ok(newShipment);
        }


        /// <summary>Loads a shipment waiting in the warehouse onto an available vehicle.</summary>
        [HttpPut("{shipmentId}/load-to-vehicle/{vehicleId}")]
        [ProducesResponseType(typeof(Shipment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LoadFromWarehouseToVehicle(int shipmentId, int vehicleId)
        {
            var shipment = await _context.Shipments.FindAsync(shipmentId);
            if (shipment == null) return NotFound("Shipment not found.");
            if (shipment.VehicleId != null) return BadRequest("This shipment is already assigned to a vehicle.");

            var vehicle = await _context.Vehicles.FindAsync(vehicleId);
            if (vehicle == null) return NotFound("Vehicle not found.");

            var error = ShipmentAssignmentRules.Validate(vehicle, shipment.Weight);
            if (error != null) return BadRequest(error);

            ShipmentAssignmentRules.Assign(shipment, vehicle);

            await _context.SaveChangesAsync();
            return Ok(shipment);
        }
        /// <summary>Marks an in-transit shipment as delivered and frees its vehicle.</summary>
        /// <remarks>The vehicle returns to Available when it has no other shipment in transit.</remarks>
        [HttpPut("{id}/deliver")]
        [ProducesResponseType(typeof(Shipment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeliverShipment(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null) return NotFound("Shipment not found.");

            var error = ShipmentAssignmentRules.ValidateDelivery(shipment);
            if (error != null) return Conflict(error);

            Vehicle? vehicle = null;
            var othersInTransit = false;
            if (shipment.VehicleId != null)
            {
                vehicle = await _context.Vehicles.FindAsync(shipment.VehicleId);
                othersInTransit = await _context.Shipments.AnyAsync(s =>
                    s.VehicleId == shipment.VehicleId && s.Id != shipment.Id && s.Status == ShipmentStatus.InTransit);
            }

            ShipmentAssignmentRules.Deliver(shipment, vehicle, othersInTransit);
            await _context.SaveChangesAsync();
            return Ok(shipment);
        }

        /// <summary>Updates tracking number, weight and address of a shipment.</summary>
        /// <remarks>The vehicle cannot be changed here (use load-to-vehicle), and the weight cannot exceed the assigned vehicle's capacity.</remarks>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(Shipment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateShipmentById(int id, [FromBody] ShipmentAddingDto shipmentDto)
        {
            if (id <= 0)
            {
                return BadRequest("Invalid shipment ID.");
            }
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null)
            {
                return NotFound();
            }
            // Moving a shipment to another vehicle must go through the same
            // capacity and availability checks as a normal assignment.
            if (shipmentDto.VehicleId != shipment.VehicleId)
            {
                return BadRequest("Use the load-to-vehicle endpoint to change a shipment's vehicle.");
            }

            var newWeight = Math.Round(shipmentDto.Weight, 2);
            if (shipment.VehicleId != null && newWeight > shipment.Weight)
            {
                var vehicle = await _context.Vehicles.FindAsync(shipment.VehicleId);
                if (vehicle != null && newWeight > vehicle.MaxCapacity)
                {
                    return BadRequest($"Capacity exceeded! The vehicle can carry a maximum of {vehicle.MaxCapacity}, but the shipment weighs {newWeight}.");
                }
            }

            var trackingNumber = string.IsNullOrWhiteSpace(shipmentDto.TrackingNumber)
                ? shipment.TrackingNumber
                : shipmentDto.TrackingNumber.Trim();
            if (trackingNumber != shipment.TrackingNumber && await TrackingNumberTaken(trackingNumber))
            {
                return Conflict($"Tracking number '{trackingNumber}' is already in use.");
            }

            shipment.TrackingNumber = trackingNumber;
            shipment.Weight = newWeight;
            shipment.ReceiverAddress = shipmentDto.ReceiverAddress;
            await _context.SaveChangesAsync();
            return Ok(shipment);
        }

        /// <summary>Deletes a shipment that is not currently in transit.</summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteShipmentById(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null)
            {
                return NotFound();
            }
            if (shipment.Status == ShipmentStatus.InTransit)
            {
                return Conflict("A shipment that is in transit cannot be deleted.");
            }

            _context.Shipments.Remove(shipment);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private Task<bool> TrackingNumberTaken(string trackingNumber) =>
            _context.Shipments.AnyAsync(s => s.TrackingNumber == trackingNumber);

        private static string NewTrackingNumberIfMissing(string? trackingNumber) =>
            string.IsNullOrWhiteSpace(trackingNumber)
                ? Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
                : trackingNumber.Trim();
    }
}
