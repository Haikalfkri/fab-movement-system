using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using Antlr.Runtime.Misc;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using ok1.Data;
using ok1.Models;
using ok1.Services;
using PagedList;
using Z.EntityFramework.Extensions;
// using PagedList.Mvc;
using static System.Data.Entity.Infrastructure.Design.Executor;
using System.Diagnostics;

namespace ok1.Controllers
{
    public class UsersController : Controller
    {
        private ok1Context db = new ok1Context();
        private masterContext og = new masterContext();
        private TCFunc funcs;
        private TimeClass tc;
        private logweek lw;
        private GroupMappingOption grp;

        public UsersController()
        {
            funcs = new TCFunc();
            tc = new TimeClass();
            lw = new logweek();
            grp = new GroupMappingOption();
        }
        public string CalculateTimeClass(string badgeId, string day, TimeSpan? crTime, string fridayPray, TimeSpan? busui, string ahm, string exitPermission, string fabAmend, string targetTimes, List<FabMove> moves)
        {
            var filteredMoves = moves.Where(c => c.EmpBadgeId.Equals(badgeId)).ToList();
            if (!filteredMoves.Any())
            {
                return "No data";
            }
            foreach (var empTimeclass in filteredMoves)
            {
                var workplace = og.gmds.FirstOrDefault(x => x.BadgeId == badgeId);
                var office = workplace?.Production?.Trim().ToLowerInvariant();
                var shift = db.BFabMoves
                    .Where(x => x.EmpBadgeId == badgeId && x.WorkDay == empTimeclass.WorkDay)
                    .Select(x => x.Shift)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(office) && office.Contains("production(nh)"))
                {
                    return "Passed";
                }

                if ((day.Equals("saturday", StringComparison.OrdinalIgnoreCase) || day.Equals("sunday", StringComparison.OrdinalIgnoreCase)) &&
                    (shift.ToLower().Trim() == "normal shift" || shift == "n"))
                {
                    return "Failed";
                }

                TimeSpan SafeParse(string input)
                {
                    return string.IsNullOrEmpty(input) || !TimeSpan.TryParse(input, out var result)
                        ? TimeSpan.Zero
                        : result;
                }

                // Parse all string inputs
                TimeSpan pray = SafeParse(fridayPray);
                TimeSpan qm = SafeParse(ahm);
                TimeSpan amend = SafeParse(fabAmend);
                TimeSpan permit = SafeParse(exitPermission);

                // Handle nullable TimeSpans
                TimeSpan crTimeValue = crTime ?? TimeSpan.Zero;
                TimeSpan busuiValue = busui ?? TimeSpan.Zero;

                // Calculate total time
                var totalTime = (crTime ?? TimeSpan.Zero) + pray + (busui ?? TimeSpan.Zero) + qm + amend + permit;

                if (string.IsNullOrEmpty(targetTimes))
                {
                    return "Failed";
                }

                TimeSpan target = SafeParse(targetTimes);
                if (target == TimeSpan.Zero)
                {
                    return "Failed";
                }

                return totalTime >= target ? "Passed" : "Failed";
            }
            return "Failed";
        }

        public void UpdateTimeClass(string badgeId, List<FabMove> moves)
        {
            // Filter semua data dengan badgeId
            var userMoves = moves.Where(f => f.EmpBadgeId == badgeId).ToList();

            foreach (var data in userMoves)
            {
                // Ambil nilai spesifik dari setiap entri
                string day = data.Day;
                TimeSpan? crTime = data.CrTime;
                string fridayPray = data.FridayPray;
                TimeSpan? busui = data.Busui;
                string ahm = data.Ahm;
                string exitPermission = data.ExitPermit;
                string fabAmend = data.Amend;

                // Ambil TargetTime record
                var targetTimeRecord = db.TargetTimes.FirstOrDefault(t => t.EmpBadgeId == badgeId);
                if (targetTimeRecord != null)
                {
                    TimeSpan targetTime = TimeSpan.Zero;

                    // Pilih TargetTime berdasarkan shift
                    if (!string.IsNullOrEmpty(data.Shift))
                    {
                        switch (data.Shift.ToLower().Trim())
                        {
                            case "normal shift":
                                targetTime = targetTimeRecord.NormalShift;
                                break;
                            case "morning shift":
                                targetTime = targetTimeRecord.MorningShift;
                                break;
                            case "afternoon shift":
                                targetTime = targetTimeRecord.AfternoonShift;
                                break;
                            case "night shift":
                                targetTime = targetTimeRecord.NightShift;
                                break;
                            default:
                                targetTime = TimeSpan.Zero;
                                break;
                        }
                    }

                    // Hitung TimeClass untuk data spesifik ini
                    string timeClass = CalculateTimeClass(
                         data.EmpBadgeId,
                         data.Day,
                         data.CrTime,
                         data.FridayPray,
                         data.Busui,
                         data.Ahm,
                         data.ExitPermit,
                         data.Amend,
                         targetTime.ToString(@"hh\:mm\:ss"),
                         moves
                     );

                    // Update TimeClass
                    data.TimeClass = timeClass;
                }
                else
                {
                    // Jika TargetTime tidak ditemukan
                    data.TimeClass = "Failed";
                }
            }

            // Simpan perubahan setelah semua entri diproses
            db.SaveChanges();
        }

