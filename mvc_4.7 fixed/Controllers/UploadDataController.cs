using Antlr.Runtime.Misc;
using OfficeOpenXml;
using ok1.Data;
using ok1.Models;
using ok1.Services;
using PagedList;
using System;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Web.WebSockets;
using System.Diagnostics;
using System.Threading;
using System.Globalization;


namespace ok1.Controllers
{
    [FeatureAuthorize("UploadData")]

    public class UploadDataController : Controller
    {
        private TCFunc funcs;
        public UploadDataController()
        {
            funcs = new TCFunc();
        }
        private ok1Context db = new ok1Context();
        private masterContext master = new masterContext();

        // GET: UploadData
       public ActionResult Index(int? page = 1)
        {
            int pageNumber = page ?? 1;
            int pageSize = 15;

            // Use IQueryable for deferred execution and efficient paging
            var query = db.BFabMoves.OrderBy(f => f.Id);
            var pagedList = query.ToPagedList(pageNumber, pageSize);

            return View(pagedList);
        }

        // List all uploaded files
        public ActionResult fileRecords(int? page)
        {
            int pageSize = 10;
            int pageNumber = (page ?? 1);

            var records = db.UploadFileRecord.OrderByDescending(r => r.UploadDate)
                .ToPagedList(pageNumber, pageSize);

            return View(records);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RefreshFileRecords(int id)
        {
            // Find all FabMove records linked to this file
            var fabMoves = db.BFabMoves.Where(f => f.UploadFileRecordId == id).ToList();

            if (!fabMoves.Any())
            {
                TempData["message"] = "No FabMove records found for this file.";
                return RedirectToAction("fileRecords");
            }

            // Preload reference data
            var publicHolidays = db.PublicHolidays.AsNoTracking().ToList();
            var breastfeedings = db.Breastfeedings.AsNoTracking().ToList();
            var exitPermitData = master.tbl_Testing.AsNoTracking().ToList();
            var attendeeData = db.GetAttendees.AsNoTracking().ToList();

            var eventDataRaw = db.EventRegs.AsNoTracking().ToList();
            var eventData = eventDataRaw
                .GroupBy(e => new
                {
                    e.TrainerId,
                    e.TrainerName,
                    e.EventDate,
                    e.EventName,
                    e.StartTime,
                    e.EndTime,
                    e.EventDur
                })
                .Select(g => g.First())
                .ToList();

            var eventAttData = db.EventAttendees.AsNoTracking().ToList();
            var formData = master.FormRequest.AsNoTracking().ToList();
            var targetTimes = db.TargetTimes.AsNoTracking().ToList();

            var tcFunc = new TCFunc();

            // Recalculate all fields using your service methods
            await tcFunc.UpdateFridayPray(fabMoves);
            await tcFunc.UpdateFabMoveWithBusui(fabMoves, breastfeedings);
            await tcFunc.UpdateFabMoveExitPermit(fabMoves, exitPermitData);
            await tcFunc.UpdateFabMoveWithEvent(fabMoves, attendeeData, eventData, eventAttData);
            await tcFunc.UpdateFabMoveWithFormData(fabMoves, formData, attendeeData, eventAttData, eventData);
            await tcFunc.UpdateWithHoliday(publicHolidays, fabMoves);
            tcFunc.UpdateTargetTime(targetTimes, fabMoves);

            // --- Optimized TimeClass calculation ---
            var gmdsList = master.gmds.AsNoTracking().ToList();
            var targetTimesList = db.TargetTimes.AsNoTracking().ToList();

            var badgeToWorkplace = gmdsList
                .GroupBy(g => g.BadgeId)
                .ToDictionary(g => g.Key, g => g.First().Production?.Trim().ToLowerInvariant());

            var badgeToTargetTime = targetTimesList
                .GroupBy(t => t.EmpBadgeId)
                .ToDictionary(g => g.Key, g => g.First());

            var timeClassService = new TimeClass();
            timeClassService.UpdateTimeClass(fabMoves, badgeToWorkplace, badgeToTargetTime);

            // Update record count for the file record
            var uploadRecord = db.UploadFileRecord.Find(id);
            if (uploadRecord != null)
            {
                uploadRecord.RecordCount = fabMoves.Count();
                uploadRecord.RefreshTime = DateTime.Now;
            }

            TempData["message"] = "File records refreshed successfully.";
            ClearFabMovesCache();
            ClearUserSummariesCache();
            ClearFabMovesPageCache();
            ClearPassedChartCache();

            // Recalculate percentage 
            var logweekService = new logweek();
            var gmdsDict = gmdsList.ToDictionary(g => g.BadgeId, g => g);
            var targetDict = targetTimesList.ToDictionary(t => t.EmpBadgeId, t => t);

            var userSummaries = fabMoves
                .GroupBy(fm => new { fm.EmpBadgeId, LogWeek = logweekService.GetLogWeek(fm.WorkDay) })
                .Select(g =>
                {
                    var badgeId = g.Key.EmpBadgeId;
                    var logWeek = g.Key.LogWeek;
                    var gmds = gmdsDict.ContainsKey(badgeId) ? gmdsDict[badgeId] : null;
                    var target = targetDict.ContainsKey(badgeId) ? targetDict[badgeId] : null;

                    int total = g.Count();
                    int passed = g.Count(fm => string.Equals(fm.TimeClass, "Passed", StringComparison.OrdinalIgnoreCase));
                    double avgPoints = total > 0 ? Math.Round((passed / (double)total) * 100, 2) : 0.0;

                    return new User
                    {
                        EmpBadgeId = badgeId,
                        EmpName = gmds?.Name ?? "-",
                        EmpDept = gmds?.Department ?? "-",
                        Group = target?.Group ?? "-",
                        Area = target?.Area ?? "-",
                        AreaDI = target?.AreaDI ?? "-",
                        LogWeek = logWeek,
                        Points = passed,
                        AvgPoints = avgPoints
                    };
                })
                .ToList();

            var badgeLogWeeks = userSummaries.Select(u => new { u.EmpBadgeId, u.LogWeek }).ToList();
            var badgeIds = badgeLogWeeks.Select(x => x.EmpBadgeId).Distinct().ToList();
            var logWeeks = badgeLogWeeks.Select(x => x.LogWeek).Distinct().ToList();

            var existingUsers = db.Users
                .Where(u => badgeIds.Contains(u.EmpBadgeId) && logWeeks.Contains(u.LogWeek))
                .ToList();

            var newUsers = new List<User>();
            foreach (var summary in userSummaries)
            {
                var existing = existingUsers.FirstOrDefault(u =>
                    u.EmpBadgeId == summary.EmpBadgeId && u.LogWeek == summary.LogWeek);

                if (existing == null)
                {
                    newUsers.Add(summary);
                }
                else
                {
                    if (existing.EmpName != summary.EmpName ||
                        existing.EmpDept != summary.EmpDept ||
                        existing.Group != summary.Group ||
                        existing.Area != summary.Area ||
                        existing.AreaDI != summary.AreaDI ||
                        existing.Points != summary.Points ||
                        existing.AvgPoints != summary.AvgPoints)
                    {
                        existing.EmpName = summary.EmpName;
                        existing.EmpDept = summary.EmpDept;
                        existing.Group = summary.Group;
                        existing.Area = summary.Area;
                        existing.AreaDI = summary.AreaDI;
                        existing.Points = summary.Points;
                        existing.AvgPoints = summary.AvgPoints;
                    }
                }
            }
            if (newUsers.Any())
            {
                db.Users.AddRange(newUsers);
            }

            await db.SaveChangesAsync();

            // --- Webhook notification ---
            if (fabMoves.Any())
            {
                var webhookService = new WebhookService();

                var badgeMonthGroups = fabMoves
                    .GroupBy(fm => new
                    {
                        fm.EmpBadgeId,
                        Month = fm.WorkDay.Month,
                        Year = fm.WorkDay.Year
                    })
                    .ToList();

                var allBadgeIds = badgeMonthGroups
                    .Select(g => g.Key.EmpBadgeId)
                    .Distinct()
                    .ToList();

                var allTargetTimes = db.TargetTimes
                    .Where(t => allBadgeIds.Contains(t.EmpBadgeId))
                    .AsNoTracking()
                    .ToList();

                var allGmds = master.gmds
                    .Where(g => allBadgeIds.Contains(g.BadgeId))
                    .AsNoTracking()
                    .ToList();

                var targetDicts = allTargetTimes
                    .GroupBy(t => t.EmpBadgeId)
                    .ToDictionary(g => g.Key, g => g.First());

                var gmdsDicts = allGmds
                    .GroupBy(g => g.BadgeId)
                    .ToDictionary(g => g.Key, g => g.First());

                // --- Get overall date range for single DB query ---
                var monthRanges = badgeMonthGroups
                    .Select(g => new
                    {
                        g.Key.EmpBadgeId,
                        g.Key.Month,
                        g.Key.Year,
                        MonthStart = new DateTime(g.Key.Year, g.Key.Month, 1),
                        MonthEnd = new DateTime(g.Key.Year, g.Key.Month,
                            DateTime.DaysInMonth(g.Key.Year, g.Key.Month))
                    })
                    .ToList();

                var overallStart = monthRanges.Min(r => r.MonthStart);
                var overallEnd = monthRanges.Max(r => r.MonthEnd);

                // --- Single DB query: full month data for all badge IDs ---
                var allDbFabMoves = db.BFabMoves
                    .AsNoTracking()
                    .Where(fm =>
                        allBadgeIds.Contains(fm.EmpBadgeId) &&
                        fm.WorkDay >= overallStart &&
                        fm.WorkDay <= overallEnd)
                    .ToList();

                // --- Build lookup: "badgeId_month_year" -> full month List<FabMove> ---
                var fullMonthMoves = allDbFabMoves
                    .GroupBy(fm => $"{fm.EmpBadgeId}_{fm.WorkDay.Month}_{fm.WorkDay.Year}")
                    .ToDictionary(g => g.Key, g => g.ToList());

                _ = Task.Run(async () =>
                {
                    const int maxParallel = 20;
                    using (var semaphore = new SemaphoreSlim(maxParallel))
                    {
                        var tasks = badgeMonthGroups.Select(async group =>
                        {
                            var badgeId = group.Key.EmpBadgeId;
                            var month = group.Key.Month;
                            var year = group.Key.Year;
                            var dictKey = $"{badgeId}_{month}_{year}";

                            // Full month data from DB, not just refreshed fabMoves
                            var fabMovesForMonth = fullMonthMoves.ContainsKey(dictKey)
                                ? fullMonthMoves[dictKey]
                                : new List<FabMove>();

                            TargetTime target = targetDicts.ContainsKey(badgeId)
                                ? targetDicts[badgeId] : null;

                            tbl_GMDS gmds = gmdsDicts.ContainsKey(badgeId)
                                ? gmdsDicts[badgeId] : null;

                            await semaphore.WaitAsync();
                            try
                            {
                                await webhookService.SendWebhookAsync(
                                    fabMovesForMonth, badgeId, month, year, target, gmds);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Webhook] Error badgeId={badgeId}: {ex.Message}");
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }).ToList();

                        await Task.WhenAll(tasks);
                    }

                    Debug.WriteLine($"[Webhook] All {badgeMonthGroups.Count} webhooks completed.");
                });
            }

            return RedirectToAction("fileRecords");
        }

        // Edit FabMove data for a specific upload
        public ActionResult Edit(int? id, int? page = 1)
        {
            var uploadRecord = db.UploadFileRecord.Find(id);
            if (uploadRecord == null)
            {
                return HttpNotFound();
            }

            int pageSize = 30;
            int pageNumber = page ?? 1;

            var fabMoves = db.BFabMoves.Where(fm => fm.UploadFileRecordId == uploadRecord.Id)
                .OrderBy(fm => fm.Id)
                .ToPagedList(pageNumber, pageSize);

            var viewModel = new EditUploadFileRecordViewModel
            {
                UploadFileRecord = uploadRecord,
                FabMovesPaged = fabMoves,
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(int id, List<FabMove> fabMoves)
        {
            foreach (var move in fabMoves)
            {
                var existing = db.BFabMoves.Find(move.Id);
                if (existing != null)
                {
                    // Update properties as needed
                    existing.EmpBadgeId = move.EmpBadgeId;
                    existing.EmpName = move.EmpName;
                    existing.EmpDept = move.EmpDept;
                    existing.Designation = move.Designation;
                    existing.JobLevel = move.JobLevel;
                    existing.ShiftCode = move.ShiftCode;
                    existing.Section = move.Section;
                    existing.OrgUnit = move.OrgUnit;
                    existing.CostCenter = move.CostCenter;
                    existing.Level1 = move.Level1;
                    existing.WorkDay = move.WorkDay;
                    existing.Day = move.Day;
                    existing.Shift = move.Shift;
                    existing.FabIn = move.FabIn;
                    existing.FabOut = move.FabOut;
                    existing.FirstCrIn = move.FirstCrIn;
                    existing.LastCrOut = move.LastCrOut;
                    existing.FabTime = move.FabTime;
                    existing.CrTime = move.CrTime;
                    existing.BrTime = move.BrTime;
                    existing.FabCount = move.FabCount;
                    existing.CrCount = move.CrCount;
                    existing.FabLateIn = move.FabLateIn;
                    existing.FabEarlyOut = move.FabEarlyOut;

                }
            }
            db.SaveChanges();

            TempData["message"] = "FabMove records updated!";
            return RedirectToAction("FileRecords");
        }


        // Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var uploadRecord = db.UploadFileRecord.Find(id);
            if (uploadRecord == null)
            {
                return HttpNotFound();
            }

            // Get all FabMoves for this file (only needed fields)
            var fabMoves = db.BFabMoves
                .Where(fm => fm.UploadFileRecordId == id)
                .Select(fm => new { fm.Id, fm.EmpBadgeId, fm.WorkDay })
                .ToList();

            if (fabMoves.Any())
            {
                var lwService = new logweek();

                // Calculate all unique (EmpBadgeId, LogWeek) pairs
                var badgeLogWeeks = fabMoves
                    .Select(fm => new { fm.EmpBadgeId, LogWeek = lwService.GetLogWeek(fm.WorkDay) })
                    .Distinct()
                    .ToList();

                // For each pair, delete matching User records
                foreach (var pair in badgeLogWeeks)
                {
                    var usersToDelete = db.Users
                        .Where(u => u.EmpBadgeId == pair.EmpBadgeId && u.LogWeek == pair.LogWeek);

                    db.Users.RemoveRange(usersToDelete);
                }

                // Remove all FabMoves for this upload record in batches
                var fabMoveIds = fabMoves.Select(fm => fm.Id).ToList();
                foreach (var batch in Batch(fabMoveIds, 500))
                {
                    var fabMovesToDelete = db.BFabMoves.Where(fm => batch.Contains(fm.Id));
                    db.BFabMoves.RemoveRange(fabMovesToDelete);
                }
            }

            db.UploadFileRecord.Remove(uploadRecord);

            ClearFabMovesCache();
            ClearUserSummariesCache();
            ClearFabMovesPageCache();
            ClearPassedChartCache();

            db.SaveChanges();

            TempData["message"] = "Upload file, related FabMove, and User records deleted!";
            return RedirectToAction("fileRecords");
        }

        public ActionResult Template(HttpPostedFileBase file)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Fab Data");

                //set header
                worksheet.Cells["A1"].Value = "Employee Name";
                worksheet.Cells["B1"].Value = "Badge ID";
                worksheet.Cells["C1"].Value = "Department";
                worksheet.Cells["D1"].Value = "Designation";
                worksheet.Cells["E1"].Value = "Job Level";
                worksheet.Cells["F1"].Value = "Shift Code";
                worksheet.Cells["G1"].Value = "Section";
                worksheet.Cells["H1"].Value = "Org Unit";
                worksheet.Cells["I1"].Value = "Cost Center";
                worksheet.Cells["J1"].Value = "Level 1";
                worksheet.Cells["K1"].Value = "WorkDay";
                worksheet.Cells["L1"].Value = "Day";
                worksheet.Cells["M1"].Value = "Shift";
                worksheet.Cells["N1"].Value = "Fab In";
                worksheet.Cells["O1"].Value = "Fab Out";
                worksheet.Cells["P1"].Value = "First CR In";
                worksheet.Cells["Q1"].Value = "Last CR Out";
                worksheet.Cells["R1"].Value = "Fab Time";
                worksheet.Cells["S1"].Value = "CR Time";
                worksheet.Cells["T1"].Value = "BR Time";
                worksheet.Cells["U1"].Value = "Fab Count";
                worksheet.Cells["V1"].Value = "CR Count";
                worksheet.Cells["W1"].Value = "Fab Late In";
                worksheet.Cells["X1"].Value = "Fab Early Out";
                worksheet.Cells["Y1"].Value = "Logweek";

                //set column width
                for (var col = 1; col <= 25; col++)
                {
                    worksheet.Column(col).Width = 20;
                }
                //set format datetime
                //should be checked if it's works or received as another data type
                worksheet.Cells["K1"].Style.Numberformat.Format = "hh:mm:ss";
                worksheet.Column(2).Style.Numberformat.Format = "@";


                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = "Template_FabData.xlsx";
                string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                return File(stream, contentType, fileName);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Import(HttpPostedFileBase file)
        {
            int skippedRows = 0;

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (file == null || file.ContentLength == 0)
                    {
                        Debug.WriteLine("No file uploaded");
                        TempData["ImportDebugMessages"] = "No file uploaded";
                        return HttpNotFound("No file uploaded");
                    }

                    string filename = Path.GetFileName(file.FileName);
                    Debug.WriteLine($"Filename: {filename}");

                    var uploadRecord = new UploadFileRecord
                    {
                        FileName = filename,
                        UploadDate = DateTime.Now,
                        UploadedBy = User.Identity.Name,
                        RecordCount = 0
                    };

                    db.UploadFileRecord.Add(uploadRecord);
                    await db.SaveChangesAsync();

                    int uploadRecordId = uploadRecord.Id;

                    // Preload reference data
                    var publicHolidays = db.PublicHolidays.AsNoTracking().ToList();
                    var breastfeedings = db.Breastfeedings.AsNoTracking().ToList();
                    var exitPermitData = master.tbl_Testing
                        .AsNoTracking()
                        .Where(t => t.SubmitTime.HasValue
                            && t.OutOf != null && t.OutOf.Trim().ToLower() == "perusahaan"
                            && t.Status != null && t.Status.Trim().ToLower() != "waiting approval")
                        .ToList();
                    var attendeeData = db.GetAttendees.AsNoTracking().ToList();
                    var eventDataRaw = db.EventRegs.AsNoTracking().ToList();
                    var eventData = eventDataRaw
                        .GroupBy(e => new
                        {
                            e.TrainerId,
                            e.TrainerName,
                            e.EventDate,
                            e.EventName,
                            e.StartTime,
                            e.EndTime,
                            e.EventDur
                        })
                        .Select(g => g.First())
                        .ToList();
                    var eventAttData = db.EventAttendees.AsNoTracking().ToList();
                    var formData = master.FormRequest.AsNoTracking().ToList();
                    var targetTimes = db.TargetTimes.AsNoTracking().ToList();

                    var importedMoves = new List<FabMove>();

                    using (var package = new ExcelPackage(file.InputStream))
                    {
                        var worksheet = package.Workbook.Worksheets.First();
                        if (worksheet.Dimension == null)
                        {
                            TempData["ImportDebugMessages"] = "Excel worksheet is empty";
                            return RedirectToAction(nameof(Index));
                        }

                        // Get row count/total row
                        var rowCount = worksheet.Dimension.Rows;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            var empBadgeId = worksheet.Cells[row, 2].Text?.Trim().ToUpper();
                            // Check if Badge Id is null then skip
                            if (string.IsNullOrWhiteSpace(empBadgeId))
                            {
                                skippedRows++;
                                continue;
                            }

                            var workDayCell = worksheet.Cells[row, 11].GetValue<DateTime?>();
                            // Check if Workday is null then skip
                            if (!workDayCell.HasValue)
                            {
                                skippedRows++;
                                continue;
                            }
                            DateTime workDay = workDayCell.Value.Date;

                            var dayOfWeek = workDayCell.Value.DayOfWeek;
                            var shiftValue = worksheet.Cells[row, 13].Text?.Trim().ToLower();

                            // Skip saturday/sunday with normal shift
                            if ((dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday) &&
                                (shiftValue == "normal shift" || shiftValue == "n"))
                            {
                                skippedRows++;
                                continue;
                            }

                            var holiday = publicHolidays.FirstOrDefault(ph => ph.DateFrom.Date <= workDay && ph.DateTo.Date >= workDay);
                            string holidayRemark = holiday != null ? holiday.RemarkDate : "No Holiday";

                            // Skip holiday with normal shift
                            if (holiday != null && (shiftValue == "normal shift" || shiftValue == "n"))
                            {
                                skippedRows++;
                                continue;
                            }

                            var masterData = GetMasterData(empBadgeId);
                            var targetTime = targetTimes.FirstOrDefault(tt => tt.EmpBadgeId == empBadgeId);
                            TimeSpan? normalShift = targetTime?.NormalShift;

                            // Skip if workplace is not Production or Production(nh)
                            var workplace = masterData?.Production?.Trim().ToLower();
                            if (workplace != "production" && workplace != "production(nh)")
                            {
                                skippedRows++;
                                continue;
                            }

                            // Validate fabtime based on shift
                            var fabTimeValue = SafeGetTime(worksheet, row, 18);
                            bool isFabTimeValid = false;

                            if (shiftValue == "morning shift")
                            {
                                isFabTimeValid = fabTimeValue >= new TimeSpan(7, 30, 0);
                            }
                            else if (shiftValue == "afternoon shift")
                            {
                                isFabTimeValid = fabTimeValue >= new TimeSpan(7, 25, 0);
                            }
                            else if (shiftValue == "night shift")
                            {
                                isFabTimeValid = fabTimeValue >= new TimeSpan(7, 15, 0);
                            }
                            else if (shiftValue == "normal shift" || shiftValue == "n")
                            {
                                isFabTimeValid = fabTimeValue >= new TimeSpan(8, 0, 0);
                            }
                            else
                            {
                                isFabTimeValid = false;
                            }

                            if (!isFabTimeValid)
                            {
                                skippedRows++;
                                continue;
                            }

                            var fabMove = new FabMove();
                            MapFabMoveData(fabMove, worksheet, row);

                            if (masterData != null)
                            {
                                fabMove.Gender = masterData.Gender;
                                fabMove.Area = masterData.Area;
                                fabMove.Group = masterData.Group;
                                fabMove.AreaDI = masterData.AreaDI;
                                fabMove.Religion = masterData.Religion;
                                fabMove.Workplace = masterData.Production;
                            }
                            fabMove.PublicHoliday = holidayRemark;
                            fabMove.UploadFileRecordId = uploadRecordId;
                            if (normalShift.HasValue)
                            {
                                fabMove.TargetTime = normalShift.Value.ToString();
                            }

                            importedMoves.Add(fabMove);
                        }
                    }

                    // Process all importedMoves in memory
                    var tcFunc = new TCFunc();

                    await tcFunc.UpdateFridayPray(importedMoves);
                    await tcFunc.UpdateFabMoveWithBusui(importedMoves, breastfeedings);
                    await tcFunc.UpdateFabMoveExitPermit(importedMoves, exitPermitData);
                    await tcFunc.UpdateFabMoveWithEvent(importedMoves, attendeeData, eventData, eventAttData);
                    await tcFunc.UpdateFabMoveWithFormData(importedMoves, formData, attendeeData, eventAttData, eventData);
                    tcFunc.UpdateTargetTime(targetTimes, importedMoves);

                    // Preload master data and target times once
                    var gmdsList = master.gmds.AsNoTracking().ToList();
                    var targetTimesList = db.TargetTimes.AsNoTracking().ToList();

                    // Build look up dictionaries
                    var badgeToWorkplace = gmdsList
                        .GroupBy(g => g.BadgeId)
                        .ToDictionary(g => g.Key, g => g.First().Production?.Trim().ToLowerInvariant());

                    var badgeToTargetTime = targetTimesList
                        .GroupBy(t => t.EmpBadgeId)
                        .ToDictionary(g => g.Key, g => g.First());

                    // Call the optimized method
                    var timeClassService = new TimeClass();
                    timeClassService.UpdateTimeClass(importedMoves, badgeToWorkplace, badgeToTargetTime);

                    // ----------- INSERT-ONLY LOGIC WITH BATCHING -----------
                    if (importedMoves.Any())
                    {
                        const int batchSize = 200;
                        for (int i = 0; i < importedMoves.Count; i += batchSize)
                        {
                            var batch = importedMoves.Skip(i).Take(batchSize).ToList();
                            db.BFabMoves.AddRange(batch);
                            await db.SaveChangesAsync();
                            db.ChangeTracker.Entries<FabMove>().ToList().ForEach(e => e.State = EntityState.Detached);
                        }
                    }
                    // ----------- END INSERT-ONLY LOGIC -----------

                    // --- User Table Update After FabMove Import ---
                    var gmdsDict = gmdsList.ToDictionary(g => g.BadgeId, g => g);
                    var targetDict = targetTimesList.ToDictionary(t => t.EmpBadgeId, t => t);

                    var logweekService = new logweek();
                    var userSummaries = importedMoves
                        .GroupBy(fm => new { fm.EmpBadgeId, LogWeek = logweekService.GetLogWeek(fm.WorkDay) })
                        .Select(g =>
                        {
                            var badgeId = g.Key.EmpBadgeId;
                            var logWeek = g.Key.LogWeek;
                            var gmds = gmdsDict.ContainsKey(badgeId) ? gmdsDict[badgeId] : null;
                            var target = targetDict.ContainsKey(badgeId) ? targetDict[badgeId] : null;

                            int total = g.Count();
                            int passed = g.Count(fm => string.Equals(fm.TimeClass, "Passed", StringComparison.OrdinalIgnoreCase));
                            double avgPoints = total > 0 ? Math.Round((passed / (double)total) * 100, 2) : 0.0;

                            return new User
                            {
                                EmpBadgeId = badgeId,
                                EmpName = gmds?.Name ?? "-",
                                EmpDept = gmds?.Department ?? "-",
                                Group = target?.Group ?? "-",
                                Area = target?.Area ?? "-",
                                AreaDI = target?.AreaDI ?? "-",
                                LogWeek = logWeek,
                                Points = passed,
                                AvgPoints = avgPoints
                            };
                        })
                        .ToList();

                    var badgeLogWeeks = userSummaries.Select(u => new { u.EmpBadgeId, u.LogWeek }).ToList();
                    var badgeIds = badgeLogWeeks.Select(x => x.EmpBadgeId).Distinct().ToList();
                    var logWeeks = badgeLogWeeks.Select(x => x.LogWeek).Distinct().ToList();

                    var existingUsers = db.Users
                        .Where(u => badgeIds.Contains(u.EmpBadgeId) && logWeeks.Contains(u.LogWeek))
                        .ToList();

                    var newUsers = new List<User>();
                    foreach (var summary in userSummaries)
                    {
                        var existing = existingUsers.FirstOrDefault(u =>
                            u.EmpBadgeId == summary.EmpBadgeId && u.LogWeek == summary.LogWeek);

                        if (existing == null)
                        {
                            newUsers.Add(summary);
                        }
                        else
                        {
                            if (existing.EmpName != summary.EmpName ||
                                existing.EmpDept != summary.EmpDept ||
                                existing.Group != summary.Group ||
                                existing.Area != summary.Area ||
                                existing.AreaDI != summary.AreaDI ||
                                existing.Points != summary.Points ||
                                existing.AvgPoints != summary.AvgPoints)
                            {
                                existing.EmpName = summary.EmpName;
                                existing.EmpDept = summary.EmpDept;
                                existing.Group = summary.Group;
                                existing.Area = summary.Area;
                                existing.AreaDI = summary.AreaDI;
                                existing.Points = summary.Points;
                                existing.AvgPoints = summary.AvgPoints;
                            }
                        }
                    }
                    if (newUsers.Any())
                    {
                        db.Users.AddRange(newUsers);
                    }
                    await db.SaveChangesAsync();

                    // --- End User Table Update ---

                    // Always reload the uploadRecord from DB before updating RecordCount
                    var freshUploadRecord = db.UploadFileRecord.Find(uploadRecord.Id);
                    if (freshUploadRecord != null)
                    {
                        int actualCount = db.BFabMoves.Count(fm => fm.UploadFileRecordId == uploadRecord.Id);
                        freshUploadRecord.RecordCount = actualCount;
                        TempData["message"] = "Upload data fab movement successfully";
                        ClearPassedChartCache();
                        await db.SaveChangesAsync();
                    }

                    transaction.Commit();

                    // Webhook sending 
                    // Webhook sending
                    if (importedMoves.Any())
                    {
                        var webhookService = new WebhookService();

                        var badgeMonthGroups = importedMoves
                            .GroupBy(fm => new {
                                fm.EmpBadgeId,
                                Month = fm.WorkDay.Month,
                                Year = fm.WorkDay.Year
                            })
                            .ToList();

                        var allBadgeIds = badgeMonthGroups
                            .Select(g => g.Key.EmpBadgeId)
                            .Distinct()
                            .ToList();

                        var allTargetTimes = db.TargetTimes
                            .Where(t => allBadgeIds.Contains(t.EmpBadgeId))
                            .AsNoTracking()
                            .ToList();

                        var allGmds = master.gmds
                            .Where(g => allBadgeIds.Contains(g.BadgeId))
                            .AsNoTracking()
                            .ToList();

                        var targetDicts = allTargetTimes
                            .GroupBy(t => t.EmpBadgeId)
                            .ToDictionary(g => g.Key, g => g.First());

                        var gmdsDicts = allGmds
                            .GroupBy(g => g.BadgeId)
                            .ToDictionary(g => g.Key, g => g.First());

                        // --- Get all unique month ranges from importedMoves ---
                        var monthRanges = badgeMonthGroups
                            .Select(g => new
                            {
                                g.Key.EmpBadgeId,
                                g.Key.Month,
                                g.Key.Year,
                                MonthStart = new DateTime(g.Key.Year, g.Key.Month, 1),
                                MonthEnd = new DateTime(g.Key.Year, g.Key.Month,
                                    DateTime.DaysInMonth(g.Key.Year, g.Key.Month))
                            })
                            .ToList();

                        // --- Get overall date range to do SINGLE DB query ---
                        var overallStart = monthRanges.Min(r => r.MonthStart);
                        var overallEnd = monthRanges.Max(r => r.MonthEnd);

                        // Single DB query - fetch all relevant FabMoves at once
                        var allDbFabMoves = db.BFabMoves
                            .AsNoTracking()
                            .Where(fm =>
                                allBadgeIds.Contains(fm.EmpBadgeId) &&
                                fm.WorkDay >= overallStart &&
                                fm.WorkDay <= overallEnd)
                            .ToList();

                        // Build lookup dictionary: "badgeId_month_year" -> List<FabMove>
                        var fullMonthMoves = allDbFabMoves
                            .GroupBy(fm => $"{fm.EmpBadgeId}_{fm.WorkDay.Month}_{fm.WorkDay.Year}")
                            .ToDictionary(g => g.Key, g => g.ToList());

                        // Fire and Forget
                        _ = Task.Run(async () =>
                        {
                            const int maxParallel = 20;
                            using (var semaphore = new SemaphoreSlim(maxParallel))
                            {
                                var tasks = badgeMonthGroups.Select(async group =>
                                {
                                    var badgeId = group.Key.EmpBadgeId;
                                    var month = group.Key.Month;
                                    var year = group.Key.Year;
                                    var dictKey = $"{badgeId}_{month}_{year}";

                                    var fabMovesForMonth = fullMonthMoves.ContainsKey(dictKey)
                                        ? fullMonthMoves[dictKey]
                                        : new List<FabMove>();

                                    TargetTime target = targetDicts.ContainsKey(badgeId)
                                        ? targetDicts[badgeId] : null;

                                    tbl_GMDS gmds = gmdsDicts.ContainsKey(badgeId)
                                        ? gmdsDicts[badgeId] : null;

                                    await semaphore.WaitAsync();
                                    try
                                    {
                                        await webhookService.SendWebhookAsync(
                                            fabMovesForMonth, badgeId, month, year, target, gmds);
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                    }
                                }).ToList();

                                await Task.WhenAll(tasks);
                            }
                        });
                    }

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["ImportDebugMessages"] = $"Save changes error {ex}";
                    return RedirectToAction("Index");
                }
            }
        }

