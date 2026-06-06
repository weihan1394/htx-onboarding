using HrService.Models;
using HrService.DTOs;

namespace HrService.Repositories;

// data access contract — implemented by EmployeeRepository (Dapper + Postgres)
public interface IEmployeeRepository
{
    Task<IEnumerable<Employee>> GetAllAsync();
    Task<Employee?> GetByIdAsync(Guid id);
    Task<Employee?> GetByEmailAsync(string email);      // used for duplicate-email check
    Task<Employee> CreateAsync(CreateEmployeeRequest request);
    Task<bool> UpdateStatusAsync(Guid id, string status); // returns false if employee not found
}
