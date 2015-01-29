using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;

namespace m4dModels.Tests
{
    public class MockUserStore : IUserStore<ApplicationUser>, IUserRoleStore<ApplicationUser>
    {
        public MockUserStore(IDanceMusicContext dmc)
        {
            _dmc = dmc;
        }

        private readonly IDanceMusicContext _dmc;

        public System.Threading.Tasks.Task CreateAsync(ApplicationUser user)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task DeleteAsync(ApplicationUser user)
        {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task<ApplicationUser> FindByIdAsync(string userId)
        {
            return _dmc.Users.FirstOrDefaultAsync(u => u.Id == userId);
        }

        public System.Threading.Tasks.Task<ApplicationUser> FindByNameAsync(string userName)
        {
            return _dmc.Users.FirstOrDefaultAsync(u => u.UserName == userName);
        }

        public System.Threading.Tasks.Task UpdateAsync(ApplicationUser user)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {            
        }

        public Task AddToRoleAsync(ApplicationUser user, string roleName)
        {
            throw new NotImplementedException();
        }

        public Task<IList<string>> GetRolesAsync(ApplicationUser user)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName)
        {
            // TODO: We should consider mocking roles for real
            return Task.FromResult(true);
        }

        public Task RemoveFromRoleAsync(ApplicationUser user, string roleName)
        {
            throw new NotImplementedException();
        }
    }
}