        // HELPER
        private void MapFabMoveData(FabMove fabMove, ExcelWorksheet worksheet, int row)
        {
            fabMove.EmpName = worksheet.Cells[row, 1].Text?.Trim();
            fabMove.EmpBadgeId = worksheet.Cells[row, 2].Text.Trim().ToUpper();
            fabMove.EmpDept = worksheet.Cells[row, 3].Text.Trim();
            fabMove.Designation = worksheet.Cells[row, 4].Text.Trim();
            fabMove.JobLevel = worksheet.Cells[row, 5].Text.Trim();
            fabMove.ShiftCode = worksheet.Cells[row, 6].Text.Trim();
            fabMove.Section = worksheet.Cells[row, 7].Text.Trim();
            fabMove.OrgUnit = worksheet.Cells[row, 8].Text.Trim();
            fabMove.CostCenter = worksheet.Cells[row, 9].Text.Trim();
            fabMove.Level1 = worksheet.Cells[row, 10].Text.Trim();

            var workDayCell = worksheet.Cells[row, 11].GetValue<DateTime?>();
            fabMove.WorkDay = workDayCell.HasValue ? workDayCell.Value.Date : DateTime.MinValue;

            fabMove.Day = worksheet.Cells[row, 12].Text.Trim();
            fabMove.Shift = worksheet.Cells[row, 13].Text.Trim();

            fabMove.FabIn = SafeGetTime(worksheet, row, 14);
            fabMove.FabOut = SafeGetTime(worksheet, row, 15);
            fabMove.FirstCrIn = SafeGetTime(worksheet, row, 16);
            fabMove.LastCrOut = SafeGetTime(worksheet, row, 17); 
            fabMove.FabTime = SafeGetTime(worksheet, row, 18);
            fabMove.CrTime = SafeGetTime(worksheet, row, 19);

            fabMove.BrTime = worksheet.Cells[row, 20].Text.Trim();
            fabMove.FabCount = worksheet.Cells[row, 21].Text.Trim();
            fabMove.CrCount = worksheet.Cells[row, 22].Text.Trim();
            fabMove.FabLateIn = worksheet.Cells[row, 23].Text.Trim();
            fabMove.FabEarlyOut = worksheet.Cells[row, 24].Text.Trim();
            var missingValue = worksheet.Cells[row, 25].Text.Trim().ToLowerInvariant();
            fabMove.Missing = missingValue == "yes" ? "yes" : "no";
        }

