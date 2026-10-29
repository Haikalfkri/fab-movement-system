using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;
using System.Xml.Linq;
using System.Xml.Serialization;
using ok1.Data;
using ok1.Models;
using PagedList;
using OfficeOpenXml;

namespace ok1.Controllers
{

    [FeatureAuthorize("Breastfeedings")]
    public class BreastfeedingsController : Controller
    {
        private ok1Context db = new ok1Context();
        private masterContext og = new masterContext();

        public ActionResult Index(string sortOrder, string searchString, int? page = 1)
        {
            ViewBag.CurrentSort = sortOrder;
            ViewBag.NameSortParm = String.IsNullOrEmpty(sortOrder) ? "badge_id" : "";
            ViewBag.DateSortParm = sortOrder == "date_desc" ? "date_asc" : "date_desc";

            if (searchString != null)
            {
                page = 1;
            }
            ViewBag.CurrentFilter = searchString;

            int pageSize = 20;
            int pageNumber = (page ?? 1);

            // Build a unique cache key for this query
            string cacheKey = $"Breastfeedings_{sortOrder}_{searchString}_{pageNumber}";

            // Try to get from cache
            var cached = HttpRuntime.Cache[cacheKey] as IPagedList<Breastfeeding>;
            if (cached != null)
            {
                return View(cached);
            }

            var mom = db.Breastfeedings.AsQueryable();
            if (!String.IsNullOrEmpty(searchString))
            {
                mom = mom.Where(s => s.EmpBadgeId.Contains(searchString) || s.EmpName.Contains(searchString));
            }

            switch (sortOrder)
            {
                case "date_asc":
                    mom = mom.OrderBy(s => s.DateOfBirth);
                    break;
                case "date_desc":
                    mom = mom.OrderByDescending(s => s.DateOfBirth);
                    break;
                default:
                    mom = mom.OrderByDescending(s => s.DateOfBirth);
                    break;
            }

            var pagedEvents = mom.ToPagedList(pageNumber, pageSize);

            // Cache the result for 1 hour
            HttpRuntime.Cache.Insert(
                cacheKey,
                pagedEvents,
                null,
                DateTime.Now.AddHours(1),
                System.Web.Caching.Cache.NoSlidingExpiration
            );

            return View(pagedEvents);
        }

        //GET EMP Data
        [HttpGet]
        public JsonResult GetMomData(string empBadgeId)
        {
            if (string.IsNullOrWhiteSpace(empBadgeId))
            {
                return Json(new { error = "Badge ID is required." }, JsonRequestBehavior.AllowGet);
            }

            // Simulate fetching data from the database
            var employee = og.gmds
                .Where(e => e.BadgeId == empBadgeId)
                .Select(e => new
                {
                    EmpName = e.Name,
                    EmpDept = e.Department
                })
                .FirstOrDefault();

            if (employee == null)
            {
                return Json(new { error = "Employee not found." }, JsonRequestBehavior.AllowGet);
            }

            return Json(employee, JsonRequestBehavior.AllowGet);
        }

        private ActionResult BadRequest(string v)
        {
            throw new NotImplementedException();
        }


        // GET: Breastfeedings/Create
        public ActionResult Create()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,EmpBadgeId,EmpName,EmpDept,DateOfBirth")] Breastfeeding breastfeeding)
        {
            // Validate DateOfBirth
            if (breastfeeding.DateOfBirth > DateTime.Now)
            {
                ModelState.AddModelError("DateOfBirth", "Date of Birth cannot be in the future.");
            }

            // Auto-calculate Expiration Date and Status
            breastfeeding.ExpDate = breastfeeding.DateOfBirth.AddYears(2);
            breastfeeding.Status = breastfeeding.ExpDate > DateTime.Now ? "Active" : "Inactive";

            // Check for duplicate EmpBadgeId
            if (db.Breastfeedings.Any(b => b.EmpBadgeId == breastfeeding.EmpBadgeId))
            {
                ModelState.AddModelError("EmpBadgeId", "This Badge ID already exists.");
                ViewBag.DuplicateBadgeId = true;
            }

            // Save to database if ModelState is valid
            if (ModelState.IsValid)
            {
                db.Breastfeedings.Add(breastfeeding);
                db.SaveChanges();
                ClearBreastfeedingCache();
                TempData["message"] = "Data created successfully!";
                return RedirectToAction("Index");
            }

            // If ModelState is invalid, return to the Create view
            TempData["ErrorMessage"] = "Something went wrong. Please check the form and try again.";
            return View(breastfeeding);
        }


