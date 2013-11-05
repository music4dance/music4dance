namespace SongDatabase.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddLog : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.SongLogs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Time = c.DateTime(nullable: false),
                        Action = c.String(),
                        SongReference = c.Int(nullable: false),
                        Data = c.String(),
                        User_UserId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.UserProfile", t => t.User_UserId)
                .Index(t => t.User_UserId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.SongLogs", "User_UserId", "dbo.UserProfile");
            DropIndex("dbo.SongLogs", new[] { "User_UserId" });
            DropTable("dbo.SongLogs");
        }
    }
}
