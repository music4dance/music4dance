namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveRings : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.TagTypes", "PrimaryId", "dbo.TagTypes");
            DropIndex("dbo.TagTypes", new[] { "PrimaryId" });
            CreateTable(
                "dbo.TagGroups",
                c => new
                    {
                        Key = c.String(nullable: false, maxLength: 128),
                        Modified = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        PrimaryId = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("dbo.TagGroups", t => t.PrimaryId)
                .Index(t => t.PrimaryId);
            
            DropTable("dbo.TagTypes");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.TagTypes",
                c => new
                    {
                        Key = c.String(nullable: false, maxLength: 128),
                        Modified = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Count = c.Int(nullable: false),
                        PrimaryId = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Key);
            
            DropForeignKey("dbo.TagGroups", "PrimaryId", "dbo.TagGroups");
            DropIndex("dbo.TagGroups", new[] { "PrimaryId" });
            DropTable("dbo.TagGroups");
            CreateIndex("dbo.TagTypes", "PrimaryId");
            AddForeignKey("dbo.TagTypes", "PrimaryId", "dbo.TagTypes", "Key");
        }
    }
}
