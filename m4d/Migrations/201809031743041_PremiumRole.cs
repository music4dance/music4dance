namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PremiumRole : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AspNetUsers", "SubscriptionStart", c => c.DateTime(precision: 7, storeType: "datetime2"));
            AddColumn("dbo.AspNetUsers", "SubscriptionEnd", c => c.DateTime(precision: 7, storeType: "datetime2"));
            AddColumn("dbo.AspNetUsers", "SubscriptionLevel", c => c.Int(nullable: false, defaultValue: 0));
        }
        
        public override void Down()
        {
            DropColumn("dbo.AspNetUsers", "SubscriptionLevel");
            DropColumn("dbo.AspNetUsers", "SubscriptionEnd");
            DropColumn("dbo.AspNetUsers", "SubscriptionStart");
        }
    }
}
