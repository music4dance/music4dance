namespace m4d.Migrations
{
    using DanceLibrary;
    using m4d.Models;
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<m4d.Models.DanceMusicContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(m4d.Models.DanceMusicContext context)
        {
            DoSeed(context);
        }

        static public void DoSeed(m4d.Models.DanceMusicContext context)
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

            foreach (string name in _diagUsers)
            {
                var user = context.Users.FirstOrDefault(u => u.UserName == name);
                if (user == null)
                {
                    user = new ApplicationUser { UserName = name };

                    umanager.Create(user, "marley");
                }

                AddToRole(umanager, user.Id, _diagRole);
                AddToRole(umanager, user.Id, _editRole);
                AddToRole(umanager, user.Id, _dbaRole);
            }

            foreach (string name in _editUsers)
            {
                if (!context.Users.Any(u => u.UserName == name))
                {
                    var user = new ApplicationUser { UserName = name };

                    umanager.Create(user, "_this_is_a_placeholder_");
                    umanager.AddToRole(user.Id, _editRole);
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

        private static string _editRole = "canEdit";
        private static string _diagRole = "showDiagnostics";
        private static string _dbaRole = "dbAdmin";

        private static string[] _roles = new string[] { _diagRole, _editRole, _dbaRole };
        private static string[] _diagUsers = new string[] { "administrator", "dwgray" };
        private static string[] _editUsers = new string[] { "SalsaSwingBallroom", "SandiegoDJ", "UsaSwingNet", "LetsDanceDenver", "SteveThatDJ", "JohnCrossan", "WaltersDanceCenter" };
    }
}
