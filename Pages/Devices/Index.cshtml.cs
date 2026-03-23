using Microsoft.AspNetCore.Mvc; 
using Microsoft.AspNetCore.Mvc.RazorPages; 
using Microsoft.EntityFrameworkCore; 
using AiBoxCenter.Data; 
using AiBoxCenter.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AiBoxCenter.Pages.Devices 
{ 
    public class IndexModel : PageModel 
    { 
        private readonly AppDbContext _context; 
        
        public IndexModel(AppDbContext context) 
        { 
            _context = context; 
        } 
        
        public IList<Device> Devices { get; set; } 
        
        [BindProperty] public Device DeviceInput { get; set; } 
        
        public async Task OnGetAsync() 
        { 
            Devices = await _context.Devices.AsNoTracking().ToListAsync(); 
        } 
        
        public async Task<IActionResult> OnPostCreateAsync() 
        { 
            if (!ModelState.IsValid) return RedirectToPage(); 
            _context.Devices.Add(DeviceInput); 
            await _context.SaveChangesAsync(); 
            return RedirectToPage(); 
        } 
        
        public async Task<IActionResult> OnPostEditAsync() 
        { 
            var dev = await _context.Devices.FindAsync(DeviceInput.Id); 
            if (dev != null) 
            { 
                dev.DeviceName = DeviceInput.DeviceName; 
                dev.DeviceIP = DeviceInput.DeviceIP; 
                dev.DevicePort = DeviceInput.DevicePort; 
                dev.DeviceSerialNo = DeviceInput.DeviceSerialNo; 
                dev.DeviceUser = DeviceInput.DeviceUser; 
                dev.DevicePassword = DeviceInput.DevicePassword; 
                dev.MqttIP = DeviceInput.MqttIP; 
                dev.MqttPort = DeviceInput.MqttPort; 
                dev.MqttSerialNo = DeviceInput.MqttSerialNo; 
                dev.MqttUser = DeviceInput.MqttUser; 
                dev.MqttPassword = DeviceInput.MqttPassword; 
                dev.IsEnable = DeviceInput.IsEnable; 
                await _context.SaveChangesAsync(); 
            } 
            return RedirectToPage(); 
        } 
        
        public async Task<IActionResult> OnPostDeleteAsync(int id) 
        { 
            var dev = await _context.Devices.FindAsync(id); 
            if (dev != null) 
            { 
                _context.Devices.Remove(dev); 
                await _context.SaveChangesAsync(); 
            } 
            return RedirectToPage(); 
        } 
    } 
}