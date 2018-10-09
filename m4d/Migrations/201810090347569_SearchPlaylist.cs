namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SearchPlaylist : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PlayLists", "Name", c => c.String());
            AddColumn("dbo.PlayLists", "Description", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.PlayLists", "Description");
            DropColumn("dbo.PlayLists", "Name");
        }
    }
}
