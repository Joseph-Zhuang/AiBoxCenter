using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AiBoxCenter.Data;
using AiBoxCenter.Models;
using Microsoft.Extensions.Configuration;

namespace AiBoxCenter.Pages.Cameras
{
    public class DeviceMonitorModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public DeviceMonitorModel(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public List<Area> Areas { get; set; } = new();
        public List<Camera> PlacedCameras { get; set; } = new();

        [BindProperty]
        public int? SelectedAreaId { get; set; }

        public string? BackgroundImageUrl { get; set; }
        public string WsUrl { get; set; } = "";

        public async Task OnGetAsync(int? areaId)
        {
            WsUrl = _configuration["DeviceMonitorSettings:WsUrl"] ?? "ws://localhost:8013/ws";

            Areas = await _context.Areas.OrderBy(a => a.SortOrder).ToListAsync();
            SelectedAreaId = areaId ?? Areas.FirstOrDefault()?.Id;

            if (SelectedAreaId.HasValue)
            {
                var area = Areas.FirstOrDefault(a => a.Id == SelectedAreaId);
                BackgroundImageUrl = area?.Img_Url;

                // 必須 Include(c => c.Device)，前端才能讀取到 cam.Device.DeviceName
                PlacedCameras = await _context.Cameras
                    .Include(c => c.Device)
                    .Where(c => c.AreaId == SelectedAreaId.Value)
                    .ToListAsync();
            }
        }
    }
}
