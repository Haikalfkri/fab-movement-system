using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ok1.Models
{

    public class Breastfeeding
    {
        public int Id { get; set; }

        public string EmpBadgeId { get; set; }
        public string EmpName { get; set; }
        public string EmpDept { get; set; }



        public string Status { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = " Baby date of birth is required.")]
        public DateTime DateOfBirth { get; set; }
        public DateTime ExpDate { get; set; }
    }
}