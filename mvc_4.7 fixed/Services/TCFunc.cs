using ok1.Data;
using ok1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Windows.Automation.Peers;
using Microsoft.Ajax.Utilities;
using System.EnterpriseServices.Internal;

namespace ok1.Services
{
    public class TCFunc
    {
        private ok1Context db = new ok1Context();
        private masterContext master = new masterContext();

/*DB STORED*/
        //GETBusui
        public async Task UpdateFabMoveWithBusui(List<FabMove> fabMoveData, List<Breastfeeding> busuiData)
        {
            var busuiDictionary = busuiData
                .GroupBy(b => b.EmpBadgeId.Trim().ToLower())
                .ToDictionary(g => g.Key, g => g.ToList());

            TimeSpan deduction = TimeSpan.FromMinutes(30);
            int updatedCount = 0;

            foreach (var fabMove in fabMoveData)
            {
                var badgeId = fabMove.EmpBadgeId.Trim().ToLower();

                if (busuiDictionary.TryGetValue(badgeId, out var busuiList))
                {
                    // Find if any busui record covers this workday
                    var busui = busuiList.FirstOrDefault(b =>
                        fabMove.WorkDay.Date >= b.DateOfBirth.Date &&
                        fabMove.WorkDay.Date <= b.ExpDate.Date);

                    if (busui != null)
                    {
                        if (fabMove.Busui != deduction)
                        {
                            fabMove.Busui = deduction;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        if (fabMove.Busui != TimeSpan.Zero)
                        {
                            fabMove.Busui = TimeSpan.Zero;
                            updatedCount++;
                        }
                    }
                }
                else
                {
                    if (fabMove.Busui != TimeSpan.Zero)
                    {
                        fabMove.Busui = TimeSpan.Zero;
                        updatedCount++;
                    }
                }
            }
            await db.SaveChangesAsync();
        }
        /*STORED RELIGION AND GENDER*/
        public async Task UpdateGenderAndReligion(List<FabMove> fabmove)
        {
            // Preload gmds data into a dictionary (key = BadgeId)
            var gmdsDictionary = await master.gmds
                .ToDictionaryAsync(g => g.BadgeId.Trim().ToLower());

            foreach (var fabMove in fabmove)
            {
                var empBadgeIdKey = fabMove.EmpBadgeId.Trim().ToLower(); // Normalize key
                if (gmdsDictionary.TryGetValue(empBadgeIdKey, out var trial))
                {
                    fabMove.Gender = trial.Gender.Trim() ?? "unknown";
                    fabMove.Religion = trial.Religion.Trim() ?? "unknown";
                    fabMove.Workplace = trial.Production.Trim() ?? "unknown";
                }
            }
            await db.SaveChangesAsync();
        }

        /*DEFINE FRIDAY PRAY*/
        //public string GetFridayPermit(string badgeId, string day, string shift) //UI
        //{
        //    var trial = master.gmds.FirstOrDefault(x => x.BadgeId == badgeId);
        //    if (trial == null)
        //    {
        //        return "00:00:00";
        //    }
        //    string gender = trial.Gender.ToLowerInvariant().Trim() ?? "";
        //    string religion = trial.Religion.ToLowerInvariant().Trim() ?? "";
        //    day = day?.ToLowerInvariant().Trim() ?? "";
        //    shift = shift.ToLowerInvariant().Trim() ?? "";
        //    if (string.IsNullOrEmpty(gender) || string.IsNullOrEmpty(religion))
        //    {
        //        return "00:00:00";
        //    }
        //    if (!(gender == "male" && religion == "muslim" && day == "friday"))
        //    {
        //        return "00:00:00";
        //    }
        //    if (day == "friday")
        //    {
        //        //if (shift == "morning shift" && gender == "male" && religion == "muslim")
        //        if (shift == "morning shift")
        //        {
        //            return "01:45:00";
        //        }

        //        //else if (shift == "normal shift" && gender == "male" && religion == "muslim")
        //        else if (shift == "normal shift")
        //        {
        //            return "01:30:00";
        //        }
        //    }
        //    return "00:00:00";
        //}
        public string GetFridayPermit(string badgeId, string day, string shift)
        {
            var trial = master.gmds.FirstOrDefault(x => x.BadgeId == badgeId);
            if (trial == null)
            {
                return "00:00:00";
            }

            string gender = trial.Gender?.Trim().ToLowerInvariant() ?? "";
            string religion = trial.Religion?.Trim().ToLowerInvariant() ?? "";
            day = day?.Trim().ToLowerInvariant() ?? "";
            shift = shift?.Trim().ToLowerInvariant() ?? "";

            if (string.IsNullOrEmpty(gender) || string.IsNullOrEmpty(religion))
            {
                return "00:00:00";
            }
            if (day == "friday")
            {   
                if (shift == "morning shift" && gender == "male" && religion == "muslim")
                {
                    return "01:45:00";
                }
                else if (shift == "normal shift")
                {
                    return "01:00:00";
                }
                else
                {
                    return "00:00:00";
                }
            }
            return "00:00:00";
        }

        public async Task UpdateFridayPray(List<FabMove> fabmove)
        {
            // Preload all gmds records into a dictionary for fast lookup
            var gmdsDictionary = await master.gmds
                .ToDictionaryAsync(
                    g => g.BadgeId.Trim().ToLowerInvariant(),
                    g => new
                    {
                        Gender = g.Gender?.Trim().ToLowerInvariant() ?? "",
                        Religion = g.Religion?.Trim().ToLowerInvariant() ?? ""
                    });

            foreach (var move in fabmove)
            {
                var badgeIdKey = move.EmpBadgeId?.Trim().ToLowerInvariant() ?? "";
                if (gmdsDictionary.TryGetValue(badgeIdKey, out var trial))
                {
                    string gender = trial.Gender.ToLower();
                    string religion = trial.Religion.ToLower();
                    string day = move.Day?.Trim().ToLowerInvariant() ?? "";
                    string shift = move.Shift?.Trim().ToLowerInvariant() ?? "";

                    if (day == "friday")
                    {
                        if (shift == "morning shift" && gender == "male" && religion == "muslim")
                        {
                            move.FridayPray = "01:45:00";
                        }
                        //else if (shift == "morning shift")
                        //{
                        //    move.FridayPray = "01:45:00";
                        //}
                        else if (shift == "normal shift")
                        {
                            move.FridayPray = "01:00:00";
                        }
                        else
                        {
                            move.FridayPray = "00:00:00";
                        }
                    }
                    else
                    {
                        move.FridayPray = "00:00:00";
                    }
                }
                else
                {
                    move.FridayPray = "00:00:00";
                }
            }

            await db.SaveChangesAsync();
        }



        /*FAB AMEND SECTION*/
        //public string GetFabDur(string badgeId, DateTime workDay, List<FormRequest> amendData)
        //{
        //    var employee = amendData.Where(e => e.Badge == badgeId && e.tgl_keluar == workDay.Date);
        //    var totalDuration = TimeSpan.FromSeconds(employee.Sum(x => x.durasi.TotalSeconds));
        //    if (employee == null) return "00:00:00";
        //    return totalDuration > TimeSpan.Zero ? totalDuration.ToString(@"hh\:mm\:ss") : "00:00:00";
        //}
        public string GetFabDur(string badgeId, DateTime workDay, List<FormRequest> amendData)
        {
            // Only include records with status "amended" 
            var employee = amendData
                .Where(e => e.Badge == badgeId
                    && e.tgl_keluar.Date == workDay.Date
                    && e.status != null
                    && e.status.Trim().ToLower() == "amended");

            var totalDuration = TimeSpan.FromSeconds(employee.Sum(x => x.durasi.TotalSeconds));
            // No need to check employee == null
            return totalDuration > TimeSpan.Zero ? totalDuration.ToString(@"hh\:mm\:ss") : "00:00:00";
        }

        //Get Amend data
        //public async Task UpdateFabMoveWithFormData(List<FabMove> fabMoves, List<FormRequest> formData)
        //{
        //    var amendDict = formData
        //        .GroupBy(e => new { e.Badge, Tgl = e.tgl_keluar.Date })
        //        .ToDictionary(
        //            g => g.Key,
        //            g => TimeSpan.FromSeconds(g.Sum(f => f.durasi.TotalSeconds)).ToString(@"hh\:mm\:ss")
        //        );

        //    foreach (var move in fabMoves)
        //    {
        //        var key = new { Badge = move.EmpBadgeId, Tgl = move.WorkDay.Date }; 
        //        if (amendDict.TryGetValue(key, out string amendDur)) 
        //        {
        //            move.Amend = amendDur; // Set the amendment duration
        //        }
        //        else
        //        {
        //            move.Amend = "00:00:00";
        //        }
        //    }

        //    await db.SaveChangesAsync();
        //}
        public async Task UpdateFabMoveWithFormData(
            List<FabMove> fabMoves, 
            List<FormRequest> formData,
            List<Attendees> attendeeData,
            List<EventAttendee> eventAttData,
            List<EventReg> eventData
        )
        {
            // Build event duration lookup: (BadgeId, EventDate, StartTime, EndTime, Duration) => true
            var eventDurationSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var attendee in attendeeData)
            {
                var badgeId = attendee.EmpBadgeId?.Trim().ToUpper();
                if (string.IsNullOrEmpty(badgeId))
                    continue;

                // Get all EventRegIds for this attendee
                var eventRegIds = eventAttData
                    .Where(ea => ea.AttendeesId == attendee.Id)
                    .Select(ea => ea.EventRegId)
                    .ToList();

                if (!eventRegIds.Any())
                    continue;

                foreach (var ev in eventData.Where(e => eventRegIds.Contains(e.Id)))
                {
                    var eventDate = ev.EventDate.Date;
                    var startTime = ev.StartTime;
                    var endTime = ev.EndTime;
                    var duration = (ev.EndTime - ev.StartTime).ToString(@"hh\:mm\:ss");
                    var key = $"{badgeId}|{eventDate:yyyyMMdd}|{startTime:hhmmss}|{endTime:hhmmss}|{duration}";
                    eventDurationSet.Add(key);
                }
            }

            // Filter only "amended" status in formData
            var amendedData = formData
                .Where(e => e.status != null && e.status.Trim().ToLower() == "amended");

            // Build amendDict as before, but keep the group for later check
            var amendDict = amendedData
                .GroupBy(e => new { e.Badge, Tgl = e.tgl_keluar.Date })
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList()
                );

