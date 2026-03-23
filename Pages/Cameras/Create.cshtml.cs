using Microsoft.AspNetCore.Mvc; 
using Microsoft.AspNetCore.Mvc.RazorPages; 
using Microsoft.AspNetCore.Mvc.Rendering; 
using Microsoft.EntityFrameworkCore; 
using AiBoxCenter.Data; 
using AiBoxCenter.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AiBoxCenter.Pages.Cameras 
{ 
    public class CreateModel : PageModel 
    { 
        private readonly AppDbContext _context; 
        
        public CreateModel(AppDbContext context) 
        { 
            _context = context; 
        } 
        
        [BindProperty] public CameraInput Input { get; set; } 
        
        public List<SelectListItem> ConnectionMethods { get; set; } 
        public List<SelectListItem> Algorithms { get; set; } 
        public List<SelectListItem> Areas { get; set; } 
        public List<SelectListItem> Groups { get; set; } 
        public List<SelectListItem> Devices { get; set; } 
        
        public class CameraInput : Camera 
        { 
            public List<int> AlgorithmIds { get; set; } = new(); 
        } 
        
        public async Task OnGetAsync() 
        { 
            await LoadDropdowns(); 
        } 
        
        public async Task<IActionResult> OnPostAsync() 
        { 
            if (!ModelState.IsValid) 
            { 
                await LoadDropdowns(); 
                return Page(); 
            } 
            
            var cam = new Camera { 
                Name = Input.Name, 
                DeviceId = Input.DeviceId, 
                StreamUrl = Input.StreamUrl, 
                ConnectionMethodId = Input.ConnectionMethodId, 
                AreaId = Input.AreaId, 
                GroupId = Input.GroupId, 
                CameraId = Input.CameraId 
            }; 
            
            foreach(var id in Input.AlgorithmIds) 
            { 
                cam.CameraAlgorithms.Add(new CameraAlgorithm { AlgorithmId = id }); 
            } 
            
            _context.Cameras.Add(cam); 
            await _context.SaveChangesAsync(); 
            return RedirectToPage("./Index"); 
        } 
        
        private async Task LoadDropdowns() 
        { 
            ConnectionMethods = await _context.ConnectionMethods.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(); 
            Algorithms = await _context.Algorithms.Select(x => new SelectListItem(x.AlgName, x.Id.ToString())).ToListAsync(); 
            Areas = await _context.Areas.Select(x => new SelectListItem(x.AreaName, x.Id.ToString())).ToListAsync(); 
            Groups = await _context.Groups.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(); 
            Devices = await _context.Devices.Select(x => new SelectListItem(x.DeviceName + " (" + x.DeviceIP + ":" + x.DevicePort + ")", x.Id.ToString())).ToListAsync(); 
        } 
    } 
}