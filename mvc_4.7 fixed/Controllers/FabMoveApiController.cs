using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;          
using System.Threading.Tasks;
using System.Web.Http;           
using ok1.Models;
using ok1.Models.ApiResponse;
using ok1.Models.ApiResponse.FabMove;
using ok1.Data;
using FabReport.Filters;
using ok1.Services;
using System.Data.Entity;

namespace FabReport.Controllers.Api
{
    [ApiKeyAuthFilter]
    [RoutePrefix("api/fabmove")]
    public class FabMoveApiController : ApiController
    {
        private readonly ok1Context _db;
        private readonly masterContext _og;

        public FabMoveApiController()
        {
            _db = new ok1Context();
            _og = new masterContext();
        }

        [HttpGet]
        [Route("")]
        public async Task<IHttpActionResult> GetFabMove(
            string badgeId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string statusFilter = "failed",
            int page = 1,
            int pageSize = 10
        )
        {
            try
            {
                // Validate page
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                // Get Windows username from header
                IEnumerable<string> windowsUserValues;
                var windAcc = Request.Headers.TryGetValues("X-Windows-User", out windowsUserValues)
                    ? windowsUserValues.FirstOrDefault()
                    : null;

                // Strip domain prefix
                if (!string.IsNullOrEmpty(windAcc) && windAcc.Contains("\\"))
                {
                    windAcc = windAcc.Split('\\').Last();
                }

                if (string.IsNullOrEmpty(windAcc))
                {
                    return Content(
                        HttpStatusCode.Unauthorized,
                        new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Unauthorized.",
                            Error = "Windows User is required."
                        }
                    );
                }

                // Get user access
                var userAccess = new UserAccess(_db);
                bool isAdmin = userAccess.IsAdmin(windAcc);
                bool isManager = userAccess.IsManager(windAcc);

                // ============================================================
                // ACCESS CONTROL
                // ============================================================

                // Public user: must have both badgeId and date
                if (!isAdmin && !isManager)
                {
                    bool hasBadgeId = !string.IsNullOrEmpty(badgeId);
                    bool hasDate = fromDate.HasValue && toDate.HasValue;

                    if (!hasBadgeId || !hasDate)
                    {
                        return Content(
                            HttpStatusCode.Forbidden,
                            new ApiResponse<object>
                            {
                                Success = false,
                                Message = !hasBadgeId
                                    ? "Please input Badge ID to view data."
                                    : "Please input date range (From & To) to view data.",
                                Data = null
                            }
                        );
                    }
                }

                // Admin/Manager: must have at least one filter
                if (isAdmin || isManager)
                {
                    bool hasAnyFilter = !string.IsNullOrEmpty(badgeId)
                                     || fromDate.HasValue
                                     || toDate.HasValue;

                    if (!hasAnyFilter)
                    {
                        return Content(
                            HttpStatusCode.Forbidden,
                            new ApiResponse<object>
                            {
                                Success = false,
                                Message = "Please input at least one filter (Badge ID or Date) to search data.",
                                Data = null
                            }
                        );
                    }
                }

                // Adjust toDate to end of day
                if (toDate.HasValue)
                {
                    toDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                }

                // ============================================================
                // GET MANAGER BADGE IDs
                // ============================================================
                List<string> managerBadgeIds = null;

                if (isManager && !isAdmin)
                {
                    var managerRecords = await _db.ManagerList
                        .AsNoTracking()
                        .Where(m => !string.IsNullOrEmpty(m.Manager))
                        .Select(m => new { m.Manager, m.BadgeId })
                        .ToListAsync();

                    managerBadgeIds = managerRecords
                        .Where(m => System.Text.RegularExpressions.Regex
                            .Split(m.Manager, @"\$+")
                            .Select(x => x.Trim().ToLower())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .Contains(windAcc))
                        .Select(m => m.BadgeId)
                        .Distinct()
                        .ToList();

                    // Add own badge ID
                    var ownBadgeId = await _og.gmds
                        .Where(g => g.WindowsAccount.Trim().ToLower() == windAcc)
                        .Select(g => g.BadgeId)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(ownBadgeId) && !managerBadgeIds.Contains(ownBadgeId))
                    {
                        managerBadgeIds.Add(ownBadgeId);
                    }

                    // Check if specific badge is accessible
                    if (!string.IsNullOrEmpty(badgeId) && !managerBadgeIds.Contains(badgeId.Trim()))
                    {
                        return Content(
                            HttpStatusCode.Forbidden,
                            new ApiResponse<object>
                            {
                                Success = false,
                                Message = "You don't have access to this badge id.",
                                Data = null
                            }
                        );
                    }

                    if (!managerBadgeIds.Any())
                    {
                        return Content(
                            HttpStatusCode.Forbidden,
                            new ApiResponse<object>
                            {
                                Success = false,
                                Message = "No team members found under your account.",
                                Data = null
                            }
                        );
                    }
                }

