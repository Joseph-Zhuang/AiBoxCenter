using System.ComponentModel.DataAnnotations; 
using System.ComponentModel.DataAnnotations.Schema; 
using System.Collections.Generic;

namespace AiBoxCenter.Models 
{
    public class Algorithm { public int Id { get; set; } public string AlgName { get; set; } public int AlgType { get; set; } }
    
    public class Area 
    { 
        public int Id { get; set; } 
        public string AreaName { get; set; } 
        [MaxLength(100)] public string? AreaId { get; set; } 
        public string? Img_Url { get; set; } 
        public int? ParentId { get; set; } 
        public int SortOrder { get; set; }
        [ForeignKey("ParentId")] public Area? Parent { get; set; } 
        public List<Area> Children { get; set; } = new();
    }
    
    public class Group 
    {
        public int Id { get; set; } 
        public string Name { get; set; } 
        public int? ParentId { get; set; } 
        public int SortOrder { get; set; }
        [ForeignKey("ParentId")] public Group? Parent { get; set; } 
        public List<Group> Children { get; set; } = new();
    }
    
    public class ConnectionMethod { public int Id { get; set; } public string Name { get; set; } }
    
    [Table("ObjectType")] 
    public class ObjectType { public int Id { get; set; } public int TypeCode { get; set; } [MaxLength(20)] public string LocaleName { get; set; } [MaxLength(100)] public string TypeName { get; set; } }
}