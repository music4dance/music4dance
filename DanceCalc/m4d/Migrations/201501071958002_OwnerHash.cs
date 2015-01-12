using System.Data.Entity.Migrations;

namespace m4d.Migrations
{
    public partial class OwnerHash : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.ModifiedRecords", "Owned", c => c.Int());
        }
        
        public override void Down()
        {
            DropColumn("dbo.ModifiedRecords", "Owned");
        }
    }
}
