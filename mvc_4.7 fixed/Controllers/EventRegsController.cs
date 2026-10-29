using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using OfficeOpenXml;
using ok1.Data;
using ok1.Models;
using PagedList;


namespace ok1.Controllers
{
    [FeatureAuthorize("EventRegs")]

    public class EventRegsController : Controller
    {
        private ok1Context db = new ok1Context();
        private masterContext og = new masterContext();


        // GET: EventRegs
        public ActionResult Index(string sortOrder, string searchString, string searchDate, string currentFilter, int? page = 1)
        {
            ViewBag.CurrentSort = sortOrder;
            ViewBag.NameSortParm = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.DateSortParm = sortOrder == "Date" ? "date_desc" : "Date";

            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewBag.CurrentFilter = searchString;

            int pageSize = 20;
            var pageNumber = (page ?? 1);

            // Build a unique cache key for this query
            string cacheKey = $"EventRegs_{sortOrder}_{searchString}_{searchDate}_{currentFilter}_{pageNumber}";

            // Try to get from cache
            var cached = HttpRuntime.Cache[cacheKey] as IPagedList<EventReg>;
            if (cached != null)
            {
                return View(cached);
            }

            var events = db.EventRegs.AsQueryable();

            // Search by event name and trainer name
            if (!String.IsNullOrEmpty(searchString))
            {
                events = events.Where(s => s.EventName.Contains(searchString) || s.TrainerId.Contains(searchString));
            }

            // Search by event date
            if (!String.IsNullOrEmpty(searchDate))
            {
                DateTime date;
                if (DateTime.TryParse(searchDate, out date))
                {
                    var startDate = date.Date;
                    var endDate = startDate.AddDays(1);
                    events = events.Where(e => e.EventDate >= startDate && e.EventDate < endDate);
                }
            }

            // Sorting
            switch (sortOrder)
            {
                case "name_desc":
                    events = events.OrderByDescending(s => s.EventName);
                    break;
                case "Date":
                    events = events.OrderBy(s => s.EventDate);
                    break;
                case "date_desc":
                    events = events.OrderByDescending(s => s.EventDate);
                    break;
                case "Name":
                    events = events.OrderBy(s => s.EventName);
                    break;
                default: // Default newest to oldest
                    events = events.OrderByDescending(s => s.EventDate);
                    break;
            }

            var pagedEvents = events.ToPagedList(pageNumber, pageSize);

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


        //To get data partial
        public ActionResult GetFormParticipant()
        {
            var attendData = db.GetAttendees.ToList();
            return View(attendData);
        }

        // GET: EventRegs/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            EventReg eventReg = db.EventRegs.Find(id);
            if (eventReg == null)
            {
                return HttpNotFound();
            }
            return View(eventReg);
        }
        //GET Trainer Data
        [HttpGet]
        public JsonResult GetTrainerData(string trainerId)
        {
            /*
            var trainer = og.gmds.Where(t => t.BadgeId == trainerId)
                .Select(t => new {
                    TrainerId = t.BadgeId,
                    TrainerName = t.Name,
                })
                .FirstOrDefault();
            return Json(trainer, JsonRequestBehavior.AllowGet);
            */
    
            if (string.IsNullOrWhiteSpace(trainerId))
            {
                return Json(null, JsonRequestBehavior.AllowGet);
            }

            var normalizedTrainerId = trainerId.Trim();

            var trainer = og.gmds
                .AsNoTracking()
                .Where(t => t.BadgeId == normalizedTrainerId)
                .Select(t => new
                {
                    TrainerId = t.BadgeId,
                    TrainerName = t.Name
                })
                .FirstOrDefault();

            return Json(trainer, JsonRequestBehavior.AllowGet);
        }

