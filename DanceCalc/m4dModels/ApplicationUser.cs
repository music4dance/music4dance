using Microsoft.AspNet.Identity.EntityFramework;
using System.Collections.Generic;

namespace m4dModels
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        public virtual ICollection<ModifiedRecord> Modified { get; set; }
    }
}