            // For each FabMove, set Amend to "00:00:00" if event duration exists, else use form duration
            foreach (var move in fabMoves)
            {
                var badgeKey = move.EmpBadgeId?.Trim().ToUpper();
                var dateKey = move.WorkDay.Date;
                var dictKey = new { Badge = badgeKey, Tgl = dateKey };

                if (amendDict.TryGetValue(dictKey, out List<FormRequest> formList) && formList.Any())
                {
                    // If any form in the group matches an event, set Amend to 00:00:00
                    string amendDur = "00:00:00";
                    foreach (var form in formList)
                    {
                        var formStart = form.jam_keluar;
                        var formEnd = form.jam_kembali;
                        var formDur = form.durasi.ToString(@"hh\:mm\:ss");
                        var key = $"{badgeKey}|{dateKey:yyyyMMdd}|{formStart:hhmmss}|{formEnd:hhmmss}|{formDur}";
                        if (!eventDurationSet.Contains(key))
                        {
                            // If not matched, accumulate duration
                            if (amendDur == "00:00:00")
                                amendDur = formDur;
                            else
                            {
                                // Sum durations if multiple unmatched forms
                                TimeSpan prev = TimeSpan.Parse(amendDur);
                                TimeSpan curr = form.durasi;
                                amendDur = (prev + curr).ToString(@"hh\:mm\:ss");
                            }
                        }
                    }
                    move.Amend = amendDur;
                }
                else
                {
                    move.Amend = "00:00:00";
                }
            }

