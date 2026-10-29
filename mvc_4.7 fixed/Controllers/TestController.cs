using Newtonsoft.Json;
using ok1.Data;
using ok1.Models;
using ok1.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace ok1.Controllers
{
    public class TestController : Controller
    {
        private ok1Context db = new ok1Context();
        private masterContext master = new masterContext();

        // Dummy data
        private const string BadgeId1 = "132435";
        private const string BadgeId2 = "678901";
        private const int TestMonth = 6;
        private const int TestYear = 2026;

        // GET: Test/WebhookFabMove
        public async Task<ActionResult> WebhookFabMove(string badgeId, int month, int year)
        {
            if (string.IsNullOrEmpty(badgeId) || month < 1 || month > 12 || year < 1)
            {
                return Content("Invalid parameters. " +
                                "Usage: /Test/WebhookFabMove?badgeId=XXX&month=1&year=2025");
            }

            var fabMoves = db.BFabMoves
                .Where(fm => fm.EmpBadgeId == badgeId
                           && fm.WorkDay.Month == month
                           && fm.WorkDay.Year == year)
                .AsNoTracking()
                .ToList();

            if (!fabMoves.Any())
            {
                return Content($"No fab move data found for " +
                               $"badgeId{badgeId}, month={month}, year={year}");
            }

            var target = db.TargetTimes
                .AsNoTracking()
                .FirstOrDefault(t => t.EmpBadgeId == badgeId);

            var gmds = master.gmds
                .AsNoTracking()
                .FirstOrDefault(g => g.BadgeId == badgeId);

            // Calculate total & passed
            int total = fabMoves.Count;
            int passed = fabMoves.Count(fm =>
                string.Equals(fm.TimeClass, "Passed", StringComparison.OrdinalIgnoreCase));
            int failed = total - passed;

            try
            {
                var webhookService = new WebhookService();
                await webhookService.SendWebhookAsync(
                    fabMoves, badgeId, month, year, target, gmds);

                return Content(
                    $"Webhook sent successfully.\n" +
                    $"BadgeId  : {badgeId}\n" +
                    $"Month    : {month}\n" +
                    $"Year     : {year}\n" +
                    $"Total    : {total}\n" +
                    $"Passed   : {passed}\n" +
                    $"Failed   : {failed}\n" +
                    $"Target   : {(target != null ? "Found" : "Not Found")}\n" +
                    $"GMDS     : {(gmds != null ? gmds.Name : "Not Found")}",
                    "text/plain");
            }
            catch (Exception ex)
            {
                return Content(
                    $"Webhook failed.\n" +
                    $"BadgeId  : {badgeId}\n" +
                    $"Error    : {ex.Message}",
                    "text/plain");
            }
        }

        // GET: Test/WebhookFabMoveDummy
        public async Task<ActionResult> WebhookFabMoveDummy()
        {
            var dummyFabMoves1 = new List<FabMove>
            {
                new FabMove
                {
                    EmpBadgeId  = BadgeId1,
                    EmpName     = "Kyle",
                    WorkDay     = new DateTime(TestYear, TestMonth, 1),
                    TimeClass   = "Passed",
                    AreaDI      = "Area A",
                    Area        = "Zone 1",
                    Group       = "Morning",
                    Workplace   = "Production"
                },
                new FabMove
                {
                    EmpBadgeId  = BadgeId1,
                    EmpName     = "Kyle",
                    WorkDay     = new DateTime(TestYear, TestMonth, 2),
                    TimeClass   = "Failed",
                    AreaDI      = "Area A",
                    Area        = "Zone 1",
                    Group       = "Morning",
                    Workplace   = "Production"
                },
                new FabMove
                {
                    EmpBadgeId  = BadgeId1,
                    EmpName     = "Kyle",
                    WorkDay     = new DateTime(TestYear, TestMonth, 3),
                    TimeClass   = "Passed",
                    AreaDI      = "Area A",
                    Area        = "Zone 1",
                    Group       = "Morning",
                    Workplace   = "Production"
                }
            };

            var dummyFabMoves2 = new List<FabMove>
            {
                new FabMove
                {
                    EmpBadgeId  = BadgeId2,
                    EmpName     = "Kall",
                    WorkDay     = new DateTime(TestYear, TestMonth, 1),
                    TimeClass   = "Failed",
                    AreaDI      = "Area B",
                    Area        = "Zone 2",
                    Group       = "Night",
                    Workplace   = "Production"
                },
                new FabMove
                {
                    EmpBadgeId  = BadgeId2,
                    EmpName     = "Kall",
                    WorkDay     = new DateTime(TestYear, TestMonth, 2),
                    TimeClass   = "Failed",
                    AreaDI      = "Area B",
                    Area        = "Zone 2",
                    Group       = "Night",
                    Workplace   = "Production"
                },
                new FabMove
                {
                    EmpBadgeId  = BadgeId2,
                    EmpName     = "Kall",
                    WorkDay     = new DateTime(TestYear, TestMonth, 3),
                    TimeClass   = "Passed",
                    AreaDI      = "Area B",
                    Area        = "Zone 2",
                    Group       = "Night",
                    Workplace   = "Production"
                }
            };

            // Dummy target & gmds - no DB query needed
            var dummyTarget1 = new TargetTime
            {
                EmpBadgeId = BadgeId1,
                Area = "Zone 1",
                AreaDI = "Area A",
                Group = "Morning"
            };
            var dummyTarget2 = new TargetTime
            {
                EmpBadgeId = BadgeId2,
                Area = "Zone 2",
                AreaDI = "Area B",
                Group = "Night"
            };

            var dummyGmds1 = new tbl_GMDS
            {
                BadgeId = BadgeId1,
                Name = "Kyle",
                Department = "IT",
                Designation = "Staff",
                Group = "Morning"
            };
            var dummyGmds2 = new tbl_GMDS
            {
                BadgeId = BadgeId2,
                Name = "Kall",
                Department = "IT",
                Designation = "Staff",
                Group = "Night"
            };

            var results = new List<string>();
            var webhookService = new WebhookService();

            // Sequential - badge1 dulu, baru badge2
            results.Add(await RunFabMoveTest(
                webhookService, dummyFabMoves1, BadgeId1, TestMonth, TestYear, dummyTarget1, dummyGmds1));

            results.Add(await RunFabMoveTest(
                webhookService, dummyFabMoves2, BadgeId2, TestMonth, TestYear, dummyTarget2, dummyGmds2));

            // Build response
            var sb = new StringBuilder();
            sb.AppendLine("=== FabMove Dummy Webhook Test ===");
            sb.AppendLine($"Timestamp : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Month/Year: {TestMonth}/{TestYear}");
            sb.AppendLine("==================================");

            foreach (var result in results)
            {
                sb.AppendLine(result);
            }

            sb.AppendLine("==================================");
            sb.AppendLine($"Total  : {results.Count} tests");
            sb.AppendLine($"Success: {results.Count(r => r.Contains("✅"))}");
            sb.AppendLine($"Failed : {results.Count(r => r.Contains("❌"))}");

            return Content(sb.ToString(), "text/plain");
        }

        private async Task<string> RunFabMoveTest(
            WebhookService webhookService,
            List<FabMove> fabMoves,
            string badgeId,
            int month,
            int year,
            TargetTime target,
            tbl_GMDS gmds
        )
        {
            try
            {
                int total = fabMoves.Count;
                int passed = fabMoves.Count(fm =>
                    string.Equals(fm.TimeClass, "Passed", StringComparison.OrdinalIgnoreCase));
                int failed = total - passed;

                await webhookService.SendWebhookAsync(
                    fabMoves, badgeId, month, year, target, gmds);

                return $"FabMove | badgeId={badgeId} | " +
                       $"Total={total} Passed={passed} Failed={failed} | Success";
            }
            catch (Exception ex)
            {
                return $"FabMove | badgeId={badgeId} | Error: {ex.Message}";
            }
        }

        [HttpGet]
        public async Task<ActionResult> ResendWebhook(int month, int year)
        {
            try
            {
                if (month < 1 || month > 12 || year < 2000 || year > 2100)
                {
                    return Content("Invalid month or year.");
                }

                var monthStart = new DateTime(year, month, 1);
                var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

                // 1. Fetch FabMove data for target month/year
                var fabMoves = db.BFabMoves
                    .AsNoTracking()
                    .Where(fm => fm.WorkDay >= monthStart && fm.WorkDay <= monthEnd)
                    .ToList();

                if (!fabMoves.Any())
                {
                    return Content($"No FabMove data found for {month}/{year}.");
                }

                // 2. Get distinct BadgeIds
                var badgeIds = fabMoves
                    .Select(fm => fm.EmpBadgeId)
                    .Distinct()
                    .ToList();

                // 3. Fetch reference data
                var targetTimes = db.TargetTimes
                    .AsNoTracking()
                    .Where(t => badgeIds.Contains(t.EmpBadgeId))
                    .ToList();

                var gmdsList = master.gmds
                    .AsNoTracking()
                    .Where(g => badgeIds.Contains(g.BadgeId))
                    .ToList();

                // 4. Build dictionaries
                var targetDict = targetTimes
                    .GroupBy(t => t.EmpBadgeId)
                    .ToDictionary(g => g.Key, g => g.First());

                var gmdsDict = gmdsList
                    .GroupBy(g => g.BadgeId)
                    .ToDictionary(g => g.Key, g => g.First());

                // 5. Group by BadgeId
                var groupedByBadge = fabMoves
                    .GroupBy(fm => fm.EmpBadgeId)
                    .ToList();

                // 6. Fire and forget
                _ = Task.Run(async () =>
                {
                    var webhookService = new WebhookService();
                    const int maxParallel = 20;

                    using (var semaphore = new SemaphoreSlim(maxParallel))
                    {
                        var tasks = groupedByBadge.Select(async group =>
                        {
                            var badgeId = group.Key;
                            var movesForBadge = group.ToList();

                            targetDict.TryGetValue(badgeId, out var target);
                            gmdsDict.TryGetValue(badgeId, out var gmds);

                            await semaphore.WaitAsync();
                            try
                            {
                                await webhookService.SendWebhookAsync(
                                    movesForBadge, badgeId, month, year, target, gmds);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }).ToList();

                        await Task.WhenAll(tasks);
                    }
                });

                return Content($"Webhook triggered for {month}/{year}. " +
                               $"Records: {fabMoves.Count}, " +
                               $"Employees: {badgeIds.Count}.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResendWebhook] Exception: {ex}");
                return Content($"Error: {ex.Message}");
            }
        }
    }
}