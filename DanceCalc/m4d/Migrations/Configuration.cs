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
            if (!context.Roles.Any(r => r.Name == _editRole))
            {
                var store = new RoleStore<IdentityRole>(context);
                var manager = new RoleManager<IdentityRole>(store);

                foreach (string roleName in _roles)
                {
                    var role = new IdentityRole { Name = roleName };
                    manager.Create(role);
                }
            }

            if (!context.Users.Any(u => u.UserName == "dwgray"))
            {
                var store = new UserStore<ApplicationUser>(context);
                var manager = new UserManager<ApplicationUser>(store);

                foreach (string name in _diagUsers)
                {
                    var user = new ApplicationUser { UserName = name };

                    manager.Create(user, "marley");
                    manager.AddToRole(user.Id, _diagRole);
                    manager.AddToRole(user.Id, _editRole);
                }

                foreach (string name in _editUsers)
                {
                    var user = new ApplicationUser { UserName = name };

                    manager.Create(user, "_this_is_a_placeholder_");
                    manager.AddToRole(user.Id, _editRole);
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

        private static string _editRole = "canEdit";
        private static string _diagRole = "showDiagnostics";

        private string[] _roles = new string[] { _diagRole, _editRole };
        private string[] _diagUsers = new string[] { "administrator", "dwgray" };
        private string[] _editUsers = new string[] { "SalsaSwingBallroom", "SandiegoDJ", "UsaSwingNet", "LetsDanceDenver", "SteveThatDJ", "JohnCrossan", "WaltersDanceCenter" };
    }
}
