using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AiBoxCenter.Data;
using AiBoxCenter.Models;
using System.Text.Json;

namespace AiBoxCenter.Pages.Cameras
{
    public class DeviceLayoutModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeviceLayoutModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Area> Areas { get; set; } = new();
        public List<Device> AiBoxes { get; set; } = new(); // 對應 [dbo].[Devices]
        public List<Camera> Cameras { get; set; } = new(); // 對應 [dbo].[Cameras]

        [BindProperty]
        public int? SelectedAreaId { get; set; }

        [BindProperty]
        public int? SelectedAiBoxId { get; set; }

        [BindProperty]
        public int? SelectedCameraId { get; set; }

        public string? BackgroundImageUrl { get; set; }

        [BindProperty]
        public string? CameraPositionsJson { get; set; }

        public string ServerMessage { get; set; } = "";

        public async Task OnGetAsync(int? areaId)
        {
            await LoadDataAsync(areaId);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDataAsync(SelectedAreaId);
            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (SelectedAreaId.HasValue && SelectedCameraId.HasValue)
            {
                var camera = await _context.Cameras.FindAsync(SelectedCameraId.Value);
                if (camera != null)
                {
                    if (camera.AreaId == null || camera.AreaId == 0)
                    {
                        camera.AreaId = SelectedAreaId.Value;
                        camera.LocationX = 50; // 預設初始位置
                        camera.LocationY = 50;
                        await _context.SaveChangesAsync();
                        ServerMessage = "攝影機已新增至區域";
                    }
                    else
                    {
                        ServerMessage = "此攝影機已在其他區域配置！";
                    }
                }
            }
            await LoadDataAsync(SelectedAreaId);
            return Page();
        }

        public async Task<IActionResult> OnPostRemoveAsync()
        {
            if (SelectedAreaId.HasValue && SelectedCameraId.HasValue)
            {
                var camera = await _context.Cameras.FindAsync(SelectedCameraId.Value);
                if (camera != null && camera.AreaId == SelectedAreaId.Value)
                {
                    camera.AreaId = null;
                    camera.LocationX = -1;
                    camera.LocationY = -1;
                    await _context.SaveChangesAsync();
                    ServerMessage = "攝影機已從區域移除";
                }
            }
            await LoadDataAsync(SelectedAreaId);
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (!string.IsNullOrEmpty(CameraPositionsJson))
            {
                var positions = JsonSerializer.Deserialize<Dictionary<string, Position>>(CameraPositionsJson);
                if (positions != null)
                {
                    foreach (var kvp in positions)
                    {
                        if (int.TryParse(kvp.Key, out int camId))
                        {
                            var camera = await _context.Cameras.FindAsync(camId);
                            if (camera != null && camera.AreaId == SelectedAreaId)
                            {
                                camera.LocationX = kvp.Value.X;
                                camera.LocationY = kvp.Value.Y;
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                    ServerMessage = "佈局位置已儲存";
                }
            }
            await LoadDataAsync(SelectedAreaId);
            return Page();
        }

        private async Task LoadDataAsync(int? areaId)
        {
            Areas = await _context.Areas.OrderBy(a => a.SortOrder).ToListAsync();
            AiBoxes = await _context.Devices.ToListAsync(); // 獲取 Aibox 內容
            Cameras = await _context.Cameras.ToListAsync(); // 獲取 Camera 內容

            SelectedAreaId = areaId ?? Areas.FirstOrDefault()?.Id;
            if (SelectedAreaId.HasValue)
            {
                var area = Areas.FirstOrDefault(a => a.Id == SelectedAreaId);
                BackgroundImageUrl = area?.Img_Url;
            }
        }

        public class Position { public int X { get; set; } public int Y { get; set; } }
    }
}