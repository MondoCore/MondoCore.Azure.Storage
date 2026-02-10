
using MondoCore.Common;

namespace MondoCore.Azure.Storage.Function
{
    public interface IStorageTestService
    {
        Task<IEnumerable<Person>> Run();
        Task                      HandlePerson(Person person);
    }  
    
    internal class StorageTestService(IPersonRepository repo, IBlobStore<Person> storage) : IStorageTestService
    {
        public async Task<IEnumerable<Person>> Run()
        {
            var persons = repo.GetPersons();
            var result = await persons.ToListAsync();

            return result.Where( p=> p.FirstName == "Felix" );            
        }

        public Task HandlePerson(Person person)
        {
            var content = $"{person.FirstName},{person.MiddleName},{person.Surname},{person.Addresses![0].City},{person.Addresses[0].State}\r\n";

            return storage.Writer.Put("personlist", content);
        }
    }
}
