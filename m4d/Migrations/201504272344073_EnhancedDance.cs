namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EnhancedDance : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.TopNs",
                c => new
                    {
                        DanceId = c.String(nullable: false, maxLength: 5),
                        SongId = c.Guid(nullable: false),
                        Rank = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.DanceId, t.SongId })
                .ForeignKey("dbo.Dances", t => t.DanceId, cascadeDelete: true)
                .ForeignKey("dbo.Songs", t => t.SongId, cascadeDelete: true)
                .Index(t => t.DanceId)
                .Index(t => t.SongId);
            
            AddColumn("dbo.Dances", "SongCount", c => c.Int(nullable: false));
            AddColumn("dbo.Dances", "MaxWeight", c => c.Int(nullable: false));
            AddColumn("dbo.Dances", "SongTags_Summary", c => c.String());
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.TopNs", "SongId", "dbo.Songs");
            DropForeignKey("dbo.TopNs", "DanceId", "dbo.Dances");
            DropIndex("dbo.TopNs", new[] { "SongId" });
            DropIndex("dbo.TopNs", new[] { "DanceId" });
            DropColumn("dbo.Dances", "SongTags_Summary");
            DropColumn("dbo.Dances", "MaxWeight");
            DropColumn("dbo.Dances", "SongCount");
            DropTable("dbo.TopNs");
        }
    }
}
