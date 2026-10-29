using ok1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ok1.Models
{

    public class EventAttendee
    {
        public int Id { get; set; }

        public int EventRegId { get; set; }
        public virtual EventReg EventRegs { get; set; }

        public int AttendeesId { get; set; }
        public virtual Attendees Attendee { get; set; }
    }
}