                // ============================================================
                // BUILD SQL
                // ============================================================
                int offset = (page - 1) * pageSize;

                string badgeIdFilterSql = "";
                if (managerBadgeIds != null && managerBadgeIds.Any())
                {
                    var badgeParamNames = managerBadgeIds
                        .Select((id, i) => $"@bid{i}")
                        .ToList();
                    badgeIdFilterSql = $"AND [EmpBadgeId] IN ({string.Join(",", badgeParamNames)})";
                }
                else if (!string.IsNullOrEmpty(badgeId))
                {
                    badgeIdFilterSql = "AND [EmpBadgeId] = @specificBadgeId";
                }

                string sqlQuery = $@"
                    SELECT [Id],[EmpBadgeId],[EmpName],[EmpDept],[Designation],[JobLevel],
                           [ShiftCode],[Section],[OrgUnit],[CostCenter],[Level1],[WorkDay],
                           [Day],[Shift],[FabIn],[FabOut],[FirstCrIn],[LastCrOut],[FabTime],
                           [CrTime],[BrTime],[FabCount],[CrCount],[FabLateIn],[FabEarlyOut],
                           [PublicHoliday],[IsValid],[Workplace],[Gender],[Religion],
                           [FridayPray],[Busui],[Ahm],[Amend],[ExitPermit],[TargetTime],
                           [TimeClass],[Group],[Area],[AreaDI],[UploadFileRecordId],[Missing]
                    FROM [Discipline_Index].[FAB_Report].[FabMoves]
                    WHERE
                        (@fromDate IS NULL OR [WorkDay] >= @fromDate) AND
                        (@toDate IS NULL OR [WorkDay] < @toDate) AND
                        (@searchString IS NULL OR [EmpBadgeId] = @searchString) AND
                        (@statusFilter IS NULL OR @statusFilter = 'all' OR LOWER([TimeClass]) = @statusFilter)
                        {badgeIdFilterSql}
                    ORDER BY [WorkDay] ASC
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

                string countQuery = $@"
                    SELECT COUNT(*)
                    FROM [Discipline_Index].[FAB_Report].[FabMoves]
                    WHERE
                        (@fromDate IS NULL OR [WorkDay] >= @fromDate) AND
                        (@toDate IS NULL OR [WorkDay] < @toDate) AND
                        (@searchString IS NULL OR [EmpBadgeId] = @searchString) AND
                        (@statusFilter IS NULL OR @statusFilter = 'all' OR LOWER([TimeClass]) = @statusFilter)
                        {badgeIdFilterSql}";

                // ============================================================
                // BUILD PARAMETERS
                // ============================================================
                Func<List<SqlParameter>> buildBadgeParams = () =>
                {
                    var list = new List<SqlParameter>();
                    if (managerBadgeIds != null && managerBadgeIds.Any())
                    {
                        for (int i = 0; i < managerBadgeIds.Count; i++)
                            list.Add(new SqlParameter($"@bid{i}", managerBadgeIds[i]));
                    }
                    else if (!string.IsNullOrEmpty(badgeId))
                    {
                        list.Add(new SqlParameter("@specificBadgeId", badgeId));
                    }
                    return list;
                };

