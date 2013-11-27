namespace SongDatabase.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SongSignature : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.SongLogs", "SongSignature", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.SongLogs", "SongSignature");
        }
    }
}
