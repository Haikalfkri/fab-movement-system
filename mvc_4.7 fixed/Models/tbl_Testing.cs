using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ok1.Models
{
    public class tbl_Testing
    {
        public int Id { get; set; }
        public string BadgeId { get; set; }
        public TimeSpan? Duration { get; set; }
        public DateTime? SubmitTime { get; set; }
        public string LongOut { get; set; }
        public string Shift { get; set; }
        public string Status { get; set; }
        public string OutOf { get; set; }
    }
}
