namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Basics : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.DanceRatings",
                c => new
                    {
                        SongId = c.Int(nullable: false),
                        DanceId = c.String(nullable: false, maxLength: 5),
                        Weight = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.SongId, t.DanceId })
                .ForeignKey("dbo.Dances", t => t.DanceId, cascadeDelete: true)
                .ForeignKey("dbo.Songs", t => t.SongId, cascadeDelete: true)
                .Index(t => t.DanceId)
                .Index(t => t.SongId);
            
            CreateTable(
                "dbo.Dances",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 5),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Songs",
                c => new
                    {
                        SongId = c.Int(nullable: false, identity: true),
                        Tempo = c.Decimal(precision: 6, scale: 2),
                        Title = c.String(),
                        Artist = c.String(),
                        Album = c.String(),
                        Genre = c.String(),
                        Length = c.Int(),
                        Created = c.DateTime(nullable: false),
                        Modified = c.DateTime(nullable: false),
                        TitleHash = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.SongId);
            
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
                "dbo.SongLogs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Time = c.DateTime(nullable: false),
                        Action = c.String(),
                        SongReference = c.Int(nullable: false),
                        SongSignature = c.String(),
                        Data = c.String(),
                        User_Id = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.User_Id)
                .Index(t => t.User_Id);
            
            CreateTable(
                "dbo.ApplicationUserSongs",
                c => new
                    {
                        ApplicationUser_Id = c.String(nullable: false, maxLength: 128),
                        Song_SongId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.ApplicationUser_Id, t.Song_SongId })
                .ForeignKey("dbo.AspNetUsers", t => t.ApplicationUser_Id, cascadeDelete: true)
                .ForeignKey("dbo.Songs", t => t.Song_SongId, cascadeDelete: true)
                .Index(t => t.ApplicationUser_Id)
                .Index(t => t.Song_SongId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.SongLogs", "User_Id", "dbo.AspNetUsers");
            DropForeignKey("dbo.SongProperties", "SongId", "dbo.Songs");
            DropForeignKey("dbo.ApplicationUserSongs", "Song_SongId", "dbo.Songs");
            DropForeignKey("dbo.ApplicationUserSongs", "ApplicationUser_Id", "dbo.AspNetUsers");
            DropForeignKey("dbo.DanceRatings", "SongId", "dbo.Songs");
            DropForeignKey("dbo.DanceRatings", "DanceId", "dbo.Dances");
            DropIndex("dbo.SongLogs", new[] { "User_Id" });
            DropIndex("dbo.SongProperties", new[] { "SongId" });
            DropIndex("dbo.ApplicationUserSongs", new[] { "Song_SongId" });
            DropIndex("dbo.ApplicationUserSongs", new[] { "ApplicationUser_Id" });
            DropIndex("dbo.DanceRatings", new[] { "SongId" });
            DropIndex("dbo.DanceRatings", new[] { "DanceId" });
            DropTable("dbo.ApplicationUserSongs");
            DropTable("dbo.SongLogs");
            DropTable("dbo.SongProperties");
            DropTable("dbo.Songs");
            DropTable("dbo.Dances");
            DropTable("dbo.DanceRatings");
        }
    }
}