            await db.SaveChangesAsync();


            //// Filter only "amended" status
            //var amendedData = formData
            //    .Where(e => e.status != null && e.status.Trim().ToLower() == "amended");

            //var amendDict = amendedData
            //    .GroupBy(e => new { e.Badge, Tgl = e.tgl_keluar.Date })
            //    .ToDictionary(
            //        g => g.Key,
            //        g => TimeSpan.FromSeconds(g.Sum(f => f.durasi.TotalSeconds)).ToString(@"hh\:mm\:ss")
            //    );

            //foreach (var move in fabMoves)
            //{
            //    var key = new { Badge = move.EmpBadgeId, Tgl = move.WorkDay.Date };
            //    if (amendDict.TryGetValue(key, out string amendDur))
            //    {
            //        move.Amend = amendDur;
            //    }
            //    else
            //    {
            //        move.Amend = "00:00:00";
            //    }
            //}

            //await db.SaveChangesAsync();
        }


        /*EXIT PERMISSION*/
        // To get shift
        public string GetFabWithExitPermit(string badgeId, string shift, DateTime workDay, List<tbl_Testing> testing)
        {
            // Check if the testing list is null or empty
            if (testing == null || !testing.Any())
            {
                return "00:00:00";
            }

            // Adjust workDay for night shifts (if applicable)
            if (!string.IsNullOrEmpty(shift) && shift.ToLower().Contains("malam"))
            {
                workDay = workDay.AddDays(-1);
            }

            // Filter the matching record from testing
            var checkDur = testing
                .Where(c => c.OutOf.Trim().ToLower() == "perusahaan" 
                            && c.Status.Trim().ToLower() != "waiting approval"
                            && c.BadgeId == badgeId 
                            && c.SubmitTime?.Date == workDay.Date) 
                .FirstOrDefault(); // Get the first record that matches the criteria

            // Return the Duration value if a matching record is found, otherwise return "00:00:00"
            return checkDur != null && checkDur.Duration != null
                ? checkDur.Duration.Value.ToString(@"hh\:mm\:ss") 
                : "00:00:00"; 
        }

