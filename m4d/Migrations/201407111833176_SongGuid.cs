using System.Data.Entity.Migrations;

namespace m4d.Migrations
{
    public partial class SongGuid : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.DanceRatings", "SongId", "dbo.Songs");
            DropForeignKey("dbo.ModifiedRecords", "SongId", "dbo.Songs");
            DropForeignKey("dbo.SongProperties", "SongId", "dbo.Songs");
            DropIndex("dbo.DanceRatings", new[] { "SongId" });
            DropIndex("dbo.ModifiedRecords", new[] { "SongId" });
            DropIndex("dbo.SongProperties", new[] { "SongId" });
            DropPrimaryKey("dbo.DanceRatings");
            DropPrimaryKey("dbo.Songs");
            DropPrimaryKey("dbo.ModifiedRecords");

            DropColumn("dbo.DanceRatings", "SongId");
            DropColumn("dbo.Songs", "SongId");
            DropColumn("dbo.ModifiedRecords", "SongId");
            DropColumn("dbo.SongProperties", "SongId");
            DropColumn("dbo.SongLogs", "SongReference");

            Sql("TRUNCATE TABLE [dbo].[DanceRatings]");
            Sql("TRUNCATE TABLE [dbo].[ModifiedRecords]");
            Sql("TRUNCATE TABLE [dbo].[SongProperties]");
            Sql("TRUNCATE TABLE [dbo].[SongLogs]");
            Sql("TRUNCATE TABLE [dbo].[Songs]");

            AddColumn("dbo.DanceRatings", "SongId", c => c.Guid(nullable: false));
            AddColumn("dbo.Songs", "SongId", c => c.Guid(nullable: false));
            AddColumn("dbo.ModifiedRecords", "SongId", c => c.Guid(nullable: false));
            AddColumn("dbo.SongProperties", "SongId", c => c.Guid(nullable: false));
            AddColumn("dbo.SongLogs", "SongReference", c => c.Guid(nullable: false));
            AddPrimaryKey("dbo.DanceRatings", new[] { "SongId", "DanceId" });
            AddPrimaryKey("dbo.Songs", "SongId");
            AddPrimaryKey("dbo.ModifiedRecords", new[] { "ApplicationUserId", "SongId" });
            CreateIndex("dbo.DanceRatings", "SongId");
            CreateIndex("dbo.ModifiedRecords", "SongId");
            CreateIndex("dbo.SongProperties", "SongId");
            AddForeignKey("dbo.DanceRatings", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.ModifiedRecords", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.SongProperties", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.SongProperties", "SongId", "dbo.Songs");
            DropForeignKey("dbo.ModifiedRecords", "SongId", "dbo.Songs");
            DropForeignKey("dbo.DanceRatings", "SongId", "dbo.Songs");
            DropIndex("dbo.SongProperties", new[] { "SongId" });
            DropIndex("dbo.ModifiedRecords", new[] { "SongId" });
            DropIndex("dbo.DanceRatings", new[] { "SongId" });
            DropPrimaryKey("dbo.ModifiedRecords");
            DropPrimaryKey("dbo.Songs");
            DropPrimaryKey("dbo.DanceRatings");

            DropColumn("dbo.DanceRatings", "SongId");
            DropColumn("dbo.Songs", "SongId");
            DropColumn("dbo.ModifiedRecords", "SongId");
            DropColumn("dbo.SongProperties", "SongId");
            DropColumn("dbo.SongLogs", "SongReference");

            Sql("TRUNCATE TABLE [dbo].[DanceRatings]");
            Sql("TRUNCATE TABLE [dbo].[ModifiedRecords]");
            Sql("TRUNCATE TABLE [dbo].[SongProperties]");
            Sql("TRUNCATE TABLE [dbo].[SongLogs]");
            Sql("TRUNCATE TABLE [dbo].[Songs]");

            AddColumn("dbo.SongLogs", "SongReference", c => c.Int(nullable: false));
            AddColumn("dbo.SongProperties", "SongId", c => c.Int(nullable: false));
            AddColumn("dbo.ModifiedRecords", "SongId", c => c.Int(nullable: false));
            AddColumn("dbo.Songs", "SongId", c => c.Int(nullable: false, identity: true));
            AddColumn("dbo.DanceRatings", "SongId", c => c.Int(nullable: false));

            AddPrimaryKey("dbo.ModifiedRecords", new[] { "ApplicationUserId", "SongId" });
            AddPrimaryKey("dbo.Songs", "SongId");
            AddPrimaryKey("dbo.DanceRatings", new[] { "SongId", "DanceId" });
            CreateIndex("dbo.SongProperties", "SongId");
            CreateIndex("dbo.ModifiedRecords", "SongId");
            CreateIndex("dbo.DanceRatings", "SongId");
            AddForeignKey("dbo.SongProperties", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.ModifiedRecords", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
            AddForeignKey("dbo.DanceRatings", "SongId", "dbo.Songs", "SongId", cascadeDelete: true);
        }
    }
}
