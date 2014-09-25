namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class StartData : DbMigration
    {
        private static readonly DateTime defaultDate = new DateTime(2014, 9, 1);
        public override void Up()
        {
            AddColumn("dbo.AspNetUsers", "StartDate", c => c.DateTime(nullable: false, defaultValue:defaultDate));
        }
        
        public override void Down()
        {
            DropColumn("dbo.AspNetUsers", "StartDate");
        }
    }
}