        //Get Participant Data
        [HttpGet]
        public JsonResult GetParticipantData(string empId)
        {
            if (string.IsNullOrWhiteSpace(empId))
            {
                return Json(null, JsonRequestBehavior.AllowGet);
            }

            var normalizedEmpId = empId.Trim();

            // Use AsNoTracking for read-only query (faster)
            var employee = og.gmds
                .AsNoTracking()
                .Where(p => p.BadgeId == normalizedEmpId)
                .Select(p => new
                {
                    EmpBadgeId = p.BadgeId,
                    EmpName = p.Name,
                    EmpDept = p.Department,
                })
                .FirstOrDefault();

            if (employee != null)
            {
                Debug.WriteLine($"Found: {employee.EmpBadgeId} - {employee.EmpName} - {employee.EmpDept}");
            }
            else
            {
                Debug.WriteLine($"Employee not found for BadgeId: {normalizedEmpId}");
            }

            return Json(employee, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetFormData()
        {
            var events = db.EventRegs.ToList();
            return View(events);
        }

        //POST: Event/UploadFile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Import(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                return await Task.FromResult<ActionResult>(new HttpStatusCodeResult(400, "No file uploaded"));
            }

            using (var package = new ExcelPackage(file.InputStream))
            {
                var worksheet = package.Workbook.Worksheets.First();
                var rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++)
                {
                    if (string.IsNullOrWhiteSpace(worksheet.Cells[row, 1].Text))
                        break;

                    var trainerId = worksheet.Cells[row, 1].Text;
                    var trainerName = worksheet.Cells[row, 2].Text;
                    var eventDate = worksheet.Cells[row, 3].GetValue<DateTime>().Date;
                    var eventName = worksheet.Cells[row, 4].Text;
                    var startTime = worksheet.Cells[row, 5].GetValue<DateTime>().TimeOfDay;
                    var endTime = worksheet.Cells[row, 6].GetValue<DateTime>().TimeOfDay;
                    object durCell = worksheet.Cells[row, 7].Value;
                    string eventDur = "";

                    if (durCell is TimeSpan ts)
                    {
                        eventDur = ts.ToString(@"hh\:mm\:ss");
                    }
                    else if (durCell is DateTime dt)
                    {
                        eventDur = dt.TimeOfDay.ToString(@"hh\:mm\:ss");
                    }
                    else if (durCell != null)
                    {
                        // Try to parse as TimeSpan from string
                        if (TimeSpan.TryParse(durCell.ToString(), out var ts2))
                            eventDur = ts2.ToString(@"hh\:mm\:ss");
                        else
                            eventDur = durCell.ToString();
                    }

                    // To check if event existed
                    var existingEvent = await db.EventRegs.FirstOrDefaultAsync(e =>
                        e.TrainerId == trainerId &&
                        e.TrainerName.Trim().ToLower() == trainerName.ToLower() &&
                        e.EventDate == eventDate &&
                        e.EventName.Trim().ToLower() == eventName.ToLower()
                    );

                    if (existingEvent == null)
                    {
                        existingEvent = new EventReg
                        {
                            TrainerId = trainerId,
                            TrainerName = trainerName,
                            EventDate = eventDate,
                            EventName = eventName,
                            StartTime = startTime,
                            EndTime = endTime,
                            EventDur = eventDur,
                        };

                        db.EventRegs.Add(existingEvent);
                        TempData["message"] = "Upload data successful!"; 
                    }
                    else
                    {
                        // Optional: update info if event
                        existingEvent.StartTime = startTime;
                        existingEvent.EndTime = endTime;
                        existingEvent.EventDur = eventDur;
                        db.Entry(existingEvent).State = EntityState.Modified;
                    }

                    // Attaendance data, need to check if it can handle multiple data
                    var empName = worksheet.Cells[row, 9].Text;
                    var empBadgeId = worksheet.Cells[row, 10].Text;
                    var empDept = worksheet.Cells[row, 11].Text;

                    if (!string.IsNullOrWhiteSpace(empBadgeId))
                    {
                        // Check if the attendee exists (by EmpBadgeId)
                        var existingAttendee = await db.GetAttendees
                            .FirstOrDefaultAsync(a => a.EmpBadgeId == empBadgeId);

                        if (existingAttendee == null)
                        {
                            // Create a new attendee if it doesn’t exist
                            existingAttendee = new Attendees
                            {
                                EmpBadgeId = empBadgeId,
                                EmpName = empName,
                                EmpDept = empDept
                            };
                            db.GetAttendees.Add(existingAttendee);
                            await db.SaveChangesAsync();
                        }
                        else
                        {
                            if (existingAttendee.EmpName != empName || existingAttendee.EmpDept != empDept)
                            {
                                existingAttendee.EmpName = empName;
                                existingAttendee.EmpDept = empDept;

                                await db.SaveChangesAsync();
                            }
                        }

                        // Check if the attendee is already linked to the event
                        var eventAttendee = await db.EventAttendees
                                .FirstOrDefaultAsync(ea =>
                                    ea.EventRegId == existingEvent.Id &&
                                    ea.AttendeesId == existingAttendee.Id
                                );

                        if (eventAttendee == null)
                        {
                            // Link the attendee to the event if not already linked
                            var newEventAttendee = new EventAttendee
                            {
                                EventRegId = existingEvent.Id,
                                AttendeesId = existingAttendee.Id
                            };

                            db.EventAttendees.Add(newEventAttendee);
                        }
                        // Save all changes
                        await db.SaveChangesAsync();
                        // Tambah relasi EventAttendee jika belum ada
                        var joined = await db.EventAttendees.AnyAsync(ea =>
                            ea.EventRegId == existingEvent.Id &&
                            ea.AttendeesId == existingAttendee.Id
                        );

                        if (!joined)
                        {
                            db.EventAttendees.Add(new EventAttendee
                            {
                                EventRegId = existingEvent.Id,
                                AttendeesId = existingAttendee.Id
                            });
                            await db.SaveChangesAsync();
                        }
                    }
                }
            }

            TempData["Success"] = "Upload data succesful.";
            ClearEventRegsCache();
            return RedirectToAction(nameof(Index));
        }