        // Helper for safe time extraction
        //private TimeSpan SafeGetTime(ExcelWorksheet ws, int row, int col)
        //{
        //    try
        //    {
        //        var cell = ws.Cells[row, col];
        //        var text = cell.Text?.Trim();

        //        // 1. Try parse as TimeSpan (handles "24:43" etc.)
        //        if (!string.IsNullOrEmpty(text) && TimeSpan.TryParse(text, out var ts))
        //        {
        //            var max = new TimeSpan(23, 59, 59);
        //            return ts > max ? max : ts;
        //        }

        //        // 2. Try parse as double (Excel stores time as fraction of a day)
        //        if (double.TryParse(text, out var dbl))
        //        {
        //            var tr = TimeSpan.FromDays(dbl);
        //            var max = new TimeSpan(23, 59, 59);
        //            return tr > max ? max : tr;
        //        }

        //        // 3. Try parse as DateTime (for normal Excel time cells)
        //        var dt = cell.GetValue<DateTime?>();
        //        if (dt.HasValue)
        //        {
        //            var t = dt.Value.TimeOfDay;
        //            var max = new TimeSpan(23, 59, 59);
        //            return t > max ? max : t;
        //        }
        //    }
        //    catch
        //    {
        //        // Optionally log: Debug.WriteLine($"Invalid time at row {row}, col {col}: '{ws.Cells[row, col].Text}'");
        //    }
        //    return TimeSpan.Zero;
        //}

