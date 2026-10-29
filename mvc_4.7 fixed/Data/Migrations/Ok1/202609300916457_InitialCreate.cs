namespace ok1.Data.Migrations.Ok1
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "FAB_Report.FabMoves",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        EmpBadgeId = c.String(),
                        EmpName = c.String(),
                        EmpDept = c.String(),
                        Designation = c.String(),
                        JobLevel = c.String(),
                        ShiftCode = c.String(),
                        Section = c.String(),
                        OrgUnit = c.String(),
                        CostCenter = c.String(),
                        Level1 = c.String(),
                        WorkDay = c.DateTime(nullable: false),
                        Day = c.String(),
                        Shift = c.String(),
                        FabIn = c.Time(nullable: false, precision: 7),
                        FabOut = c.Time(nullable: false, precision: 7),
                        FirstCrIn = c.Time(nullable: false, precision: 7),
                        LastCrOut = c.Time(nullable: false, precision: 7),
                        FabTime = c.Time(nullable: false, precision: 7),
                        CrTime = c.Time(nullable: false, precision: 7),
                        BrTime = c.String(),
                        FabCount = c.String(),
                        CrCount = c.String(),
                        FabLateIn = c.String(),
                        FabEarlyOut = c.String(),
                        Group = c.String(),
                        Area = c.String(),
                        AreaDI = c.String(),
                        PublicHoliday = c.String(),
                        IsValid = c.Boolean(nullable: false),
                        Workplace = c.String(),
                        Gender = c.String(),
                        Religion = c.String(),
                        FridayPray = c.String(),
                        Busui = c.Time(nullable: false, precision: 7),
                        Ahm = c.String(),
                        Amend = c.String(),
                        ExitPermit = c.String(),
                        TargetTime = c.String(),
                        TimeClass = c.String(),
                        UploadFileRecordId = c.Int(),
                        Missing = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("FAB_Report.UploadFileRecord", t => t.UploadFileRecordId)
                .Index(t => t.UploadFileRecordId);
            
            CreateTable(
                "FAB_Report.UploadFileRecord",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FileName = c.String(nullable: false),
                        UploadDate = c.DateTime(nullable: false),
                        RefreshTime = c.DateTime(),
                        RecordCount = c.Int(nullable: false),
                        UploadedBy = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "FAB_Report.Breastfeedings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        EmpBadgeId = c.String(),
                        EmpName = c.String(),
                        EmpDept = c.String(),
                        Status = c.String(),
                        DateOfBirth = c.DateTime(nullable: false),
                        ExpDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "FAB_Report.Credentials",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        EmpBadgeId = c.String(nullable: false),
                        EmpName = c.String(),
                        WindAcc = c.String(),
                        isAdmin = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "FAB_Report.PageAccessUsers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        CredentialId = c.Int(nullable: false),
                        EmpBadgeId = c.String(),
                        PageName = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("FAB_Report.Credentials", t => t.CredentialId, cascadeDelete: true)
                .Index(t => t.CredentialId);
            
            CreateTable(
                "FAB_Report.EventAttendees",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        EventRegId = c.Int(nullable: false),
                        AttendeesId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("FAB_Report.Attendees", t => t.AttendeesId, cascadeDelete: true)
                .ForeignKey("FAB_Report.EventRegs", t => t.EventRegId, cascadeDelete: true)
                .Index(t => t.EventRegId)
                .Index(t => t.AttendeesId);
            
            CreateTable(
                "FAB_Report.Attendees",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        EmpBadgeId = c.String(nullable: false),
                        EmpName = c.String(),
                        EmpDept = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "FAB_Report.EventRegs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TrainerId = c.String(nullable: false),
                        TrainerName = c.String(),
                        EventDate = c.DateTime(nullable: false),
                        EventName = c.String(),
                        StartTime = c.Time(nullable: false, precision: 7),
                        EndTime = c.Time(nullable: false, precision: 7),
                        EventDur = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "FAB_Report.GroupMapping",
                c => new
                    {
                        id = c.Int(nullable: false, identity: true),
                        GroupName = c.String(),
                        Area = c.String(),
                        AreaDI = c.String(),
                    })
                .PrimaryKey(t => t.id);
            
            CreateTable(
                "DI_Enhancement_System.ManagerList",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        BadgeId = c.String(),
                        Name = c.String(),
                        Manager = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "FAB_Report.PublicHolidays",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DateFrom = c.DateTime(nullable: false),
                        DateTo = c.DateTime(nullable: false),
                        RemarkDate = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "FAB_Report.TargetTimes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        EmpBadgeId = c.String(),
                        EmpName = c.String(),
                        EmpDept = c.String(),
                        NormalShift = c.Time(nullable: false, precision: 7),
                        MorningShift = c.Time(nullable: false, precision: 7),
                        AfternoonShift = c.Time(nullable: false, precision: 7),
                        NightShift = c.Time(nullable: false, precision: 7),
                        Group = c.String(),
                        AreaDI = c.String(),
                        Area = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "FAB_Report.Users",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        EmpBadgeId = c.String(),
                        EmpName = c.String(),
                        EmpDept = c.String(),
                        Group = c.String(),
                        Area = c.String(),
                        AreaDI = c.String(),
                        LogWeek = c.String(),
                        Points = c.Int(nullable: false),
                        AvgPoints = c.Double(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("FAB_Report.EventAttendees", "EventRegId", "FAB_Report.EventRegs");
            DropForeignKey("FAB_Report.EventAttendees", "AttendeesId", "FAB_Report.Attendees");
            DropForeignKey("FAB_Report.PageAccessUsers", "CredentialId", "FAB_Report.Credentials");
            DropForeignKey("FAB_Report.FabMoves", "UploadFileRecordId", "FAB_Report.UploadFileRecord");
            DropIndex("FAB_Report.EventAttendees", new[] { "AttendeesId" });
            DropIndex("FAB_Report.EventAttendees", new[] { "EventRegId" });
            DropIndex("FAB_Report.PageAccessUsers", new[] { "CredentialId" });
            DropIndex("FAB_Report.FabMoves", new[] { "UploadFileRecordId" });
            DropTable("FAB_Report.Users");
            DropTable("FAB_Report.TargetTimes");
            DropTable("FAB_Report.PublicHolidays");
            DropTable("DI_Enhancement_System.ManagerList");
            DropTable("FAB_Report.GroupMapping");
            DropTable("FAB_Report.EventRegs");
            DropTable("FAB_Report.Attendees");
            DropTable("FAB_Report.EventAttendees");
            DropTable("FAB_Report.PageAccessUsers");
            DropTable("FAB_Report.Credentials");
            DropTable("FAB_Report.Breastfeedings");
            DropTable("FAB_Report.UploadFileRecord");
            DropTable("FAB_Report.FabMoves");
        }
    }
}
