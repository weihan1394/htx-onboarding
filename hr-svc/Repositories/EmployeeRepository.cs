using Dapper;
using HrService.DTOs;
using HrService.Models;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace HrService.Repositories;

// all SQL queries for the hr.employees table
[ExcludeFromCodeCoverage]
public class EmployeeRepository : IEmployeeRepository
{
    private readonly string _connectionString;

    public EmployeeRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not configured.");
    }

    // newest employees first
    public async Task<IEnumerable<Employee>> GetAllAsync()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<Employee>(@"
            SELECT employee_id, employee_number, first_name, last_name, email,
                   department, position, hire_date, status, created_at, updated_at
            FROM hr.employees
            ORDER BY created_at DESC");
    }

    public async Task<Employee?> GetByIdAsync(Guid id)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<Employee>(@"
            SELECT employee_id, employee_number, first_name, last_name, email,
                   department, position, hire_date, status, created_at, updated_at
            FROM hr.employees
            WHERE employee_id = @Id", new { Id = id });
    }

    // used for duplicate-email check before insert
    public async Task<Employee?> GetByEmailAsync(string email)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<Employee>(@"
            SELECT employee_id, employee_number, first_name, last_name, email,
                   department, position, hire_date, status, created_at, updated_at
            FROM hr.employees
            WHERE email = @Email", new { Email = email });
    }

    // employee number is sequential (EMP-0001, EMP-0002, …) based on total row count
    // Table lock prevents two concurrent inserts from reading the same count
    public async Task<Employee> CreateAsync(CreateEmployeeRequest request)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync(
            "LOCK TABLE hr.employees IN SHARE ROW EXCLUSIVE MODE", transaction: tx);

        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM hr.employees", transaction: tx);
        var employeeNumber = $"EMP-{(count + 1):D4}";

        var employee = await conn.QuerySingleAsync<Employee>(@"
            INSERT INTO hr.employees
                (employee_number, first_name, last_name, email, department, position, hire_date, status)
            VALUES
                (@EmployeeNumber, @FirstName, @LastName, @Email, @Department, @Position, @HireDate, 'active')
            RETURNING employee_id, employee_number, first_name, last_name, email,
                      department, position, hire_date, status, created_at, updated_at",
            new
            {
                EmployeeNumber = employeeNumber,
                request.FirstName,
                request.LastName,
                request.Email,
                request.Department,
                request.Position,
                request.HireDate
            }, transaction: tx);

        await tx.CommitAsync();
        return employee;
    }

    // returns false if the employee doesn't exist
    public async Task<bool> UpdateStatusAsync(Guid id, string status)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var affected = await conn.ExecuteAsync(@"
            UPDATE hr.employees SET status = @Status WHERE employee_id = @Id",
            new { Status = status, Id = id });
        return affected > 0;
    }
}
