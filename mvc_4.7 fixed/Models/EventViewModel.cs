using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ok1.Models
{
    public class EventDetailViewModel
    {
        public string BadgeId { get; set; }
        public string EmpName { get; set; }
        public string EmpDept { get; set; }
        public string EventName { get; set; }
        public DateTime EventDate { get; set; }
        public string MonthName { get; set; }
        public int Year { get; set; }
        public string EventDuration { get; set; }
    }
}