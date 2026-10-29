using Newtonsoft.Json;
using ok1.Data;
using ok1.Models;
using ok1.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Caching;
using System.Security.AccessControl;

namespace ok1.Controllers
{
    public class ChartController : Controller
    {
        private logweek lw;
        private GroupMappingOption grp;
        private ok1Context db = new ok1Context();
        private masterContext og = new masterContext();
        public ChartController()
        {
            lw = new logweek();
            grp = new GroupMappingOption();
        }

        // Returns areas and area details for a selected group
        public async Task<ActionResult> GetAreaByGroup(string groupName)
        {
            List<string> areaList;
            List<string> areaDIList;

            if (!string.IsNullOrEmpty(groupName))
            {
                // Get areas for the group
                areaList = await db.TargetTimes
                    .Where(t => t.Group == groupName)
                    .Select(t => t.Area)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();

                // Get area details for the group
                areaDIList = await db.TargetTimes
                    .Where(t => t.Group == groupName)
                    .Select(t => t.AreaDI)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
            }
            else
            {
                // If no group selected, show all
                areaList = await db.TargetTimes.Select(t => t.Area).Distinct().OrderBy(x => x).ToListAsync();
                areaDIList = await db.TargetTimes.Select(t => t.AreaDI).Distinct().OrderBy(x => x).ToListAsync();
            }

            return Json(new { areaList, areaDIList }, JsonRequestBehavior.AllowGet);
        }

        // Returns area details for a selected area (optionally filtered by group)
        public async Task<ActionResult> GetAreaDetailsByArea(string area, string groupName)
        {
            List<string> areaDIList;

            if (!string.IsNullOrEmpty(area))
            {
                var query = db.TargetTimes.AsQueryable();
                query = query.Where(t => t.Area == area);
                if (!string.IsNullOrEmpty(groupName))
                    query = query.Where(t => t.Group == groupName);

                areaDIList = await query
                    .Select(t => t.AreaDI)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
            }
            else
            {
                areaDIList = await db.TargetTimes.Select(t => t.AreaDI).Distinct().OrderBy(x => x).ToListAsync();
            }

            return Json(new { areaDIList }, JsonRequestBehavior.AllowGet);
        }


