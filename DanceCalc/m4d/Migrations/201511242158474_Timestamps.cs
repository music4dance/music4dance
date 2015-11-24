namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Timestamps : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Dances", "Modified", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));
            AddColumn("dbo.ModifiedRecords", "Like", c => c.Boolean(nullable: false));
            AddColumn("dbo.AspNetUsers", "LastActive", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));
            AddColumn("dbo.AspNetUsers", "ListLength", c => c.Int());
            AddColumn("dbo.AspNetUsers", "ColumnDefaults", c => c.String());
            AddColumn("dbo.TagTypes", "Modified", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));
        }
        
        public override void Down()
        {
            DropColumn("dbo.TagTypes", "Modified");
            DropColumn("dbo.AspNetUsers", "ColumnDefaults");
            DropColumn("dbo.AspNetUsers", "ListLength");
            DropColumn("dbo.AspNetUsers", "LastActive");
            DropColumn("dbo.ModifiedRecords", "Like");
            DropColumn("dbo.Dances", "Modified");
        }
    }
}
