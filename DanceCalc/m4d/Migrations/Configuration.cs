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
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method 
            //  to avoid creating duplicate seed data. E.g.
            //
            //    context.People.AddOrUpdate(
            //      p => p.FullName,
            //      new Person { FullName = "Andrew Peters" },
            //      new Person { FullName = "Brice Lambson" },
            //      new Person { FullName = "Rowan Miller" }
            //    );
            //
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

            var ustore = new UserStore<ApplicationUser>(context);
            var umanager = new UserManager<ApplicationUser>(ustore);

            foreach (string name in _adminUsers)
            {
                var user = context.Users.FirstOrDefault(u => u.UserName == name);
                if (user == null)
                {
                    user = new ApplicationUser { UserName = name };

                    umanager.Create(user, "maggie");
                }

                AddToRole(umanager, user.Id, DanceMusicContext.DiagRole);
                AddToRole(umanager, user.Id, DanceMusicContext.EditRole);
                AddToRole(umanager, user.Id, DanceMusicContext.DbaRole);
            }

            foreach (string name in _diagUsers)
            {
                var user = context.Users.FirstOrDefault(u => u.UserName == name);
                if (user == null)
                {
                    user = new ApplicationUser { UserName = name };

                    umanager.Create(user, "marley");
                    AddToRole(umanager, user.Id, DanceMusicContext.DiagRole);
                    AddToRole(umanager, user.Id, DanceMusicContext.EditRole);
                }
                else
                {
                    RemoveFromRole(umanager, user.Id, DanceMusicContext.DbaRole);
                }
            }

            foreach (string name in _editUsers)
            {
                if (!context.Users.Any(u => u.UserName == name))
                {
                    var user = new ApplicationUser { UserName = name };

                    umanager.Create(user, "_this_is_a_placeholder_");
                }
            }

            if (!context.Dances.Any(d => d.Id == "CHA"))
            {
                Dances dances = Dances.Instance;

                foreach (DanceObject d in dances.AllDances)
                {
                    Dance dance = new Dance { Id = d.Id };
                    context.Dances.Add(dance);
                }
            }
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

        private static string[] _roles = new string[] { DanceMusicContext.DiagRole, DanceMusicContext.EditRole, DanceMusicContext.DbaRole };
        private static string[] _adminUsers = new string[] { "administrator", "dwgray", "batch" };
        private static string[] _diagUsers = new string[] { "lukim", "glennn" };
        private static string[] _editUsers = new string[] { "ajy", "SalsaSwingBallroom", "SandiegoDJ", "UsaSwingNet", "LetsDanceDenver", "SteveThatDJ", "JohnCrossan", "WaltersDanceCenter", "breanna", "buzzle", "michelleds", "shawntrautman", "danceforums" };
    }
}