        private string CalculateWeekCount(List<FabMove> moves)
        {
            if (moves == null || !moves.Any())
            {
                return "0";
            }

            var logw = moves
                .Select(m =>
                {
                    var logWeek = lw.GetLogWeek(m.WorkDay);
                    return logWeek;
                })
                .Distinct()
                .ToList();
            string LogWeek = string.Join(",", logw);
            return LogWeek;
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

        // GET: Users
        public async Task<ActionResult> Index(string sortOrder, string searchString, string areaFilter, string grpFilter,
            string areaDIFilter, string shiftFilter, string statusFilter, DateTime? from, DateTime? to, int? page)
        {
            UpdateBreastfeedingStatuses();
            // ================================================================
            // ACCESS CONTROL
            // ================================================================
            var userAccess = new UserAccess(db);
            string windowsAccount = userAccess.GetUserAccount(User.Identity.Name);
            bool isAdmin = userAccess.IsAdmin(windowsAccount);
            bool isManager = userAccess.IsManager(windowsAccount);
            bool isPrivilegedUser = isAdmin || isManager;

            DateTime? fromDate = from?.Date;
            DateTime? toDate = to?.Date.AddDays(1).AddTicks(-1);

            bool isFiltered = !string.IsNullOrEmpty(searchString)
                || !string.IsNullOrEmpty(areaFilter)
                || !string.IsNullOrEmpty(grpFilter)
                || !string.IsNullOrEmpty(areaDIFilter)
                || !string.IsNullOrEmpty(shiftFilter)
                || fromDate.HasValue
                || toDate.HasValue;

            ViewBag.IsFiltered = isFiltered;

            // ================================================================
            // LOAD DROPDOWN DATA - Run in parallel, only ONCE
            // ================================================================
            var shiftsTask = og.gmds
                .Where(g => g.Production != null
                       && g.Production.Trim().ToLower() == "production"
                       && !string.IsNullOrEmpty(g.Group))
                .Select(g => g.Group)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var groupsTask = db.TargetTimes
                .Select(t => t.Group)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            await Task.WhenAll(shiftsTask, groupsTask);
            var allShifts = shiftsTask.Result;
            var allGroups = groupsTask.Result;

            // ================================================================
            // ROLE-BASED VALIDATION
            // ================================================================

            // PUBLIC USER: MUST have BOTH badge ID AND date range
            if (!isPrivilegedUser)
            {
                bool hasSearchString = !string.IsNullOrEmpty(searchString);
                bool hasDateRange = fromDate.HasValue && toDate.HasValue;

                if (!hasSearchString || !hasDateRange)
                {
                    await SetDropdownsAsync(db, null, null, allGroups, allShifts,
                        areaFilter, areaDIFilter, grpFilter, shiftFilter, fromDate, toDate);

                    if (isFiltered)
                    {
                        if (!hasSearchString)
                            ViewBag.ErrorMessage = "Please scan your Badge ID to view data.";
                        else if (!hasDateRange)
                            ViewBag.ErrorMessage = "Please input date range (From & To) to view data.";

                        ViewBag.IsFiltered = true;
                    }
                    else
                    {
                        ViewBag.IsFiltered = false;
                    }

                    // Pass role to view before early return
                    ViewBag.IsAdmin = isAdmin;
                    ViewBag.IsManager = isManager;
                    ViewBag.IsPrivilegedUser = isPrivilegedUser;

                    return View(new StaticPagedList<User>(new List<User>(), 1, 30, 0));
                }
            }

            // ADMIN / MANAGER: Must have at least ONE filter (any filter is fine)
            if (isPrivilegedUser && !isFiltered)
            {
                await SetDropdownsAsync(db, null, null, allGroups, allShifts,
                    areaFilter, areaDIFilter, grpFilter, shiftFilter, fromDate, toDate);

                // Pass role to view before early return
                ViewBag.IsAdmin = isAdmin;
                ViewBag.IsManager = isManager;
                ViewBag.IsPrivilegedUser = isPrivilegedUser;

                return View(new StaticPagedList<User>(new List<User>(), 1, 30, 0));
            }

            // ================================================================
            // BUILD DROPDOWN LISTS - Based on current filters
            // ================================================================
            await SetDropdownsAsync(db, grpFilter, areaFilter, allGroups, allShifts,
                areaFilter, areaDIFilter, grpFilter, shiftFilter, fromDate, toDate);

            // ================================================================
            // GET MANAGER BADGE IDs (only if manager, single query)
            // ================================================================
            List<string> managerBadgeIds = null;

            if (isManager && !isAdmin)
            {
                var managerRecords = await db.ManagerList
                    .AsNoTracking()
                    .Where(m => !string.IsNullOrEmpty(m.Manager))
                    .Select(m => new { m.Manager, m.BadgeId })
                    .ToListAsync();

                managerBadgeIds = managerRecords
                    .Where(m => System.Text.RegularExpressions.Regex
                        .Split(m.Manager, @"\$+")
                        .Select(x => x.Trim().ToLower())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Contains(windowsAccount))
                    .Select(m => m.BadgeId)
                    .Distinct()
                    .ToList();

                // Always include manager's own badge ID
                var ownBadgeId = await og.gmds
                    .Where(g => g.WindowsAccount.Trim().ToLower() == windowsAccount)
                    .Select(g => g.BadgeId)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(ownBadgeId) && !managerBadgeIds.Contains(ownBadgeId))
                    managerBadgeIds.Add(ownBadgeId);

                // Manager searches specific badge ID → check access
                if (!string.IsNullOrEmpty(searchString))
                {
                    if (!managerBadgeIds.Contains(searchString.Trim()))
                    {
                        ViewBag.ErrorMessage = "You don't have access to this badge ID.";
                        ViewBag.IsAdmin = isAdmin;
                        ViewBag.IsManager = isManager;
                        ViewBag.IsPrivilegedUser = isPrivilegedUser;
                        return View(new StaticPagedList<User>(new List<User>(), 1, 30, 0));
                    }
                }

                // Manager has no team and no own badge
                if (!managerBadgeIds.Any())
                {
                    ViewBag.ErrorMessage = "No team members found under your account.";
                    ViewBag.IsAdmin = isAdmin;
                    ViewBag.IsManager = isManager;
                    ViewBag.IsPrivilegedUser = isPrivilegedUser;
                    return View(new StaticPagedList<User>(new List<User>(), 1, 30, 0));
                }
            }

            // ================================================================
            // BUILD FINAL BADGE ID LIST
            // ================================================================
            List<string> filteredBadgeIds = null;

            bool hasTargetFilter = !string.IsNullOrEmpty(grpFilter)
                || !string.IsNullOrEmpty(areaFilter)
                || !string.IsNullOrEmpty(areaDIFilter)
                || !string.IsNullOrEmpty(shiftFilter);

            if (hasTargetFilter || (isManager && !isAdmin))
            {
                if (hasTargetFilter)
                {
                    var targetQuery = db.TargetTimes.AsNoTracking().AsQueryable();

                    if (!string.IsNullOrEmpty(grpFilter))
                        targetQuery = targetQuery.Where(t => t.Group == grpFilter);

                    if (!string.IsNullOrEmpty(areaFilter))
                        targetQuery = targetQuery.Where(t => t.Area == areaFilter);

                    if (!string.IsNullOrEmpty(areaDIFilter))
                        targetQuery = targetQuery.Where(t => t.AreaDI == areaDIFilter);

                    if (!string.IsNullOrEmpty(shiftFilter))
                    {
                        var shiftBadgeIds = await og.gmds
                            .Where(g => g.Production != null
                                   && g.Production.Trim().ToLower() == "production"
                                   && g.Group == shiftFilter)
                            .Select(g => g.BadgeId)
                            .Distinct()
                            .ToListAsync();

                        targetQuery = targetQuery.Where(t => shiftBadgeIds.Contains(t.EmpBadgeId));
                    }

                    filteredBadgeIds = await targetQuery
                        .Select(t => t.EmpBadgeId)
                        .Distinct()
                        .ToListAsync();
                }

                // Intersect with manager badge IDs
                if (isManager && !isAdmin)
                {
                    filteredBadgeIds = filteredBadgeIds != null
                        ? filteredBadgeIds.Intersect(managerBadgeIds).ToList()
                        : managerBadgeIds;
                }

                // No matching badge IDs after all filters
                if (filteredBadgeIds != null && !filteredBadgeIds.Any())
                {
                    ViewBag.IsAdmin = isAdmin;
                    ViewBag.IsManager = isManager;
                    ViewBag.IsPrivilegedUser = isPrivilegedUser;
                    return View(new StaticPagedList<User>(new List<User>(), 1, 30, 0));
                }
            }

            // ================================================================
            // QUERY FAB DATA - Apply all filters at DB level
            // ================================================================
            var fabDataQuery = db.BFabMoves.AsNoTracking().AsQueryable();

            if (fromDate.HasValue)
                fabDataQuery = fabDataQuery.Where(f => f.WorkDay >= fromDate.Value);

            if (toDate.HasValue)
                fabDataQuery = fabDataQuery.Where(f => f.WorkDay <= toDate.Value);

            if (filteredBadgeIds != null)
                fabDataQuery = fabDataQuery.Where(f => filteredBadgeIds.Contains(f.EmpBadgeId));

            if (!string.IsNullOrEmpty(searchString))
                fabDataQuery = fabDataQuery.Where(f => f.EmpBadgeId == searchString.Trim());

            var fabData = await fabDataQuery
                .Select(f => new { f.EmpBadgeId, f.WorkDay, f.TimeClass })
                .ToListAsync();

            // ================================================================
            // GROUP AND SUMMARIZE IN MEMORY
            // ================================================================
            var grouped = fabData
                .GroupBy(fab => new { fab.EmpBadgeId, LogWeek = lw.GetLogWeek(fab.WorkDay) })
                .Select(g => new
                {
                    g.Key.EmpBadgeId,
                    g.Key.LogWeek,
                    Points = g.Count(f => f.TimeClass == "Passed"),
                    Total = g.Count()
                })
                .ToList();

            // Status filter
            if (statusFilter == "failed")
            {
                grouped = grouped
                    .Where(u => u.Total > 0
                           && Math.Round((u.Points / (double)u.Total) * 100, 2) < 100)
                    .ToList();
            }

            // Sorting
            grouped = sortOrder == "name_desc"
                ? grouped.OrderBy(u => u.LogWeek).ThenByDescending(u => u.EmpBadgeId).ToList()
                : grouped.OrderBy(u => u.LogWeek).ThenBy(u => u.EmpBadgeId).ToList();

            // ================================================================
            // PAGING
            // ================================================================
            int pageSize = 30;
            int pageNumber = page ?? 1;
            int totalCount = grouped.Count;

            var pageData = grouped
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ================================================================
            // BATCH LOAD SUPPORTING DATA - Current page only, run in parallel
            // ================================================================
            var pageBadgeIds = pageData.Select(x => x.EmpBadgeId).Distinct().ToList();

            var gmdsTask2 = og.gmds
                .Where(g => pageBadgeIds.Contains(g.BadgeId))
                .Select(g => new { g.BadgeId, g.Name, g.Department })
                .ToListAsync();

            var targetDataTask = db.TargetTimes
                .AsNoTracking()
                .Where(t => pageBadgeIds.Contains(t.EmpBadgeId))
                .Select(t => new { t.EmpBadgeId, t.Group, t.Area, t.AreaDI })
                .ToListAsync();

            await Task.WhenAll(gmdsTask2, targetDataTask);

            var gmdsDict = gmdsTask2.Result
                .GroupBy(x => x.BadgeId)
                .ToDictionary(g => g.Key, g => g.First());

            var targetDict = targetDataTask.Result
                .GroupBy(x => x.EmpBadgeId)
                .ToDictionary(g => g.Key, g => g.First());

            // ================================================================
            // PROJECT TO USER MODEL
            // ================================================================
            var summaries = pageData.Select(x =>
            {
                gmdsDict.TryGetValue(x.EmpBadgeId, out var g);
                targetDict.TryGetValue(x.EmpBadgeId, out var t);
                return new User
                {
                    EmpBadgeId = x.EmpBadgeId,
                    EmpName = g?.Name ?? "-",
                    EmpDept = g?.Department ?? "-",
                    Group = t?.Group ?? "-",
                    Area = t?.Area ?? "-",
                    AreaDI = t?.AreaDI ?? "-",
                    LogWeek = x.LogWeek,
                    Points = x.Points,
                    AvgPoints = x.Total > 0
                        ? Math.Round((x.Points / (double)x.Total) * 100, 2)
                        : 0.0
                };
            }).ToList();

            // ================================================================
            // SET VIEWBAG
            // ================================================================
            ViewBag.SortOrder = sortOrder;
            ViewBag.SearchString = searchString;
            ViewBag.AreaFilter = areaFilter;
            ViewBag.GrpFilter = grpFilter;
            ViewBag.AreaDIFilter = areaDIFilter;
            ViewBag.shiftFilter = shiftFilter;
            ViewBag.statusFilter = statusFilter;
            ViewBag.startDate = fromDate?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.endDate = toDate?.ToString("yyyy-MM-dd") ?? "";

            // Role flags for view
            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsManager = isManager;
            ViewBag.IsPrivilegedUser = isPrivilegedUser;

            return View(new StaticPagedList<User>(summaries, pageNumber, pageSize, totalCount));
        }

