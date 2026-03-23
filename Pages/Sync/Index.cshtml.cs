using Microsoft.AspNetCore.Mvc; 
using Microsoft.AspNetCore.Mvc.RazorPages; 
using Microsoft.EntityFrameworkCore; 
using AiBoxCenter.Data; 
using AiBoxCenter.Models; 
using AiBoxCenter.Services; 
using System.Linq; 
using System.Collections.Generic; 
using System.Threading.Tasks;
using System;

namespace AiBoxCenter.Pages.Sync 
{
    [IgnoreAntiforgeryToken(Order = 1001)] 
    public class IndexModel : PageModel 
    {
        private readonly AppDbContext _context; 
        private readonly BoxApiService _boxApi; 
        
        public IndexModel(AppDbContext context, BoxApiService boxApi) 
        { 
            _context = context; 
            _boxApi = boxApi; 
        }
        
        public SyncViewModel ViewModel { get; set; } = new();
        
        public async Task<IActionResult> OnGetAsync(int? deviceId) 
        {
            ViewModel.DeviceList = await _context.Devices.AsNoTracking().ToListAsync();
            
            if (!Request.Query.ContainsKey("deviceId")) 
            {
                if (ViewModel.DeviceList.Any()) 
                {
                    ViewModel.SelectedDeviceId = ViewModel.DeviceList.First().Id;
                }
                return Page();
            }
            
            if (deviceId.HasValue) 
            {
                ViewModel.SelectedDeviceId = deviceId.Value;
                var device = ViewModel.DeviceList.FirstOrDefault(d => d.Id == deviceId.Value);
                if (device == null) return Page();
                
                var rawCameras = await _context.Cameras.AsNoTracking().Where(c => c.DeviceId == deviceId).ToListAsync();
                var camIds = rawCameras.Select(c => c.Id).ToList();
                
                var algMap = (from ca in _context.CameraAlgorithms.AsNoTracking() 
                              join a in _context.Algorithms.AsNoTracking() on ca.AlgorithmId equals a.Id 
                              where camIds.Contains(ca.CameraId) 
                              select new { ca.CameraId, a.AlgType })
                              .ToList()
                              .GroupBy(x => x.CameraId)
                              .ToDictionary(g => g.Key, g => string.Join(",", g.Select(x => x.AlgType)));
                              
                var areaMap = await _context.Areas.AsNoTracking().Where(a => a.AreaId != null).ToDictionaryAsync(a => a.Id, a => a.AreaId ?? "");
                
                var dbCameras = rawCameras.Select(c => new DbCameraRow { 
                    CameraId = c.CameraId ?? "", 
                    Name = c.Name, 
                    ConnectionMethod = c.ConnectionMethodId.ToString(), 
                    StreamUrl = c.StreamUrl ?? "", 
                    AreaId = c.AreaId.HasValue && areaMap.ContainsKey(c.AreaId.Value) ? areaMap[c.AreaId.Value] : "", 
                    AlgTypes = algMap.ContainsKey(c.Id) ? algMap[c.Id] : "" 
                }).ToList();
                
                var apiCameras = new List<ApiCamera>();
                var token = await _boxApi.LoginAsync(device.DeviceIP, device.DevicePort, device.DeviceUser, device.DevicePassword);
                if (!string.IsNullOrEmpty(token)) 
                { 
                    apiCameras = await _boxApi.GetCamerasAsync(device.DeviceIP, device.DevicePort, token); 
                }
                
                var allKeys = dbCameras.Select(d => d.CameraId).Union(apiCameras.Select(j => j.id)).OrderBy(k => k).ToList();
                
                foreach (var key in allKeys) 
                { 
                    ViewModel.Rows.Add(new JoinedCameraRow { 
                        DbData = dbCameras.FirstOrDefault(d => d.CameraId == key), 
                        JsonData = apiCameras.FirstOrDefault(j => j.id == key) 
                    }); 
                }

                while (ViewModel.Rows.Count < 7) 
                {
                    ViewModel.Rows.Add(new JoinedCameraRow { DbData = null, JsonData = null });
                }
            } 
            return Page();
        }
        
