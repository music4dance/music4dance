namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Search : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Searches",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        ApplicationUserId = c.String(maxLength: 128),
                        Name = c.String(),
                        Query = c.String(nullable: false),
                        Favorite = c.Boolean(nullable: false),
                        Count = c.Int(nullable: false),
                        Created = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Modified = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.ApplicationUserId)
                .Index(t => t.ApplicationUserId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Searches", "ApplicationUserId", "dbo.AspNetUsers");
            DropIndex("dbo.Searches", new[] { "ApplicationUserId" });
            DropTable("dbo.Searches");
        }
    }
}
