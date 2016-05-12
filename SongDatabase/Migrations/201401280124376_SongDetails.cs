namespace SongDatabase.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SongDetails : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.Songs", "Publisher");
            DropColumn("dbo.Songs", "Track");
            DropColumn("dbo.Songs", "Purchase");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Songs", "Purchase", c => c.String());
            AddColumn("dbo.Songs", "Track", c => c.Int());
            AddColumn("dbo.Songs", "Publisher", c => c.String());
        }
    }
}
