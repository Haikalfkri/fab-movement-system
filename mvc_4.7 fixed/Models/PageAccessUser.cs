using ok1.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ok1.Models
{

    public class PageAccessUser
    {
        public int Id { get; set; }
        public int CredentialId { get; set; }
        public virtual Credential Credential { get; set; }
        public string EmpBadgeId { get; set; }
        public string PageName { get; set; }
    }
}