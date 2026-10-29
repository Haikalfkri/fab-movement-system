using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ok1.Models
{

    public class User
    {
        public int Id { get; set; }
        public string EmpBadgeId { get; set; }
        public string EmpName { get; set; }
        public string EmpDept { get; set; }
        public string Group { get; set; }
        public string Area { get; set; }
        public string AreaDI { get; set; }
        public string LogWeek { get; set; }
        public int Points { get; set; }
        public double AvgPoints { get; set; }

    }
}