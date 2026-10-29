using ok1.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ok1.Services
{
    public class UserAccess
    {
        private readonly ok1Context _db;

        public UserAccess(ok1Context db)
        {
            _db = db;
        }

        // Get current user's windows account (lowercase, no domain)
        public string GetUserAccount(string windowsIdentityName)
        {
            return windowsIdentityName?.Split('\\').LastOrDefault()?.ToLower();
        }

        // Check if the user is a manager for any badge
        public bool IsManager(string userAcc)
        {
            // Get all manager records with non-empty Manager field
            var managerList = _db.ManagerList
                .AsNoTracking()
                .Where(m => !string.IsNullOrEmpty(m.Manager))
                .ToList();

            return managerList.Any(m =>
                System.Text.RegularExpressions.Regex
                    .Split(m.Manager, @"\$+")
                    .Select(x => x.Trim().ToLower())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Contains(userAcc)
            );
        }

        // Check if user is a manager for specific badge
        public bool IsManagerForBadge(string userAcc, string badgeId)
        {
            // Get the manager record for the specified badge
            var record = _db.ManagerList.AsNoTracking()
                .FirstOrDefault(m => m.BadgeId == badgeId && !string.IsNullOrEmpty(m.Manager));

            if (record == null) return false;

            // Split the Manager field by '$', trim and lowercase each entry, and check if userAcc is in the list
            var managers = System.Text.RegularExpressions.Regex
                .Split(record.Manager, @"\$+")
                .Select(x => x.Trim().ToLower())
                .Where(x => !string.IsNullOrEmpty(x));
            return managers.Contains(userAcc);
        }

        // Check if user is admin
        public bool IsAdmin(string windAcc)
        {
            //if (string.IsNullOrWhiteSpace(windAcc))
            //{
            //    return false;
            //}

            //// Normalize WindAcc (ensure INFINEON\ prefix)
            //string formatted = windAcc.Trim();
            //if (!formatted.StartsWith("INFINEON\\", StringComparison.OrdinalIgnoreCase))
            //{
            //    formatted = "INFINEON\\" + formatted;
            //}

            //return _db.Credentials.Any(u => u.WindAcc.Equals(formatted, StringComparison.OrdinalIgnoreCase) && u.isAdmin);
        
            if (string.IsNullOrWhiteSpace(windAcc))
            {
                return false;
            }

            var incoming = windAcc.Trim();
            var incomingUser = incoming.Contains("\\")
                ? incoming.Split('\\').Last().ToLower()
                : incoming.ToLower();

            var incomingPattern = "\\" + incomingUser;

            return _db.Credentials.Any(u =>
                u.isAdmin
                && u.WindAcc != null
                && (u.WindAcc.ToLower() == incomingUser || u.WindAcc.ToLower().EndsWith(incomingPattern.ToLower()))
            );
        }
    }
}