        // GET: Breastfeedings/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Retrieve the Breastfeeding record by ID
            Breastfeeding breastfeeding = db.Breastfeedings.Find(id);
            if (breastfeeding == null)
            {
                return HttpNotFound();
            }

            return View(breastfeeding);
        }

        // POST: Breastfeedings/Edit/5
        // Update breastfeeding record with validation and auto-calculation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,DateOfBirth,Status")] Breastfeeding breastfeeding)
        {
            // Validate DateOfBirth
            if (breastfeeding.DateOfBirth > DateTime.Now)
            {
                ModelState.AddModelError("DateOfBirth", "Date of Birth cannot be in the future.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Fetch the existing record from database
                    var existing = db.Breastfeedings.Find(breastfeeding.Id);
                    if (existing == null)
                    {
                        return HttpNotFound();
                    }

                    // Update only editable fields
                    existing.DateOfBirth = breastfeeding.DateOfBirth;
                    // Always calculate ExpDate from DateOfBirth
                    existing.ExpDate = breastfeeding.DateOfBirth.AddYears(2);

                    // If user left status blank, auto-calculate. Otherwise, keep user selection
                    if (string.IsNullOrEmpty(breastfeeding.Status))
                    {
                        existing.Status = existing.ExpDate > DateTime.Now ? "Active" : "Inactive";
                    }
                    else
                    {
                        // Ensure status is valid
                        if (breastfeeding.Status != "Active" && breastfeeding.Status != "Inactive")
                        {
                            existing.Status = existing.ExpDate > DateTime.Now ? "Active" : "Inactive";
                        }
                        else
                        {
                            existing.Status = breastfeeding.Status;
                        }
                    }

                    db.SaveChanges();
                    ClearBreastfeedingCache();

                    // If status is set to Inactive, set Busui = 00:00:00 for future FabMove Records
                    if (existing.Status == "Inactive")
                    {
                        var today = DateTime.Today;
                        var badgeId = existing.EmpBadgeId;
                        var affectedFabMoves = db.BFabMoves
                            .Where(f => f.EmpBadgeId == badgeId && DbFunctions.TruncateTime(f.WorkDay) >= today)
                            .ToList();

                        foreach (var fabMove in affectedFabMoves)
                        {
                            if (fabMove.Busui != TimeSpan.Zero)
                            {
                                fabMove.Busui = TimeSpan.Zero;
                            }
                        }
                        db.SaveChanges();
                        ClearUserSummariesCache();
                    }

                    TempData["message"] = "Data updated successfully";
                    return RedirectToAction("Index");
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An error occured while updating the record.";
                    ModelState.AddModelError("", "An error occured while saving the data.");
                }
            }

            // Re-populate non-editable fields for display
            var original = db.Breastfeedings.AsNoTracking().FirstOrDefault(b => b.Id == breastfeeding.Id);
            if (original != null)
            {
                breastfeeding.EmpBadgeId = original.EmpBadgeId;
                breastfeeding.EmpName = original.EmpName;
                breastfeeding.EmpDept = original.EmpDept;
                breastfeeding.ExpDate = original.DateOfBirth.AddYears(2); // Always recalculate for display
            }

            return View(breastfeeding);
        }

