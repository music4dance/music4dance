namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SongPurchase : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Songs", "Purchase", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Songs", "Purchase");
        }
    }
}
