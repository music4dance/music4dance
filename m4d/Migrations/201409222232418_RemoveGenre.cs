using System.Data.Entity.Migrations;

namespace m4d.Migrations
{
    public partial class RemoveGenre : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.Songs", "Genre");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Songs", "Genre", c => c.String());
        }
    }
}
