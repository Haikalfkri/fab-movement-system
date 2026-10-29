using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ok1.Models
{
    public class UploadFileRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; }
        public DateTime UploadDate { get; set; }
        public DateTime? RefreshTime { get; set; }
        public int RecordCount { get; set; }
        public string UploadedBy { get; set; }

    }
}