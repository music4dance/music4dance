using System.Data.Entity.Migrations;
using System.Linq;
using m4d.Context;
using m4dModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace m4d.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<DanceMusicContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(DanceMusicContext context)
        {
            DoSeed(context);
        }

        public static void DoSeed(DanceMusicContext context)
        {
            var ustore = new UserStore<ApplicationUser>(context);
            var umanager = new UserManager<ApplicationUser>(ustore);

            var dms = new DanceMusicService(context,umanager);

            //  This method will be called after migrating to the latest version.

            var rstore = new RoleStore<IdentityRole>(context);
            var rmanager = new RoleManager<IdentityRole>(rstore);

            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var roleName in s_roles)
            {
                if (context.Roles.Any(r => r.Name == roleName)) continue;

                var role = new IdentityRole { Name = roleName };
                rmanager.Create(role);
            }

            foreach (string name in s_adminUsers)
            {
                var user = context.Users.FirstOrDefault(u => u.UserName == name);
                if (user == null)
                {
                    user = new ApplicationUser { UserName = name };

                    umanager.Create(user, "maggie");
                }

                AddToRole(umanager, user.Id, DanceMusicService.TagRole);
                AddToRole(umanager, user.Id, DanceMusicService.EditRole);
                AddToRole(umanager, user.Id, DanceMusicService.DiagRole);
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

        private static readonly string[] s_roles = { DanceMusicService.DiagRole, DanceMusicService.EditRole, DanceMusicService.DbaRole, DanceMusicService.TagRole };
        private static readonly string[] s_adminUsers = {"administrator"}; //, "dwgray", "batch"};
        //private static string[] _diagUsers = new string[] { "lukim", "glennn" };
        //private static string[] _editUsers = new string[] { "ajy", "SalsaSwingBallroom", "SandiegoDJ", "UsaSwingNet", "LetsDanceDenver", "SteveThatDJ", "JohnCrossan", "WaltersDanceCenter", "breanna", "buzzle", "michelleds", "shawntrautman", "danceforums", "Century", "bdlist", "DWTS", "SYTYCD" };
    }
}
