namespace ok1.Data.Migrations.Master
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.FormRequests",
                c => new
                    {
                        ID_request = c.Int(nullable: false, identity: true),
                        id_user = c.Int(nullable: false),
                        tgl_keluar = c.DateTime(nullable: false),
                        durasi = c.Time(nullable: false, precision: 7),
                        jam_keluar = c.Time(nullable: false, precision: 7),
                        jam_kembali = c.Time(nullable: false, precision: 7),
                        status = c.String(),
                        Badge = c.String(),
                    })
                .PrimaryKey(t => t.ID_request);
            
            CreateTable(
                "dbo.tbl_GMDS",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        BadgeId = c.String(),
                        Name = c.String(),
                        Department = c.String(),
                        Gender = c.String(),
                        Designation = c.String(),
                        WindowsAccount = c.String(),
                        Production = c.String(),
                        IFX = c.String(),
                        Religion = c.String(),
                        Group = c.String(),
                        Area = c.String(),
                        AreaDI = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.tbl_Testing",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        BadgeId = c.String(),
                        Duration = c.Time(precision: 7),
                        SubmitTime = c.DateTime(),
                        LongOut = c.String(),
                        Shift = c.String(),
                        Status = c.String(),
                        OutOf = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.tbl_Testing");
            DropTable("dbo.tbl_GMDS");
            DropTable("dbo.FormRequests");
        }
    }
}
