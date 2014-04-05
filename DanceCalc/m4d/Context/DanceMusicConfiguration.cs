using System.Data.Entity;
using System.Data.Entity.SqlServer;

namespace m4d.Context
{
    public class DanceMusicConfiguration : DbConfiguration
    {
        public DanceMusicConfiguration()
        {
            SetExecutionStrategy("System.Data.SqlClient", () => new SqlAzureExecutionStrategy());

            //SetDatabaseInitializer(new DropCreateDatabaseAlways<DanceMusicContext>());
        }

    }
}