using Microsoft.EntityFrameworkCore; 
using AiBoxCenter.Models;

namespace AiBoxCenter.Data 
{
    public class AppDbContext : DbContext 
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        
        public DbSet<Camera> Cameras { get; set; } 
        public DbSet<Device> Devices { get; set; } 
        public DbSet<CameraAlgorithm> CameraAlgorithms { get; set; }
        public DbSet<Algorithm> Algorithms { get; set; } 
        public DbSet<Area> Areas { get; set; } 
        public DbSet<ConnectionMethod> ConnectionMethods { get; set; }
        public DbSet<Group> Groups { get; set; } 
        public DbSet<ObjectType> ObjectTypes { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder) 
        { 
            base.OnModelCreating(modelBuilder); 
            modelBuilder.Entity<CameraAlgorithm>().HasKey(ca => new { ca.CameraId, ca.AlgorithmId }); 
        }
    }
}