        // ================================================================
        // HELPER - Set dropdowns (avoid duplicate queries)
        // ================================================================
        private async Task SetDropdownsAsync(
            DbContext db,
            string grpFilter,
            string areaFilter,
            List<string> allGroups,
            List<string> allShifts,
            string selectedArea,
            string selectedAreaDI,
            string selectedGrp,
            string selectedShift,
            DateTime? fromDate,
            DateTime? toDate)
        {
            List<string> areaList;
            List<string> areaDIList;

            if (!string.IsNullOrEmpty(grpFilter))
            {
                // Single query filtered by group, split in memory
                var areaData = await db.Set<TargetTime>()
                    .Where(t => t.Group == grpFilter)
                    .Select(t => new { t.Area, t.AreaDI })
                    .Distinct()
                    .ToListAsync();

                areaList = areaData
                    .Select(x => x.Area)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                areaDIList = !string.IsNullOrEmpty(areaFilter)
                    ? areaData.Where(x => x.Area == areaFilter).Select(x => x.AreaDI).Distinct().OrderBy(x => x).ToList()
                    : areaData.Select(x => x.AreaDI).Distinct().OrderBy(x => x).ToList();
            }
            else
            {
                // Single query for all, split in memory
                var areaData = await db.Set<TargetTime>()
                    .Select(t => new { t.Area, t.AreaDI })
                    .Distinct()
                    .ToListAsync();

                areaList = areaData
                    .Select(x => x.Area)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                areaDIList = !string.IsNullOrEmpty(areaFilter)
                    ? areaData.Where(x => x.Area == areaFilter).Select(x => x.AreaDI).Distinct().OrderBy(x => x).ToList()
                    : areaData.Select(x => x.AreaDI).Distinct().OrderBy(x => x).ToList();
            }

            ViewBag.areaList = new SelectList(areaList, selectedArea);
            ViewBag.areaDIList = new SelectList(areaDIList, selectedAreaDI);
            ViewBag.grpList = new SelectList(allGroups, selectedGrp);
            ViewBag.shiftList = new SelectList(allShifts, selectedShift);
            ViewBag.startDate = fromDate?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.endDate = toDate?.ToString("yyyy-MM-dd") ?? "";
        }

        // ================================================================
        // EXPORT ACTION
        // ================================================================
        [HttpGet]
        public ActionResult Export(
            HttpPostedFileBase file,
            string searchString,
            string areaFilter,
            string grpFilter,
            string areaDIFilter,
            string shiftFilter,
            string statusFilter,
            DateTime? from,
            DateTime? to)
        {
            // ================================================================
            // ACCESS CONTROL - Same as Index
            // ================================================================
            var userAccess = new UserAccess(db);
            string windowsAccount = userAccess.GetUserAccount(User.Identity.Name);
            bool isAdmin = userAccess.IsAdmin(windowsAccount);
            bool isManager = userAccess.IsManager(windowsAccount);

            DateTime? fromDate = from?.Date;
            DateTime? toDate = to?.Date.AddDays(1).AddTicks(-1);

            // ================================================================
            // PUBLIC USER: Must have both badge ID and date range
            // ================================================================
            if (!isAdmin && !isManager)
            {
                bool hasSearchString = !string.IsNullOrEmpty(searchString);
                bool hasDateRange = fromDate.HasValue && toDate.HasValue;

                if (!hasSearchString || !hasDateRange)
                {
                    return ReturnEmptyExcel("You must provide Badge ID and date range (From & To) to export data.");
                }
            }

            // ================================================================
            // GET WORKDAYS AND LOGWEEKS
            // ================================================================
            var workDays = db.BFabMoves
                .Where(f => (!fromDate.HasValue || f.WorkDay >= fromDate) &&
                            (!toDate.HasValue || f.WorkDay <= toDate))
                .Select(f => f.WorkDay)
                .Distinct()
                .ToList();

            var logWeeks = workDays
                .Select(date => lw.GetLogWeek(date))
                .Distinct()
                .ToList();

            // ================================================================
            // BUILD USER QUERY
            // ================================================================
            var userQuery = db.Users.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                userQuery = userQuery.Where(u => u.EmpBadgeId == searchString.Trim());
            if (!string.IsNullOrEmpty(areaFilter))
                userQuery = userQuery.Where(u => u.Area.Contains(areaFilter));
            if (!string.IsNullOrEmpty(grpFilter))
                userQuery = userQuery.Where(u => u.Group.Contains(grpFilter));
            if (!string.IsNullOrEmpty(areaDIFilter))
                userQuery = userQuery.Where(u => u.AreaDI.Contains(areaDIFilter));
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter.ToLower() == "failed")
                userQuery = userQuery.Where(u => u.AvgPoints < 100);
            if (logWeeks.Any())
                userQuery = userQuery.Where(u => logWeeks.Contains(u.LogWeek));

            // Shift filter
            if (!string.IsNullOrEmpty(shiftFilter))
            {
                var shiftBadgeIds = og.gmds
                    .Where(g => g.Production != null
                           && g.Production.Trim().ToLower() == "production"
                           && g.Group == shiftFilter)
                    .Select(g => g.BadgeId)
                    .Distinct()
                    .ToList();

                if (shiftBadgeIds.Any())
                    userQuery = userQuery.Where(u => shiftBadgeIds.Contains(u.EmpBadgeId));
                else
                    return ReturnEmptyExcel("No data for selected shift filter.");
            }

            // ================================================================
            // MANAGER RESTRICTION - Same logic as Index
            // ================================================================
            if (isManager && !isAdmin)
            {
                var managerList = db.ManagerList
                    .AsNoTracking()
                    .Where(m => !string.IsNullOrEmpty(m.Manager))
                    .Select(m => new { m.Manager, m.BadgeId })
                    .ToList();

                var managerBadgeIds = managerList
                    .Where(m => System.Text.RegularExpressions.Regex
                        .Split(m.Manager, @"\$+")
                        .Select(x => x.Trim().ToLower())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Contains(windowsAccount))
                    .Select(m => m.BadgeId)
                    .Distinct()
                    .ToList();

                // FIX: Get manager's own badge ID
                // so manager can always export their own data
                var ownBadgeId = og.gmds
                    .Where(g => g.WindowsAccount.Trim().ToLower() == windowsAccount)
                    .Select(g => g.BadgeId)
                    .FirstOrDefault();

                // Add own badge ID if not already in list
                if (!string.IsNullOrEmpty(ownBadgeId) && !managerBadgeIds.Contains(ownBadgeId))
                {
                    managerBadgeIds.Add(ownBadgeId);
                }

                // Manager searches specific badge ID → check access
                if (!string.IsNullOrEmpty(searchString))
                {
                    if (!managerBadgeIds.Contains(searchString.Trim()))
                    {
                        return ReturnEmptyExcel("You don't have access to this badge ID.");
                    }
                }

                // Manager has no team members and no own badge
                if (!managerBadgeIds.Any())
                {
                    return ReturnEmptyExcel("No team members found under your account.");
                }

                // Restrict to manager's team members + own badge only
                userQuery = userQuery.Where(u => managerBadgeIds.Contains(u.EmpBadgeId));
            }

            // ================================================================
            // EXECUTE QUERY
            // ================================================================
            var detailPersonal = userQuery.Select(x => new
            {
                x.EmpBadgeId,
                x.EmpName,
                x.EmpDept,
                x.Group,
                x.Area,
                x.AreaDI,
                x.LogWeek,
                x.AvgPoints
            }).ToList();

            // ================================================================
            // GENERATE EXCEL
            // ================================================================
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("User");

                // Header
                worksheet.Cells["A1"].Value = "Badge ID";
                worksheet.Cells["B1"].Value = "Employee Name";
                worksheet.Cells["C1"].Value = "Department";
                worksheet.Cells["D1"].Value = "Group";
                worksheet.Cells["E1"].Value = "Area";
                worksheet.Cells["F1"].Value = "Area Detail";
                worksheet.Cells["G1"].Value = "Logweek";
                worksheet.Cells["H1"].Value = "Points";

