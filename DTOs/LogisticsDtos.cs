using System.ComponentModel.DataAnnotations;
using LogisticDesk.Enums;

namespace LogisticDesk.DTOs
{
    public class ShipmentAddingDto
    {
        [StringLength(32)]
        public string? TrackingNumber { get; set; }

        [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "Weight must be between 0.01 and 100000.")]
        public decimal Weight { get; set; }

        [Required]
        [StringLength(500)]
        public string ReceiverAddress { get; set; } = string.Empty;

        public int? VehicleId { get; set; }
    }

    public class VehicleAddingDto
    {
        [Required]
        [StringLength(20)]
        public string PlateNumber { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "MaxCapacity must be between 0.01 and 100000.")]
        public decimal MaxCapacity { get; set; }

        public VehicleStatus VehicleStatus { get; set; } = VehicleStatus.Available;

        public bool IsActive { get; set; } = true;
    }
}
