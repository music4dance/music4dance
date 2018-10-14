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
            RenameColumn("dbo.PlayLists", "Tags", "Data1");
            RenameColumn("dbo.PlayLists", "SongIds", "Data2");
        }

        public override void Down()
        {
            RenameColumn("dbo.PlayLists", "Data1", "Tags");
            RenameColumn("dbo.PlayLists", "Data2", "SongIds");
            DropColumn("dbo.PlayLists", "Description");
            DropColumn("dbo.PlayLists", "Name");
        }
    }
}
