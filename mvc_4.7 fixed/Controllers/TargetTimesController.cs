using System;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using OfficeOpenXml;
using ok1.Data;
using ok1.Models;
using PagedList;

namespace ok1.Controllers
{
    [FeatureAuthorize("TargetTimes")]

    public class TargetTimesController : Controller
    {
        private ok1Context db = new ok1Context();
        private masterContext og = new masterContext();

        // GET: TargetTimes
        public ActionResult Index(string sortOrder, string searchBadgeId, string searchDept, string currentFilter, int? page = 1)
        {
            ViewBag.BadgeSortParm = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.DeptSortParm = String.IsNullOrEmpty(sortOrder) ? "dept_desc" : "";

            if (searchBadgeId != null || searchDept != null)
            {
                page = 1;
            }
            else
            {
                searchBadgeId = currentFilter; // to keep the last search
            }

            ViewBag.CurrentBadgeFilter = searchBadgeId;
            ViewBag.CurrentDeptFilter = searchDept;

            var targets = from t in db.TargetTimes
                          select t;

            // Search by badge ID and department
            if (!String.IsNullOrEmpty(searchBadgeId))
            {
                targets = targets.Where(t => t.EmpBadgeId.Contains(searchBadgeId));
            }

            if (!String.IsNullOrEmpty(searchDept))
            {
                targets = targets.Where(t => t.EmpDept.Contains(searchDept));
            }

            switch (sortOrder)
            {
                case "name_desc":
                    targets = targets.OrderByDescending(t => t.EmpBadgeId);
                    break;
                case "dept_desc":
                    targets = targets.OrderByDescending(t => t.EmpDept);
                    break;
                default:  // Name ascending 
                    targets = targets.OrderBy(s => s.EmpBadgeId);
                    break;
            }

            int pageSize = 20;
            var pageNumber = (page ?? 1);

            var targetTime = targets.ToPagedList(pageNumber, pageSize);
            return View(targetTime);
        }
        // GET: TargetTimes/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TargetTime targetTime = db.TargetTimes.Find(id);
            if (targetTime == null)
            {
                return HttpNotFound();
            }
            return View(targetTime);
        }
        [HttpGet]
        public JsonResult GetEmpData(string empBadgeId)
        {
            //as user requested, area, group area, and area details will be manual input. 
            //not fetch from gmds
            var employee = og.gmds.Where(e => e.BadgeId == empBadgeId)
                .Select(e => new
                {
                    EmpBadgeId = e.BadgeId,
                    EmpName = e.Name,
                    EmpDept = e.Department,
                }).FirstOrDefault();
            return Json(employee, JsonRequestBehavior.AllowGet);
        }

