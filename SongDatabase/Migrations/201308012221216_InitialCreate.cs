namespace SongDatabase.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Songs",
                c => new
                    {
                        SongId = c.Int(nullable: false, identity: true),
                        Tempo = c.Decimal(precision: 6, scale: 2),
                        Title = c.String(),
                        Artist = c.String(),
                        Album = c.String(),
                        Publisher = c.String(),
                        Genre = c.String(),
                        Track = c.Int(nullable: false),
                        Length = c.Int(nullable: false),
                        Created = c.DateTime(nullable: false),
                        Modified = c.DateTime(nullable: false),
                        TitleHash = c.Int(nullable: false),
                        Purchase = c.String(),
                    })
                .PrimaryKey(t => t.SongId);
            
            CreateTable(
                "dbo.Dances",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 5),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.UserProfile",
                c => new
                    {
                        UserId = c.Int(nullable: false, identity: true),
                        UserName = c.String(),
                    })
                .PrimaryKey(t => t.UserId);
            
            CreateTable(
                "dbo.SongProperties",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        SongId = c.Int(nullable: false),
                        Name = c.String(),
                        Value = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Songs", t => t.SongId, cascadeDelete: true)
                .Index(t => t.SongId);
            
            CreateTable(
                "dbo.DanceSongs",
                c => new
                    {
                        Dance_Id = c.String(nullable: false, maxLength: 5),
                        Song_SongId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.Dance_Id, t.Song_SongId })
                .ForeignKey("dbo.Dances", t => t.Dance_Id, cascadeDelete: true)
                .ForeignKey("dbo.Songs", t => t.Song_SongId, cascadeDelete: true)
                .Index(t => t.Dance_Id)
                .Index(t => t.Song_SongId);
            
            CreateTable(
                "dbo.UserProfileSongs",
                c => new
                    {
                        UserProfile_UserId = c.Int(nullable: false),
                        Song_SongId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.UserProfile_UserId, t.Song_SongId })
                .ForeignKey("dbo.UserProfile", t => t.UserProfile_UserId, cascadeDelete: true)
                .ForeignKey("dbo.Songs", t => t.Song_SongId, cascadeDelete: true)
                .Index(t => t.UserProfile_UserId)
                .Index(t => t.Song_SongId);
            
        }
        
        public override void Down()
        {
            DropIndex("dbo.UserProfileSongs", new[] { "Song_SongId" });
            DropIndex("dbo.UserProfileSongs", new[] { "UserProfile_UserId" });
            DropIndex("dbo.DanceSongs", new[] { "Song_SongId" });
            DropIndex("dbo.DanceSongs", new[] { "Dance_Id" });
            DropIndex("dbo.SongProperties", new[] { "SongId" });
            DropForeignKey("dbo.UserProfileSongs", "Song_SongId", "dbo.Songs");
            DropForeignKey("dbo.UserProfileSongs", "UserProfile_UserId", "dbo.UserProfile");
            DropForeignKey("dbo.DanceSongs", "Song_SongId", "dbo.Songs");
            DropForeignKey("dbo.DanceSongs", "Dance_Id", "dbo.Dances");
            DropForeignKey("dbo.SongProperties", "SongId", "dbo.Songs");
            DropTable("dbo.UserProfileSongs");
            DropTable("dbo.DanceSongs");
            DropTable("dbo.SongProperties");
            DropTable("dbo.UserProfile");
            DropTable("dbo.Dances");
            DropTable("dbo.Songs");
        }
    }
}
