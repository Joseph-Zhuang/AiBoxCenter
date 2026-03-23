using System.ComponentModel.DataAnnotations;

namespace AiBoxCenter.Models 
{
    public class Device 
    {
        public int Id { get; set; }
        [Required] [MaxLength(40)] public string DeviceName { get; set; }
        [Required] [MaxLength(32)] public string DeviceSerialNo { get; set; }
        [Required] [MaxLength(20)] public string DeviceIP { get; set; }
        [Required] [MaxLength(6)] public string DevicePort { get; set; }
        [Required] [MaxLength(20)] public string DeviceUser { get; set; }
        [Required] [MaxLength(32)] public string DevicePassword { get; set; }
        [MaxLength(20)] public string? MqttIP { get; set; }
        [MaxLength(6)] public string? MqttPort { get; set; }
        [MaxLength(32)] public string? MqttSerialNo { get; set; }
        [MaxLength(20)] public string? MqttUser { get; set; }
        [MaxLength(32)] public string? MqttPassword { get; set; }
        public bool IsEnable { get; set; } = true;
    }
}