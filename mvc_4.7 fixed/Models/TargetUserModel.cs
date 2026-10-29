using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ok1.Models
{
    public class TargetUserModel
    {
        public int Id { get; set; }
        public string EmpBadgeId { get; set; }
        public string EmpName { get; set; }
        public string EmpDept {  get; set; }
        public string Designation {  get; set; }
        public string JobLevel { get; set; }
        public string ShiftCode { get; set; }
        public string Section {  get; set; }
        public string OrgUnit { get; set; }
        public string CostCenter { get; set; }
        public string Level1 { get; set; }
        public DateTime WorkDay { get; set; }
        public string Day {  get; set; }
        public string Shift { get; set; }
        public TimeSpan FabIn { get; set; }
        public TimeSpan FabOut { get; set;}
        public TimeSpan FirstCrIn { get; set; }
        public TimeSpan LastCrOut { get; set; }
        public TimeSpan FabTime { get; set; }
        public TimeSpan CrTime { get; set; }
        public string BrTime { get; set; }
        public string FabCount { get; set; }
        public string CrCount { get; set; }
        public string FabLateIn { get; set; }
        public string FabEarlyOut { get;set; }
        public string Logweek { get; set; }
        public string PublicHoliday { get; set; }
        public string Workplace { get; set; }
        public string Gender { get; set; }
        public string Religion { get; set; }
        public string FridayPray { get; set; }
        public TimeSpan Busui { get; set; }
        public string Ahm {  get; set; }
        public string Amend { get; set; }
        public string ExitPermit { get; set; }
        public string TargetTime { get; set; }
        public string TimeClass { get; set; }
    }
}