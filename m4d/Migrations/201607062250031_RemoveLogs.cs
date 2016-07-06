namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveLogs : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.SongLogs", "User_Id", "dbo.AspNetUsers");
            DropIndex("dbo.SongLogs", new[] { "User_Id" });
            DropTable("dbo.SongLogs");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.SongLogs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Time = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                        Action = c.String(),
                        SongReference = c.Guid(nullable: false),
                        SongSignature = c.String(),
                        Data = c.String(),
                        User_Id = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateIndex("dbo.SongLogs", "User_Id");
            AddForeignKey("dbo.SongLogs", "User_Id", "dbo.AspNetUsers", "Id");
        }
    }
}
