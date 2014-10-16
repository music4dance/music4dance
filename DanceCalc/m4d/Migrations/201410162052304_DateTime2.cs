namespace m4d.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DateTime2 : DbMigration
    {
        private static readonly DateTime defaultDate = new DateTime(2014, 9, 1);
        public override void Up()
        {
            AlterColumn("dbo.Songs", "Created", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));
            AlterColumn("dbo.Songs", "Modified", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));

            DropDefaultConstraint("dbo.AspNetUsers", "StartDate", q => Sql(q));
            AlterColumn("dbo.AspNetUsers", "StartDate", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));
            AlterColumn("dbo.AspNetUsers", "LockoutEndDateUtc", c => c.DateTime(precision: 7, storeType: "datetime2"));

            AlterColumn("dbo.SongLogs", "Time", c => c.DateTime(nullable: false, precision: 7, storeType: "datetime2"));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.SongLogs", "Time", c => c.DateTime(nullable: false));

            AlterColumn("dbo.AspNetUsers", "LockoutEndDateUtc", c => c.DateTime());
            AlterColumn("dbo.AspNetUsers", "StartDate", c => c.DateTime(nullable: true));
            AlterColumn("dbo.AspNetUsers", "StartDate", c => c.DateTime(nullable: false, defaultValue: defaultDate));

            AlterColumn("dbo.Songs", "Modified", c => c.DateTime(nullable: false));
            AlterColumn("dbo.Songs", "Created", c => c.DateTime(nullable: false));
        }

        public static void DropDefaultConstraint(string tableName, string columnName, Action<string> executeSQL)
        {
            string constraintVariableName = string.Format("@constraint_{0}", Guid.NewGuid().ToString("N"));

            string sql = string.Format(@"
            DECLARE {0} nvarchar(128)
            SELECT {0} = name
            FROM sys.default_constraints
            WHERE parent_object_id = object_id(N'{1}')
            AND col_name(parent_object_id, parent_column_id) = '{2}';
            IF {0} IS NOT NULL
                EXECUTE('ALTER TABLE {1} DROP CONSTRAINT [' + {0} + ']')",
                constraintVariableName,
                tableName,
                columnName);

            executeSQL(sql);
        }
    }
}