        public async Task<IActionResult> OnPostUpdateDatabaseAsync([FromBody] SyncUpdateRequest request) 
        {
            if (request == null || request.Items == null || !request.Items.Any()) return new JsonResult(new { success = false, message = "沒有接收到更新資料" });
            try 
            {
                foreach (var item in request.Items) 
                {
                    int? mappedAreaId = null;
                    if (!string.IsNullOrWhiteSpace(item.AreaId) && item.AreaId != "0") 
                    { 
                        var area = _context.Areas.FirstOrDefault(a => a.AreaId == item.AreaId); 
                        if (area == null) 
                        { 
                            area = new Area { AreaId = item.AreaId, AreaName = "未定義", SortOrder = 0 }; 
                            _context.Areas.Add(area); 
                            _context.SaveChanges(); 
                        } 
                        mappedAreaId = area.Id; 
                    }
                    
                    int connMethod = 1; 
                    int.TryParse(item.ConnectionMethod, out connMethod); 
                    if (connMethod == 0) connMethod = 1;
                    
                    if (item.State == 1) 
                    { 
                        var cam = new Camera { 
                            DeviceId = request.DeviceId, 
                            CameraId = item.CameraId, 
                            Name = item.Name ?? "", 
                            StreamUrl = item.StreamUrl, 
                            ConnectionMethodId = connMethod, 
                            AreaId = mappedAreaId, 
                            LocationX = -1, 
                            LocationY = -1 
                        }; 
                        _context.Cameras.Add(cam); 
                        _context.SaveChanges(); 
                        UpdateCameraAlgorithms(cam.Id, item.AlgTypes); 
                    }
                    else if (item.State == 2) 
                    { 
                        var cam = _context.Cameras.FirstOrDefault(c => c.CameraId == item.CameraId && c.DeviceId == request.DeviceId); 
                        if (cam != null) 
                        { 
                            cam.Name = item.Name ?? ""; 
                            cam.StreamUrl = item.StreamUrl; 
                            cam.ConnectionMethodId = connMethod; 
                            cam.AreaId = mappedAreaId; 
                            _context.SaveChanges(); 
                            UpdateCameraAlgorithms(cam.Id, item.AlgTypes); 
                        } 
                    }
                    else if (item.State == 3) 
                    { 
                        var cam = _context.Cameras.FirstOrDefault(c => c.CameraId == item.CameraId && c.DeviceId == request.DeviceId); 
                        if (cam != null) 
                        { 
                            var oldAlgs = _context.CameraAlgorithms.Where(ca => ca.CameraId == cam.Id).ToList(); 
                            _context.CameraAlgorithms.RemoveRange(oldAlgs); 
                            _context.Cameras.Remove(cam); 
                            _context.SaveChanges(); 
                        } 
                    }
                } 
                return new JsonResult(new { success = true });
            } 
            catch (Exception ex) 
            { 
                return new JsonResult(new { success = false, message = ex.Message }); 
            }
        }
        
        private void UpdateCameraAlgorithms(int camId, string algTypesStr) 
        {
            var oldAlgs = _context.CameraAlgorithms.Where(ca => ca.CameraId == camId).ToList(); 
            _context.CameraAlgorithms.RemoveRange(oldAlgs); 
            _context.SaveChanges();
            
            if (!string.IsNullOrWhiteSpace(algTypesStr)) 
            { 
                var types = algTypesStr.Split(',').Select(s => int.TryParse(s.Trim(), out int n) ? n : -1).Where(n => n != -1).ToList(); 
                var validAlgIds = _context.Algorithms.Where(a => types.Contains(a.AlgType)).Select(a => a.Id).ToList(); 
                foreach(var aid in validAlgIds) 
                { 
                    _context.CameraAlgorithms.Add(new CameraAlgorithm { CameraId = camId, AlgorithmId = aid }); 
                } 
                _context.SaveChanges(); 
            }
        }
        
        public async Task<IActionResult> OnPostUpdateAiBoxAsync([FromBody] SyncUpdateRequest request) 
        {
            if (request == null || request.Items == null || !request.Items.Any()) return new JsonResult(new { success = false, message = "沒有接收到更新資料" });
            try 
            {
                var device = _context.Devices.FirstOrDefault(d => d.Id == request.DeviceId);
                if (device == null) return new JsonResult(new { success = false, message = "找不到指定的設備" });
                
                var token = await _boxApi.LoginAsync(device.DeviceIP, device.DevicePort, device.DeviceUser, device.DevicePassword);
                if (string.IsNullOrEmpty(token)) return new JsonResult(new { success = false, message = "設備登入失敗，請檢查連線與帳密" });
                
                foreach (var item in request.Items) 
                {
                    var apiCam = new ApiCamera { 
                        id = item.CameraId, 
                        name = item.Name ?? "", 
                        accessType = string.IsNullOrEmpty(item.ConnectionMethod) ? 1 : (int.TryParse(item.ConnectionMethod, out int accType) ? accType : 1), 
                        url = item.StreamUrl ?? "", 
                        type = string.IsNullOrWhiteSpace(item.AlgTypes) ? new List<object>() : item.AlgTypes.Split(',').Select(s => int.TryParse(s.Trim(), out int n) ? (object)n : s.Trim()).ToList(), 
                        areaId = string.IsNullOrEmpty(item.AreaId) ? "0" : item.AreaId, 
                        code = "" 
                    };
                    
                    if (item.State == 1) { await _boxApi.AddCameraAsync(device.DeviceIP, device.DevicePort, token, apiCam); }
                    else if (item.State == 2) { await _boxApi.UpdateCameraAsync(device.DeviceIP, device.DevicePort, token, apiCam); }
                    else if (item.State == 3) { await _boxApi.DeleteCameraAsync(device.DeviceIP, device.DevicePort, token, item.CameraId); }
                } 
                return new JsonResult(new { success = true });
            } 
            catch (Exception ex) 
            { 
                return new JsonResult(new { success = false, message = ex.Message }); 
            }
        }
    }
}