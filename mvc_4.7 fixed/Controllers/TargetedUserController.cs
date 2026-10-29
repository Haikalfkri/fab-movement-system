using ok1.Data;
using ok1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ok1.Controllers
{
    public class TargetedUserController
    {
        private ok1Context db = new ok1Context();
        private masterContext master = new masterContext();

        public ActionResult Index(
           string sortOrder, string searchBadgeId,
           string deptFilter, string unitFilter, string sCodeFilter,
           DateTime? from, DateTime? to, int? page = 1)
        {
            DateTime? fromDate = from;
            DateTime? toDate = to;

            TimeSpan deduction = TimeSpan.FromMinutes(30);

            // Fetch all required data once
            var publicHoliday = db.PublicHolidays.ToList();
            var fabMove = db.BFabMoves.ToList();
            var targetTimeData = db.TargetTimes.ToList();
            var permission = master.tbl_Testing.ToList();
            var fabAmend = master.FormRequest.ToList();
            var busuiData = db.Breastfeedings.ToList();
            var attendeeData = db.GetAttendees.ToList();
            var eventAttData = db.EventAttendees.ToList();
            var eventData = db.EventRegs.ToList();
            var gmdsData = master.gmds
                .Select(g => new { g.BadgeId, g.Name, g.Department, g.Group, g.Area, g.AreaDI, g.Production, g.Gender, g.Religion })
                .ToList();

            // Filter FabMove data based on date range
            var matchDate = db.BFabMoves.AsQueryable();
            if (fromDate.HasValue && toDate.HasValue)
            {
                fromDate = fromDate.Value.Date;
                toDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                matchDate = matchDate.Where(f => f.WorkDay >= fromDate && f.WorkDay <= toDate);
            }

            var fabData = matchDate.ToList();


            var fabmove = fabData.ToList(); //Re-define to adjust updated data on FabMoves
           
            // Fetch updated FabMove data after backend updates
            var sCodes = fabmove.Select(s => s.ShiftCode).Distinct().ToList();
            var byDept = gmdsData.Select(s => s.Department).Distinct().ToList();
            var byOrgUnit = fabmove.Select(s => s.OrgUnit).Distinct().ToList();

            // Apply filters to gmdsData first
            var filteredGmds = gmdsData.AsQueryable();

            if (!String.IsNullOrEmpty(searchBadgeId))
            {
                filteredGmds = filteredGmds.Where(g => g.BadgeId.Contains(searchBadgeId) ||
                                                       g.Name.Contains(searchBadgeId));
            }

            if (!String.IsNullOrEmpty(deptFilter))
            {
                filteredGmds = filteredGmds.Where(g => g.Department.Contains(deptFilter));
            }

            if (!String.IsNullOrEmpty(unitFilter))
            {
                filteredGmds = filteredGmds.Where(g => g.Group.Contains(unitFilter));
            }

            // Join filtered gmdsData with fabmove
            var targets = from f in fabmove
                          join g in filteredGmds on f.EmpBadgeId equals g.BadgeId
                          select new FabMove
                          {
                              EmpBadgeId = f.EmpBadgeId,
                              EmpName = g.Name,
                              EmpDept = g.Department, // Use Department from gmds
                              Designation = f.Designation,
                              JobLevel = f.JobLevel,
                              ShiftCode = f.ShiftCode,
                              Section = f.Section,
                              OrgUnit = f.OrgUnit, // Retain OrgUnit from fabmove since it might differ
                              CostCenter = f.CostCenter,
                              Level1 = f.Level1,
                              WorkDay = f.WorkDay,
                              Day = f.Day,
                              Shift = f.Shift,
                              FabIn = f.FabIn,
                              FabOut = f.FabOut,
                              FirstCrIn = f.FirstCrIn,
                              LastCrOut = f.LastCrOut,
                              FabTime = f.FabTime,
                              CrTime = f.CrTime,
                              BrTime = f.BrTime,
                              FabCount = f.FabCount,
                              CrCount = f.CrCount,
                              FabLateIn = f.FabLateIn,
                              FabEarlyOut = f.FabEarlyOut,
                              PublicHoliday = funcs.GetPubHoliday(f.WorkDay, publicHoliday),
                              Workplace = g.Production, // From gmds
                              Busui = f.Busui,
                              Ahm = funcs.GetEventDurByBadgeDate(f.EmpBadgeId, f.WorkDay, attendeeData, eventAttData, eventData),
                              FridayPray = funcs.GetFridayPermit(f.EmpBadgeId, f.Day, f.Shift),
                              Amend = funcs.GetFabDur(f.EmpBadgeId, f.WorkDay, fabAmend),
                              ExitPermit = funcs.GetFabWithExitPermit(f.EmpBadgeId, f.Shift, f.WorkDay, permission),
                              Gender = g.Gender, // From gmds
                              Religion = g.Religion, // From gmds
                              TargetTime = funcs.GetTargetTime(f.EmpBadgeId, f.Shift, targetTimeData),
                              TimeClass = f.TimeClass // Use the updated TimeClass from the backend
                          };

            // Apply ShiftCode filter after joining with fabmove
            if (!String.IsNullOrEmpty(sCodeFilter))
            {
                targets = targets.Where(u => u.ShiftCode.Contains(sCodeFilter));
            }


            // Sorting
            targets = sortOrder == "name_desc"
                ? targets.OrderByDescending(u => u.EmpBadgeId)
            : targets.OrderBy(u => u.EmpBadgeId);

            int pageSize = 15;
            var pageNumber = (page ?? 1);
            var pagedList = targets.ToPagedList(pageNumber, pageSize);

            return View(pagedList);
        }


    }
}