using System.ComponentModel.DataAnnotations; 
using System.Collections.Generic;

namespace AiBoxCenter.Models 
{
    public class Camera 
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "名稱為必填")] [MaxLength(100)] public string Name { get; set; }
        public string? StreamUrl { get; set; } 
        public string? CameraId { get; set; } 
        [Required(ErrorMessage = "必須選擇設備")] public int DeviceId { get; set; } 
        public virtual Device? Device { get; set; }
        public int LocationX { get; set; } = 0; 
        public int LocationY { get; set; } = 0;
        public int ConnectionMethodId { get; set; } 
        public int? AreaId { get; set; } 
        public int? GroupId { get; set; }
        public virtual ConnectionMethod? ConnectionMethod { get; set; } 
        public virtual Area? Area { get; set; } 
        public virtual Group? Group { get; set; }
        public List<CameraAlgorithm> CameraAlgorithms { get; set; } = new();
    }

    public class CameraAlgorithm 
    {
        public int CameraId { get; set; } 
        public Camera Camera { get; set; }
        public int AlgorithmId { get; set; } 
        public Algorithm Algorithm { get; set; }
    }
}