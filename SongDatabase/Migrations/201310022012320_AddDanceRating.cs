namespace SongDatabase.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDanceRating : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.DanceSongs", "Dance_Id", "dbo.Dances");
            DropForeignKey("dbo.DanceSongs", "Song_SongId", "dbo.Songs");
            DropIndex("dbo.DanceSongs", new[] { "Dance_Id" });
            DropIndex("dbo.DanceSongs", new[] { "Song_SongId" });
            CreateTable(
                "dbo.DanceRatings",
                c => new
                    {
                        SongId = c.Int(nullable: false),
                        DanceId = c.String(nullable: false, maxLength: 5),
                        Weight = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.SongId, t.DanceId })
                .ForeignKey("dbo.Songs", t => t.SongId, cascadeDelete: true)
                .ForeignKey("dbo.Dances", t => t.DanceId, cascadeDelete: true)
                .Index(t => t.SongId)
                .Index(t => t.DanceId);
            
            AddColumn("dbo.Songs", "Dance_Id", c => c.String(maxLength: 5));
            AddForeignKey("dbo.Songs", "Dance_Id", "dbo.Dances", "Id");
            CreateIndex("dbo.Songs", "Dance_Id");
            DropTable("dbo.DanceSongs");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.DanceSongs",
                c => new
                    {
                        Dance_Id = c.String(nullable: false, maxLength: 5),
                        Song_SongId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.Dance_Id, t.Song_SongId });
            
            DropIndex("dbo.DanceRatings", new[] { "DanceId" });
            DropIndex("dbo.DanceRatings", new[] { "SongId" });
            DropIndex("dbo.Songs", new[] { "Dance_Id" });
            DropForeignKey("dbo.DanceRatings", "DanceId", "dbo.Dances");
            DropForeignKey("dbo.DanceRatings", "SongId", "dbo.Songs");
            DropForeignKey("dbo.Songs", "Dance_Id", "dbo.Dances");
            DropColumn("dbo.Songs", "Dance_Id");
            DropTable("dbo.DanceRatings");
            CreateIndex("dbo.DanceSongs", "Song_SongId");
            CreateIndex("dbo.DanceSongs", "Dance_Id");
            AddForeignKey("dbo.DanceSongs", "Song_SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.DanceSongs", "Dance_Id", "dbo.Dances", "Id", cascadeDelete: true);
        }
    }
}
