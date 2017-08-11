namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PlayLists : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PlayLists",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        User = c.String(),
                        Type = c.Int(nullable: false),
                        Tags = c.String(),
                        Created = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Updated = c.DateTime(precision: 7, storeType: "datetime2"),
                        Deleted = c.Boolean(nullable: false),
                        SongIds = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.PlayLists");
        }
    }
}
