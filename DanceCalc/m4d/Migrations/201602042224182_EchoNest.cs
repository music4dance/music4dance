namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EchoNest : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Songs", "Sample", c => c.String());
            AddColumn("dbo.Songs", "Danceability", c => c.Single(nullable: true));
            AddColumn("dbo.Songs", "Energy", c => c.Single(nullable: true));
            AddColumn("dbo.Songs", "Valence", c => c.Single(nullable: true));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Songs", "Valence");
            DropColumn("dbo.Songs", "Energy");
            DropColumn("dbo.Songs", "Danceability");
            DropColumn("dbo.Songs", "Sample");
        }
    }
}
