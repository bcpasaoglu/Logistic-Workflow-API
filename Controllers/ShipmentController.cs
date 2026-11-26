using LogisticDesk.Data;
using LogisticDesk.DTOs;
using LogisticDesk.Enums;
using LogisticDesk.Models;
using LogisticDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogisticDesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShipmentController : ControllerBase
    {
        private readonly LogiDbContext _context;
        public ShipmentController(LogiDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetShipments()
        {
            var shipments = await _context.Shipments.ToListAsync();
            return Ok(shipments);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetShipmentById(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null)
            {
                return NotFound();
            }
            return Ok(shipment);
        }


        [HttpPost("warehouse")] // URL: api/Shipment/warehouse
        public async Task<IActionResult> AddShipmentToWarehouse([FromBody] ShipmentAddingDto shipmentDto)
        {
            if (shipmentDto == null)
            {
                return BadRequest("Shipment data cannot be null.");
            }

            Shipment newShipment = new Shipment
            {
                TrackingNumber = NewTrackingNumberIfMissing(shipmentDto.TrackingNumber),
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


        [HttpPost("assign-direct")] // URL: api/Shipment/assign-direct
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

            Shipment newShipment = new Shipment
            {
                TrackingNumber = NewTrackingNumberIfMissing(shipmentDto.TrackingNumber),
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


        [HttpPut("{shipmentId}/load-to-vehicle/{vehicleId}")]
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
        [HttpPut("{id}")]
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

            shipment.TrackingNumber = shipmentDto.TrackingNumber ?? shipment.TrackingNumber;
            shipment.Weight = newWeight;
            shipment.ReceiverAddress = shipmentDto.ReceiverAddress;
            await _context.SaveChangesAsync();
            return Ok(shipment);
        }

        private static string NewTrackingNumberIfMissing(string? trackingNumber) =>
            string.IsNullOrWhiteSpace(trackingNumber)
                ? Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
                : trackingNumber.Trim();
    }
}
