namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveSongs : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.DanceRatings", "DanceId", "dbo.Dances");
            DropForeignKey("dbo.TopNs", "DanceId", "dbo.Dances");
            DropForeignKey("dbo.DanceRatings", "SongId", "dbo.Songs");
            DropForeignKey("dbo.ModifiedRecords", "ApplicationUserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.ModifiedRecords", "SongId", "dbo.Songs");
            DropForeignKey("dbo.SongProperties", "SongId", "dbo.Songs");
            DropForeignKey("dbo.TopNs", "SongId", "dbo.Songs");
            DropIndex("dbo.DanceRatings", new[] { "SongId" });
            DropIndex("dbo.DanceRatings", new[] { "DanceId" });
            DropIndex("dbo.TopNs", new[] { "DanceId" });
            DropIndex("dbo.TopNs", new[] { "SongId" });
            DropIndex("dbo.ModifiedRecords", new[] { "ApplicationUserId" });
            DropIndex("dbo.ModifiedRecords", new[] { "SongId" });
            DropIndex("dbo.SongProperties", new[] { "SongId" });
            DropColumn("dbo.Dances", "SongCount");
            DropColumn("dbo.Dances", "MaxWeight");
            DropColumn("dbo.Dances", "SongTags_Summary");
            DropColumn("dbo.Dances", "TagSummary_Summary");
            DropTable("dbo.DanceRatings");
            DropTable("dbo.TopNs");
            DropTable("dbo.Songs");
            DropTable("dbo.ModifiedRecords");
            DropTable("dbo.SongProperties");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.SongProperties",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        SongId = c.Guid(nullable: false),
                        Name = c.String(),
                        Value = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.ModifiedRecords",
                c => new
                    {
                        ApplicationUserId = c.String(nullable: false, maxLength: 128),
                        SongId = c.Guid(nullable: false),
                        Owned = c.Int(),
                        Like = c.Boolean(),
                    })
                .PrimaryKey(t => new { t.ApplicationUserId, t.SongId });
            
            CreateTable(
                "dbo.Songs",
                c => new
                    {
                        SongId = c.Guid(nullable: false),
                        TitleHash = c.Int(nullable: false),
                        Tempo = c.Decimal(precision: 6, scale: 2),
                        Title = c.String(),
                        Artist = c.String(),
                        Length = c.Int(),
                        Purchase = c.String(),
                        Sample = c.String(),
                        Danceability = c.Single(),
                        Energy = c.Single(),
                        Valence = c.Single(),
                        Created = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Modified = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Album = c.String(),
                        TagSummary_Summary = c.String(),
                    })
                .PrimaryKey(t => t.SongId);
            
            CreateTable(
                "dbo.TopNs",
                c => new
                    {
                        DanceId = c.String(nullable: false, maxLength: 5),
                        SongId = c.Guid(nullable: false),
                        Rank = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.DanceId, t.SongId });
            
            CreateTable(
                "dbo.DanceRatings",
                c => new
                    {
                        SongId = c.Guid(nullable: false),
                        DanceId = c.String(nullable: false, maxLength: 5),
                        Weight = c.Int(nullable: false),
                        TagSummary_Summary = c.String(),
                    })
                .PrimaryKey(t => new { t.SongId, t.DanceId });
            
            AddColumn("dbo.Dances", "TagSummary_Summary", c => c.String());
            AddColumn("dbo.Dances", "SongTags_Summary", c => c.String());
            AddColumn("dbo.Dances", "MaxWeight", c => c.Int(nullable: false));
            AddColumn("dbo.Dances", "SongCount", c => c.Int(nullable: false));
            CreateIndex("dbo.SongProperties", "SongId");
            CreateIndex("dbo.ModifiedRecords", "SongId");
            CreateIndex("dbo.ModifiedRecords", "ApplicationUserId");
            CreateIndex("dbo.TopNs", "SongId");
            CreateIndex("dbo.TopNs", "DanceId");
            CreateIndex("dbo.DanceRatings", "DanceId");
            CreateIndex("dbo.DanceRatings", "SongId");
            AddForeignKey("dbo.TopNs", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.SongProperties", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.ModifiedRecords", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.ModifiedRecords", "ApplicationUserId", "dbo.AspNetUsers", "Id", cascadeDelete: true);
            AddForeignKey("dbo.DanceRatings", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.TopNs", "DanceId", "dbo.Dances", "Id", cascadeDelete: true);
            AddForeignKey("dbo.DanceRatings", "DanceId", "dbo.Dances", "Id", cascadeDelete: true);
        }
    }
}
