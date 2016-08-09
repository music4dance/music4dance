using System.Data.Entity.Migrations;

namespace m4d.Migrations
{
    public partial class GeneralTags : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Tags",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        Id = c.String(nullable: false, maxLength: 128),
                        Modified = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Tags_Summary = c.String(),
                    })
                .PrimaryKey(t => new { t.UserId, t.Id })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.TagTypes",
                c => new
                    {
                        Key = c.String(nullable: false, maxLength: 128),
                        Count = c.Int(nullable: false),
                        PrimaryId = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.TagTypes", t => t.PrimaryId)
                .Index(t => t.PrimaryId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.TagTypes", "PrimaryId", "dbo.TagGroups");
            DropForeignKey("dbo.Tags", "UserId", "dbo.AspNetUsers");
            DropIndex("dbo.TagTypes", new[] { "PrimaryId" });
            DropIndex("dbo.Tags", new[] { "UserId" });
            DropTable("dbo.TagTypes");
            DropTable("dbo.Tags");
        }
    }
}