                var sqlParams = new List<SqlParameter>
                {
                    new SqlParameter("@fromDate",     (object)fromDate     ?? DBNull.Value),
                    new SqlParameter("@toDate",       (object)toDate       ?? DBNull.Value),
                    new SqlParameter("@searchString", (object)badgeId      ?? DBNull.Value),
                    new SqlParameter("@statusFilter", (object)statusFilter ?? DBNull.Value),
                    new SqlParameter("@offset",       offset),
                    new SqlParameter("@pageSize",     pageSize)
                };
                sqlParams.AddRange(buildBadgeParams());

                var countParams = new List<SqlParameter>
                {
                    new SqlParameter("@fromDate",     (object)fromDate     ?? DBNull.Value),
                    new SqlParameter("@toDate",       (object)toDate       ?? DBNull.Value),
                    new SqlParameter("@searchString", (object)badgeId      ?? DBNull.Value),
                    new SqlParameter("@statusFilter", (object)statusFilter ?? DBNull.Value)
                };
                countParams.AddRange(buildBadgeParams());

                // ============================================================
                // EXECUTE
                // ============================================================
                var fabMoves = await _db.Database
                    .SqlQuery<FabMove>(sqlQuery, sqlParams.ToArray())
                    .ToListAsync();

                int totalRecords = await _db.Database
                    .SqlQuery<int>(countQuery, countParams.ToArray())
                    .FirstOrDefaultAsync();

                int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                // ============================================================
                // MAP TO DTO - Include color classes
                // ============================================================
                var dtoList = fabMoves.Select(x => new FabMoveDto
                {
                    empBadgeId = x.EmpBadgeId,
                    empName = x.EmpName,
                    empDept = x.EmpDept,
                    designation = x.Designation,
                    jobLevel = x.JobLevel,
                    shiftCode = x.ShiftCode,
                    section = x.Section,
                    orgUnit = x.OrgUnit,
                    costCenter = x.CostCenter,
                    level1 = x.Level1,
                    workDay = x.WorkDay.ToString("yyyy-MM-dd"),
                    day = x.Day,
                    shift = x.Shift,
                    fabIn = x.FabIn.ToString(@"hh\:mm\:ss"),
                    fabOut = x.FabOut.ToString(@"hh\:mm\:ss"),
                    firstCrIn = x.FirstCrIn.ToString(@"hh\:mm\:ss"),
                    lastCrOut = x.LastCrOut.ToString(@"hh\:mm\:ss"),
                    fabTime = x.FabTime.ToString(@"hh\:mm\:ss"),
                    crTime = x.CrTime.ToString(@"hh\:mm\:ss"),
                    brTime = x.BrTime,
                    fabCount = x.FabCount,
                    crCount = x.CrCount,
                    fabLateIn = x.FabLateIn,
                    fabEarlyOut = x.FabEarlyOut,
                    group = x.Group,
                    area = x.Area,
                    areaDI = x.AreaDI,
                    publicHoliday = x.PublicHoliday,
                    isValid = x.IsValid,
                    workplace = x.Workplace,
                    gender = x.Gender,
                    religion = x.Religion,
                    fridayPray = x.FridayPray,
                    busui = x.Busui.ToString(@"hh\:mm\:ss"),
                    ahm = x.Ahm,
                    amend = x.Amend,
                    exitPermit = x.ExitPermit,
                    targetTime = x.TargetTime,
                    timeClass = x.TimeClass,
                    missing = x.Missing,

                    // Color classes computed server side
                    rowClass = GetRowClass(x.Missing),
                    fabInClass = GetFabInClass(x.Shift, x.FabIn),
                    fabOutClass = GetFabOutClass(x.Shift, x.Day, x.FabOut),
                    firstCrInClass = GetFirstCrInClass(x.Shift, x.FirstCrIn),
                    lastCrOutClass = GetLastCrOutClass(x.Shift, x.Day, x.LastCrOut),
                    crTimeClass = GetCrTimeClass(x.TimeClass)

                }).ToList();

