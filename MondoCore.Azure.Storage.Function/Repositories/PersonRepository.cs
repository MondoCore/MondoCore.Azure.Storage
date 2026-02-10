
using MondoCore.Data;

namespace MondoCore.Azure.Storage.Function
{
    public interface IPersonRepository
    {
        IAsyncEnumerable<Person> GetPersons();
    }    
    
    internal class PersonRepository(IDatabase db) : IPersonRepository
    {
        public IAsyncEnumerable<Person> GetPersons()
        {
            return db.GetRepositoryReader<Guid, Person>("persons").Get((id)=> true);
        }
    }

    public class Person : IIdentifiable<Guid>
    {
        public Guid           Id            { get; set; } = Guid.NewGuid();
        public string         Surname       { get; set; } = "";
        public string         FirstName     { get; set; } = "";
        public string         MiddleName    { get; set; } = "";
        public string         Birthdate     { get; set; } = "";
        public List<Address>? Addresses     { get; set; }
    }

    public class Address
    {
        public string StreetNumber  { get; set; } = "";
        public string StreetName    { get; set; } = "";
        public string City          { get; set; } = "";
        public string State         { get; set; } = "";
        public string ZipCode       { get; set; } = "";
    }
}
