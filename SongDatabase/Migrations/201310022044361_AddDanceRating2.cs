namespace SongDatabase.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDanceRating2 : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Songs", "Dance_Id", "dbo.Dances");
            DropIndex("dbo.Songs", new[] { "Dance_Id" });
            DropColumn("dbo.Songs", "Dance_Id");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Songs", "Dance_Id", c => c.String(maxLength: 5));
            CreateIndex("dbo.Songs", "Dance_Id");
            AddForeignKey("dbo.Songs", "Dance_Id", "dbo.Dances", "Id");
        }
    }
}