                return Ok(new ApiResponse<List<FabMoveDto>>
                {
                    Success = true,
                    Message = totalRecords == 0
                        ? "Data not found."
                        : "Data retrieved successfully.",
                    Data = dtoList,
                    Pagination = new PaginationMeta
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalRecords = totalRecords,
                        TotalPages = totalPages,
                        HasPreviousPage = page > 1,
                        HasNextPage = page < totalPages
                    }
                });
            }
            catch (Exception ex)
            {
                return Content(
                    HttpStatusCode.InternalServerError,
                    new ApiResponse<object>
                    {
                        Success = false,
                        Message = "An unexpected error occurred.",
                        Error = ex.Message + " | " + ex.InnerException?.Message
                    }
                );
            }
        }

        // ============================================================
        // Color Class Helpers - Same logic as View
        // ============================================================
        private string GetFabInClass(string shift, TimeSpan fabIn)
        {
            if (string.IsNullOrWhiteSpace(shift)) return "";
            shift = shift.Trim().ToLower();
            switch (shift)
            {
                case "morning shift": return fabIn > new TimeSpan(7, 0, 0) ? "cell-red" : "";
                case "afternoon shift": return fabIn > new TimeSpan(15, 45, 0) ? "cell-red" : "";
                case "night shift": return fabIn > new TimeSpan(23, 25, 0) ? "cell-red" : "";
                case "normal shift": return fabIn > new TimeSpan(7, 45, 0) ? "cell-red" : "";
            }
            return "";
        }

        private string GetFabOutClass(string shift, string day, TimeSpan fabOut)
        {
            if (string.IsNullOrWhiteSpace(shift)) return "";
            shift = shift.Trim().ToLower();
            day = day?.Trim().ToLower() ?? "";
            switch (shift)
            {
                case "morning shift":
                    return fabOut < new TimeSpan(15, 45, 0) ? "cell-red" : "";
                case "afternoon shift":
                    return (fabOut >= new TimeSpan(15, 45, 0) && fabOut < new TimeSpan(23, 25, 0))
                        ? "cell-red" : "";
                case "night shift":
                    return fabOut < new TimeSpan(7, 0, 0) ? "cell-red" : "";
                case "normal shift":
                    return day == "friday"
                        ? fabOut < new TimeSpan(16, 15, 0) ? "cell-red" : ""
                        : fabOut < new TimeSpan(17, 0, 0) ? "cell-red" : "";
            }
            return "";
        }

        private string GetFirstCrInClass(string shift, TimeSpan firstCrIn)
        {
            if (string.IsNullOrWhiteSpace(shift)) return "";
            shift = shift.Trim().ToLower();
            switch (shift)
            {
                case "morning shift": return firstCrIn > new TimeSpan(6, 55, 0) ? "cell-yellow" : "";
                case "afternoon shift": return firstCrIn > new TimeSpan(15, 40, 0) ? "cell-yellow" : "";
                case "night shift": return firstCrIn > new TimeSpan(23, 20, 0) ? "cell-yellow" : "";
                case "normal shift": return firstCrIn > new TimeSpan(7, 40, 0) ? "cell-yellow" : "";
            }
            return "";
        }

        private string GetLastCrOutClass(string shift, string day, TimeSpan lastCrOut)
        {
            if (string.IsNullOrWhiteSpace(shift)) return "";
            shift = shift.Trim().ToLower();
            day = day?.Trim().ToLower() ?? "";
            switch (shift)
            {
                case "morning shift":
                    return lastCrOut < new TimeSpan(15, 45, 0) ? "cell-yellow" : "";
                case "afternoon shift":
                    return (lastCrOut > new TimeSpan(15, 45, 0) && lastCrOut < new TimeSpan(23, 25, 0))
                        ? "cell-yellow" : "";
                case "night shift":
                    return lastCrOut < new TimeSpan(7, 0, 0) ? "cell-yellow" : "";
                case "normal shift":
                    return day == "friday"
                        ? lastCrOut < new TimeSpan(16, 15, 0) ? "cell-yellow" : ""
                        : lastCrOut < new TimeSpan(17, 0, 0) ? "cell-yellow" : "";
            }
            return "";
        }

        private string GetCrTimeClass(string timeClass)
        {
            return (!string.IsNullOrEmpty(timeClass) &&
                    timeClass.Trim().ToLower() == "failed")
                ? "cell-red" : "";
        }

        private string GetRowClass(string missing)
        {
            return string.Equals(missing, "yes", StringComparison.OrdinalIgnoreCase)
                ? "cell-orange" : "";
        }
    }
}