                // Data rows
                int row = 2;
                foreach (var details in detailPersonal)
                {
                    worksheet.Cells[row, 1].Value = details.EmpBadgeId;
                    worksheet.Cells[row, 2].Value = details.EmpName;
                    worksheet.Cells[row, 3].Value = details.EmpDept;
                    worksheet.Cells[row, 4].Value = details.Group;
                    worksheet.Cells[row, 5].Value = details.Area;
                    worksheet.Cells[row, 6].Value = details.AreaDI;
                    worksheet.Cells[row, 7].Value = details.LogWeek;
                    worksheet.Cells[row, 8].Value = details.AvgPoints;
                    row++;
                }

                for (int col = 1; col <= 8; col++)
                {
                    worksheet.Column(col).Width = 20;
                }

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Point Reports.xlsx");
            }
        }

        // ================================================================
        // HELPER: Return Excel file with message (reusable)
        // ================================================================
        private ActionResult ReturnEmptyExcel(string message)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("User");
                worksheet.Cells["A1"].Value = message;
                worksheet.Column(1).Width = 60;

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Point Reports.xlsx");
            }
        }

        // FAB Report Details
        public async Task<ActionResult> Details(
            string searchString,
            string areaFilter,
            string grpFilter,
            string areaDIFilter,
            string shiftFilter,
            DateTime? from,
            DateTime? to,
            string statusFilter,
            string[] repeatedBadgeIds = null,
            int? page = 1)
        {
            // ================================================================
            // ACCESS CONTROL - Same as Index
            // ================================================================
            var userAccess = new UserAccess(db);
            var windowsAccount = userAccess.GetUserAccount(User.Identity.Name);
            bool isAdmin = userAccess.IsAdmin(windowsAccount);
            bool isManager = userAccess.IsManager(windowsAccount);

            DateTime? fromDate = from?.Date;
            DateTime? toDate = to?.Date.AddDays(1).AddTicks(-1);

            bool isFiltered = !string.IsNullOrEmpty(searchString)
                || !string.IsNullOrEmpty(grpFilter)
                || !string.IsNullOrEmpty(areaFilter)
                || !string.IsNullOrEmpty(areaDIFilter)
                || !string.IsNullOrEmpty(shiftFilter)
                || fromDate.HasValue
                || toDate.HasValue
                || !string.IsNullOrEmpty(statusFilter)
                || (repeatedBadgeIds != null && repeatedBadgeIds.Any());

            ViewBag.IsFiltered = isFiltered;

            // ================================================================
            // ROLE-BASED VALIDATION - Early return if invalid
            // ================================================================

            // Public user: MUST have both badge ID and date range
            if (!isAdmin && !isManager)
            {
                bool hasSearchString = !string.IsNullOrEmpty(searchString);
                bool hasDateRange = fromDate.HasValue && toDate.HasValue;
                bool hasRepeatedBadgeIds = repeatedBadgeIds != null && repeatedBadgeIds.Any();

                if (!hasRepeatedBadgeIds && (!hasSearchString || !hasDateRange))
                {
                    if (isFiltered)
                    {
                        ViewBag.ErrorMessage = !hasSearchString
                            ? "Please input Badge ID to view data."
                            : "Please input date range (From & To) to view data.";
                        ViewBag.IsFiltered = true;
                    }
                    else
                    {
                        ViewBag.IsFiltered = false;
                    }

                    SetDetailsViewBag(searchString, areaFilter, grpFilter, areaDIFilter, shiftFilter, fromDate, toDate, statusFilter, repeatedBadgeIds);
                    return View(new StaticPagedList<FabMove>(new List<FabMove>(), 1, 30, 0));
                }
            }

            // Manager / Admin: Must have at least one filter
            if ((isAdmin || isManager) && !isFiltered)
            {
                SetDetailsViewBag(searchString, areaFilter, grpFilter, areaDIFilter, shiftFilter, fromDate, toDate, statusFilter, repeatedBadgeIds);
                return View(new StaticPagedList<FabMove>(new List<FabMove>(), page ?? 1, 30, 0));
            }

            // ================================================================
            // GET MANAGER BADGE IDs (only if manager, single query)
            // ================================================================
            List<string> managerBadgeIds = null;

            if (isManager && !isAdmin)
            {
                // OPTIMIZATION: Select only needed columns
                var managerRecords = await db.ManagerList
                    .AsNoTracking()
                    .Where(m => !string.IsNullOrEmpty(m.Manager))
                    .Select(m => new { m.Manager, m.BadgeId })
                    .ToListAsync();

                managerBadgeIds = managerRecords
                    .Where(m => System.Text.RegularExpressions.Regex
                        .Split(m.Manager, @"\$+")
                        .Select(x => x.Trim().ToLower())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Contains(windowsAccount))
                    .Select(m => m.BadgeId)
                    .Distinct()
                    .ToList();

                // FIX: Get manager's own badge ID
                // so manager can always access their own data
                // even if their badge is assigned to another manager
                var ownBadgeId = await og.gmds
                    .Where(g => g.WindowsAccount.Trim().ToLower() == windowsAccount)
                    .Select(g => g.BadgeId)
                    .FirstOrDefaultAsync();

                // Add own badge ID if not already in list
                if (!string.IsNullOrEmpty(ownBadgeId) && !managerBadgeIds.Contains(ownBadgeId))
                {
                    managerBadgeIds.Add(ownBadgeId);
                }

                // Manager searches specific badge ID → check access
                if (!string.IsNullOrEmpty(searchString))
                {
                    if (!managerBadgeIds.Contains(searchString.Trim()))
                    {
                        ViewBag.ErrorMessage = "You don't have access to this badge id";
                        SetDetailsViewBag(searchString, areaFilter, grpFilter, areaDIFilter, shiftFilter, fromDate, toDate, statusFilter, repeatedBadgeIds);
                        return View(new StaticPagedList<FabMove>(new List<FabMove>(), page ?? 1, 30, 0));
                    }
                }

                // Manager has no team and no own badge
                if (!managerBadgeIds.Any())
                {
                    ViewBag.ErrorMessage = "No team members found under your account.";
                    SetDetailsViewBag(searchString, areaFilter, grpFilter, areaDIFilter, shiftFilter, fromDate, toDate, statusFilter, repeatedBadgeIds);
                    return View(new StaticPagedList<FabMove>(new List<FabMove>(), page ?? 1, 30, 0));
                }
            }

            // ================================================================
            // BUILD BADGE ID FILTER - Same as original
            // ================================================================
            List<string> filteredBadgeIds = null;

            if (!string.IsNullOrEmpty(grpFilter)
                || !string.IsNullOrEmpty(areaFilter)
                || !string.IsNullOrEmpty(areaDIFilter))
            {
                var targetQuery = db.TargetTimes.AsQueryable();

                if (!string.IsNullOrEmpty(grpFilter))
                    targetQuery = targetQuery.Where(t => t.Group.Contains(grpFilter));
                if (!string.IsNullOrEmpty(areaFilter))
                    targetQuery = targetQuery.Where(t => t.Area.Contains(areaFilter));
                if (!string.IsNullOrEmpty(areaDIFilter))
                    targetQuery = targetQuery.Where(t => t.AreaDI.Contains(areaDIFilter));

                filteredBadgeIds = await targetQuery
                    .Select(t => t.EmpBadgeId)
                    .Distinct()
                    .ToListAsync();
            }

            List<string> shiftBadgeIds = null;

            if (!string.IsNullOrEmpty(shiftFilter))
            {
                shiftBadgeIds = await og.gmds
                    .Where(g => g.Production != null
                           && g.Production.Trim().ToLower() == "production"
                           && g.Group == shiftFilter)
                    .Select(g => g.BadgeId)
                    .Distinct()
                    .ToListAsync();
            }

            // Combine filteredBadgeIds and shiftBadgeIds - Same as original
            List<string> finalBadgeIds = null;

            if (filteredBadgeIds != null && shiftBadgeIds != null)
                finalBadgeIds = filteredBadgeIds.Intersect(shiftBadgeIds).ToList();
            else if (filteredBadgeIds != null)
                finalBadgeIds = filteredBadgeIds;
            else if (shiftBadgeIds != null)
                finalBadgeIds = shiftBadgeIds;

            // Intersect with repeatedbadgeids
            if (repeatedBadgeIds != null && repeatedBadgeIds.Any())
            {
                var repeatedList = repeatedBadgeIds
                    .Where(x => !string.IsNullOrEmpty(x)).ToList();
                finalBadgeIds = finalBadgeIds != null
                    ? finalBadgeIds.Intersect(repeatedList).ToList()
                    : repeatedList;
            }

            // Intersect with manager badge IDs
            if (isManager && !isAdmin)
            {
                finalBadgeIds = finalBadgeIds != null
                    ? finalBadgeIds.Intersect(managerBadgeIds).ToList()
                    : managerBadgeIds;
            }

            // ================================================================
            // PAGING - Same as original
            // ================================================================
            int pageSize = 30;
            int pageNumber = page ?? 1;
            int offset = (pageNumber - 1) * pageSize;

            // If badge filter is present but empty, return empty result
            if (finalBadgeIds != null && !finalBadgeIds.Any())
            {
                SetDetailsViewBag(searchString, areaFilter, grpFilter, areaDIFilter, shiftFilter, fromDate, toDate, statusFilter, repeatedBadgeIds);
                return View(new StaticPagedList<FabMove>(new List<FabMove>(), pageNumber, pageSize, 0));
            }

            // ================================================================
            // BUILD SQL BADGE ID FILTER - Same as original
            // ================================================================
            string badgeIdFilterSql = "";

            if (finalBadgeIds != null)
            {
                var badgeParamNames = finalBadgeIds.Select((id, i) => $"@bid{i}").ToList();
                badgeIdFilterSql = $"AND [EmpBadgeId] IN ({string.Join(",", badgeParamNames)})";
            }

            // ================================================================
            // BUILD SQL QUERIES - Same as original
            // ================================================================
            string sqlQuery = $@"
                SELECT [Id],[EmpBadgeId],[EmpName],[EmpDept],[Designation],[JobLevel],[ShiftCode],[Section],[OrgUnit],[CostCenter],[Level1],
                       [WorkDay],[Day],[Shift],[FabIn],[FabOut],[FirstCrIn],[LastCrOut],[FabTime],[CrTime],[BrTime],[FabCount],[CrCount],
                       [FabLateIn],[FabEarlyOut],[PublicHoliday],[IsValid],[Workplace],[Gender],[Religion],[FridayPray],[Busui],[Ahm],[Amend],
                       [ExitPermit],[TargetTime],[TimeClass],[Group],[Area],[AreaDI],[UploadFileRecordId],[Missing]
                FROM [test_db].[FAB_Report].[FabMoves]
                WHERE
                    (@fromDate IS NULL OR [WorkDay] >= @fromDate) AND
                    (@toDate IS NULL OR [WorkDay] < @toDate) AND
                    (@searchString IS NULL OR [EmpBadgeId] LIKE '%' + @searchString + '%') AND
                    (@statusFilter IS NULL OR @statusFilter = 'all' OR LOWER([TimeClass]) = @statusFilter)
                    {badgeIdFilterSql}
                ORDER BY [Id] ASC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

            string countQuery = $@"
                SELECT COUNT(*)
                FROM [test_db].[FAB_Report].[FabMoves]
                WHERE
                    (@fromDate IS NULL OR [WorkDay] >= @fromDate) AND
                    (@toDate IS NULL OR [WorkDay] < @toDate) AND
                    (@searchString IS NULL OR [EmpBadgeId] LIKE '%' + @searchString + '%') AND
                    (@statusFilter IS NULL OR @statusFilter = 'all' OR LOWER([TimeClass]) = @statusFilter)
                    {badgeIdFilterSql}";

            // Helper: create new badge params every call
            System.Func<List<SqlParameter>> buildBadgeParams = () =>
            {
                var list = new List<SqlParameter>();
                if (finalBadgeIds != null)
                    for (int i = 0; i < finalBadgeIds.Count; i++)
                        list.Add(new SqlParameter($"@bid{i}", finalBadgeIds[i]));
                return list;
            };

            // Main query params - NEW instances
            var sqlParams = new List<SqlParameter>
            {
                new SqlParameter("@fromDate", (object)fromDate ?? DBNull.Value),
                new SqlParameter("@toDate", (object)toDate ?? DBNull.Value),
                new SqlParameter("@searchString", (object)searchString ?? DBNull.Value),
                new SqlParameter("@statusFilter", (object)statusFilter ?? DBNull.Value),
                new SqlParameter("@offset", offset),
                new SqlParameter("@pageSize", pageSize)
            };
            sqlParams.AddRange(buildBadgeParams());

            // Count query params - NEW instances
            var countParams = new List<SqlParameter>
            {
                new SqlParameter("@fromDate", (object)fromDate ?? DBNull.Value),
                new SqlParameter("@toDate", (object)toDate ?? DBNull.Value),
                new SqlParameter("@searchString", (object)searchString ?? DBNull.Value),
                new SqlParameter("@statusFilter", (object)statusFilter ?? DBNull.Value)
            };
            countParams.AddRange(buildBadgeParams());

            // ================================================================
            // EXECUTE QUERIES - Sequential (same 'db' context, not thread safe)
            // ================================================================
            var fabMoves = await db.Database
                .SqlQuery<FabMove>(sqlQuery, sqlParams.ToArray())
                .ToListAsync();

            int totalRecords = await db.Database
                .SqlQuery<int>(countQuery, countParams.ToArray())
                .FirstOrDefaultAsync();

            // ================================================================
            // UPDATE BUSUI - Only if results exist - Same as original
            // ================================================================
            if (fabMoves.Count > 0)
            {
                var busuiData = await db.Breastfeedings.ToListAsync();
                await funcs.UpdateFabMoveWithBusui(fabMoves, busuiData);
            }

            SetDetailsViewBag(searchString, areaFilter, grpFilter, areaDIFilter, shiftFilter, fromDate, toDate, statusFilter, repeatedBadgeIds);

            var pagedList = new StaticPagedList<FabMove>(fabMoves, pageNumber, pageSize, totalRecords);

            return View(pagedList);
        }

        [HttpGet]
        public async Task<ActionResult> ExportDet(
            string searchString,
            string areaFilter,
            string grpFilter,
            string areaDIFilter,
            string shiftFilter,
            string statusFilter,
            DateTime? from,
            DateTime? to,
            string[] repeatedBadgeId = null
        )
        {
            List<string> repeatedBadgeIds = repeatedBadgeId != null
                ? repeatedBadgeId
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => x.Trim())
                    .ToList()
                : new List<string>();

            // ================================================================
            // ACCESS CONTROL - Same as Index & Details
            // ================================================================
            var userAccess = new UserAccess(db);
            var windowsAccount = userAccess.GetUserAccount(User.Identity.Name);
            bool isAdmin = userAccess.IsAdmin(windowsAccount);
            bool isManager = userAccess.IsManager(windowsAccount);

            DateTime? fromDate = from?.Date;
            DateTime? toDate = to?.Date.AddDays(1).AddTicks(-1);

            // ================================================================
            // PUBLIC USER: Must have both badge ID and date range
            // ================================================================
            if (!isAdmin && !isManager)
            {
                bool hasSearchString = !string.IsNullOrEmpty(searchString);
                bool hasDateRange = fromDate.HasValue && toDate.HasValue;
                bool hasRepeatedBadgeIds = repeatedBadgeIds != null && repeatedBadgeIds.Any();

                if (!hasRepeatedBadgeIds && (!hasSearchString || !hasDateRange))
                {
                    return ReturnEmptyExcelDet(!hasSearchString
                        ? "Please input Badge ID to export data."
                        : "Please input date range (From & To) to export data.");
                }
            }

            // ================================================================
            // GET MANAGER BADGE IDs (only if manager)
            // ================================================================
            List<string> managerBadgeIds = null;

            if (isManager && !isAdmin)
            {
                var managerRecords = await db.ManagerList
                    .AsNoTracking()
                    .Where(m => !string.IsNullOrEmpty(m.Manager))
                    .Select(m => new { m.Manager, m.BadgeId })
                    .ToListAsync();

                managerBadgeIds = managerRecords
                    .Where(m => System.Text.RegularExpressions.Regex
                        .Split(m.Manager, @"\$+")
                        .Select(x => x.Trim().ToLower())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Contains(windowsAccount))
                    .Select(m => m.BadgeId)
                    .Distinct()
                    .ToList();

                // FIX: Get manager's own badge ID
                var ownBadgeId = await og.gmds
                    .Where(g => g.WindowsAccount.Trim().ToLower() == windowsAccount)
                    .Select(g => g.BadgeId)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(ownBadgeId) && !managerBadgeIds.Contains(ownBadgeId))
                    managerBadgeIds.Add(ownBadgeId);

                // Manager searches specific badge ID → check access
                if (!string.IsNullOrEmpty(searchString) && !managerBadgeIds.Contains(searchString.Trim()))
                    return ReturnEmptyExcelDet("You don't have access to this badge ID.");

                if (!managerBadgeIds.Any())
                    return ReturnEmptyExcelDet("No team members found under your account.");
            }

            // ================================================================
            // BUILD BADGE ID FILTER - Same as Details
            // ================================================================
            List<string> filteredBadgeIds = null;

            if (!string.IsNullOrEmpty(grpFilter)
                || !string.IsNullOrEmpty(areaFilter)
                || !string.IsNullOrEmpty(areaDIFilter))
            {
                var targetQuery = db.TargetTimes.AsQueryable();

                if (!string.IsNullOrEmpty(grpFilter))
                    targetQuery = targetQuery.Where(t => t.Group.Contains(grpFilter));
                if (!string.IsNullOrEmpty(areaFilter))
                    targetQuery = targetQuery.Where(t => t.Area.Contains(areaFilter));
                if (!string.IsNullOrEmpty(areaDIFilter))
                    targetQuery = targetQuery.Where(t => t.AreaDI.Contains(areaDIFilter));

                filteredBadgeIds = await targetQuery
                    .Select(t => t.EmpBadgeId)
                    .Distinct()
                    .ToListAsync();
            }

            List<string> shiftBadgeIds = null;

            if (!string.IsNullOrEmpty(shiftFilter))
            {
                shiftBadgeIds = await og.gmds
                    .Where(g => g.Production != null
                           && g.Production.Trim().ToLower() == "production"
                           && g.Group == shiftFilter)
                    .Select(g => g.BadgeId)
                    .Distinct()
                    .ToListAsync();
            }

            // Combine badge ID filters
            List<string> finalBadgeIds = null;

            if (filteredBadgeIds != null && shiftBadgeIds != null)
                finalBadgeIds = filteredBadgeIds.Intersect(shiftBadgeIds).ToList();
            else if (filteredBadgeIds != null)
                finalBadgeIds = filteredBadgeIds;
            else if (shiftBadgeIds != null)
                finalBadgeIds = shiftBadgeIds;

            // Intersect with repeatedbadgeids
            if (repeatedBadgeIds != null && repeatedBadgeIds.Any())
            {
                var repeatedList = repeatedBadgeIds
                    .Where(x => !string.IsNullOrEmpty(x)).ToList();
                finalBadgeIds = finalBadgeIds != null
                    ? finalBadgeIds.Intersect(repeatedList).ToList()
                    : repeatedList;
            }

            // Intersect with manager badge IDs
            if (isManager && !isAdmin)
            {
                finalBadgeIds = finalBadgeIds != null
                    ? finalBadgeIds.Intersect(managerBadgeIds).ToList()
                    : managerBadgeIds;
            }

            // ================================================================
            // BUILD SQL BADGE ID FILTER
            // ================================================================
            string badgeIdFilterSql = "";
            List<SqlParameter> badgeIdParams = new List<SqlParameter>();

            if (finalBadgeIds != null)
            {
                var badgeParamNames = finalBadgeIds.Select((id, i) => $"@bid{i}").ToList();
                badgeIdFilterSql = $"AND [EmpBadgeId] IN ({string.Join(",", badgeParamNames)})";
                for (int i = 0; i < finalBadgeIds.Count; i++)
                    badgeIdParams.Add(new SqlParameter(badgeParamNames[i], finalBadgeIds[i]));
            }

            // ================================================================
            // BUILD SQL QUERY - Same as original (no paging for export)
            // ================================================================
            string sqlQuery = $@"
                SELECT [Id],[EmpBadgeId],[EmpName],[EmpDept],[Designation],[JobLevel],[ShiftCode],[Section],[OrgUnit],[CostCenter],[Level1],
                       [WorkDay],[Day],[Shift],[FabIn],[FabOut],[FirstCrIn],[LastCrOut],[FabTime],[CrTime],[BrTime],[FabCount],[CrCount],
                       [FabLateIn],[FabEarlyOut],[PublicHoliday],[IsValid],[Workplace],[Gender],[Religion],[FridayPray],[Busui],[Ahm],[Amend],
                       [ExitPermit],[TargetTime],[TimeClass],[Group],[Area],[AreaDI],[UploadFileRecordId],[Missing]
                FROM [test_db].[FAB_Report].[FabMoves]
                WHERE
                    (@fromDate IS NULL OR [WorkDay] >= @fromDate) AND
                    (@toDate IS NULL OR [WorkDay] < @toDate) AND
                    (@searchString IS NULL OR [EmpBadgeId] LIKE '%' + @searchString + '%') AND
                    (@statusFilter IS NULL OR @statusFilter = 'all' OR LOWER([TimeClass]) = @statusFilter)
                    {badgeIdFilterSql}
                ORDER BY [Id] ASC";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@fromDate", (object)fromDate ?? DBNull.Value),
                new SqlParameter("@toDate", (object)toDate ?? DBNull.Value),

                // Treat empty string as NULL
                new SqlParameter("@searchString",
                    string.IsNullOrEmpty(searchString) ? DBNull.Value : (object)searchString),
    
                // Treat empty string as NULL    
                new SqlParameter("@statusFilter",
                    string.IsNullOrEmpty(statusFilter) ? DBNull.Value : (object)statusFilter)
            };
            parameters.AddRange(badgeIdParams);

            // ================================================================
            // EXECUTE QUERY
            // ================================================================
            var fabData = await db.Database
                .SqlQuery<FabMove>(sqlQuery, parameters.ToArray())
                .ToListAsync();

            if (!fabData.Any())
                return ReturnEmptyExcelDet("No data found for selected filters.");

            // ================================================================
            // LOAD GMDS DATA - For joining
            // ================================================================
            var badgeIds = fabData.Select(f => f.EmpBadgeId).Distinct().ToList();

            var gmdsData = await og.gmds
                .Where(g => badgeIds.Contains(g.BadgeId))
                .Select(g => new { g.BadgeId, g.Name, g.Department, g.Production, g.Gender, g.Religion })
                .ToListAsync();

            var gmdsDict = gmdsData
                .GroupBy(x => x.BadgeId)
                .ToDictionary(g => g.Key, g => g.First());

            // ================================================================
            // GENERATE EXCEL
            // ================================================================
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("FabMove");

                // Headers
                var headers = new[]
                {
                    "Employee Name", "Badge Number", "Department", "Designation", "Job Level",
                    "Shift Code", "Section", "Org Unit", "Cost Center", "Level 1",
                    "Workday", "Day", "Shift", "Fab In", "Fab Out",
                    "First CR In", "Last CR Out", "FAB Time", "CR Time", "BR Time",
                    "Fab Count", "CR Count", "Fab Late In", "Fab Early Out", "Public Holiday",
                    "Prod/Office", "Gender", "Religion", "Friday Pray", "Busui",
                    "AHM", "Fab Amend", "Keluar Perusahaan", "Target Time", "Time Class"
                };

                for (int i = 0; i < headers.Length; i++)
                    worksheet.Cells[1, i + 1].Value = headers[i];

                int row = 2;

                foreach (var f in fabData)
                {
                    gmdsDict.TryGetValue(f.EmpBadgeId, out var g);

                    // Basic data
                    worksheet.Cells[row, 1].Value = g?.Name ?? "-";
                    worksheet.Cells[row, 2].Value = f.EmpBadgeId;
                    worksheet.Cells[row, 3].Value = g?.Department ?? "-";
                    worksheet.Cells[row, 4].Value = f.Designation ?? "-";
                    worksheet.Cells[row, 5].Value = f.JobLevel ?? "-";
                    worksheet.Cells[row, 6].Value = f.ShiftCode ?? "-";
                    worksheet.Cells[row, 7].Value = f.Section ?? "-";
                    worksheet.Cells[row, 8].Value = f.OrgUnit ?? "-";
                    worksheet.Cells[row, 9].Value = f.CostCenter ?? "-";
                    worksheet.Cells[row, 10].Value = f.Level1 ?? "-";
                    worksheet.Cells[row, 11].Value = f.WorkDay.ToString("dd/MM/yyyy");
                    worksheet.Cells[row, 12].Value = f.Day ?? "-";
                    worksheet.Cells[row, 13].Value = f.Shift ?? "-";

                    // FabIn with color
                    worksheet.Cells[row, 14].Value = f.FabIn.ToString();
                    if (IsFabInLate(f.Shift, f.FabIn))
                        SetCellColor(worksheet.Cells[row, 14], Color.Red);

                    // FabOut with color
                    worksheet.Cells[row, 15].Value = f.FabOut.ToString();
                    if (IsFabOutEarly(f.Shift, f.FabOut, f.Day))
                        SetCellColor(worksheet.Cells[row, 15], Color.Red);

                    // FirstCrIn with color
                    worksheet.Cells[row, 16].Value = f.FirstCrIn.ToString();
                    if (IsFirstCrInLate(f.Shift, f.FirstCrIn))
                        SetCellColor(worksheet.Cells[row, 16], Color.Yellow);

                    // LastCrOut with color
                    worksheet.Cells[row, 17].Value = f.LastCrOut.ToString();
                    if (IsLastCrOutEarly(f.Shift, f.LastCrOut, f.Day))
                        SetCellColor(worksheet.Cells[row, 17], Color.Yellow);

                    worksheet.Cells[row, 18].Value = f.FabTime.ToString();

                    // CrTime with color
                    worksheet.Cells[row, 19].Value = f.CrTime.ToString();
                    if (!string.IsNullOrEmpty(f.TimeClass) && f.TimeClass.ToLower() == "failed")
                        SetCellColor(worksheet.Cells[row, 19], Color.Red);

                    worksheet.Cells[row, 20].Value = f.BrTime.ToString();
                    worksheet.Cells[row, 21].Value = f.FabCount;
                    worksheet.Cells[row, 22].Value = f.CrCount;
                    worksheet.Cells[row, 23].Value = f.FabLateIn;
                    worksheet.Cells[row, 24].Value = f.FabEarlyOut;
                    worksheet.Cells[row, 25].Value = f.PublicHoliday;
                    worksheet.Cells[row, 26].Value = g?.Production ?? "-";
                    worksheet.Cells[row, 27].Value = g?.Gender ?? "-";
                    worksheet.Cells[row, 28].Value = g?.Religion ?? "-";
                    worksheet.Cells[row, 29].Value = f.FridayPray;
                    worksheet.Cells[row, 30].Value = f.Busui.ToString();
                    worksheet.Cells[row, 31].Value = f.Ahm;
                    worksheet.Cells[row, 32].Value = f.Amend;
                    worksheet.Cells[row, 33].Value = f.ExitPermit;
                    worksheet.Cells[row, 34].Value = f.TargetTime;
                    worksheet.Cells[row, 35].Value = f.TimeClass;

                    // Highlight entire row orange if Missing == "yes"
                    if (string.Equals(f.Missing, "yes", StringComparison.OrdinalIgnoreCase))
                        for (int col = 1; col <= 35; col++)
                            SetCellColor(worksheet.Cells[row, col], Color.Orange);

                    row++;
                }

                worksheet.DefaultColWidth = 25;

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Fab Movement Reports.xlsx");
            }
        }

        // ================================================================
        // HELPER for ExportDet
        // ================================================================
        private ActionResult ReturnEmptyExcelDet(string message)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("FabMove");
                worksheet.Cells["A1"].Value = message;
                worksheet.Column(1).Width = 60;

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Fab Movement Reports.xlsx");
            }
        }

        private void SetCellColor(ExcelRange cell, Color color)
        {
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(color);
        }

        private bool IsFabInLate(string shift, TimeSpan fabIn)
        {
            if (string.IsNullOrWhiteSpace(shift)) return false;
            switch (shift.Trim().ToLower())
            {
                case "morning shift": return fabIn > new TimeSpan(7, 0, 0);
                case "afternoon shift": return fabIn > new TimeSpan(15, 45, 0);
                case "night shift": return fabIn > new TimeSpan(23, 25, 0);
                case "normal shift": return fabIn > new TimeSpan(7, 45, 0);
                default: return false;
            }
        }

        private bool IsFabOutEarly(string shift, TimeSpan fabOut, string day)
        {
            if (string.IsNullOrWhiteSpace(shift)) return false;
            switch (shift.Trim().ToLower())
            {
                case "morning shift": return fabOut < new TimeSpan(15, 45, 0);
                case "afternoon shift": return fabOut >= new TimeSpan(15, 45, 0) && fabOut < new TimeSpan(23, 25, 0);
                case "night shift": return fabOut < new TimeSpan(7, 0, 0);
                case "normal shift":
                    return day?.Trim().ToLower() == "friday"
                        ? fabOut < new TimeSpan(16, 15, 0)
                        : fabOut < new TimeSpan(17, 0, 0);
                default: return false;
            }
        }

        private bool IsFirstCrInLate(string shift, TimeSpan firstCrIn)
        {
            if (string.IsNullOrWhiteSpace(shift)) return false;
            switch (shift.Trim().ToLower())
            {
                case "morning shift": return firstCrIn > new TimeSpan(6, 55, 0);
                case "afternoon shift": return firstCrIn > new TimeSpan(15, 40, 0);
                case "night shift": return firstCrIn > new TimeSpan(23, 20, 0);
                case "normal shift": return firstCrIn > new TimeSpan(7, 40, 0);
                default: return false;
            }
        }

        private bool IsLastCrOutEarly(string shift, TimeSpan lastCrOut, string day)
        {
            if (string.IsNullOrWhiteSpace(shift)) return false;
            switch (shift.Trim().ToLower())
            {
                case "morning shift": return lastCrOut < new TimeSpan(15, 45, 0);
                case "afternoon shift": return lastCrOut > new TimeSpan(15, 45, 0) && lastCrOut < new TimeSpan(23, 25, 0);
                case "night shift": return lastCrOut < new TimeSpan(7, 0, 0);
                case "normal shift":
                    return day?.Trim().ToLower() == "friday"
                        ? lastCrOut < new TimeSpan(16, 15, 0)
                        : lastCrOut < new TimeSpan(17, 0, 0);
                default: return false;
            }
        }

        public ActionResult RepeatedViolations(int? page, string logWeek, string searchString, string areaFilter)
        {
            // ================================================================
            // ACCESS CONTROL
            // ================================================================
            var userAccess = new UserAccess(db);
            string windowsAccount = userAccess.GetUserAccount(User.Identity.Name);
            bool isAdmin = userAccess.IsAdmin(windowsAccount);
            bool isManager = userAccess.IsManager(windowsAccount);

            int pageSize = 30;
            int pageNumber = page ?? 1;

            // ================================================================
            // GET ALL LOGWEEKS
            // ================================================================
            var allLogWeeks = db.Users
                .Select(u => u.LogWeek)
                .Distinct()
                .OrderByDescending(lw => lw)
                .ToList();

            // ================================================================
            // DETERMINE SELECTED 4 LOGWEEKS
            // ================================================================
            List<string> selectedLogWeeks;
            if (!string.IsNullOrEmpty(logWeek))
            {
                int idx = allLogWeeks.IndexOf(logWeek);
                if (idx < 0) idx = 0;
                selectedLogWeeks = allLogWeeks.Skip(idx).Take(4).ToList();
                ViewBag.SelectedLogWeek = logWeek;
            }
            else
            {
                selectedLogWeeks = allLogWeeks.Take(4).ToList();
                ViewBag.SelectedLogWeek = allLogWeeks.FirstOrDefault();
            }

            ViewBag.LogWeeks = allLogWeeks;
            ViewBag.SelectedLogWeeksRange = string.Join(", ", selectedLogWeeks);

            // ================================================================
            // CONVERT SELECTED LOGWEEKS TO DATE RANGE FOR DETAILS LINK
            // ================================================================
            var matchingDates = db.BFabMoves
                .Select(f => f.WorkDay)
                .Distinct()
                .ToList()
                .Where(d => selectedLogWeeks.Contains(lw.GetLogWeek(d)))
                .ToList();

            DateTime? fromDateForDetails = matchingDates.Any() ? matchingDates.Min() : (DateTime?)null;
            DateTime? toDateForDetails = matchingDates.Any() ? matchingDates.Max() : (DateTime?)null;

            ViewBag.FromDateForDetails = fromDateForDetails?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.ToDateForDetails = toDateForDetails?.ToString("yyyy-MM-dd") ?? "";

            // ================================================================
            // PREPARE AREA LIST DROPDOWN
            // ================================================================
            var areaListFinal = db.Users
                .Select(u => u.Area)
                .Distinct()
                .OrderBy(a => a)
                .ToList();
            ViewBag.areaList = new SelectList(areaListFinal, areaFilter);

            // Set ViewBag early - used in all return paths
            ViewBag.searchString = searchString;
            ViewBag.areaFilter = areaFilter;

            // ================================================================
            // FILTER USERS FOR SELECTED 4 LOGWEEKS
            // ================================================================
            var usersQuery = db.Users.Where(u => selectedLogWeeks.Contains(u.LogWeek));

            if (!string.IsNullOrEmpty(searchString))
                usersQuery = usersQuery.Where(u => u.EmpBadgeId.Contains(searchString));
            if (!string.IsNullOrEmpty(areaFilter))
                usersQuery = usersQuery.Where(u => u.Area == areaFilter);

            var usersList = usersQuery.ToList();

            // ================================================================
            // MANAGER RESTRICTION
            // ================================================================
            if (isManager && !isAdmin)
            {
                var managerList = db.ManagerList
                    .AsNoTracking()
                    .Where(m => !string.IsNullOrEmpty(m.Manager))
                    .Select(m => new { m.Manager, m.BadgeId })
                    .ToList();

                var managerBadgeIds = managerList
                    .Where(m => System.Text.RegularExpressions.Regex
                        .Split(m.Manager, @"\$+")
                        .Select(x => x.Trim().ToLower())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Contains(windowsAccount))
                    .Select(m => m.BadgeId)
                    .Distinct()
                    .ToList();

                // Get manager's own badge ID
                var ownBadgeId = og.gmds
                    .Where(g => g.WindowsAccount.Trim().ToLower() == windowsAccount)
                    .Select(g => g.BadgeId)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(ownBadgeId) && !managerBadgeIds.Contains(ownBadgeId))
                    managerBadgeIds.Add(ownBadgeId);

                // Check access if searching specific badge ID
                if (!string.IsNullOrEmpty(searchString))
                {
                    if (!managerBadgeIds.Contains(searchString.Trim()))
                    {
                        ViewBag.ErrorMessage = "You don't have access to this badge id";
                        ViewBag.RepeatedBadgeIds = "";
                        return View(new StaticPagedList<ok1.Models.User>(
                            new List<ok1.Models.User>(),
                            pageNumber,
                            pageSize,
                            0
                        ));
                    }
                }

                // Restrict to manager's team members only
                usersList = usersList
                    .Where(u => managerBadgeIds.Contains(u.EmpBadgeId))
                    .ToList();
            }

            // ================================================================
            // FIND REPEATED VIOLATIONS
            // Badge IDs with at least 2 logweeks below 100%
            // ================================================================
            var repeatedBadgeIds = usersList
                .Where(u => u.AvgPoints < 100)
                .GroupBy(u => u.EmpBadgeId)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .ToList();

            //Pass repeated badge IDs to ViewBag for Show Data button
            ViewBag.RepeatedBadgeIds = string.Join(",", repeatedBadgeIds);

            // Only include records for repeated badge IDs in the 4 logweeks
            var resultUsers = usersList
                .Where(u => repeatedBadgeIds.Contains(u.EmpBadgeId))
                .ToList();

            // ================================================================
            // GROUP BY EMPLOYEE FOR PAGING
            // ================================================================
            var grouped = resultUsers
                .GroupBy(u => new { u.EmpBadgeId, u.EmpName, u.Area })
                .OrderBy(g => g.Key.EmpBadgeId)
                .ToList();

            var pagedGroups = grouped
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedUsers = pagedGroups.SelectMany(g => g).ToList();

            var pagedListResult = new StaticPagedList<ok1.Models.User>(
                pagedUsers,
                pageNumber,
                pageSize,
                grouped.Count
            );

            return View(pagedListResult);
        }

        [HttpGet]
        public ActionResult ExportRepeatedViolations(string logWeek, string searchString, string areaFilter)
        {
            var allLogWeeks = db.Users
                .Select(u => u.LogWeek)
                .Distinct()
                .OrderByDescending(lw => lw)
                .ToList();

            List<string> selectedLogWeeks;
            if (!string.IsNullOrEmpty(logWeek))
            {
                int idx = allLogWeeks.IndexOf(logWeek);
                if (idx < 0) idx = 0;
                selectedLogWeeks = allLogWeeks.Skip(idx).Take(4).ToList();
            }
            else
            {
                selectedLogWeeks = allLogWeeks.Take(4).ToList();
            }

            var usersQuery = db.Users.Where(u => selectedLogWeeks.Contains(u.LogWeek));
            if (!string.IsNullOrEmpty(searchString))
            {
                usersQuery = usersQuery.Where(u => u.EmpBadgeId.Contains(searchString));
            }
            if (!string.IsNullOrEmpty(areaFilter))
            {
                usersQuery = usersQuery.Where(u => u.Area == areaFilter);
            }

            var usersList = usersQuery.ToList();

            var repeatedBadgeIds = usersList
                .Where(u => u.AvgPoints < 100)
                .GroupBy(u => u.EmpBadgeId)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .ToList();

            var resultUsers = usersList
                .Where(u => repeatedBadgeIds.Contains(u.EmpBadgeId))
                .ToList();

            var grouped = resultUsers
                .GroupBy(u => new { u.EmpBadgeId, u.EmpName, u.Area })
                .OrderBy(g => g.Key.EmpBadgeId)
                .ToList();

            using (var package = new OfficeOpenXml.ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Repeated Violations");

                // Header
                worksheet.Cells[1, 1].Value = "No";
                worksheet.Cells[1, 2].Value = "Badge ID";
                worksheet.Cells[1, 3].Value = "Name";
                worksheet.Cells[1, 4].Value = "Area";
                for (int i = 0; i < selectedLogWeeks.Count; i++)
                {
                    worksheet.Cells[1, 5 + i].Value = selectedLogWeeks[i];
                }

                // Data
                int row = 2;
                int no = 1;
                foreach (var group in grouped)
                {
                    worksheet.Cells[row, 1].Value = no;
                    worksheet.Cells[row, 2].Value = group.Key.EmpBadgeId;
                    worksheet.Cells[row, 3].Value = group.Key.EmpName;
                    worksheet.Cells[row, 4].Value = group.Key.Area;
                    for (int i = 0; i < selectedLogWeeks.Count; i++)
                    {
                        var lw = selectedLogWeeks[i];
                        var user = group.FirstOrDefault(u => string.Equals(u.LogWeek, lw, StringComparison.OrdinalIgnoreCase));
                        worksheet.Cells[row, 5 + i].Value = user != null ? string.Format("{0:0.##}%", user.AvgPoints) : "No Data";
                    }
                    row++;
                    no++;
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                var stream = new System.IO.MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = "RepeatedViolations.xlsx";
                string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return File(stream, contentType, fileName);
            }
        }

        // HELPER convert logweek
        private (DateTime from, DateTime to) GetDateRangeFromLogWeeks(List<string> logWeeks, logweek lw)
        {
            // Get all dates from BFabmoves that match the logweeks
            var allDates = db.BFabMoves
                .Select(f => f.WorkDay)
                .Distinct()
                .ToList()
                .Where(d => logWeeks.Contains(lw.GetLogWeek(d)))
                .ToList();

            if (!allDates.Any())
                return (DateTime.MinValue, DateTime.MaxValue);

            return (allDates.Min(), allDates.Max());
        }

        // HELPER 

        private void SetDetailsViewBag(
            string searchString, string areaFilter, string grpFilter, string areaDIFilter, string shiftFilter,
            DateTime? fromDate, DateTime? toDate, string statusFilter, string[] repeatedBadgeIds = null)
        {
            ViewBag.SearchString = searchString;
            ViewBag.AreaFilter = areaFilter;
            ViewBag.GrpFilter = grpFilter;
            ViewBag.AreaDIFilter = areaDIFilter;
            ViewBag.shiftFilter = shiftFilter;
            ViewBag.startDate = fromDate?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.endDate = toDate?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.StatusFilter = statusFilter;

            // Consistent date naming used by BOTH pagination and export form
            ViewBag.FromDateExport = fromDate.HasValue
                ? fromDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.ToDateExport = toDate.HasValue
                ? toDate.Value.ToString("yyyy-MM-dd") : "";

            // RepeatedBadgeIds for pagination and export form
            ViewBag.RepeatedBadgeIds = repeatedBadgeIds != null && repeatedBadgeIds.Any()
                ? string.Join(",", repeatedBadgeIds)
                : null;
        }
        private void ClearUserSummariesCache()
        {
            var cache = System.Web.HttpRuntime.Cache;
            var keysToRemove = new List<string>();

            foreach (System.Collections.DictionaryEntry entry in cache)
            {
                if (entry.Key is string key && key.StartsWith("UserSummaries_"))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                cache.Remove(key);
            }
        }

        private void UpdateBreastfeedingStatuses()
        {
            var now = DateTime.Now;
            var records = db.Breastfeedings.ToList();
            bool changed = false;

            foreach (var item in records)
            {
                string correctStatus = item.ExpDate > now ? "Active" : "Inactive";
                if (item.Status != correctStatus)
                {
                    item.Status = correctStatus;
                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
                ClearBreastfeedingCache();
            }
        }

        private void ClearBreastfeedingCache()
        {
            var cache = System.Web.HttpRuntime.Cache;
            var keysToRemove = new List<string>();

            foreach (System.Collections.DictionaryEntry entry in cache)
            {
                if (entry.Key is string key && key.StartsWith("Breastfeedings_"))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                cache.Remove(key);
            }
        }

        private void ClearFabMovesCache()
        {
            var cache = System.Web.HttpRuntime.Cache;
            var keysToRemove = new List<string>();

            foreach (System.Collections.DictionaryEntry entry in cache)
            {
                if (entry.Key is string key && key.StartsWith("FabMoves_"))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                cache.Remove(key);
            }
        }

        // DTOs for raw SQL mapping
        private class GmdsDto
        {
            public string BadgeId { get; set; }
            public string Name { get; set; }
            public string Department { get; set; }
        }

        private class TargetDto
        {
            public string EmpBadgeId { get; set; }
            public string Group { get; set; }
            public string Area { get; set; }
            public string AreaDI { get; set; }
        }

        private class AreaMappingDto
        {
            public string Area { get; set; }
            public string AreaDI { get; set; }
        }
    }
}

