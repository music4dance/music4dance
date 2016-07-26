namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveUserTags : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Tags", "UserId", "dbo.AspNetUsers");
            DropIndex("dbo.Tags", new[] { "UserId" });
            DropTable("dbo.Tags");
        }
        
        public override void Down()
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
                .PrimaryKey(t => new { t.UserId, t.Id });
            
            CreateIndex("dbo.Tags", "UserId");
            AddForeignKey("dbo.Tags", "UserId", "dbo.AspNetUsers", "Id", cascadeDelete: true);
        }
    }
}
