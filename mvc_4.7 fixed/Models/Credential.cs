using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Web.UI.WebControls;

namespace ok1.Models
{

    public class Credential
    {
        [Key]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Badge ID is required.")]
        public string EmpBadgeId { get; set; }
        public string EmpName { get; set; }
        public string WindAcc { get; set; }
        public bool isAdmin { get; set; }
        public virtual ICollection<PageAccessUser> PageAccessUsers { get; set; }
    }


}