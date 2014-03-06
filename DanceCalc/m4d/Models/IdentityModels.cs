using Microsoft.AspNet.Identity.EntityFramework;

namespace m4d.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
    }

    public class DanceMusicContext : IdentityDbContext<ApplicationUser>
    {
        public DanceMusicContext()
            : base("DefaultConnection")
        {
        }
    }
}