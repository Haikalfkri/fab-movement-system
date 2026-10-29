using ok1.Data;
using ok1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ok1.Services
{
    public class logweek
    {
        private ok1Context db = new ok1Context();
        private masterContext og = new masterContext();

        public string GetLogWeek(DateTime date)
        {
            // Validation
            if (date == DateTime.MinValue || date == DateTime.MaxValue)
                return "LW0000";

            // Find the last Saturday of September for the current year
            DateTime lastSaturdayOfSeptember = GetLastSaturdayOfSeptember(date.Year);

            // Fiscal year starts from the first Saturday after the last Saturday of September
            DateTime fiscalYearStart = lastSaturdayOfSeptember.AddDays(7);

            int fiscalYear;
            if (date >= fiscalYearStart)
            {
                fiscalYear = lastSaturdayOfSeptember.Year + 1;
            }
            else
            {
                // If before fiscal year start, use previous year's fiscal year start
                lastSaturdayOfSeptember = GetLastSaturdayOfSeptember(date.Year - 1);
                fiscalYearStart = lastSaturdayOfSeptember.AddDays(7);
                fiscalYear = lastSaturdayOfSeptember.Year + 1;
            }

            int daysSinceFiscalStart = (date.Date - fiscalYearStart.Date).Days;
            if (daysSinceFiscalStart < 0)
                return "LW0000";

            int weekNumber = (daysSinceFiscalStart / 7) + 1;
            string yearIdentifier = (fiscalYear % 100).ToString("D2");

            return $"LW{yearIdentifier}{weekNumber:D2}";
        }

        // Helper: Get the last Saturday of September for a given year
        private DateTime GetLastSaturdayOfSeptember(int year)
        {
            DateTime lastDayOfSeptember = new DateTime(year, 9, 30);
            int daysToSubtract = ((int)lastDayOfSeptember.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
            if (daysToSubtract == 0 && lastDayOfSeptember.DayOfWeek != DayOfWeek.Saturday)
                daysToSubtract = 7;
            return lastDayOfSeptember.AddDays(-daysToSubtract);
        }
    }
}