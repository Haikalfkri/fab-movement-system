using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ok1.Models
{
    public class ManagerList
    {
        [Key]
        public int Id { get; set; }
        public string BadgeId { get; set; }
        public string Name { get; set; }
        public string Manager { get; set; }
    }
}