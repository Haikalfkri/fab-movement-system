using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ok1.Models
{

    public class Attendees
    {
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Participant id is required.")]
        public string EmpBadgeId { get; set; }
        public string EmpName { get; set; }
        public string EmpDept { get; set; }

        public virtual ICollection<EventAttendee> EventAttendees { get; set; }

        public static implicit operator string(Attendees v)
        {
            throw new NotImplementedException();
        }
    }
}