        public async Task UpdateFabMoveExitPermit(List<FabMove> fabMoves, List<tbl_Testing> testings)
        {
            if (fabMoves == null || testings == null || !testings.Any())
                return;

            // Filter the testings list to include only valid records
            var filteredTestings = testings
                .Where(t => t.SubmitTime.HasValue
                    && t.OutOf != null && t.OutOf.Trim().ToLower() == "perusahaan"
                    && t.Status != null && t.Status.Trim().ToLower() != "waiting approval")
                .ToList();

            // Group the filtered records by BadgeId and WorkDay, adjust for night shift
            var testingDictionary = filteredTestings
                .GroupBy(t =>
                {
                    var badgeId = t.BadgeId?.Trim().ToLower() ?? "";
                    var shift = t.Shift?.Trim().ToLower() ?? "";
                    var workDay = t.SubmitTime.Value.Date;
                    if (shift.Contains("malam"))
                    {
                        workDay = workDay.AddDays(-1);
                    }
                    return new { BadgeId = badgeId, WorkDay = workDay };
                })
                .ToDictionary(g => g.Key, g => g.First());

            // Update each FabMove with matching data from the testing dictionary
            foreach (var move in fabMoves)
            {
                var badgeIdKey = move.EmpBadgeId?.Trim().ToLower() ?? "";
                var shift = move.Shift?.Trim().ToLower() ?? "";
                var workDayKey = move.WorkDay.Date;

                // Only adjust lookup key, not the actual move.WorkDay
                if (shift.Contains("malam"))
                {
                    workDayKey = workDayKey.AddDays(-1);
                }

                var key = new { BadgeId = badgeIdKey, WorkDay = workDayKey };

                if (testingDictionary.TryGetValue(key, out var match))
                {
                    move.ExitPermit = match.Duration?.ToString(@"hh\:mm\:ss") ?? "00:00:00";
                }
                else
                {
                    move.ExitPermit = "00:00:00";
                }
            }

            // Save changes to the database
            await db.SaveChangesAsync();
        }


