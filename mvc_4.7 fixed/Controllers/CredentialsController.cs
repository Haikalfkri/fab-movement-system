using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using System.Web.Mvc;
using ok1.Data;
using ok1.Models;

namespace ok1.Controllers
{
    [FeatureAuthorize("Credentials")]

    public class CredentialsController : Controller
    {
        private ok1Context db = new ok1Context();
        private masterContext og = new masterContext();
        // GET: Credentials
        public async Task<ActionResult> Index()
        {
            var credentials = await db.Credentials
                .Where(c => !c.isAdmin) // Filter non-admin
                .Include(c => c.PageAccessUsers) // Eager load PageAccessUsers
                .ToListAsync();
            ViewBag.AvailablePages = new List<string> { "Breastfeedings", "EventRegs", "PublicHolidays", "TargetTimes" };
            return View(credentials);
        }


        //GET: Fetch user data from master table
        [HttpGet]
        public JsonResult GetCredsData(string empBadgeId)
        {
            System.Diagnostics.Debug.WriteLine($"Received BadgeId: {empBadgeId}");
            var employee = og.gmds.Where(e => e.BadgeId == empBadgeId)
                .Select(e => new {
                    EmpBadgeId = e.BadgeId,
                    EmpName = e.Name,
                    WindAcc = e.WindowsAccount.Trim().ToLower(),
                })
                .FirstOrDefault();
            return Json(employee, JsonRequestBehavior.AllowGet);
        }
        // GET: Credentials/Create (Partial View)
        public ActionResult Create()
        {
            ViewBag.AvailablePages = new List<string> { "Breastfeedings", "EventRegs", "PublicHolidays", "TargetTimes" };
            return PartialView("_Create", new Credential());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Create([Bind(Include = "EmpBadgeId,EmpName,WindAcc")] Credential model, string[] SelectedPages)
        {
            if (db.Credentials.Any(c => c.EmpBadgeId.ToLower() == model.EmpBadgeId.ToLower()))
            {
                return Json(new { success = false, errors = new { EmpBadgeId = "This Badge ID already exists." } });
            }

            // Normalize WindAcc: strip domain if present and use lowercase username only
            if (!string.IsNullOrWhiteSpace(model.WindAcc))
            {
                model.WindAcc = model.WindAcc.Trim();
                if (model.WindAcc.Contains("\\"))
                    model.WindAcc = model.WindAcc.Split('\\').Last();
                model.WindAcc = model.WindAcc.ToLower();
            }
            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        model.isAdmin = false; // Admin via DB manual
                        db.Credentials.Add(model);
                        await db.SaveChangesAsync();

                        if (SelectedPages != null && SelectedPages.Any())
                        {
                            foreach (var page in SelectedPages)
                            {
                                db.PageAccessUsers.Add(new PageAccessUser
                                {
                                    CredentialId = model.Id,
                                    EmpBadgeId = model.EmpBadgeId,
                                    PageName = page
                                });
                            }
                            await db.SaveChangesAsync();
                        }

                        transaction.Commit();
                        return Json(new { success = true, message = "Credential created successfully!" });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, errors = new { General = "An error occurred: " + ex.Message } });
                    }
                }
            }

            var errors = ModelState.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()
            );
            return Json(new { success = false, errors });
        }        //To get data partial
        public ActionResult GetFormData()
        {
            var credentials = db.Credentials.ToList();
            return View(credentials);
        }
        //Get/Edit
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var creds = await db.Credentials
                .Include(c => c.PageAccessUsers) // Eager load PageAccessUsers
                .FirstOrDefaultAsync(c => c.Id == id);
            if (creds == null)
            {
                return HttpNotFound();
            }

            ViewBag.GrantedCredentials = creds.PageAccessUsers.Select(p => p.PageName).ToList();
            ViewBag.AvailablePages = new List<string> { "Breastfeedings", "EventRegs", "PublicHolidays", "TargetTimes" };
            return View(creds);
        }
        // POST: Credentials/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "Id,EmpBadgeId,EmpName,WindAcc")] Credential model, string[] SelectedPages)
        {
            string domainPrefix = "INFINEON\\";
            if (!model.WindAcc.StartsWith(domainPrefix, StringComparison.OrdinalIgnoreCase))
            {
                model.WindAcc = domainPrefix + model.WindAcc.Trim(); // Add the domain prefix and trim whitespace
            }
            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var credential = await db.Credentials.FindAsync(model.Id);
                        if (credential == null)
                        {
                            return HttpNotFound();
                        }

                        credential.EmpBadgeId = model.EmpBadgeId;
                        credential.EmpName = model.EmpName;
                        credential.WindAcc = model.WindAcc;
                        db.Entry(credential).State = EntityState.Modified;

                        var existingAccess = db.PageAccessUsers.Where(p => p.CredentialId == credential.Id);
                        db.PageAccessUsers.RemoveRange(existingAccess);

                        if (SelectedPages != null)
                        {
                            foreach (var page in SelectedPages)
                            {
                                db.PageAccessUsers.Add(new PageAccessUser
                                {
                                    CredentialId = credential.Id,
                                    EmpBadgeId = credential.EmpBadgeId,
                                    PageName = page
                                });
                            }
                        }

                        await db.SaveChangesAsync();
                        transaction.Commit();
                        TempData["message"] = "Credential updated successfully!";
                        return RedirectToAction("Index");
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", "An error occurred while updating the credential.");
                    }
                }
            }

            ViewBag.GrantedCredentials = db.PageAccessUsers
                .Where(p => p.CredentialId == model.Id)
                .Select(p => p.PageName)
                .ToList();
            ViewBag.AvailablePages = new List<string> { "Breastfeedings", "EventRegs", "PublicHolidays", "TargetTimes" };
            return View(model);
        }

        [HttpPost]
        public async Task<JsonResult> DeleteCreds(int? id)
        {
            if (id == null)
            {
                return Json(new { success = false, message = "Invalid ID." });
            }

            try
            {
                var credential = await db.Credentials.FindAsync(id);
                if (credential == null)
                {
                    return Json(new { success = false, message = "Credential not found." });
                }

                // Remove associated page access entries
                var pageAccess = db.PageAccessUsers.Where(p => p.CredentialId == credential.Id);
                db.PageAccessUsers.RemoveRange(pageAccess);

                // Remove the credential
                db.Credentials.Remove(credential);
                await db.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                Console.WriteLine("An error occurred while deleting the credential: " + ex.Message);
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
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
