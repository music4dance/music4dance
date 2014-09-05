namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Tags : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Tags",
                c => new
                    {
                        SongId = c.Guid(nullable: false),
                        Value = c.String(nullable: false, maxLength: 128),
                        Count = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.SongId, t.Value })
                .ForeignKey("dbo.Songs", t => t.SongId, cascadeDelete: true)
                .ForeignKey("dbo.TagTypes", t => t.Value, cascadeDelete: true)
                .Index(t => t.SongId)
                .Index(t => t.Value);
            
            CreateTable(
                "dbo.TagTypes",
                c => new
                    {
                        Value = c.String(nullable: false, maxLength: 128),
                        Categories = c.String(),
                        Count = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Value);
            
            AddColumn("dbo.Songs", "TagSummary", c => c.String());
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Tags", "Value", "dbo.TagTypes");
            DropForeignKey("dbo.Tags", "SongId", "dbo.Songs");
            DropIndex("dbo.Tags", new[] { "Value" });
            DropIndex("dbo.Tags", new[] { "SongId" });
            DropColumn("dbo.Songs", "TagSummary");
            DropTable("dbo.TagTypes");
            DropTable("dbo.Tags");
        }
    }
}