        //GET: Event/ExportFile
        [HttpGet]
        public ActionResult Export(int? id)
        {
            var event1 = db.EventRegs.Include(e => e.EventAttendees.
            Select(a => a.Attendee)).FirstOrDefault(i => i.Id == id);

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("EventReg");

                // Set headers
                worksheet.Cells["A1"].Value = "Trainer ID";
                worksheet.Cells["B1"].Value = "Trainer Name";
                worksheet.Cells["C1"].Value = "Event Date";
                worksheet.Cells["D1"].Value = "Event Name";
                worksheet.Cells["E1"].Value = "Start Time";
                worksheet.Cells["F1"].Value = "End Time";
                worksheet.Cells["G1"].Value = "Event Duration";
                worksheet.Cells["H1"].Value = "Total Participant";
                worksheet.Cells["I1"].Value = "Participant Name";
                worksheet.Cells["J1"].Value = "Badge ID";
                worksheet.Cells["K1"].Value = "Department";

                int row = 2;

                // Check if there are attendees for the event
                if (event1.EventAttendees.Any())
                { 
                    foreach (var e in event1.EventAttendees)
                    {
                        // Fill in event details for the first attendee
                        worksheet.Cells[row, 1].Value = event1.TrainerId;
                        worksheet.Cells[row, 2].Value = event1.TrainerName;
                        worksheet.Cells[row, 3].Value = event1.EventDate.ToString("yyyy-MM-dd");
                        worksheet.Cells[row, 4].Value = event1.EventName;
                        worksheet.Cells[row, 5].Value = event1.StartTime.ToString();
                        worksheet.Cells[row, 6].Value = event1.EndTime.ToString();
                        worksheet.Cells[row, 7].Value = event1.EventDur.ToString();
                        worksheet.Cells[row, 8].Value = event1.TotalAttendees.ToString();
                        worksheet.Cells[row, 9].Value = e.Attendee.EmpName;
                        worksheet.Cells[row, 10].Value = e.Attendee.EmpBadgeId;
                        worksheet.Cells[row, 11].Value = e.Attendee.EmpDept;

                        row++;

                    }
                   
                }
                else
                {
                    // If there are no attendees, just move to the next row
                    row++;
                }
                worksheet.DefaultColWidth = 20;

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = "Event Details.xlsx";
                string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                return File(stream, contentType, fileName);
            }
        }
        public ActionResult Template(HttpPostedFileBase file)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Event Registration");

                //set header

                worksheet.Cells["A1"].Value = "Trainer ID";
                worksheet.Cells["B1"].Value = "Trainer Name";
                worksheet.Cells["C1"].Value = "Event Date";
                worksheet.Cells["D1"].Value = "Event Name";
                worksheet.Cells["E1"].Value = "Start Time";
                worksheet.Cells["F1"].Value = "End Time";
                worksheet.Cells["G1"].Value = "Event Duration";
                worksheet.Cells["H1"].Value = "Total Participant";
                worksheet.Cells["I1"].Value = "Participant Name";
                worksheet.Cells["J1"].Value = "Participant Badge ID";
                worksheet.Cells["K1"].Value = "Participant Department";

                //set column width
                worksheet.Column(1).Width = 15;
                worksheet.Column(2).Width = 15;
                worksheet.Column(3).Width = 15;
                worksheet.Column(4).Width = 15;
                worksheet.Column(5).Width = 15;
                worksheet.Column(6).Width = 15;
                worksheet.Column(7).Width = 20;
                worksheet.Column(8).Width = 20;
                worksheet.Column(9).Width = 20;
                worksheet.Column(10).Width = 20;
                worksheet.Column(11).Width = 20;

                //set format datetime
                //should be checked if it's works or received as another data type
                worksheet.Cells["C1"].Style.Numberformat.Format = "hh:mm:ss";
                worksheet.Column(1).Style.Numberformat.Format = "@";
                worksheet.Column(10).Style.Numberformat.Format = "@";

                /*worksheet.Cells["E1"].Style.Numberformat.Format = "hh:mm:ss";
                worksheet.Cells["F1"].Style.Numberformat.Format = "hh:mm:ss";*/

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = "Template_EventRegistration.xlsx";
                string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                return File(stream, contentType, fileName);
            }
        }
        private ActionResult BadRequest(string v)
        {
            throw new NotImplementedException();
        }

        // GET: EventRegs/Create
        public ActionResult Create()
        {
            ViewBag.Attendees = new List<Attendees>();
            return View(new EventReg());
        }

        // POST: EventRegs/Create
        // Post event data to database ( trainer information, event information, participant information )
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,TrainerId,TrainerName,EventDate,EventName,StartTime,EndTime,EventDur")] EventReg eventReg, string[] EmpBadgeId, string[] EmpName, string[] EmpDept)
        {
            // Step 1: Validate or Create the Event
            var existingEvent = db.EventRegs.FirstOrDefault(e =>
                e.TrainerId.Trim().ToLower() == eventReg.TrainerId.Trim().ToLower() &&
                e.TrainerName.Trim().ToLower() == eventReg.TrainerName.Trim().ToLower() &&
                e.EventName.Trim().ToLower() == eventReg.EventName.Trim().ToLower() &&
                e.EventDate == eventReg.EventDate);

            if (existingEvent == null)
            {
                // If no existing event, create a new one
                existingEvent = new EventReg
                {
                    TrainerId = eventReg.TrainerId,
                    TrainerName = eventReg.TrainerName,
                    EventName = eventReg.EventName,
                    EventDate = eventReg.EventDate,
                    StartTime = eventReg.StartTime,
                    EndTime = eventReg.EndTime,
                    EventDur = eventReg.EventDur,
                };

                db.EventRegs.Add(existingEvent);
                db.SaveChanges(); // Save the new event to generate its ID
            }
            else
            {
                // Update existing event details if necessary (Optional)
                existingEvent.StartTime = eventReg.StartTime;
                existingEvent.EndTime = eventReg.EndTime;
                existingEvent.EventDur = eventReg.EventDur;
                db.SaveChanges();
            }

            // Step 2: Add or Update Attendees
            if (EmpBadgeId != null && EmpBadgeId.Length > 0)
            {
                for (int i = 0; i < EmpBadgeId.Length; i++)
                {
                    if (!string.IsNullOrEmpty(EmpBadgeId[i]))
                    {
                        var badgeId = EmpBadgeId[i].Trim();

                        // Check if the attendee already exists
                        var attendee = db.GetAttendees.FirstOrDefault(a => a.EmpBadgeId == badgeId);

                        if (attendee == null)
                        {
                            // cereate if attemdee doesnt exist
                            attendee = new Attendees
                            {
                                EmpBadgeId = badgeId,
                                EmpName = EmpName[i],
                                EmpDept = EmpDept[i],
                            };
                            foreach (var id in EmpBadgeId)
                            {
                                Debug.WriteLine("BadgeId: " + id);
                            }

                            foreach (var name in EmpName)
                            {
                                Debug.WriteLine("EmpName: " + name);
                            }

                            foreach (var dept in EmpDept)
                            {
                                Debug.WriteLine("EmpDept: " + dept);
                            }

                            db.GetAttendees.Add(attendee);
                            db.SaveChanges(); // Save to generate AttendeesId
                        }

                        var eventAttendee = db.EventAttendees.FirstOrDefault(ea =>
                            ea.EventRegId == existingEvent.Id && ea.AttendeesId == attendee.Id);

                        if (eventAttendee == null)
                        {
                            // Create the relationship if it doesn't exist
                            eventAttendee = new EventAttendee
                            {
                                EventRegId = existingEvent.Id,
                                AttendeesId = attendee.Id,
                            };

                            db.EventAttendees.Add(eventAttendee);
                        }
                    }
                }
            }
            db.SaveChanges();

            // Provide feedback to the user
            TempData["message"] = "Event created or updated successfully with attendees!";
            return RedirectToAction("Index");
        }


        // Auto-Save functionality for auto save event data
        // In case PC error
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult AutoSave([Bind(Include = "Id,TrainerId,TrainerName,EventDate,EventName,StartTime,EndTime,EventDur")] EventReg eventReg, string[] EmpBadgeId, string[] EmpName, string[] EmpDept)
        {
            try
            {
                EventReg existingEvent = null;

                // 1. Always try to find by id first
                if (eventReg.Id > 0)
                {
                    existingEvent = db.EventRegs.FirstOrDefault(e => e.Id == eventReg.Id);
                }

                // 2. If not found by id, try to find by unique fields (legacy support)
                if (existingEvent == null)
                {
                    existingEvent = db.EventRegs.FirstOrDefault(e =>
                        e.TrainerId.Trim().ToLower() == eventReg.TrainerId.Trim().ToLower() &&
                        e.TrainerName.Trim().ToLower() == eventReg.TrainerName.Trim().ToLower() &&
                        e.EventDate == eventReg.EventDate &&
                        e.EventName.Trim().ToLower() == (eventReg.EventName ?? "").Trim().ToLower()
                    );
                }

                // 3. If still not found, create a new event
                if (existingEvent == null)
                {
                    existingEvent = new EventReg
                    {
                        TrainerId = eventReg.TrainerId,
                        TrainerName = eventReg.TrainerName,
                        EventDate = eventReg.EventDate,
                        EventName = eventReg.EventName,
                        StartTime = eventReg.StartTime,
                        EndTime = eventReg.EndTime,
                        EventDur = eventReg.EventDur,
                    };

                    db.EventRegs.Add(existingEvent);
                    db.SaveChanges();
                }
                else
                {
                    // 4. Update all fields (event if changed)
                    existingEvent.TrainerId = eventReg.TrainerId;
                    existingEvent.TrainerName = eventReg.TrainerName;
                    existingEvent.EventName = eventReg.EventName;
                    existingEvent.EventDate = eventReg.EventDate;
                    existingEvent.StartTime = eventReg.StartTime;
                    existingEvent.EndTime = eventReg.EndTime;
                    existingEvent.EventDur = eventReg.EventDur;

                    db.SaveChanges();
                }

                // 5. Add or Update Attendees
                var formBadgeIds = new List<int>();
                var badgeIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (EmpBadgeId != null && EmpBadgeId.Length > 0)
                {
                    for (int i = 0; i < EmpBadgeId.Length; i++)
                    {
                        var badgeId = EmpBadgeId[i]?.Trim();
                        if (string.IsNullOrWhiteSpace(badgeId) || badgeIdSet.Contains(badgeId))
                            continue; // Skip empty or duplicate badge IDs

                        badgeIdSet.Add(badgeId);

                        // Validate badgeId against master data
                        var masterBadge = og.gmds.FirstOrDefault(g => g.BadgeId == badgeId);
                        if (masterBadge == null)
                        {
                            return Json(new { success = false, message = $"Invalid badge id or badge id not found: {badgeId}" });
                        }

                        var attendee = db.GetAttendees.FirstOrDefault(a => a.EmpBadgeId == badgeId);

                        if (attendee == null)
                        {
                            attendee = new Attendees
                            {
                                EmpBadgeId = badgeId,
                                EmpName = EmpName[i],
                                EmpDept = EmpDept[i],
                            };
                            db.GetAttendees.Add(attendee);
                            db.SaveChanges(); // Ensure attendee.Id is set before linking
                        }
                        else
                        {
                            // Update name/department if changed
                            if (attendee.EmpName != EmpName[i] || attendee.EmpDept != EmpDept[i])
                            {
                                attendee.EmpName = EmpName[i];
                                attendee.EmpDept = EmpDept[i];
                                db.Entry(attendee).State = EntityState.Modified;
                                db.SaveChanges();
                            }
                        }

                        // Now attendee.Id is always set
                        formBadgeIds.Add(attendee.Id);

                        // Check if the attendee is already linked to the event
                        var eventAttendee = db.EventAttendees
                            .FirstOrDefault(ea => ea.EventRegId == existingEvent.Id && ea.AttendeesId == attendee.Id);

                        if (eventAttendee == null)
                        {
                            db.EventAttendees.Add(new EventAttendee
                            {
                                EventRegId = existingEvent.Id,
                                AttendeesId = attendee.Id,
                            });
                        }
                    }

                    // Remove attendees that are no longer in the form
                    var toRemove = db.EventAttendees
                        .Where(ea => ea.EventRegId == existingEvent.Id && !formBadgeIds.Contains(ea.AttendeesId))
                        .ToList();

                    foreach (var ea in toRemove)
                    {
                        db.EventAttendees.Remove(ea);
                    }

                    // Defensive: Remove duplicate EventAttendees for the event
                    var eventAttendeesList = db.EventAttendees
                        .Where(ea => ea.EventRegId == existingEvent.Id)
                        .ToList();

                    var duplicates = eventAttendeesList
                        .GroupBy(ea => ea.AttendeesId)
                        .Where(g => g.Count() > 1)
                        .SelectMany(g => g.Skip(1))
                        .ToList();

                    foreach (var dup in duplicates)
                    {
                        db.EventAttendees.Remove(dup);
                    }
                }

                db.SaveChanges();

                // Clear the vent cache after saving changes
                ClearEventRegsCache();

                // Return the eventId so the frontend can keep it for next autosave
                return Json(new { success = true, message = "Save successful!", eventId = existingEvent.Id });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AutoSave Error: " + ex.Message);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult EditParticipantAutoSave([Bind(Include = "Id,TrainerId,TrainerName,EventDate,EventName,StartTime,EndTime,EventDur")] EventReg eventReg, string[] EmpBadgeId, string[] EmpName, string[] EmpDept)
        {
            try
            {
                EventReg existingEvent = null;

                // 1. Always try to find by id first
                if (eventReg.Id > 0)
                {
                    existingEvent = db.EventRegs.FirstOrDefault(e => e.Id == eventReg.Id);
                }

                // 2. If not found by id, try to find by unique fields (legacy support)
                if (existingEvent == null)
                {
                    existingEvent = db.EventRegs.FirstOrDefault(e =>
                        e.TrainerId.Trim().ToLower() == eventReg.TrainerId.Trim().ToLower() &&
                        e.TrainerName.Trim().ToLower() == eventReg.TrainerName.Trim().ToLower() &&
                        e.EventDate == eventReg.EventDate &&
                        e.EventName.Trim().ToLower() == (eventReg.EventName ?? "").Trim().ToLower()
                    );
                }

                // 3. If still not found, create a new event
                if (existingEvent == null)
                {
                    existingEvent = new EventReg
                    {
                        TrainerId = eventReg.TrainerId,
                        TrainerName = eventReg.TrainerName,
                        EventDate = eventReg.EventDate,
                        EventName = eventReg.EventName,
                        StartTime = eventReg.StartTime,
                        EndTime = eventReg.EndTime,
                        EventDur = eventReg.EventDur,
                    };

                    db.EventRegs.Add(existingEvent);
                    db.SaveChanges();
                }
                else
                {
                    // 4. Update all fields (event if changed)
                    existingEvent.TrainerId = eventReg.TrainerId;
                    existingEvent.TrainerName = eventReg.TrainerName;
                    existingEvent.EventName = eventReg.EventName;
                    existingEvent.EventDate = eventReg.EventDate;
                    existingEvent.StartTime = eventReg.StartTime;
                    existingEvent.EndTime = eventReg.EndTime;
                    existingEvent.EventDur = eventReg.EventDur;

                    db.SaveChanges();
                }

                // 5. Add or Update Attendees (merge, do not remove old ones)
                var badgeIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (EmpBadgeId != null && EmpBadgeId.Length > 0)
                {
                    for (int i = 0; i < EmpBadgeId.Length; i++)
                    {
                        var badgeId = EmpBadgeId[i]?.Trim();
                        if (string.IsNullOrWhiteSpace(badgeId) || badgeIdSet.Contains(badgeId))
                            continue; // Skip empty or duplicate badge IDs

                        badgeIdSet.Add(badgeId);

                        // Validate badgeId against master data
                        var masterBadge = og.gmds.FirstOrDefault(g => g.BadgeId == badgeId);
                        if (masterBadge == null)
                        {
                            return Json(new { success = false, message = $"Invalid badge id or badge id not found: {badgeId}" });
                        }

                        var attendee = db.GetAttendees.FirstOrDefault(a => a.EmpBadgeId == badgeId);

                        if (attendee == null)
                        {
                            attendee = new Attendees
                            {
                                EmpBadgeId = badgeId,
                                EmpName = EmpName[i],
                                EmpDept = EmpDept[i],
                            };
                            db.GetAttendees.Add(attendee);
                            db.SaveChanges(); // Ensure attendee.Id is set before linking
                        }
                        else
                        {
                            // Update name/department if changed
                            if (attendee.EmpName != EmpName[i] || attendee.EmpDept != EmpDept[i])
                            {
                                attendee.EmpName = EmpName[i];
                                attendee.EmpDept = EmpDept[i];
                                db.Entry(attendee).State = EntityState.Modified;
                                db.SaveChanges();
                            }
                        }

                        // Check if the attendee is already linked to the event
                        var eventAttendee = db.EventAttendees
                            .FirstOrDefault(ea => ea.EventRegId == existingEvent.Id && ea.AttendeesId == attendee.Id);

                        if (eventAttendee == null)
                        {
                            db.EventAttendees.Add(new EventAttendee
                            {
                                EventRegId = existingEvent.Id,
                                AttendeesId = attendee.Id,
                            });
                        }
                    }

                    // Defensive: Remove duplicate EventAttendees for the event
                    var eventAttendeesList = db.EventAttendees
                        .Where(ea => ea.EventRegId == existingEvent.Id)
                        .ToList();

                    var duplicates = eventAttendeesList
                        .GroupBy(ea => ea.AttendeesId)
                        .Where(g => g.Count() > 1)
                        .SelectMany(g => g.Skip(1))
                        .ToList();

                    foreach (var dup in duplicates)
                    {
                        db.EventAttendees.Remove(dup);
                    }
                }

                db.SaveChanges();

                // Clear the event cache after saving changes
                ClearEventRegsCache();

                // Return the eventId so the frontend can keep it for next autosave
                return Json(new { success = true, message = "Save successful!", eventId = existingEvent.Id });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AutoSave Error: " + ex.Message);
                if (ex.InnerException != null)
                {
                    Debug.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: EventRegs/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            EventReg eventReg = db.EventRegs.Find(id);
            if (eventReg == null)
            {
                return HttpNotFound();
            }
            var attendees = db.EventAttendees.Where(a => a.EventRegId == id).Select(ea => ea.Attendee).ToList();
            ViewBag.Attendees = attendees;

            return View(eventReg);
        }

        // POST: EventRegs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,TrainerId,TrainerName,EventDate,EventName,StartTime,EndTime,EventDur")] EventReg eventReg, string[] EmpBadgeId, string[] EmpName, string[] EmpDept)
        {
            if (ModelState.IsValid)
            {
                db.Entry(eventReg).State = EntityState.Modified;
                db.SaveChanges();

                var updatedBadgeIds = new List<string>();

                if (EmpBadgeId != null)
                {
                    for (int i = 0; i < EmpBadgeId.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(EmpName[i]))
                        {
                            var badgeId = EmpBadgeId[i].Trim();
                            updatedBadgeIds.Add(badgeId);

                            // Check if attendee already exists
                            var attendee = db.GetAttendees.FirstOrDefault(a => a.EmpBadgeId == badgeId);
                            if (attendee == null)
                            {
                                attendee = new Attendees
                                {
                                    EmpBadgeId = badgeId,
                                    EmpName = EmpName[i],
                                    EmpDept = EmpDept[i],
                                };
                                db.GetAttendees.Add(attendee);
                                db.SaveChanges();
                            }
                            else
                            {
                                // Update name/department if changed
                                if (attendee.EmpName != EmpName[i] || attendee.EmpDept != EmpDept[i])
                                {
                                    attendee.EmpName = EmpName[i];
                                    attendee.EmpDept = EmpDept[i];
                                    db.Entry(attendee).State = EntityState.Modified;
                                    db.SaveChanges();
                                }
                            }

                            // Check if event-attendee link exists
                            var eventParticipant = db.EventAttendees.FirstOrDefault(ea =>
                                ea.EventRegId == eventReg.Id && ea.AttendeesId == attendee.Id);

                            if (eventParticipant == null)
                            {
                                eventParticipant = new EventAttendee
                                {
                                    EventRegId = eventReg.Id,
                                    AttendeesId = attendee.Id,
                                };
                                db.EventAttendees.Add(eventParticipant);
                            }
                        }
                    }
                }
                db.SaveChanges();

                // Update FabMove.Ahm for affected badge ids and event date
                if (updatedBadgeIds.Any())
                {
                    var tcFunc = new ok1.Services.TCFunc();

                    // Get all affected FabMove records for this event date and badge IDs
                    var eventDate = eventReg.EventDate.Date;
                    var fabMovesToUpdate = db.BFabMoves
                        .Where(fm => updatedBadgeIds.Contains(fm.EmpBadgeId) && DbFunctions.TruncateTime(fm.WorkDay) == eventDate)
                        .ToList();

                    // Get all event/attendee data needed for duration calculation
                    var attendeeData = db.GetAttendees.ToList();
                    var eventData = db.EventRegs.ToList();
                    var eventAttData = db.EventAttendees.ToList();

                    // Update ahm for each affected FabMove
                    foreach (var fabMove in fabMovesToUpdate)
                    {
                        fabMove.Ahm = tcFunc.GetEventDurByBadgeDate(
                            fabMove.EmpBadgeId,
                            fabMove.WorkDay,
                            attendeeData,
                            eventAttData,
                            eventData
                        );
                    }
                    db.SaveChanges();
                }
                ClearEventRegsCache();
                TempData["message"] = "Update event successfull";
                return RedirectToAction("Index");
            }

            // If something went wrong, it will return the view
            var attendees = new List<Attendees>();
            if (EmpBadgeId != null)
            {
                for (int i = 0; i < EmpBadgeId.Length; i++)
                {
                    attendees.Add(new Attendees
                    {
                        EmpBadgeId = EmpBadgeId[i],
                        EmpName = EmpName[i],
                        EmpDept = EmpDept[i],
                    });
                }
            }
            ViewBag.Attendees = attendees;
            return View(eventReg);
        }

        //DElete event
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            EventReg eventReg = db.EventRegs.Find(id);
            if (eventReg == null)
            {
                return HttpNotFound();
            }
            var attendees = db.EventAttendees.Where(a => a.EventRegId == id).Select(ea => ea.Attendee).ToList();
            ViewBag.Attendees = attendees;
            return View(eventReg);
        }

        // POST: EventRegs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            EventReg eventReg = db.EventRegs.Find(id);
            db.EventRegs.Remove(eventReg);
            db.SaveChanges();
            ClearEventRegsCache();
            TempData["message"] = "Delete event successful!";

            return RedirectToAction("Index");
        }
        [HttpPost]
        public JsonResult DeleteAttendee(int attendeeId, int eventRegId)
        {
            try
            {
                // Find the EventAttendee record
                var eventAttendee = db.EventAttendees
                    .FirstOrDefault(ea => ea.AttendeesId == attendeeId && ea.EventRegId == eventRegId);

                if (eventAttendee != null)
                {
                    // Get badge id and event date before removing
                    var attendee = db.GetAttendees.FirstOrDefault(a => a.Id == attendeeId);
                    var eventReg = db.EventRegs.FirstOrDefault(e => e.Id == eventRegId);

                    db.EventAttendees.Remove(eventAttendee);
                    db.SaveChanges();

                    if (attendee != null && eventReg != null)
                    {
                        // Find the related FabMove
                        var fabMove = db.BFabMoves
                            .FirstOrDefault(fm => fm.EmpBadgeId == attendee.EmpBadgeId && DbFunctions.TruncateTime(fm.WorkDay) == eventReg.EventDate.Date);

                        if (fabMove != null)
                        {
                            // Recalculate ahm using the latest event/attendee data
                            var tcFunc = new ok1.Services.TCFunc();
                            var attendeeData = db.GetAttendees.ToList();
                            var eventData = db.EventRegs.ToList();
                            var eventAttData = db.EventAttendees.ToList();

                            fabMove.Ahm = tcFunc.GetEventDurByBadgeDate(
                                fabMove.EmpBadgeId,
                                fabMove.WorkDay,
                                attendeeData,
                                eventAttData,
                                eventData
                            );
                            db.SaveChanges();
                        }
                    }

                    ClearEventRegsCache();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult DetailEventData(string fromMonthYear, string toMonthYear,
            string badgeFilterType = "all", string badgeId = "", int page = 1)
        {
            int pageSize = 20;
            int pageNumber = page > 0 ? page : 1;

            // ===================== FIRST LOAD =====================
            if (string.IsNullOrWhiteSpace(fromMonthYear) && string.IsNullOrWhiteSpace(toMonthYear))
            {
                return View(new StaticPagedList<EventDetailViewModel>(
                    new List<EventDetailViewModel>(), pageNumber, pageSize, 0));
            }

            // ===================== VALIDATE =====================
            if (string.IsNullOrWhiteSpace(fromMonthYear))
            {
                ModelState.AddModelError("", "From Month/Year is required.");
                return View(new StaticPagedList<EventDetailViewModel>(
                    new List<EventDetailViewModel>(), pageNumber, pageSize, 0));
            }

            if (string.IsNullOrWhiteSpace(toMonthYear))
            {
                ModelState.AddModelError("", "To Month/Year is required.");
                return View(new StaticPagedList<EventDetailViewModel>(
                    new List<EventDetailViewModel>(), pageNumber, pageSize, 0));
            }

            // ===================== PARSE DATE =====================
            DateTime startDate;
            DateTime toMonthParsed;

            if (!DateTime.TryParseExact(fromMonthYear, "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out startDate))
            {
                ModelState.AddModelError("", "Invalid From date format.");
                return View(new StaticPagedList<EventDetailViewModel>(
                    new List<EventDetailViewModel>(), pageNumber, pageSize, 0));
            }

            if (!DateTime.TryParseExact(toMonthYear, "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out toMonthParsed))
            {
                ModelState.AddModelError("", "Invalid To date format.");
                return View(new StaticPagedList<EventDetailViewModel>(
                    new List<EventDetailViewModel>(), pageNumber, pageSize, 0));
            }

            startDate = new DateTime(startDate.Year, startDate.Month, 1);
            var endDate = new DateTime(toMonthParsed.Year, toMonthParsed.Month, 1).AddMonths(1);

            if (startDate >= endDate)
            {
                ModelState.AddModelError("", "From date cannot be greater than To date.");
                return View(new StaticPagedList<EventDetailViewModel>(
                    new List<EventDetailViewModel>(), pageNumber, pageSize, 0));
            }

            // ===================== QUERY =====================
            // Step 1: Build base query — project directly in SQL, NO lazy loading, NO N+1
            var baseQuery = db.EventAttendees
                .AsNoTracking() // Read-only, no change tracking overhead
                .Where(ea =>
                    ea.EventRegs.EventDate >= startDate &&
                    ea.EventRegs.EventDate < endDate);

            // Step 2: Apply badge filter in SQL level
            if (badgeFilterType == "specific" && !string.IsNullOrWhiteSpace(badgeId))
            {
                var trimmedBadge = badgeId.Trim();
                baseQuery = baseQuery.Where(ea =>
                    ea.Attendee.EmpBadgeId == trimmedBadge);
            }

            // Step 3: Project to ViewModel directly in SQL
            // Avoid loading full entity objects — only select needed columns
            var projectedQuery = baseQuery
                .Select(ea => new EventDetailViewModel
                {
                    BadgeId = ea.Attendee.EmpBadgeId,
                    EmpName = ea.Attendee.EmpName,
                    EmpDept = ea.Attendee.EmpDept,
                    EventName = ea.EventRegs.EventName,
                    EventDate = ea.EventRegs.EventDate,
                    MonthName = "", // Cannot use .ToString("MMMM") in EF — set after
                    Year = ea.EventRegs.EventDate.Year,
                    EventDuration = ea.EventRegs.EventDur
                })
                .OrderByDescending(x => x.EventDate)
                .ThenBy(x => x.EventName);

            // Step 4: Get total count — single COUNT(*) SQL query
            int totalCount = projectedQuery.Count();

            // Step 5: Fetch ONLY current page rows — OFFSET/FETCH in SQL
            // Avoids loading ALL rows into memory
            var pagedData = projectedQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList(); // Single paginated SQL query executed here

            // Step 6: Set MonthName in memory (only on current page rows — max 20 items)
            foreach (var item in pagedData)
            {
                item.MonthName = item.EventDate.ToString("MMMM");
            }

            // Step 7: Use StaticPagedList — avoids re-querying for pagination
            // StaticPagedList(subset, pageNumber, pageSize, totalCount)
            var pagedList = new StaticPagedList<EventDetailViewModel>(
                pagedData, pageNumber, pageSize, totalCount);

            // ViewBag for filter persistence
            ViewBag.fromMonthYear = fromMonthYear;
            ViewBag.toMonthYear = toMonthYear;
            ViewBag.badgeFilterType = badgeFilterType;
            ViewBag.badgeId = badgeId;

            return View(pagedList);
        }

        public ActionResult ExportDetailEventData(string fromMonthYear, string toMonthYear,
            string badgeFilterType = "all", string badgeId = "")
        {
            // ===================== VALIDATE =====================
            if (string.IsNullOrWhiteSpace(fromMonthYear) || string.IsNullOrWhiteSpace(toMonthYear))
            {
                TempData["ExportError"] = "From and To Month/Year are required for export.";
                return RedirectToAction("DetailEventData", new { fromMonthYear, toMonthYear, badgeFilterType, badgeId });
            }

            // ===================== PARSE DATE =====================
            DateTime startDate;
            DateTime toMonthParsed;

            if (!DateTime.TryParseExact(fromMonthYear, "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out startDate))
            {
                TempData["ExportError"] = "Invalid From date format.";
                return RedirectToAction("DetailEventData", new { fromMonthYear, toMonthYear, badgeFilterType, badgeId });
            }

            if (!DateTime.TryParseExact(toMonthYear, "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out toMonthParsed))
            {
                TempData["ExportError"] = "Invalid To date format.";
                return RedirectToAction("DetailEventData", new { fromMonthYear, toMonthYear, badgeFilterType, badgeId });
            }

            startDate = new DateTime(startDate.Year, startDate.Month, 1);
            var endDate = new DateTime(toMonthParsed.Year, toMonthParsed.Month, 1).AddMonths(1);

            if (startDate >= endDate)
            {
                TempData["ExportError"] = "From date cannot be greater than To date.";
                return RedirectToAction("DetailEventData", new { fromMonthYear, toMonthYear, badgeFilterType, badgeId });
            }

            // ===================== QUERY — same as DetailEventData =====================
            var baseQuery = db.EventAttendees
                .AsNoTracking()
                .Where(ea =>
                    ea.EventRegs.EventDate >= startDate &&
                    ea.EventRegs.EventDate < endDate);

            if (badgeFilterType == "specific" && !string.IsNullOrWhiteSpace(badgeId))
            {
                var trimmedBadge = badgeId.Trim();
                baseQuery = baseQuery.Where(ea =>
                    ea.Attendee.EmpBadgeId == trimmedBadge);
            }

            // Fetch ALL rows for export (no pagination)
            var data = baseQuery
                .Select(ea => new EventDetailViewModel
                {
                    BadgeId = ea.Attendee.EmpBadgeId,
                    EmpName = ea.Attendee.EmpName,
                    EmpDept = ea.Attendee.EmpDept,
                    EventName = ea.EventRegs.EventName,
                    EventDate = ea.EventRegs.EventDate,
                    MonthName = "", // set after ToList()
                    Year = ea.EventRegs.EventDate.Year,
                    EventDuration = ea.EventRegs.EventDur
                })
                .OrderByDescending(x => x.EventDate)
                .ThenBy(x => x.BadgeId)
                .ToList();

            // Set MonthName in memory
            foreach (var item in data)
            {
                item.MonthName = item.EventDate.ToString("MMMM");
            }

            // ===================== EXPORT TO EXCEL =====================
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Event Detail");

                // ===== Header Style =====
                var headerRange = worksheet.Cells["A1:I1"];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(59, 155, 145)); // #3B9B91
                headerRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                headerRange.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                headerRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                // ===== Headers =====
                worksheet.Cells["A1"].Value = "No";
                worksheet.Cells["B1"].Value = "Badge ID";
                worksheet.Cells["C1"].Value = "Employee Name";
                worksheet.Cells["D1"].Value = "Department";
                worksheet.Cells["E1"].Value = "Event Name";
                worksheet.Cells["F1"].Value = "Event Date";
                worksheet.Cells["G1"].Value = "Month";
                worksheet.Cells["H1"].Value = "Year";
                worksheet.Cells["I1"].Value = "Duration";

                // ===== Data Rows =====
                int row = 2;
                foreach (var item in data)
                {
                    worksheet.Cells[row, 1].Value = row - 1; // No
                    worksheet.Cells[row, 2].Value = item.BadgeId;
                    worksheet.Cells[row, 3].Value = item.EmpName;
                    worksheet.Cells[row, 4].Value = item.EmpDept;
                    worksheet.Cells[row, 5].Value = item.EventName;
                    worksheet.Cells[row, 6].Value = item.EventDate.ToString("dd MMM yyyy");
                    worksheet.Cells[row, 7].Value = item.MonthName;
                    worksheet.Cells[row, 8].Value = item.Year;
                    worksheet.Cells[row, 9].Value = item.EventDuration;

                    // Alternate row color for readability
                    if (row % 2 == 0)
                    {
                        var rowRange = worksheet.Cells[row, 1, row, 9];
                        rowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(235, 250, 249)); // light teal
                    }

                    row++;
                }

                // ===== Border for all data cells =====
                if (data.Any())
                {
                    var dataRange = worksheet.Cells[1, 1, row - 1, 9];
                    dataRange.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    dataRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    dataRange.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    dataRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                }

                // ===== Column Widths =====
                worksheet.Column(1).Width = 6;  // No
                worksheet.Column(2).Width = 15; // Badge ID
                worksheet.Column(3).Width = 25; // Employee Name
                worksheet.Column(4).Width = 20; // Department
                worksheet.Column(5).Width = 30; // Event Name
                worksheet.Column(6).Width = 18; // Event Date
                worksheet.Column(7).Width = 15; // Month
                worksheet.Column(8).Width = 10; // Year
                worksheet.Column(9).Width = 15; // Duration

                // ===== Freeze header row =====
                worksheet.View.FreezePanes(2, 1);

                // ===== File name with filter info =====
                string fileName = $"EventDetail_{fromMonthYear}_{toMonthYear}" +
                                  $"{(badgeFilterType == "specific" && !string.IsNullOrWhiteSpace(badgeId) ? "_" + badgeId : "_All")}" +
                                  ".xlsx";

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
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


        private void ClearEventRegsCache()
        {
            var cache = HttpRuntime.Cache;
            var keysToRemove = new List<String>();

            foreach (System.Collections.DictionaryEntry entry in cache)
            {
                if (entry.Key is string key && key.StartsWith("EventRegs_"))
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
