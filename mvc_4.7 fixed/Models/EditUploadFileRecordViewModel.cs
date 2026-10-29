using PagedList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ok1.Models
{
    public class EditUploadFileRecordViewModel
    {
        public UploadFileRecord UploadFileRecord { get; set; }
        public IPagedList<FabMove> FabMovesPaged { get; set; }
    }
}