        // Delete Action
        [HttpPost]
        public ActionResult DeleteBusui(int? id)
        {
            if (!id.HasValue)
            {
                return Json(new { success = false, message = "Invalid ID." });
            }

            var busui = db.Breastfeedings.Find(id);
            if (busui == null)
            {
                return Json(new { success = false, message = "Record not found." });
            }

            try
            {
                db.Breastfeedings.Remove(busui);
                db.SaveChanges();
                ClearBreastfeedingCache();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Failed to delete: " + ex.Message });
            }
        }

        /*//GET:delete
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Breastfeeding busui = db.Breastfeedings.Find(id);
            if (busui == null)
            {
                return HttpNotFound();
            }
            return View(busui);
        }

        // POST: EventRegs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Breastfeeding busui = db.Breastfeedings.Find(id);
            db.Breastfeedings.Remove(busui);
            db.SaveChanges();
            TempData["message"] = "Delete event successful!";

            return RedirectToAction("Index");
        }*/
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }


        // Export brestfeeding data to excel
        [HttpGet]
        public ActionResult ExportToExcel(string searchString)
        {
            try
            {
                // Build query with optional filtering
                var data = db.Breastfeedings.AsQueryable();

                if (!string.IsNullOrEmpty(searchString))
                {
                    data = data.Where(b => b.EmpBadgeId.Contains(searchString) ||
                                            b.EmpName.Contains(searchString));
                }

                // Execute query and get data
                var breastfeedingData = data.OrderByDescending(b => b.DateOfBirth).ToList();

                // Check if data exists
                if (!breastfeedingData.Any())
                {
                    TempData["ErrorMessage"] = "No data available to export.";
                    return RedirectToAction("Index");
                }

                // Generate Excel file using EPPlus
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Breastfeeding Data");

                    worksheet.Cells["A1"].Value = "Badge ID";
                    worksheet.Cells["B1"].Value = "Employee Name";
                    worksheet.Cells["C1"].Value = "Department";
                    worksheet.Cells["D1"].Value = "Date Of Birth";
                    worksheet.Cells["E1"].Value = "Expired Date";
                    worksheet.Cells["F1"].Value = "Status";

                    // Populate data rows
                    int row = 2;
                    foreach (var item in breastfeedingData)
                    {
                        worksheet.Cells[row, 1].Value = item.EmpBadgeId;
                        worksheet.Cells[row, 2].Value = item.EmpName;
                        worksheet.Cells[row, 3].Value = item.EmpDept;

                        worksheet.Cells[row, 4].Value = item.DateOfBirth;
                        worksheet.Cells[row, 4].Style.Numberformat.Format = "yyyy-mm-dd";

                        worksheet.Cells[row, 5].Value = item.ExpDate;
                        worksheet.Cells[row, 5].Style.Numberformat.Format = "yyyy-mm-dd";

                        worksheet.Cells[row, 6].Value = item.Status;
                        row++;
                    }

                    // Set all columns to left alignment
                    worksheet.Cells[worksheet.Dimension.Address].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;

                    // Auto-fit columns widths for better readability
                    for (int col = 1; col <= 6; col++)
                    {
                        worksheet.Column(col).Width = 20; // Set a default width
                    }

                    // Generate file stream
                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    // Return Excel file for download
                    string fileName = "Breastfeeding Data Reports.xlsx";
                    string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    return File(stream, contentType, fileName);
                }
            }
            catch (Exception)
            {
                // Log error and redirect back to index with error message
                TempData["ErrorMessage"] = "An error occurred while exporting data.";
                return RedirectToAction("Index", new { searchString});
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

        private void UpdateBreastfeedingStatuses()
        {
            var now = DateTime.Now;
            var records = db.Breastfeedings.ToList();
            bool changed = false;

            foreach (var item in records)
            {
                // Only auto-update if status is "Active" and ExpDate has passed
                if (item.Status == "Active" && item.ExpDate <= now)
                {
                    item.Status = "Inactive";
                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
                ClearBreastfeedingCache();
            }    
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
    }
}