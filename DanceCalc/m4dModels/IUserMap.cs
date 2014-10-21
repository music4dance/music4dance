
using System;
namespace m4dModels
{
    // Provide the ability to map a user name to an ApplicationUser object
    // Not sure if this is the right thing to do, but it will let me
    // isolate user mapping from the database and push song<->user
    // mapping code into my model assembly
    public interface IUserMap
    {
        ApplicationUser FindUser(string name);
        ApplicationUser FindOrAddUser(string name, string role);

        ModifiedRecord CreateMapping(Guid songId, string applicationId);
    }
}