        // GET: TargetTimes/Create
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "Id,EmpBadgeId,EmpName,EmpDept,NormalShift,MorningShift,AfternoonShift,NightShift,Group,AreaDI,Area")] TargetTime targetTime)
        {
            if (ModelState.IsValid)
            {
                //check if data existed by empBadgeId, auto update
                var existingTargetTime = await db.TargetTimes.FirstOrDefaultAsync(tt => tt.EmpBadgeId == targetTime.EmpBadgeId);

                if (existingTargetTime != null)
                {
                    // Update existing record
                    existingTargetTime.EmpName = targetTime.EmpName;
                    existingTargetTime.EmpDept = targetTime.EmpDept;
                    existingTargetTime.NormalShift = targetTime.NormalShift;
                    existingTargetTime.MorningShift = targetTime.MorningShift;
                    existingTargetTime.AfternoonShift = targetTime.AfternoonShift;
                    existingTargetTime.NightShift = targetTime.NightShift;
                    existingTargetTime.Group = targetTime.Group;
                    existingTargetTime.AreaDI = targetTime.AreaDI;
                    existingTargetTime.Area = targetTime.Area;
                }
                else
                {
                    // Add new record
                    db.TargetTimes.Add(targetTime);
                }
                await db.SaveChangesAsync();
                TempData["message"] = " Successfully added target time!";

                return RedirectToAction("Index");
            }
            TempData["ErrorMessage"] = "Something went wrong!";
            return View(targetTime);
        }

        //POST: TargetTime/UploadFile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Import(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                return await Task.FromResult<ActionResult>(BadRequest("No file uploaded"));
            }
            //check if data existed by empBadgeId, auto update with newest
            using (var stream = new MemoryStream())
            {
                //await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(file.InputStream))
                {
                    var worksheet = package.Workbook.Worksheets.First();
                    var rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        // Check if the first cell (or any other relevant cell) in the row is empty
                        if (string.IsNullOrWhiteSpace(worksheet.Cells[row, 1].Text))
                        {
                            // Break the loop if the first cell is empty, assuming it's the EmpBadgeId
                            break;
                        }

                        var empBadgeId = worksheet.Cells[row, 1].Text;
                        var existingTargetTime = await db.TargetTimes.FirstOrDefaultAsync(tt => tt.EmpBadgeId == empBadgeId);

                        if (existingTargetTime != null)
                        {
                            existingTargetTime.EmpBadgeId = worksheet.Cells[row, 1].Text;
                            existingTargetTime.EmpName = worksheet.Cells[row, 2].Text;
                            existingTargetTime.EmpDept = worksheet.Cells[row, 3].Text;
                            existingTargetTime.NormalShift = worksheet.Cells[row, 4].GetValue<DateTime>().TimeOfDay;
                            existingTargetTime.MorningShift = worksheet.Cells[row, 5].GetValue<DateTime>().TimeOfDay;
                            existingTargetTime.AfternoonShift = worksheet.Cells[row, 6].GetValue<DateTime>().TimeOfDay;
                            existingTargetTime.NightShift = worksheet.Cells[row, 7].GetValue<DateTime>().TimeOfDay;
                            existingTargetTime.Group = worksheet.Cells[row, 8].Text;
                            existingTargetTime.Area = worksheet.Cells[row, 9].Text;
                            existingTargetTime.AreaDI = worksheet.Cells[row, 10].Text;
                        }
                        else
                        {
                            var targetTime = new TargetTime
                            {
                                EmpBadgeId = worksheet.Cells[row, 1].Text,
                                EmpName = worksheet.Cells[row, 2].Text,
                                EmpDept = worksheet.Cells[row, 3].Text,
                                NormalShift = worksheet.Cells[row, 4].GetValue<DateTime>().TimeOfDay,
                                MorningShift = worksheet.Cells[row, 5].GetValue<DateTime>().TimeOfDay,
                                AfternoonShift = worksheet.Cells[row, 6].GetValue<DateTime>().TimeOfDay,
                                NightShift = worksheet.Cells[row, 7].GetValue<DateTime>().TimeOfDay,
                                Group = worksheet.Cells[row, 8].Text,
                                Area = worksheet.Cells[row, 9].Text,
                                AreaDI = worksheet.Cells[row, 10].Text,
                            };
                            db.TargetTimes.Add(targetTime);
                        }
                        //return View("Index", targetTime);
                        await db.SaveChangesAsync();
                    }
                }
                return RedirectToAction(nameof(Index));
            }
        }

        //GET: TargetTime/ExportFile
        [HttpGet]
        public ActionResult Export(HttpPostedFileBase file)
        {
            var targetTime = db.TargetTimes.ToList();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("TargetTimes");

                worksheet.Cells["A1"].Value = "Badge ID";
                worksheet.Cells["B1"].Value = "Name";
                worksheet.Cells["C1"].Value = "Department";
                worksheet.Cells["D1"].Value = "Normal Shift";
                worksheet.Cells["E1"].Value = "Morning Shift";
                worksheet.Cells["F1"].Value = "Afternoon Shift";
                worksheet.Cells["G1"].Value = "Night Shift";
                worksheet.Cells["H1"].Value = "Group Area";
                worksheet.Cells["I1"].Value = "Area";
                worksheet.Cells["J1"].Value = "Detail Area";

                int row = 2;

                foreach (var tarTime in targetTime)
                {
                    worksheet.Cells[row, 1].Value = tarTime.EmpBadgeId;
                    worksheet.Cells[row, 2].Value = tarTime.EmpName;
                    worksheet.Cells[row, 3].Value = tarTime.EmpDept;
                    worksheet.Cells[row, 4].Value = tarTime.NormalShift.ToString();
                    worksheet.Cells[row, 5].Value = tarTime.MorningShift.ToString();
                    worksheet.Cells[row, 6].Value = tarTime.AfternoonShift.ToString();
                    worksheet.Cells[row, 7].Value = tarTime.NightShift.ToString();
                    worksheet.Cells[row, 8].Value = tarTime.Group;
                    worksheet.Cells[row, 9].Value = tarTime.Area;
                    worksheet.Cells[row, 10].Value = tarTime.AreaDI;

                    row++;
                }
                worksheet.DefaultColWidth = 20;

                var stream = new MemoryStream();
                TempData["message"] = "Upload data successful";
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = "TargetTime.xlsx";
                string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                return File(stream, contentType, fileName);
            }

        }

        public ActionResult Template(HttpPostedFileBase file)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("TargetTimes");

                //set header
                worksheet.Cells["A1"].Value = "Badge ID";
                worksheet.Cells["B1"].Value = "Name";
                worksheet.Cells["C1"].Value = "Department";
                worksheet.Cells["D1"].Value = "Normal Shift";
                worksheet.Cells["E1"].Value = "Morning Shift";
                worksheet.Cells["F1"].Value = "Afternoon Shift";
                worksheet.Cells["G1"].Value = "Night Shift";
                worksheet.Cells["H1"].Value = "Group Area";
                worksheet.Cells["I1"].Value = "Area";
                worksheet.Cells["J1"].Value = "Detail Area";

                //set column width
                worksheet.DefaultColWidth = 20;

                //set format datetime
                //should be checked if it's works or received as another data type
                worksheet.Cells["D1"].Style.Numberformat.Format = "hh:mm:ss";
                worksheet.Cells["E1"].Style.Numberformat.Format = "hh:mm:ss";
                worksheet.Cells["F1"].Style.Numberformat.Format = "hh:mm:ss";
                worksheet.Cells["G1"].Style.Numberformat.Format = "hh:mm:ss";

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = "Template_TargetTime.xlsx";
                string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                return File(stream, contentType, fileName);
            }
        }


        private ActionResult BadRequest(string v)
        {
            throw new NotImplementedException();
        }


        // GET: TargetTimes/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TargetTime targetTime = db.TargetTimes.Find(id);
            if (targetTime == null)
            {
                return HttpNotFound();
            }
            return View(targetTime);
        }
        //Action Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,EmpBadgeId,EmpName,EmpDept,NormalShift,MorningShift,AfternoonShift,NightShift,Group,Area,AreaDI")] TargetTime targetTime)
        {
            if (ModelState.IsValid)
            {
                db.Entry(targetTime).State = EntityState.Modified;
                db.SaveChanges();
                TempData["message"] = "Update target time successful!";

                return RedirectToAction("Index");
            }
            TempData["ErrorMessage"] = "Something went wrong";
            return View(targetTime);
        }
        // Delete Action
        [HttpPost]
        public ActionResult DeleteTarget(int? id)
        {
            if (!id.HasValue)
            {
                return Json(new { success = false, message = "Invalid ID." });
            }

            var checkTime = db.TargetTimes.Find(id);
            if (checkTime == null)
            {
                return Json(new { success = false, message = "Record not found." });
            }

            try
            {
                db.TargetTimes.Remove(checkTime);
                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Failed to delete: " + ex.Message });
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
