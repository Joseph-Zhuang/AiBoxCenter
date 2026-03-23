using Microsoft.AspNetCore.Mvc; 
using Microsoft.AspNetCore.Mvc.RazorPages; 
using Microsoft.EntityFrameworkCore; 
using AiBoxCenter.Data; 
using AiBoxCenter.Models; 
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;

namespace AiBoxCenter.Pages.Cameras 
{
    public class IndexModel : PageModel 
    {
        private readonly AppDbContext _context; 
        private readonly IWebHostEnvironment _environment;
        
        public IndexModel(AppDbContext context, IWebHostEnvironment environment) 
        { 
            _context = context; 
            _environment = environment; 
        }
        
        public IList<Camera> Cameras { get; set; } 
        public string AreaTreeJson { get; set; } 
        public string GroupTreeJson { get; set; }
        
        [BindProperty(SupportsGet = true)] public string SelectedAreaId { get; set; } 
        [BindProperty(SupportsGet = true)] public string SelectedGroupId { get; set; }
        
        public string SelectedAreaName { get; set; } = "全部"; 
        public string SelectedGroupName { get; set; } = "全部";
        
        [BindProperty] public TreeInput TreeModel { get; set; }
        
        public class TreeInput 
        { 
            public int? Id { get; set; } 
            public string Name { get; set; } 
            public string? AreaId { get; set; } 
            public string? ParentId { get; set; } 
            public IFormFile? ImageFile { get; set; } 
        }

        private string Generate18DigitId()
        {
            var rand = new Random();
            string newId;
            do {
                newId = "";
                for(int i=0; i<18; i++) newId += rand.Next(0, 10).ToString();
            } while (_context.Areas.Any(a => a.AreaId == newId));
            return newId;
        }
        
        public async Task OnGetAsync() 
        {
            var areas = await _context.Areas.OrderBy(a => a.SortOrder).ThenBy(a => a.Id).ToListAsync(); 
            AreaTreeJson = BuildTreeJson(areas.Select(a => (a.Id, a.ParentId, a.AreaName, a.Img_Url, a.SortOrder, a.AreaId)).ToList(), SelectedAreaId);
            
            var groups = await _context.Groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync(); 
            GroupTreeJson = BuildTreeJson(groups.Select(g => (g.Id, g.ParentId, g.Name, (string?)null, g.SortOrder, (string?)null)).ToList(), SelectedGroupId);
            
            var query = _context.Cameras
                .Include(c => c.Device)
                .Include(c => c.CameraAlgorithms).ThenInclude(ca => ca.Algorithm)
                .Include(c => c.ConnectionMethod)
                .Include(c => c.Area)
                .Include(c => c.Group).AsQueryable();
                
            if (!string.IsNullOrEmpty(SelectedAreaId) && SelectedAreaId != "root_all" && int.TryParse(SelectedAreaId, out int aid)) 
            { 
                var childIds = GetAreaChildIds(areas, aid); 
                childIds.Add(aid); 
                query = query.Where(c => c.AreaId.HasValue && childIds.Contains(c.AreaId.Value)); 
                SelectedAreaName = areas.FirstOrDefault(a => a.Id == aid)?.AreaName ?? "全部"; 
            }
            else if (!string.IsNullOrEmpty(SelectedGroupId) && SelectedGroupId != "root_all" && int.TryParse(SelectedGroupId, out int gid)) 
            { 
                var childIds = GetGroupChildIds(groups, gid); 
                childIds.Add(gid); 
                query = query.Where(c => c.GroupId.HasValue && childIds.Contains(c.GroupId.Value)); 
                SelectedGroupName = groups.FirstOrDefault(g => g.Id == gid)?.Name ?? "全部"; 
            }
            
            Cameras = await query.AsNoTracking().ToListAsync();
        }
        
        private string BuildTreeJson(List<(int Id, int? ParentId, string Name, string? ImgUrl, int Sort, string? AreaId)> nodes, string selectedId) 
        { 
            var treeNodes = nodes.Select(n => new { 
                id = n.Id.ToString(), 
                parent = n.ParentId.HasValue ? n.ParentId.Value.ToString() : "root_all", 
                text = n.Name, 
                icon = string.IsNullOrEmpty(n.ImgUrl) ? "fas fa-folder" : "fas fa-image", 
                state = new { opened = true, selected = (n.Id.ToString() == selectedId) }, 
                type = "default", 
                li_attr = new { data_img = n.ImgUrl ?? "", area_id = n.AreaId ?? "" } 
            }).ToList(); 
            
            treeNodes.Insert(0, new { 
                id = "root_all", 
                parent = "#", 
                text = "全部", 
                icon = "fas fa-globe", 
                state = new { opened = true, selected = (selectedId == "root_all" || string.IsNullOrEmpty(selectedId)) }, 
                type = "root", 
                li_attr = new { data_img = "", area_id = "" } 
            }); 
            return JsonConvert.SerializeObject(treeNodes); 
        }
        
