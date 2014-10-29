namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DanceInfo : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.DanceLinks",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        DanceId = c.String(maxLength: 5),
                        Description = c.String(),
                        Link = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Dances", t => t.DanceId)
                .Index(t => t.DanceId);
            
            AddColumn("dbo.Dances", "Description", c => c.String());
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.DanceLinks", "DanceId", "dbo.Dances");
            DropIndex("dbo.DanceLinks", new[] { "DanceId" });
            DropColumn("dbo.Dances", "Description");
            DropTable("dbo.DanceLinks");
        }
    }
}
