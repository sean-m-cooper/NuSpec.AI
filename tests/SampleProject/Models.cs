namespace SampleProject;

/// <summary>
/// Represents a customer in the system.
/// </summary>
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Customer status values.
/// </summary>
public enum CustomerStatus
{
    Active = 0,
    Inactive = 1,
    Suspended = 2
}

public interface ICustomerRepository
{
    /// <summary>Gets a customer by ID.</summary>
    Task<Customer?> GetByIdAsync(int id);
    Task<IEnumerable<Customer>> GetAllAsync();
    Task AddAsync(Customer customer);
}

public class CustomerRepository : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(int id) => throw new NotImplementedException();
    public Task<IEnumerable<Customer>> GetAllAsync() => throw new NotImplementedException();
    public Task AddAsync(Customer customer) => throw new NotImplementedException();
}
