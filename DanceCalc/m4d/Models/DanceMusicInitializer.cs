//using Microsoft.AspNet.Identity;
//using Microsoft.AspNet.Identity.EntityFramework;
//using System;
//using System.Collections.Generic;
//using System.Data.Entity;
//using System.Linq;
//using System.Web;

//namespace m4d.Models
//{
//    public class DanceMusicInitializer : System.Data.Entity.DropCreateDatabaseIfModelChanges<DanceMusicContext>
//    {
//        protected override void Seed(DanceMusicContext context)
//        {
//            if (!context.Roles.Any(r => r.Name == _editRole))
//            {
//                var store = new RoleStore<IdentityRole>(context);
//                var manager = new RoleManager<IdentityRole>(store);

//                foreach (string roleName in _roles)
//                {
//                    var role = new IdentityRole { Name = roleName };
//                    manager.Create(role);
//                }
//            }

//            if (!context.Users.Any(u => u.UserName == "dwgray"))
//            {
//                var store = new UserStore<ApplicationUser>(context);
//                var manager = new UserManager<ApplicationUser>(store);

//                foreach (string name in _diagUsers)
//                {
//                    var user = new ApplicationUser { UserName = name };

//                    manager.Create(user, "marley");
//                    manager.AddToRole(user.Id, _diagRole);
//                    manager.AddToRole(user.Id, _editRole);
//                }

//                foreach (string name in _editUsers)
//                {
//                    var user = new ApplicationUser { UserName = name };

//                    manager.Create(user, "_this_is_a_placeholder_");
//                    manager.AddToRole(user.Id, _editRole);
//                }
//            }
//        }


//        private static string _editRole = "canEdit";
//        private static string _diagRole = "showDiagnostics";

//        private string[] _roles = new string[] { _diagRole, _editRole };
//        private string[] _diagUsers = new string[] { "administrator", "dwgray" };
//        private string[] _editUsers = new string[] { "SalsaSwingBallroom", "SandiegoDJ", "UsaSwingNet", "LetsDanceDenver", "SteveThatDJ", "JohnCrossan", "WaltersDanceCenter" };
//    }
//}