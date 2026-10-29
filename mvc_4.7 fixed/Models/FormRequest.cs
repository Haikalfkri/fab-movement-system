using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace ok1.Models
{
    public class FormRequest
    {
        [Column("ID_request")]
        public int Id { get; set; }
        public int id_user { get; set; }
        public DateTime tgl_keluar { get; set; }
        public TimeSpan durasi { get; set; }
        public TimeSpan jam_keluar { get; set; }
        public TimeSpan jam_kembali { get; set; }
        public string status { get; set; }
        public string Badge { get; set; }
    }

}
