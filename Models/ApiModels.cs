using System.Collections.Generic;

namespace AiBoxCenter.Models
{
    public class LoginRequest 
    { 
        public string user { get; set; } = ""; 
        public string password { get; set; } = ""; 
    }

    public class LoginResponse 
    { 
        public string? reason { get; set; } 
        public string? status { get; set; } 
        public string? token { get; set; } 
    }

    public class CameraListResponse 
    { 
        public int max_device { get; set; } 
        public int size { get; set; } 
        public List<ApiCamera> devices { get; set; } = new(); 
    }

    public class ApiCamera
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public int accessType { get; set; }
        public string url { get; set; } = "";
        public List<object> type { get; set; } = new();
        public string areaId { get; set; } = "";
        public string? code { get; set; }
    }

    public class ApiAreaResponse 
    { 
        public List<ApiArea> data { get; set; } = new(); 
        public int total { get; set; } 
    }

    // 新增：用來解析新增單一區域返回的結構
    public class ApiSingleAreaResponse : BaseResponse 
    { 
        public ApiArea data { get; set; } = new(); 
    }

    public class ApiArea
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string parentId { get; set; } = "";
        public List<ApiArea> children { get; set; } = new();
    }

    public class BaseResponse 
    { 
        public string? reason { get; set; } 
        public int resultCode { get; set; } 
        public string? resultMsg { get; set; } 
        public string? status { get; set; } 
        public int code { get; set; } 
    }

    public class SyncViewModel
    {
        public int SelectedDeviceId { get; set; }
        public List<Device> DeviceList { get; set; } = new();
        public List<JoinedCameraRow> Rows { get; set; } = new();
    }

    public class JoinedCameraRow 
    { 
        public DbCameraRow? DbData { get; set; } 
        public ApiCamera? JsonData { get; set; } 
    }

    public class DbCameraRow 
    { 
        public string CameraId { get; set; } = ""; 
        public string Name { get; set; } = ""; 
        public string ConnectionMethod { get; set; } = ""; 
        public string StreamUrl { get; set; } = ""; 
        public string AlgTypes { get; set; } = ""; 
        public string AreaId { get; set; } = ""; 
    }

    public class SyncUpdateRequest 
    { 
        public int DeviceId { get; set; } 
        public List<SyncUpdateItem> Items { get; set; } = new(); 
    }

    public class SyncUpdateItem 
    { 
        public int State { get; set; } 
        public string CameraId { get; set; } = ""; 
        public string Name { get; set; } = ""; 
        public string ConnectionMethod { get; set; } = ""; 
        public string StreamUrl { get; set; } = ""; 
        public string AlgTypes { get; set; } = ""; 
        public string AreaId { get; set; } = ""; 
    }

    public class SyncAreaRequest 
    { 
        public int DeviceId { get; set; } 
        public string Id { get; set; } = ""; 
        public string NodeName { get; set; } = ""; 
        public string AreaId { get; set; } = ""; 
        public string TargetParentId { get; set; } = ""; 
    }

    public class DeleteRequest 
    { 
        public int DeviceId { get; set; } 
        public string Id { get; set; } = ""; 
    }
}