        public async Task<ActionResult> GetPassedChart(string searchString,string areaFilter, string grpFilter,string areaDIFilter, DateTime? from, DateTime? to)
        {
            var fromDate = from;
            var toDate = to;

            // Detect if any filter is applied
            bool isFiltered = !string.IsNullOrEmpty(searchString)
                || !string.IsNullOrEmpty(areaFilter)
                || !string.IsNullOrEmpty(grpFilter)
                || !string.IsNullOrEmpty(areaDIFilter)
                || fromDate.HasValue
                || toDate.HasValue;

            ViewBag.IsFiltered = isFiltered;

            // Build cache key based on filter parameters
            string cacheKey = $"GetPassedChart_{searchString}_{areaFilter}_{grpFilter}_{areaDIFilter}_{fromDate}_{toDate}";

            // Try to get cached data
            var cached = HttpRuntime.Cache[cacheKey] as Tuple<
                List<User>,
                string,
                string,
                string,
                List<string>,
                List<string>,
                List<string>
            >;

            if (cached != null)
            {
                ViewBag.AreaGroupChartData = cached.Item2;
                ViewBag.LogWeekChartData = cached.Item3;
                ViewBag.AreaDIChartData = cached.Item4;
                ViewBag.areaList = new SelectList(cached.Item5);
                ViewBag.areaDIList = new SelectList(cached.Item6);
                var Group = db.TargetTimes.Select(t => t.Group).Distinct().OrderBy(x => x).ToList();
                ViewBag.grpList = new SelectList(Group, grpFilter);
                ViewBag.lwList = new SelectList(cached.Item7);
                ViewBag.SearchString = searchString;
                ViewBag.AreaFilter = areaFilter;
                ViewBag.GrpFilter = grpFilter;
                ViewBag.AreaDIFilter = areaDIFilter;
                ViewBag.startDate = from?.ToString("yyyy-MM-dd") ?? "";
                ViewBag.endDate = to?.ToString("yyyy-MM-dd") ?? "";
                return View(cached.Item1.OrderByDescending(u => u.AvgPoints).ToList());
            }

            if (!isFiltered)
            {
                var allGroups = db.TargetTimes.Select(t => t.Group).Distinct().OrderBy(x => x).ToList();
                var allAreas = db.TargetTimes.Select(t => t.Area).Distinct().OrderBy(x => x).ToList();
                var allAreaDIs = db.TargetTimes.Select(t => t.AreaDI).Distinct().OrderBy(x => x).ToList();

                ViewBag.AreaGroupChartData = "[]";
                ViewBag.LogWeekChartData = "[]";
                ViewBag.AreaDIChartData = "[]";
                ViewBag.areaList = new SelectList(allAreas);
                ViewBag.areaDIList = new SelectList(allAreaDIs);
                ViewBag.grpList = new SelectList(allGroups);
                ViewBag.lwList = new SelectList(new List<string>());
                ViewBag.SearchString = searchString;
                ViewBag.AreaFilter = areaFilter;
                ViewBag.GrpFilter = grpFilter;
                ViewBag.AreaDIFilter = areaDIFilter;
                ViewBag.startDate = from?.ToString("yyyy-MM-dd") ?? "";
                ViewBag.endDate = to?.ToString("yyyy-MM-dd") ?? "";
                return View(new List<User>());
            }

            var fabDataQuery = db.BFabMoves.AsQueryable();
            if (fromDate.HasValue && toDate.HasValue)
            {
                fabDataQuery = fabDataQuery.Where(f => f.WorkDay >= fromDate && f.WorkDay <= toDate);
            }
            var fabData = await fabDataQuery.ToListAsync();

            var badgeIds = fabData.Select(f => f.EmpBadgeId).Distinct().ToList();

            var userData = await db.Users
                .Where(u => badgeIds.Contains(u.EmpBadgeId))
                .Select(u => new { u.EmpBadgeId, u.LogWeek })
                .ToListAsync();

            var targetData = await db.TargetTimes
                .Where(t => badgeIds.Contains(t.EmpBadgeId))
                .Select(t => new { t.EmpBadgeId, t.Group, t.Area, t.AreaDI })
                .ToListAsync();

            var gmdsData = await og.gmds
                .Where(g => badgeIds.Contains(g.BadgeId))
                .Select(g => new { g.BadgeId, g.Name, g.Department })
                .ToListAsync();

            // For logweek chart: get all logweeks in the database, sorted ascending
            var allWorkDays = db.BFabMoves
                .Select(f => f.WorkDay)
                .Distinct()
                .ToList();

            var allLogWeeks = allWorkDays
                .Select(date => lw.GetLogWeek(date))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // For logweek chart: get logweeks in the filtered range, sorted ascending
            var workDays = db.BFabMoves
                .Where(f => (!fromDate.HasValue || f.WorkDay >= fromDate) &&
                            (!toDate.HasValue || f.WorkDay <= toDate))
                .Select(f => f.WorkDay)
                .Distinct()
                .ToList();

            var filteredLogWeeks = workDays
                .Select(date => lw.GetLogWeek(date))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // Determine which logweeks to show in the logweek chart
            List<string> logWeeksForChart;
            if (filteredLogWeeks.Count > 0)
            {
                var firstFilteredLogWeek = filteredLogWeeks.First();
                int idx = allLogWeeks.IndexOf(firstFilteredLogWeek);
                int startIdx = Math.Max(0, idx - 3);
                logWeeksForChart = allLogWeeks.Skip(startIdx).Take(idx - startIdx).ToList();
                logWeeksForChart.AddRange(filteredLogWeeks);
                logWeeksForChart = logWeeksForChart.Distinct().OrderBy(x => x).ToList();
            }
            else
            {
                // If no filtered logweeks, fallback to all logweeks
                logWeeksForChart = allLogWeeks;
            }

            // Build summaries as before (filtered data)
            var logWeeks = workDays
                .Select(date => lw.GetLogWeek(date))
                .Distinct()
                .ToList();

            var summaries = (from fab in fabData
                             join user in userData on fab.EmpBadgeId equals user.EmpBadgeId into joinedUser
                             from user in joinedUser.DefaultIfEmpty()
                             join t in targetData on fab.EmpBadgeId equals t.EmpBadgeId into joinedTarget
                             from t in joinedTarget.DefaultIfEmpty()
                             join g in gmdsData on fab.EmpBadgeId equals g.BadgeId into joinedGmds
                             from g in joinedGmds.DefaultIfEmpty()
                             select new
                             {
                                 fab.EmpBadgeId,
                                 fab.WorkDay,
                                 fab.TimeClass,
                                 LogWeek = lw.GetLogWeek(fab.WorkDay),
                                 Group = t?.Group ?? "-",
                                 Area = t?.Area ?? "-",
                                 AreaDI = t?.AreaDI ?? "-",
                                 EmpName = g?.Name ?? "-",
                                 EmpDept = g?.Department ?? "-"
                             })
                .Where(f => logWeeks.Contains(f.LogWeek))
                .GroupBy(f => new { f.EmpBadgeId, f.LogWeek })
                .Select(grouped =>
                {
                    var totalProcess = grouped.Count();
                    var totalPassed = grouped.Count(f => f.TimeClass == "Passed");
                    var avgPointPassed = totalProcess > 0 ? ((double)totalPassed / totalProcess) * 100.0 : 0.0;

                    return new User
                    {
                        EmpBadgeId = grouped.Key.EmpBadgeId,
                        EmpName = grouped.First().EmpName,
                        EmpDept = grouped.First().EmpDept,
                        Group = grouped.First().Group,
                        Area = grouped.First().Area,
                        AreaDI = grouped.First().AreaDI,
                        LogWeek = grouped.Key.LogWeek,
                        Points = totalPassed,
                        AvgPoints = Math.Round(avgPointPassed, 2)
                    };
                })
                .ToList();

            var areaList = new List<string>();
            var areaDIList = new List<string>();

            if (!string.IsNullOrEmpty(grpFilter))
            {
                var mappings = await db.GroupMapping.Where(g => g.GroupName == grpFilter).ToListAsync();
                areaList = mappings.Select(m => m.Area).Distinct().OrderBy(m => m).ToList();
                areaDIList = mappings.Select(m => m.AreaDI).Distinct().OrderBy(m => m).ToList();
            }
            else
            {
                areaList = targetData.Select(t => t.Area).Distinct().OrderBy(x => x).ToList();
                areaDIList = targetData.Select(t => t.AreaDI).Distinct().OrderBy(x => x).ToList();
            }


            if (!string.IsNullOrEmpty(searchString))
            {
                summaries = summaries.Where(u => u.EmpBadgeId.Contains(searchString) || u.EmpName.Contains(searchString)).ToList();
            }
            if (!string.IsNullOrEmpty(areaFilter))
            {
                summaries = summaries.Where(u => u.Area.Contains(areaFilter)).ToList();
            }
            if (!string.IsNullOrEmpty(grpFilter))
            {
                summaries = summaries.Where(u => u.Group.Contains(grpFilter)).ToList();
            }
            if (!string.IsNullOrEmpty(areaDIFilter))
            {
                summaries = summaries.Where(u => u.AreaDI.Contains(areaDIFilter)).ToList();
            }

            List<User> chartSummaries;
            if (!isFiltered)
            {
                var currentLogWeek = logWeeksForChart.LastOrDefault();
                chartSummaries = summaries.Where(s => s.LogWeek == currentLogWeek).ToList();
            }
            else
            {
                chartSummaries = summaries.Where(s => logWeeksForChart.Contains(s.LogWeek)).ToList();
            }

            // Area/Group and AreaDI charts: use filtered summaries as before
            var areaGroupChartData = chartSummaries
                .Where(s => s.Area != "-" && s.Group != "-")
                .GroupBy(s => new { s.Area, s.Group })
                .Select(g => new
                {
                    AreaGroup = $"{g.Key.Area} - {g.Key.Group}",
                    AvgPoints = Math.Round(g.Average(u => u.AvgPoints), 2)
                })
                .OrderBy(x => x.AreaGroup)
                .ToList();

            var areaDIChartData = chartSummaries
                .Where(s => s.AreaDI != "-")
                .GroupBy(s => s.AreaDI)
                .Select(g => new
                {
                    AreaDI = g.Key.ToUpper(),
                    TotalPoints = Math.Round(g.Average(u => u.AvgPoints), 2)
                })
                .OrderBy(x => x.AreaDI)
                .ToList();

            // Logweek chart: use only logWeeksForChart, but aggregate from all summaries (not just filtered)
            // To do this, we need to re-aggregate from all FabMoves for those logweeks
            var allFabData = await db.BFabMoves.ToListAsync();
            var allBadgeIds = allFabData.Select(f => f.EmpBadgeId).Distinct().ToList();

            var allUserData = await db.Users
                .Where(u => allBadgeIds.Contains(u.EmpBadgeId))
                .Select(u => new { u.EmpBadgeId, u.LogWeek })
                .ToListAsync();

            var allTargetData = await db.TargetTimes
                .Where(t => allBadgeIds.Contains(t.EmpBadgeId))
                .Select(t => new { t.EmpBadgeId, t.Group, t.Area, t.AreaDI })
                .ToListAsync();

            var allGmdsData = await og.gmds
                .Where(g => allBadgeIds.Contains(g.BadgeId))
                .Select(g => new { g.BadgeId, g.Name, g.Department })
                .ToListAsync();

            var logWeekChartSummaries = (from fab in allFabData
                                         join user in allUserData on fab.EmpBadgeId equals user.EmpBadgeId into joinedUser
                                         from user in joinedUser.DefaultIfEmpty()
                                         join t in allTargetData on fab.EmpBadgeId equals t.EmpBadgeId into joinedTarget
                                         from t in joinedTarget.DefaultIfEmpty()
                                         join g in allGmdsData on fab.EmpBadgeId equals g.BadgeId into joinedGmds
                                         from g in joinedGmds.DefaultIfEmpty()
                                         select new
                                         {
                                             fab.EmpBadgeId,
                                             fab.WorkDay,
                                             fab.TimeClass,
                                             LogWeek = lw.GetLogWeek(fab.WorkDay),
                                             Group = t?.Group ?? "-",
                                             Area = t?.Area ?? "-",
                                             AreaDI = t?.AreaDI ?? "-",
                                             EmpName = g?.Name ?? "-",
                                             EmpDept = g?.Department ?? "-"
                                         })
                .Where(f => logWeeksForChart.Contains(f.LogWeek))
                .GroupBy(f => f.LogWeek)
                .Select(g => new
                {
                    LogWeek = g.Key,
                    AvgPoints = Math.Round(
                        g.GroupBy(x => x.EmpBadgeId)
                         .Select(grp =>
                         {
                             var totalProcess = grp.Count();
                             var totalPassed = grp.Count(fx => fx.TimeClass == "Passed");
                             return totalProcess > 0 ? ((double)totalPassed / totalProcess) * 100.0 : 0.0;
                         }).DefaultIfEmpty(0).Average(), 2)
                })
                .OrderBy(x => x.LogWeek)
                .ToList();

            ViewBag.AreaGroupChartData = JsonConvert.SerializeObject(areaGroupChartData);
            ViewBag.LogWeekChartData = JsonConvert.SerializeObject(logWeekChartSummaries);
            ViewBag.AreaDIChartData = JsonConvert.SerializeObject(areaDIChartData);

            // Serialize chart data
            string areaGroupChartJson = JsonConvert.SerializeObject(areaGroupChartData);
            string logWeekChartJson = JsonConvert.SerializeObject(logWeekChartSummaries);
            string areaDIChartJson = JsonConvert.SerializeObject(areaDIChartData);

            // Prepare lwList for dropdown
            var lwList = summaries.Select(x => x.LogWeek).Distinct().OrderBy(x => x).ToList();

            // Insert into cache for 30 minutes
            HttpRuntime.Cache.Insert(
                cacheKey,
                Tuple.Create(
                    summaries,
                    areaGroupChartJson,
                    logWeekChartJson,
                    areaDIChartJson,
                    areaList,
                    areaDIList,
                    lwList
               ),
               null,
               DateTime.Now.AddMinutes(30),
               Cache.NoSlidingExpiration
            );

            ViewBag.SearchString = searchString;
            ViewBag.AreaFilter = areaFilter;
            ViewBag.GrpFilter = grpFilter;
            ViewBag.AreaDIFilter = areaDIFilter;
            ViewBag.areaList = new SelectList(areaList, areaFilter);
            ViewBag.areaDIList = new SelectList(areaDIList, areaDIFilter);
            var Groups = db.TargetTimes.Select(t => t.Group).Distinct().OrderBy(x => x).ToList();
            ViewBag.grpList = new SelectList(Groups, grpFilter);
            ViewBag.lwList = new SelectList(summaries.Select(x => x.LogWeek).Distinct().OrderBy(x => x).ToList());
            ViewBag.startDate = fromDate?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.endDate = toDate?.ToString("yyyy-MM-dd") ?? "";

            return View(summaries.OrderByDescending(u => u.AvgPoints).ToList());
        }

        private void ClearPassedChartCache()
        {
            var cache = System.Web.HttpRuntime.Cache;
            var keysToRemove = new List<string>();

            foreach (System.Collections.DictionaryEntry entry in cache)
            {
                if (entry.Key is string key && key.StartsWith("GetPassedChart_"))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                cache.Remove(key);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

}
