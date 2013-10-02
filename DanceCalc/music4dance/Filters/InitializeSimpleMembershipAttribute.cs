using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Web.Mvc;
using System.Web.Security;
using WebMatrix.WebData;

using SongDatabase.Models;

namespace music4dance.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class InitializeSimpleMembershipAttribute : ActionFilterAttribute
    {
        private static SimpleMembershipInitializer _initializer;
        private static object _initializerLock = new object();
        private static bool _isInitialized;

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Ensure ASP.NET Simple Membership is initialized only once per app start
            LazyInitializer.EnsureInitialized(ref _initializer, ref _isInitialized, ref _initializerLock);
        }

        private class SimpleMembershipInitializer
        {
            public SimpleMembershipInitializer()
            {
                try
                {
                    using (var context = new DanceMusicContext())
                    {
                        bool exists = context.Database.Exists();
                        if (!exists)
                        {
                            // Create the SimpleMembership database without Entity Framework migration schema
                            ((IObjectContextAdapter)context).ObjectContext.CreateDatabase();
                        }

                        WebSecurity.InitializeDatabaseConnection("DefaultConnection", "UserProfile", "UserId", "UserName", autoCreateTables: true);

                        if (!exists)
                        {
                            context.Database.ExecuteSqlCommand("CREATE INDEX HashIndex ON dbo.Songs (TitleHash)");
                        }

                        AddRole(_editRole);
                        AddRole(_diagRole);

                        AddAdministrator("administrator");
                        AddAdministrator("dwgray");
                    }

                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("The ASP.NET Simple Membership database could not be initialized. For more information, please see http://go.microsoft.com/fwlink/?LinkId=256588", ex);
                }

            }

            private void AddRole(string role)
            {
                if (!Roles.RoleExists(role))
                {
                    Roles.CreateRole(role);
                }
            }

            private void AddAdministrator(string name)
            {
                if (!WebSecurity.UserExists(name))
                {
                    WebSecurity.CreateUserAndAccount(name, "marley");
                    Roles.AddUsersToRoles(new[] { name }, new[] { _editRole, _diagRole });
                }
            }

            private static string _editRole = "canEdit";
            private static string _diagRole = "showDiagnostics";

        }
    }
}
