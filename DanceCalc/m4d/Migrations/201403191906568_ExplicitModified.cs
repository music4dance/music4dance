using System.Data.Entity.Migrations;
using System.Diagnostics;

namespace m4d.Migrations
{
    public partial class ExplicitModified : DbMigration
    {
        public override void Up()
        {
            Trace.WriteLine("Entering DBMigration:ExplicitModified - Up");
            DropForeignKey("dbo.ApplicationUserSongs", "ApplicationUser_Id", "dbo.AspNetUsers");
            DropForeignKey("dbo.ApplicationUserSongs", "Song_SongId", "dbo.Songs");
            DropIndex("dbo.ApplicationUserSongs", new[] { "ApplicationUser_Id" });
            DropIndex("dbo.ApplicationUserSongs", new[] { "Song_SongId" });
            CreateTable(
                "dbo.ModifiedRecords",
                c => new
                    {
                        ApplicationUserId = c.String(nullable: false, maxLength: 128),
                        SongId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.ApplicationUserId, t.SongId })
                .ForeignKey("dbo.AspNetUsers", t => t.ApplicationUserId, cascadeDelete: true)
                .ForeignKey("dbo.Songs", t => t.SongId, cascadeDelete: true)
                .Index(t => t.ApplicationUserId)
                .Index(t => t.SongId);
            
            DropTable("dbo.ApplicationUserSongs");
            Trace.WriteLine("Exiting DBMigration:ExplicitModified - Up");
        }
        
        public override void Down()
        {
            Trace.WriteLine("Entering DBMigration:ExplicitModified - Down");
            CreateTable(
                "dbo.ApplicationUserSongs",
                c => new
                    {
                        ApplicationUser_Id = c.String(nullable: false, maxLength: 128),
                        Song_SongId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.ApplicationUser_Id, t.Song_SongId });
            
            DropForeignKey("dbo.ModifiedRecords", "SongId", "dbo.Songs");
            DropForeignKey("dbo.ModifiedRecords", "ApplicationUserId", "dbo.AspNetUsers");
            DropIndex("dbo.ModifiedRecords", new[] { "SongId" });
            DropIndex("dbo.ModifiedRecords", new[] { "ApplicationUserId" });
            DropTable("dbo.ModifiedRecords");
            CreateIndex("dbo.ApplicationUserSongs", "Song_SongId");
            CreateIndex("dbo.ApplicationUserSongs", "ApplicationUser_Id");
            AddForeignKey("dbo.ApplicationUserSongs", "Song_SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.ApplicationUserSongs", "ApplicationUser_Id", "dbo.AspNetUsers", "Id", cascadeDelete: true);
            Trace.WriteLine("Exiting DBMigration:ExplicitModified - Down");
        }
    }
}
