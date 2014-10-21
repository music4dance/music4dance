using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels.Tests
{
    // TODO: Figure out how much we need to mock here
    internal class MockUserMap : IUserMap
    {
        public ApplicationUser FindUser(string name)
        {
            ApplicationUser user = null;
            _users.TryGetValue(name, out user);
            return user;
        }

        public ApplicationUser FindOrAddUser(string name, string role)
        {
            ApplicationUser user = FindUser(name);

            if (user == null)
            {
                user = new ApplicationUser() { UserName = name, Id = Guid.NewGuid().ToString("D") };
                _users.Add(name, user);
            }

            // TODO: Should we add a concept of roles into the mock????

            return user;
        }

        public ModifiedRecord CreateMapping(Guid songId, string applicationId)
        {
            ApplicationUser user = _users.Values.First(u => u.Id == applicationId);

            return new ModifiedRecord() { SongId = songId, ApplicationUser = user, ApplicationUserId = user.Id };
        }

        //private static Dictionary<Guid,string> 
        
        private static Dictionary<string, ApplicationUser> _users = new Dictionary<string, ApplicationUser>()
        {
            {"dwgray", new ApplicationUser() {UserName="dwgray", Id="05849D25-0292-44CF-A3E6-74D07D94855C"}},
            {"batch", new ApplicationUser() {UserName="batch", Id="DE3752CA-42CD-46FB-BEE9-F7163CFB091B"}},
        };
    }
}