        /*EVENT SECTION*/
        //To update fab report with event duration
        public string GetEventDurByBadgeDate(string badgeId, DateTime workDay, List<Attendees> attendeeData, List<EventAttendee> eventAttData, List<EventReg> eventData)
        {
            // Check if the badgeId exists in attendeeData
            var attendee = attendeeData.FirstOrDefault(a => a.EmpBadgeId == badgeId);
            if (attendee == null)
            {
                // User is not registered as an attendee
                return "00:00:00";
            }

            // Get all EventRegIds associated with the attendee
            var eventRegIds = eventAttData
                .Where(ea => ea.AttendeesId == attendee.Id)
                .Select(ea => ea.EventRegId)
                .ToList();

            // If no EventRegIds are found, the user is not registered in any specific event
            if (!eventRegIds.Any())
            {
                return "00:00:00";
            }

            // Filter events by the EventRegIds and match against the specified workDay
            var matchedDurations = eventData
                .Where(e => eventRegIds.Contains(e.Id) && e.EventDate.Date == workDay.Date)
                .ToList();

            if (!matchedDurations.Any())
            {
                return "00:00:00";
            }

            TimeSpan totalDuration = TimeSpan.Zero;
            foreach (var ev in matchedDurations)
            {
                totalDuration += ev.EndTime - ev.StartTime;
            }

            // Return the total duration in hh:mm:ss format
            return totalDuration.ToString(@"hh\:mm\:ss");
        }

        public async Task UpdateFabMoveWithEvent(List<FabMove> moves, List<Attendees> attendeeData, List<EventReg> eventData, List<EventAttendee> eventAttData)
        {
            //// Get all BadgeIds from attendeeData for validation
            //var BadgeIds = attendeeData.Select(a => a.EmpBadgeId).ToList();

            //foreach (var fabMove in moves)
            //{
            //    // Check if the current EmpBadgeId exists in the list of attendees
            //    if (BadgeIds.Contains(fabMove.EmpBadgeId))
            //    {
            //        // Calculate the event duration using the refactored method
            //        fabMove.Ahm = GetEventDurByBadgeDate(fabMove.EmpBadgeId, fabMove.WorkDay, attendeeData, eventAttData, eventData);
            //    }
            //    else
            //    {
            //        // If the user is not registered in any event, set Ahm to "00:00:00"
            //        fabMove.Ahm = "00:00:00";
            //    }
            //}

            //// Save all changes to the database
            //await db.SaveChangesAsync();
            // 1. Build lookup: EmpBadgeId -> Attendees.Id
            var badgeToAttendeeId = attendeeData
                .GroupBy(a => a.EmpBadgeId)
                .ToDictionary(g => g.Key, g => g.First().Id);

            // 2. Build lookup: AttendeesId -> List<EventRegId>
            var attendeeIdToEventRegIds = eventAttData
                .GroupBy(ea => ea.AttendeesId)
                .ToDictionary(g => g.Key, g => g.Select(ea => ea.EventRegId).ToList());

            // 3. Build lookup: EventRegId -> EventReg
            var eventRegIdToEvent = eventData
                .ToDictionary(e => e.Id, e => e);

            foreach (var fabMove in moves)
            {
                if (!badgeToAttendeeId.TryGetValue(fabMove.EmpBadgeId, out int attendeeId))
                {
                    fabMove.Ahm = "00:00:00";
                    continue;
                }

                if (!attendeeIdToEventRegIds.TryGetValue(attendeeId, out var eventRegIds) || eventRegIds.Count == 0)
                {
                    fabMove.Ahm = "00:00:00";
                    continue;
                }

                // Find all events for this attendee on the same workDay
                TimeSpan totalDuration = TimeSpan.Zero;
                foreach (var eventRegId in eventRegIds)
                {
                    if (eventRegIdToEvent.TryGetValue(eventRegId, out var ev) && ev.EventDate.Date == fabMove.WorkDay.Date)
                    {
                        totalDuration += ev.EndTime - ev.StartTime;
                    }
                }

                fabMove.Ahm = totalDuration > TimeSpan.Zero
                    ? totalDuration.ToString(@"hh\:mm\:ss")
                    : "00:00:00";
            }

            await db.SaveChangesAsync();
        }

