using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using ok1.Data;
using ok1.Models;
using PagedList;

namespace ok1.Controllers
{
    [FeatureAuthorize("PublicHolidays")]

    public class PublicHolidaysController : Controller
    {
        private ok1Context db = new ok1Context();

        public ActionResult Index(int? page = 1)
        {
            var items = db.PublicHolidays.ToList();
            var pageNumber = page ?? 1;
            int pageSize = 20;

            var pagedList = items.ToPagedList(pageNumber, pageSize);
            return View(pagedList);
        }

        // GET: PublicHolidays/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: PublicHolidays/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,DateFrom,DateTo,RemarkDate")] PublicHoliday publicHoliday)
        {
            if (ModelState.IsValid)
            {
                db.PublicHolidays.Add(publicHoliday);
                db.SaveChanges();

                // Update FabMove whose Workday falls within the new holiday range
                var affectedFabMoves = db.BFabMoves
                    .Where(fm => fm.WorkDay >= publicHoliday.DateFrom && fm.WorkDay <= publicHoliday.DateTo)
                    .ToList();

                foreach (var fabMove in affectedFabMoves)
                {
                    fabMove.PublicHoliday = publicHoliday.RemarkDate;
                }
                db.SaveChanges();

                TempData["message"] = "Data created successfully";
                return RedirectToAction("Index");
            }
            else
            {
                TempData["ErrorMessage"] = "Something went wrong!";
            }
            return View(publicHoliday);
        }

        // GET: PublicHolidays/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            PublicHoliday publicHoliday = db.PublicHolidays.Find(id);
            if (publicHoliday == null)
            {
                return HttpNotFound();
            }
            return View(publicHoliday);
        }

        // POST: PublicHolidays/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,DateFrom,DateTo,RemarkDate")] PublicHoliday publicHoliday)
        {
            if (ModelState.IsValid)
            {
                // Get the old holiday data before updateing
                var oldHoliday = db.PublicHolidays.AsNoTracking().FirstOrDefault(h => h.Id == publicHoliday.Id);

                db.Entry(publicHoliday).State = EntityState.Modified;
                db.SaveChanges();

                if (oldHoliday != null)
                {
                    // Clear publicHoliday for FabMoves that were in the old range but not in the new range
                    var toClear = db.BFabMoves
                        .Where(fm =>
                            fm.WorkDay >= oldHoliday.DateFrom && fm.WorkDay <= oldHoliday.DateTo &&
                            (fm.WorkDay < publicHoliday.DateFrom || fm.WorkDay > publicHoliday.DateTo) &&
                            fm.PublicHoliday == oldHoliday.RemarkDate)
                        .ToList();

                    foreach (var fm in toClear)
                    {
                        fm.PublicHoliday = null;
                    }    
                }

                // Set publicHoliday for FabMoves in the new range
                var toSet = db.BFabMoves
                    .Where(fm => fm.WorkDay >= publicHoliday.DateFrom && fm.WorkDay <= publicHoliday.DateTo)
                    .ToList();

                foreach (var fm in toSet)
                {
                    fm.PublicHoliday = publicHoliday.RemarkDate;
                }

                db.SaveChanges();

                TempData["message"] = "Data updated!";
                return RedirectToAction("Index");
            }
            else
            {
                TempData["ErrorMessage"] = "Something went wrong!";
            }
            return View(publicHoliday);
        }
        //Delete
        [HttpPost]
        public ActionResult DeleteHoliday(int? id)
        {
            try
            {
                var holiday = db.PublicHolidays.Find(id);
                if (holiday != null)
                {
                    // Clear publicHoliday in FabMove for this holiday's date range and remark
                    var affectedFabMoves = db.BFabMoves
                        .Where(fm =>
                            fm.WorkDay >= holiday.DateFrom &&
                            fm.WorkDay <= holiday.DateTo &&
                            fm.PublicHoliday == holiday.RemarkDate)
                        .ToList();

                    foreach (var fabMove in affectedFabMoves)
                    {
                        fabMove.PublicHoliday = "No Holiday";
                    }
                    db.PublicHolidays.Remove(holiday);
                    db.SaveChanges();
                }
                else
                {
                    Debug.WriteLine($"PH not found: {id}");
                }
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
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
