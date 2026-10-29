using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ok1.Models.ApiResponse.FabMove
{
    public class FabMoveDto
    {
        public string empBadgeId { get; set; }
        public string empName { get; set; }
        public string empDept { get; set; }
        public string designation { get; set; }
        public string jobLevel { get; set; }
        public string shiftCode { get; set; }
        public string section { get; set; }
        public string orgUnit { get; set; }
        public string costCenter { get; set; }
        public string level1 { get; set; }
        public string workDay { get; set; }
        public string day { get; set; }
        public string shift { get; set; }
        public string fabIn { get; set; }
        public string fabOut { get; set; }
        public string firstCrIn { get; set; }
        public string lastCrOut { get; set; }
        public string fabTime { get; set; }
        public string crTime { get; set; }
        public string brTime { get; set; }
        public string fabCount { get; set; }
        public string crCount { get; set; }
        public string fabLateIn { get; set; }
        public string fabEarlyOut { get; set; }
        public string group { get; set; }
        public string area { get; set; }
        public string areaDI { get; set; }
        public string publicHoliday { get; set; }
        public bool isValid { get; set; }
        public string workplace { get; set; }
        public string gender { get; set; }
        public string religion { get; set; }
        public string fridayPray { get; set; }
        public string busui { get; set; }
        public string ahm { get; set; }
        public string amend { get; set; }
        public string exitPermit { get; set; }
        public string targetTime { get; set; }
        public string timeClass { get; set; }
        public string missing { get; set; }

        public string rowClass { get; set; }
        public string fabInClass { get; set; }
        public string fabOutClass { get; set; }
        public string firstCrInClass { get; set; }
        public string lastCrOutClass { get; set; }
        public string crTimeClass { get; set; }
    }
}