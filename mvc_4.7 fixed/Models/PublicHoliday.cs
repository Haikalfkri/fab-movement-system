using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ok1.Models
{

    public class PublicHoliday
    {
        public int Id { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Start date is required.")]
        public DateTime DateFrom { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "End date is required.")]
        public DateTime DateTo { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Remark is required.")]
        public string RemarkDate { get; set; }
    }

}