        /*PUBLIC HOLIDAY*/
        public async Task UpdateWithHoliday(List<PublicHoliday> holidays, List<FabMove> moves)
        {
            foreach (var m in moves)
            {
                var holiday = holidays.FirstOrDefault(h => h.DateFrom ==  m.WorkDay && m.WorkDay <= h.DateTo);
                m.PublicHoliday = holiday?.RemarkDate ?? "No Holiday";
            }
            await db.SaveChangesAsync();
        }
        public string GetPubHoliday(DateTime workDay, List<PublicHoliday> holidays)
        {
            var viewHoliday = holidays.FirstOrDefault(h => h.DateFrom == workDay.Date && h.DateTo == workDay.Date);
            if (viewHoliday == null)
            {
                return "No holiday";
            }
            return viewHoliday.RemarkDate;
        }
        //Get targetTime
        public string GetTargetTime(string empBadgeId, string shift, List<TargetTime> targetTimes)
        {
            var empTarget = targetTimes.FirstOrDefault(t => t.EmpBadgeId.Trim().Equals(empBadgeId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (empTarget == null)
            {
                return "00:00:00";
            }
            shift = shift.ToLower().Trim();
            TimeSpan targetTime;
            switch (shift) //to generate at the same time
            {
                case "normal shift":
                    targetTime = empTarget.NormalShift;
                    break;
                case "morning shift":
                    targetTime = empTarget.MorningShift;
                    break;
                case "afternoon shift":
                    targetTime = empTarget.AfternoonShift;
                    break;
                case "night shift":
                    targetTime = empTarget.NightShift;
                    break;
                default:
                    return "00:00:00";
            }
            // Return formatted time
            return targetTime.ToString(@"hh\:mm\:ss");
        }

        /*GET TARGET TIME*/
        public void UpdateTargetTime(List<TargetTime> target, List<FabMove> moves)
        {
            // Step 1: Preload TargetTime data into a dictionary for fast lookups
            var targetTimeDict = target
                .GroupBy(t => t.EmpBadgeId.Trim().ToLower()) // Normalize the key
                .ToDictionary(g => g.Key, g => g.First());   // Ensure only one record per EmpBadgeId

            // Step 2: Iterate over FabMove records to update TargetTime
            foreach (var move in moves)
            {
                var empBadgeIdKey = move.EmpBadgeId.Trim().ToLower(); // Normalize the key

                // Lookup the target time for the employee
                if (!targetTimeDict.TryGetValue(empBadgeIdKey, out var empTarget))
                {
                    move.TargetTime = "00:00:00"; // Default when no match is found
                    continue;
                }

                // Normalize the shift
                var shift = move.Shift?.ToLower().Trim();

                // Determine the target time based on the shift
                TimeSpan targetTime;
                switch (shift)
                {
                    case "normal shift":
                        targetTime = empTarget.NormalShift;
                        break;
                    case "morning shift":
                        targetTime = empTarget.MorningShift;
                        break;
                    case "afternoon shift":
                        targetTime = empTarget.AfternoonShift;
                        break;
                    case "night shift":
                        targetTime = empTarget.NightShift;
                        break;
                    default:
                        move.TargetTime = "00:00:00"; // Invalid shift
                        continue;
                }

                // Assign the formatted target time to move.TargetTime
                move.TargetTime = targetTime.ToString(@"hh\:mm\:ss");
            }

            // Step 3: Save changes to the database in one go
            db.SaveChanges();
        }


        // --- HELPER METHOD ----

        //private Dictionary<(string BadgeId, DateTime Workday, string Duration), bool> BuildEventDurationLookup(
        //    List<FabMove> fabMoves,
        //    List<Attendees> attendeeData,
        //    List<EventAttendee> eventAttData,
        //    List<EventReg> eventData
        //)
        //{
        //    var lookup = new Dictionary<(string, DateTime, string), bool>(StringComparer.OrdinalIgnoreCase);

        //    foreach (var move in fabMoves)
        //    {
        //        var eventDuration = GetEventDurByBadgeDate(move.EmpBadgeId, move.WorkDay, attendeeData, eventAttData, eventData);
        //        if (eventDuration != "00:00:00")
        //        {
        //            lookup[(move.EmpBadgeId, move.WorkDay.Date, eventDuration)] = true;
        //        }
        //    }
        //    return lookup;
        //}
    }
}