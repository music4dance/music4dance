namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveTags : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Tags", "SongId", "dbo.Songs");
            DropForeignKey("dbo.Tags", "Value", "dbo.TagTypes");
            DropIndex("dbo.Tags", new[] { "SongId" });
            DropIndex("dbo.Tags", new[] { "Value" });
            AddColumn("dbo.DanceRatings", "TagSummary_Summary", c => c.String());
            AddColumn("dbo.Dances", "TagSummary_Summary", c => c.String());
            AddColumn("dbo.Songs", "TagSummary_Summary", c => c.String());
            DropColumn("dbo.Songs", "TagSummary");
            DropTable("dbo.Tags");
            DropTable("dbo.TagTypes");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.TagTypes",
                c => new
                    {
                        Value = c.String(nullable: false, maxLength: 128),
                        Categories = c.String(),
                        Count = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Value);
            
            CreateTable(
                "dbo.Tags",
                c => new
                    {
                        SongId = c.Guid(nullable: false),
                        Value = c.String(nullable: false, maxLength: 128),
                        Count = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.SongId, t.Value });
            
            AddColumn("dbo.Songs", "TagSummary", c => c.String());
            DropColumn("dbo.Songs", "TagSummary_Summary");
            DropColumn("dbo.Dances", "TagSummary_Summary");
            DropColumn("dbo.DanceRatings", "TagSummary_Summary");
            CreateIndex("dbo.Tags", "Value");
            CreateIndex("dbo.Tags", "SongId");
            AddForeignKey("dbo.Tags", "Value", "dbo.TagTypes", "Value", cascadeDelete: true);
            AddForeignKey("dbo.Tags", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
        }
    }
}