        public async Task<IActionResult> OnPostCreateAreaAsync() 
        { 
            if (string.IsNullOrEmpty(TreeModel.Name)) return RedirectToPage(); 
            if (_context.Areas.Any(a => a.AreaName == TreeModel.Name)) {
                ModelState.AddModelError("", "資料庫已存在相同名稱的區域");
                await OnGetAsync();
                return Page();
            }

            int? dbParentId = (TreeModel.ParentId != "root_all" && int.TryParse(TreeModel.ParentId, out int pid)) ? pid : (int?)null; 
            
            var node = new Area();
            node.AreaName = TreeModel.Name; 
            node.AreaId = Generate18DigitId();
            node.ParentId = dbParentId; 
            node.SortOrder = 999; 
            
            if (TreeModel.ImageFile != null) node.Img_Url = await SaveFile(TreeModel.ImageFile); 
            _context.Add(node); 
            await _context.SaveChangesAsync(); 
            return RedirectToPage(new { SelectedAreaId, SelectedGroupId }); 
        }
        
        public async Task<IActionResult> OnPostEditAreaAsync() 
        { 
            var node = await _context.Areas.FindAsync(TreeModel.Id); 
            if (node != null) 
            { 
                if (_context.Areas.Any(a => a.AreaName == TreeModel.Name && a.Id != node.Id)) {
                    ModelState.AddModelError("", "資料庫已存在相同名稱的區域");
                    await OnGetAsync();
                    return Page();
                }

                node.AreaName = TreeModel.Name; 
                // AreaId is readonly, do not update.
                if (TreeModel.ImageFile != null) node.Img_Url = await SaveFile(TreeModel.ImageFile); 
                await _context.SaveChangesAsync(); 
            } 
            return RedirectToPage(new { SelectedAreaId, SelectedGroupId }); 
        }

        public async Task<IActionResult> OnPostCreateGroupAsync() 
        { 
            if (string.IsNullOrEmpty(TreeModel.Name)) return RedirectToPage(); 
            int? dbParentId = (TreeModel.ParentId != "root_all" && int.TryParse(TreeModel.ParentId, out int pid)) ? pid : (int?)null; 
            
            var node = new Group();
            node.Name = TreeModel.Name; 
            node.ParentId = dbParentId; 
            node.SortOrder = 999; 
            
            _context.Add(node); 
            await _context.SaveChangesAsync(); 
            return RedirectToPage(new { SelectedAreaId, SelectedGroupId }); 
        }

        public async Task<IActionResult> OnPostEditGroupAsync() 
        { 
            var node = await _context.Groups.FindAsync(TreeModel.Id); 
            if (node != null) 
            { 
                node.Name = TreeModel.Name; 
                await _context.SaveChangesAsync(); 
            } 
            return RedirectToPage(new { SelectedAreaId, SelectedGroupId }); 
        }

        public async Task<IActionResult> OnPostDeleteAreaAsync(int id) 
        { 
            var node = await _context.Areas.FindAsync(id); 
            if (node != null) 
            { 
                _context.Remove(node); 
                await _context.SaveChangesAsync(); 
            } 
            return RedirectToPage(new { SelectedAreaId, SelectedGroupId }); 
        }

        public async Task<IActionResult> OnPostDeleteGroupAsync(int id) 
        { 
            var node = await _context.Groups.FindAsync(id); 
            if (node != null) 
            { 
                _context.Remove(node); 
                await _context.SaveChangesAsync(); 
            } 
            return RedirectToPage(new { SelectedAreaId, SelectedGroupId }); 
        }
        
        public async Task<IActionResult> OnPostUpdateAreaNodeAsync(int id, string parent, int position) 
        { 
            var node = await _context.Areas.FindAsync(id); 
            if (node == null) return new JsonResult(new { success = false }); 
            node.ParentId = (parent == "root_all" || parent == "#") ? (int?)null : int.Parse(parent); 
            await _context.SaveChangesAsync(); 
            return new JsonResult(new { success = true }); 
        }

        public async Task<IActionResult> OnPostUpdateGroupNodeAsync(int id, string parent, int position) 
        { 
            var node = await _context.Groups.FindAsync(id); 
            if (node == null) return new JsonResult(new { success = false }); 
            node.ParentId = (parent == "root_all" || parent == "#") ? (int?)null : int.Parse(parent); 
            await _context.SaveChangesAsync(); 
            return new JsonResult(new { success = true }); 
        }
        
        public async Task<IActionResult> OnPostDeleteCameraAsync(int id) 
        { 
            var cam = await _context.Cameras.FindAsync(id); 
            if (cam != null) 
            { 
                _context.Cameras.Remove(cam); 
                await _context.SaveChangesAsync(); 
            } 
            return RedirectToPage(new { SelectedAreaId, SelectedGroupId }); 
        }
        
        private List<int> GetAreaChildIds(List<Area> all, int pid) 
        { 
            var ids = new List<int>(); 
            foreach(var c in all.Where(x => x.ParentId == pid)) 
            { 
                ids.Add(c.Id); 
                ids.AddRange(GetAreaChildIds(all, c.Id)); 
            } 
            return ids; 
        }
        
        private List<int> GetGroupChildIds(List<Group> all, int pid) 
        { 
            var ids = new List<int>(); 
            foreach(var c in all.Where(x => x.ParentId == pid)) 
            { 
                ids.Add(c.Id); 
                ids.AddRange(GetGroupChildIds(all, c.Id)); 
            } 
            return ids; 
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
    }
}