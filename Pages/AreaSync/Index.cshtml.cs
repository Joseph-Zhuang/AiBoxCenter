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
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace AiBoxCenter.Pages.AreaSync
{
    [IgnoreAntiforgeryToken]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly BoxApiService _boxApi;
        private readonly IWebHostEnvironment _environment;

        public IndexModel(AppDbContext context, BoxApiService boxApi, IWebHostEnvironment environment)
        {
            _context = context;
            _boxApi = boxApi;
            _environment = environment;
        }

        public int SelectedDeviceId { get; set; }
        public List<Device> DeviceList { get; set; } = new();
        public string LeftTreeJson { get; set; } = "[]";
        public string RightTreeJson { get; set; } = "[]";

        [BindProperty] public TreeInput TreeModel { get; set; }

        public class TreeInput
        {
            // 修改點：將 Id 改為 string，以相容資料庫 ID 與 API 返回的字串 ID
            public string? Id { get; set; }
            public string Name { get; set; }
            public string? AreaId { get; set; }
            public string? ParentId { get; set; }
            public IFormFile? ImageFile { get; set; }
        }

        private string Generate18DigitId()
        {
            var rand = new Random();
            string newId;
            do
            {
                newId = "";
                for (int i = 0; i < 18; i++) newId += rand.Next(0, 10).ToString();
            } while (_context.Areas.Any(a => a.AreaId == newId));
            return newId;
        }

        private bool ApiAreaExists(List<ApiArea> areas, string name, string id, string excludeId)
        {
            if (areas == null) return false;
            foreach (var a in areas)
            {
                if ((a.name == name || a.id == id) && a.id != excludeId) return true;
                if (ApiAreaExists(a.children, name, id, excludeId)) return true;
            }
            return false;
        }

        private async Task<bool> CheckApiDuplicate(int deviceId, string name, string id, string excludeId = null)
        {
            var device = await _context.Devices.FindAsync(deviceId);
            var token = await _boxApi.LoginAsync(device.DeviceIP, device.DevicePort, device.DeviceUser, device.DevicePassword);
            var apiAreas = await _boxApi.GetAreaListAsync(device.DeviceIP, device.DevicePort, token);
            return ApiAreaExists(apiAreas, name, id, excludeId);
        }

        public async Task<IActionResult> OnGetAsync(int? deviceId)
        {
            DeviceList = await _context.Devices.AsNoTracking().ToListAsync();

            // Build Left Tree (Database) without virtual root
            var areas = await _context.Areas.AsNoTracking().OrderBy(a => a.SortOrder).ThenBy(a => a.Id).ToListAsync();
            var leftNodes = areas.Select(a => new
            {
                id = "db_" + a.Id.ToString(),
                parent = a.ParentId.HasValue ? "db_" + a.ParentId.Value.ToString() : "#",
                text = a.AreaName,
                icon = string.IsNullOrEmpty(a.Img_Url) ? "fas fa-folder" : "fas fa-image",
                state = new { opened = true },
                li_attr = new { data_img = a.Img_Url ?? "", area_id = a.AreaId ?? "" }
            }).ToList();
            LeftTreeJson = JsonConvert.SerializeObject(leftNodes);

            if (!deviceId.HasValue || deviceId.Value == 0)
            {
                if (DeviceList.Any())
                {
                    SelectedDeviceId = DeviceList.First().Id;
                }
                return Page();
            }

            SelectedDeviceId = deviceId.Value;
            var device = DeviceList.FirstOrDefault(d => d.Id == deviceId.Value);
            if (device == null) return Page();

            // Build Right Tree (AiBox API)
            var token = await _boxApi.LoginAsync(device.DeviceIP, device.DevicePort, device.DeviceUser, device.DevicePassword);
            if (!string.IsNullOrEmpty(token))
            {
                var apiAreas = await _boxApi.GetAreaListAsync(device.DeviceIP, device.DevicePort, token);
                var rightNodes = new List<object>();
                FlattenApiAreas(apiAreas, "#", rightNodes);
                RightTreeJson = JsonConvert.SerializeObject(rightNodes);
            }

            return Page();
        }

        private void FlattenApiAreas(List<ApiArea> apiAreas, string parentId, List<object> result)
        {
            if (apiAreas == null) return;
            foreach (var a in apiAreas)
            {
                string currentId = "api_" + a.id;
                string currentParent = string.IsNullOrEmpty(parentId) || parentId == "0" ? "#" : parentId;

                result.Add(new
                {
                    id = currentId,
                    parent = currentParent,
                    text = a.name,
                    icon = "fas fa-folder",
                    state = new { opened = true },
                    li_attr = new { area_id = a.id }
                });

                FlattenApiAreas(a.children, currentId, result);
            }
        }

        public async Task<IActionResult> OnPostCreateAreaAsync()
        {
            if (string.IsNullOrEmpty(TreeModel.Name)) return new JsonResult(new { success = false, message = "名稱不能為空" });
            if (_context.Areas.Any(a => a.AreaName == TreeModel.Name)) return new JsonResult(new { success = false, message = "資料庫已存在相同名稱的區域" });

            int? dbParentId = null;
            if (!string.IsNullOrEmpty(TreeModel.ParentId) && TreeModel.ParentId != "#" && TreeModel.ParentId.StartsWith("db_"))
            {
                if (int.TryParse(TreeModel.ParentId.Replace("db_", ""), out int pid)) dbParentId = pid;
            }

            var node = new Area { AreaName = TreeModel.Name, AreaId = Generate18DigitId() };
            node.ParentId = dbParentId;
            node.SortOrder = 999;

            if (TreeModel.ImageFile != null) node.Img_Url = await SaveFile(TreeModel.ImageFile);
            _context.Areas.Add(node);
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostEditAreaAsync()
        {
            // 修改點：從字串轉換為 int 去資料庫查詢
            if (int.TryParse(TreeModel.Id, out int dbId))
            {
                var node = await _context.Areas.FindAsync(dbId);
                if (node != null)
                {
                    if (_context.Areas.Any(a => a.AreaName == TreeModel.Name && a.Id != node.Id))
                        return new JsonResult(new { success = false, message = "資料庫已存在相同名稱的區域" });

                    node.AreaName = TreeModel.Name;
                    if (TreeModel.ImageFile != null) node.Img_Url = await SaveFile(TreeModel.ImageFile);
                    await _context.SaveChangesAsync();
                }
            }
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostDeleteAreaAsync([FromBody] DeleteRequest req)
        {
            if (int.TryParse(req.Id, out int id))
            {
                var node = await _context.Areas.FindAsync(id);
                if (node != null)
                {
                    _context.Areas.Remove(node);
                    await _context.SaveChangesAsync();
                }
            }
            return new JsonResult(new { success = true });
        }

        // 修改點：移除 [FromForm] TreeInput 參數，直接使用 Class 級別的 TreeModel 屬性
        public async Task<IActionResult> OnPostCreateAiBoxAreaAsync([FromQuery] int deviceId)
        {
            if (string.IsNullOrEmpty(TreeModel.Name)) return new JsonResult(new { success = false, message = "名稱不能為空" });
            if (await CheckApiDuplicate(deviceId, TreeModel.Name, "")) return new JsonResult(new { success = false, message = "AiBox 已存在相同名稱的區域" });

            string parentId = "0";
            if (!string.IsNullOrEmpty(TreeModel.ParentId) && TreeModel.ParentId != "#" && TreeModel.ParentId.StartsWith("api_"))
            {
                parentId = TreeModel.ParentId.Replace("api_", "");
            }
            else if (!string.IsNullOrEmpty(TreeModel.ParentId) && TreeModel.ParentId != "#")
            {
                parentId = TreeModel.ParentId;
            }

            var device = await _context.Devices.FindAsync(deviceId);
            var token = await _boxApi.LoginAsync(device.DeviceIP, device.DevicePort, device.DeviceUser, device.DevicePassword);
            if (!string.IsNullOrEmpty(token))
            {
                var result = await _boxApi.AddAreaAsync(device.DeviceIP, device.DevicePort, token, TreeModel.Name, parentId, null);
                if (result.IsSuccess)
                {
                    // 成功時一併回傳建立的新 ID
                    return new JsonResult(new { success = true, newId = result.NewId });
                }
            }
            return new JsonResult(new { success = false, message = "新增失敗" });
        }

        // 修改點：移除 [FromForm] TreeInput 參數，並使用 TreeModel.Id 當作更新節點的正確依據
        public async Task<IActionResult> OnPostEditAiBoxAreaAsync([FromQuery] int deviceId)
        {
            string nodeIdStr = TreeModel.Id ?? ""; // AiBox API 更新時需要的真正 Node ID

            if (string.IsNullOrEmpty(TreeModel.Name)) return new JsonResult(new { success = false, message = "名稱不能為空" });
            if (await CheckApiDuplicate(deviceId, TreeModel.Name, nodeIdStr, nodeIdStr)) return new JsonResult(new { success = false, message = "AiBox 已存在相同名稱的區域" });

            string parentId = "0";
            if (!string.IsNullOrEmpty(TreeModel.ParentId) && TreeModel.ParentId != "#" && TreeModel.ParentId.StartsWith("api_"))
            {
                parentId = TreeModel.ParentId.Replace("api_", "");
            }
            else if (!string.IsNullOrEmpty(TreeModel.ParentId) && TreeModel.ParentId != "#")
            {
                parentId = TreeModel.ParentId;
            }

            var device = await _context.Devices.FindAsync(deviceId);
            var token = await _boxApi.LoginAsync(device.DeviceIP, device.DevicePort, device.DeviceUser, device.DevicePassword);
            if (!string.IsNullOrEmpty(token))
            {
                // 正確傳遞 nodeIdStr，而不再是誤傳 AreaId
                await _boxApi.UpdateAreaAsync(device.DeviceIP, device.DevicePort, token, nodeIdStr, TreeModel.Name, parentId);
            }
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostDeleteAiBoxAreaAsync([FromBody] DeleteRequest req)
        {
            var device = await _context.Devices.FindAsync(req.DeviceId);
            var token = await _boxApi.LoginAsync(device.DeviceIP, device.DevicePort, device.DeviceUser, device.DevicePassword);
            if (!string.IsNullOrEmpty(token))
            {
                await _boxApi.DeleteAreaAsync(device.DeviceIP, device.DevicePort, token, req.Id);
            }
            return new JsonResult(new { success = true });
        }

        private async Task<string> SaveFile(IFormFile file)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(_environment.WebRootPath, "images", fileName);
            using (var s = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(s);
            }
            return "/images/" + fileName;
        }

        public async Task<IActionResult> OnPostUpdateAreaNodeAsync([FromForm] int id, [FromForm] string parent, [FromForm] int position)
        {
            var node = await _context.Areas.FindAsync(id);
            if (node == null) return new JsonResult(new { success = false });
            node.ParentId = (parent == "root_all" || parent == "#") ? (int?)null : int.Parse(parent.Replace("db_", ""));
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostUpdateAiBoxAreaNodeAsync([FromBody] SyncAreaRequest req)
        {
            var device = await _context.Devices.FindAsync(req.DeviceId);
            var token = await _boxApi.LoginAsync(device.DeviceIP, device.DevicePort, device.DeviceUser, device.DevicePassword);
            if (!string.IsNullOrEmpty(token))
            {
                await _boxApi.UpdateAreaAsync(device.DeviceIP, device.DevicePort, token, req.Id, req.NodeName, req.TargetParentId);
            }
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostSyncToDbAsync([FromBody] SyncAreaRequest req)
        {
            if (_context.Areas.Any(a => a.AreaId == req.AreaId || a.AreaName == req.NodeName))
                return new JsonResult(new { success = false, message = "資料庫已存在相同的 AreaId 或 名稱" });

            int? parentId = null;
            if (!string.IsNullOrEmpty(req.TargetParentId) && req.TargetParentId != "#" && req.TargetParentId.StartsWith("db_"))
            {
                if (int.TryParse(req.TargetParentId.Replace("db_", ""), out int pid)) parentId = pid;
            }
            _context.Areas.Add(new Area { AreaName = req.NodeName, AreaId = req.AreaId, ParentId = parentId, SortOrder = 999 });
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostSyncToAiBoxAsync([FromBody] SyncAreaRequest req)
        {
            if (await CheckApiDuplicate(req.DeviceId, req.NodeName, req.AreaId))
                return new JsonResult(new { success = false, message = "AiBox 已存在相同的 AreaId 或 名稱" });

            var device = await _context.Devices.FindAsync(req.DeviceId);
            if (device == null) return new JsonResult(new { success = false });

            string parentId = "0";
            if (!string.IsNullOrEmpty(req.TargetParentId) && req.TargetParentId != "#" && req.TargetParentId.StartsWith("api_"))
            {
                parentId = req.TargetParentId.Replace("api_", "");
            }

            var token = await _boxApi.LoginAsync(device.DeviceIP, device.DevicePort, device.DeviceUser, device.DevicePassword);
            if (!string.IsNullOrEmpty(token))
            {
                await _boxApi.AddAreaAsync(device.DeviceIP, device.DevicePort, token, req.NodeName, parentId, req.AreaId);
            }
            return new JsonResult(new { success = true });
        }
    }
}