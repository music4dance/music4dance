namespace SongDatabase.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class NullableColumns : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Songs", "Track", c => c.Int());
            AlterColumn("dbo.Songs", "Length", c => c.Int());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Songs", "Length", c => c.Int(nullable: false));
            AlterColumn("dbo.Songs", "Track", c => c.Int(nullable: false));
        }
    }
}
