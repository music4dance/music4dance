namespace m4d.Migrations
{
    using DanceLibrary;
    using m4d.Context;
    using m4dModels;
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<m4d.Context.DanceMusicContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(m4d.Context.DanceMusicContext context)
        {
            DoSeed(context);
        }

        static public void DoSeed(m4d.Context.DanceMusicContext context)
        {
            var ustore = new UserStore<ApplicationUser>(context);
            var umanager = new UserManager<ApplicationUser>(ustore);

            DanceMusicService dms = new DanceMusicService(context,umanager);

            //  This method will be called after migrating to the latest version.

            var rstore = new RoleStore<IdentityRole>(context);
            var rmanager = new RoleManager<IdentityRole>(rstore);

            foreach (string roleName in _roles)
            {
                if (!context.Roles.Any(r => r.Name == roleName))
                {
                    var role = new IdentityRole { Name = roleName };
                    rmanager.Create(role);
                }
            }


            foreach (string name in _adminUsers)
            {
                var user = context.Users.FirstOrDefault(u => u.UserName == name);
                if (user == null)
                {
                    user = new ApplicationUser { UserName = name };

                    umanager.Create(user, "maggie");
                }

                AddToRole(umanager, user.Id, DanceMusicService.DiagRole);
                AddToRole(umanager, user.Id, DanceMusicService.EditRole);
                AddToRole(umanager, user.Id, DanceMusicService.DbaRole);
            }

            dms.SeedDances();
        }

        private static void AddToRole(UserManager<ApplicationUser> um, string user, string role)
        {
            if (!um.IsInRole(user, role))
            {
                um.AddToRole(user, role);
            }
        }

        private static void RemoveFromRole(UserManager<ApplicationUser> um, string user, string role)
        {
            if (um.IsInRole(user, role))
            {
                um.RemoveFromRole(user, role);
            }
        }

        private static string[] _roles = new string[] { DanceMusicService.DiagRole, DanceMusicService.EditRole, DanceMusicService.DbaRole };
        private static string[] _adminUsers = new string[] { "administrator" }; //, "dwgray", "batch" };
        //private static string[] _diagUsers = new string[] { "lukim", "glennn" };
        //private static string[] _editUsers = new string[] { "ajy", "SalsaSwingBallroom", "SandiegoDJ", "UsaSwingNet", "LetsDanceDenver", "SteveThatDJ", "JohnCrossan", "WaltersDanceCenter", "breanna", "buzzle", "michelleds", "shawntrautman", "danceforums", "Century", "bdlist", "DWTS", "SYTYCD" };
    }
}
