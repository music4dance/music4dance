namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    using System.Diagnostics;
    
    public partial class SongPurchase : DbMigration
    {
        public override void Up()
        {
            Trace.WriteLine("Entering DBMigration:SongPurchase - Up");
            AddColumn("dbo.Songs", "Purchase", c => c.String());
            Trace.WriteLine("Exiting DBMigration:SongPurchase - Up");
        }
        
        public override void Down()
        {
            Trace.WriteLine("Entering DBMigration:SongPurchase - Down");
            DropColumn("dbo.Songs", "Purchase");
            Trace.WriteLine("Exiting DBMigration:SongPurchase - Down");
        }
    }
}
