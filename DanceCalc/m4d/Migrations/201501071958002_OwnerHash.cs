namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
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
