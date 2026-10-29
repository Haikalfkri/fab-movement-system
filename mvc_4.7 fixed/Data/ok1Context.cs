
using ok1.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Net;

namespace ok1.Data
{
    public class ok1Context : DbContext
    {

        public ok1Context() : base("name=ok1Context")
        {
            Database.SetInitializer<ok1Context>(null);
        }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Konfigurasi skema untuk FabMove
            modelBuilder.Entity<Breastfeeding>().ToTable("Breastfeedings", "FAB_Report");
            modelBuilder.Entity<PublicHoliday>().ToTable("PublicHolidays", "FAB_Report");
            modelBuilder.Entity<TargetTime>().ToTable("TargetTimes", "FAB_Report");
            modelBuilder.Entity<Credential>().ToTable("Credentials", "FAB_Report");
            modelBuilder.Entity<EventReg>().ToTable("EventRegs", "FAB_Report");
            modelBuilder.Entity<Attendees>().ToTable("Attendees", "FAB_Report");
            modelBuilder.Entity<EventAttendee>().ToTable("EventAttendees", "FAB_Report");
            modelBuilder.Entity<FabMove>().ToTable("FabMoves", "FAB_Report");
            modelBuilder.Entity<PageAccessUser>().ToTable("PageAccessUsers", "FAB_Report");
            modelBuilder.Entity<User>().ToTable("Users", "FAB_Report");
            modelBuilder.Entity<GroupMapping>().ToTable("GroupMapping", "FAB_Report");
            modelBuilder.Entity<UploadFileRecord>().ToTable("UploadFileRecord", "FAB_Report");
            modelBuilder.Entity<ManagerList>().ToTable("ManagerList", "DI_Enhancement_System");
        }


        public DbSet<ok1.Models.Breastfeeding> Breastfeedings { get; set; }

        public DbSet<ok1.Models.PublicHoliday> PublicHolidays { get; set; }

        public DbSet<ok1.Models.TargetTime> TargetTimes { get; set; }

        public DbSet<ok1.Models.Credential> Credentials { get; set; }

        public DbSet<ok1.Models.EventReg> EventRegs { get; set; }
        public DbSet<ok1.Models.Attendees> GetAttendees { get; set; }
        public DbSet<ok1.Models.EventAttendee> EventAttendees { get; set; }
        public DbSet<ok1.Models.FabMove> BFabMoves { get; set; }

        public DbSet<ok1.Models.User> Users { get; set; }
        public DbSet<ok1.Models.PageAccessUser> PageAccessUsers { get; set; }
        public DbSet<ok1.Models.GroupMapping> GroupMapping { get; set; }

        public DbSet<ok1.Models.UploadFileRecord> UploadFileRecord { get; set; }
        public DbSet<ok1.Models.ManagerList> ManagerList { get; set; }
    }


}