        private TimeSpan SafeGetTime(ExcelWorksheet ws, int row, int col)
        {
            var max = new TimeSpan(23, 59, 59);
            try
            {
                var cell = ws.Cells[row, col];
                var raw = cell.Value;
                TimeSpan? result = null;

                // 1. Raw value first: independent of display format and regional settings
                if (raw is DateTime dt)
                    result = dt - new DateTime(1899, 12, 30);
                else if (raw is double d)
                    result = TimeSpan.FromDays(d);
                else if (raw is TimeSpan t)
                    result = t;
                else
                {
                    // 2. Text cells: accept "7:57", "07:57:00", "07.57", "03:00 PM"
                    var text = cell.Text?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (TimeSpan.TryParse(text.Replace('.', ':'), CultureInfo.InvariantCulture, out var ts))
                            result = ts;
                        else if (DateTime.TryParse(text.Replace('.', ':'), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
                            result = dt2.TimeOfDay;
                    }
                }

                if (result.HasValue)
                {
                    if (result.Value < TimeSpan.Zero) return TimeSpan.Zero;
                    return result.Value > max ? max : result.Value;
                }
            }
            catch { }
            return TimeSpan.Zero;
        }


        private static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int size)
        {
            var batch = new List<T>(size);
            foreach (var item in source)
            {
                batch.Add(item);
                if (batch.Count == size)
                {
                    yield return new List<T>(batch);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
                yield return batch;
        }

        private dynamic GetMasterData(string badgeId)
        {
            //Query master table for badgeId
            var masterData = master.gmds
                .Where(m => m.BadgeId == badgeId)
                .Select(m => new
                {
                    m.Gender,
                    m.Group,
                    m.Area,
                    m.AreaDI,
                    m.Religion,
                    m.Production,
                })
                .FirstOrDefault();
            return masterData;
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

        private void ClearFabMovesPageCache()
        {
            var cache = System.Web.HttpRuntime.Cache;
            var keysToRemove = new List<string>();

            foreach (System.Collections.DictionaryEntry entry in cache)
            {
                if (entry.Key is string key && key.StartsWith("FabMoves_Page_"))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                cache.Remove(key);
            }
        }
    }
}
