using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ok1.Models
{

    public class TargetTime
    { /* EMPLOYEE DATA */
        public int Id { get; set; }
        public string EmpBadgeId { get; set; }
        public string EmpName { get; set; } = string.Empty;
        public string EmpDept { get; set; }
        /* SHIFTS */
        public TimeSpan NormalShift { get; set; }
        public TimeSpan MorningShift { get; set; }
        public TimeSpan AfternoonShift { get; set; }
        public TimeSpan NightShift { get; set; }
        /* AREA */
        public string Group { get; set; }
        public string AreaDI { get; set; }
        public string Area { get; set; }
    }
}