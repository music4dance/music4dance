namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UserInfo : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AspNetUsers", "Region", c => c.String(maxLength: 2));
            AddColumn("dbo.AspNetUsers", "Privacy", c => c.Byte(nullable: false));
            AddColumn("dbo.AspNetUsers", "CanContact", c => c.Byte(nullable: false));
            AddColumn("dbo.AspNetUsers", "ServicePreference", c => c.String(maxLength: 10));
        }
        
        public override void Down()
        {
            DropColumn("dbo.AspNetUsers", "ServicePreference");
            DropColumn("dbo.AspNetUsers", "CanContact");
            DropColumn("dbo.AspNetUsers", "Privacy");
            DropColumn("dbo.AspNetUsers", "Region");
        }
    }
}
