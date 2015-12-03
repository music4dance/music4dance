namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class LikeFix : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.ModifiedRecords", "Like", c => c.Boolean());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.ModifiedRecords", "Like", c => c.Boolean(nullable: false));
        }
    }
}
