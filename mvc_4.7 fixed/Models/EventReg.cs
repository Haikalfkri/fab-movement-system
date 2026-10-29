using ok1.Data;
using ok1.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ok1.Models
{

    public class EventReg
    {
        public int Id { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Trainer ID is required.")]
        public string TrainerId { get; set; }
        public string TrainerName { get; set; }
        public DateTime EventDate { get; set; }
        public string EventName { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string EventDur { get; set; }
        [NotMapped]
        public int TotalAttendees
        {
            get
            {
                using (var db = new ok1Context())
                {
                    return db.EventAttendees.Count(e => e.EventRegId == Id);
                }

            }
        }

        public virtual ICollection<EventAttendee> EventAttendees { get; set; }

    }
}