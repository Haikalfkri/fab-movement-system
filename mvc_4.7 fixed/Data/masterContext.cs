using ok1.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace ok1.Data
{
    public class masterContext : DbContext
    {
        static masterContext()
        {
            Database.SetInitializer<masterContext>(null);
        }
        public masterContext() : base("name=masterContext")
        {
            this.Database.Initialize(force: true);
        }

        public DbSet<tbl_GMDS> gmds { get; set; }
        public DbSet<FormRequest> FormRequest { get; set; }
        public DbSet<tbl_Testing> tbl_Testing { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}