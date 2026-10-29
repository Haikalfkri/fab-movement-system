using ok1.Data;
using ok1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ok1.Services
{
    public class TimeClass
    {
        private ok1Context db = new ok1Context();
        private masterContext og = new masterContext();


        /*COUNT TIMECLASS*/
        public string CalculateTimeClass(
            string day,
            TimeSpan? crTime,
            string fridayPray,
            TimeSpan? busui,
            string ahm,
            string exitPermission,
            string fabAmend,
            string targetTimes,
            string office,
            string shift)
        {
            // Weekend + normal shift check
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
            var totalTime = crTimeValue + pray + busuiValue + qm + amend + permit;

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

        public void UpdateTimeClass(
          //string badgeId,
          //List<FabMove> moves)
            List<FabMove> moves,
            Dictionary<string, string> badgeToWorkplace,
            Dictionary<string, TargetTime> badgeToTargetTime)
        {
            foreach (var data in moves)
            {
                var badgeId = data.EmpBadgeId;
                badgeToWorkplace.TryGetValue(badgeId, out string office);

                // Jika Production(NH), langsung Passed
                if (office == "production(nh)")
                {
                    data.TimeClass = "Passed";
                    continue;
                }

                // Ambil nilai spesifik dari setiap entri
                string day = data.Day;
                TimeSpan? crTime = data.CrTime;
                string fridayPray = data.FridayPray;
                TimeSpan? busui = data.Busui;
                string ahm = data.Ahm;
                string exitPermission = data.ExitPermit;
                string fabAmend = data.Amend;

                // Ambil TargetTime record dari dictionary
                badgeToTargetTime.TryGetValue(badgeId, out TargetTime targetTimeRecord);

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
                        data.Day,
                        data.CrTime,
                        data.FridayPray,
                        data.Busui,
                        data.Ahm,
                        data.ExitPermit,
                        data.Amend,
                        targetTime.ToString(@"hh\:mm\:ss"),
                        office, // from dictionary
                        data.Shift
                     );

                    data.TimeClass = timeClass;
                }
                else
                {
                    data.TimeClass = "Failed";
                }
            }
        }
    }
}