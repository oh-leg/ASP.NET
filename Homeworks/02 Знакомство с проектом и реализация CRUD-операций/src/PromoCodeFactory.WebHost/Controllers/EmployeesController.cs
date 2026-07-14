using Microsoft.AspNetCore.Mvc;
using PromoCodeFactory.WebHost.Mapping;
using PromoCodeFactory.WebHost.Models;
using System.Diagnostics;

namespace PromoCodeFactory.WebHost.Controllers;

/// <summary>
/// Сотрудники
/// </summary>
public class EmployeesController(
    IRepository<Employee> employeeRepository,
    IRepository<Role> roleRepository
    ) : BaseController
{
    /// <summary>
    /// Получить данные всех сотрудников
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmployeeShortResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeShortResponse>>> Get(CancellationToken ct)
    {
        var employees = await employeeRepository.GetAll(ct);

        var employeesModels = employees.Select(Mapper.ToEmployeeShortResponse).ToList();

        return Ok(employeesModels);
    }

    /// <summary>
    /// Получить данные сотрудника по Id
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeResponse>> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var employee = await employeeRepository.GetById(id, ct);

        if (employee == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Сотрудник не найден",
                Status = 404,
                Detail = $"Сотрудник с идентификатором \"{id}\" не найден",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        return Ok(Mapper.ToEmployeeResponse(employee));
    }

    /// <summary>
    /// Создать сотрудника
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeResponse>> Create([FromBody] EmployeeCreateRequest request, CancellationToken ct)
    {
        var role = await roleRepository.GetById(request.RoleId, ct);

        if (role == null)
        {
            // Из ДЗ: "Если не найден Role, то возвращать BadRequest"
            // Спорный статус, если не найдено не должно ли быть NotFound?
            return BadRequest(new ProblemDetails
            {
                Title = "Роль не найдена",
                Status = 400,
                Detail = $"Не удалось поднять роль по идентификатору {request.RoleId}",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var employeeCreate = Mapper.ToEmployee(request, role);
        await employeeRepository.Add(employeeCreate, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = employeeCreate.Id },
            Mapper.ToEmployeeResponse(employeeCreate)
        );
    }

    /// <summary>
    /// Обновить сотрудника
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] EmployeeUpdateRequest request,
        CancellationToken ct)
    {
        var employee = await employeeRepository.GetById(id, ct);

        if (employee == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Сотрудник не найден",
                Status = 404,
                Detail = $"Сотрудник с идентификатором \"{id}\" не найден",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var role = await roleRepository.GetById(request.RoleId, ct);

        if (role == null)
        {
            // Из ДЗ: "Если не найден Role, то возвращать BadRequest"
            // Спорный статус, если не найдено не должно ли быть NotFound?
            return BadRequest(new ProblemDetails
            {
                Title = "Роль не найдена",
                Status = 400,
                Detail = $"Не удалось поднять роль по идентификатору {request.RoleId}",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var updateEmployee = Mapper.ToEmployee(id, employee.AppliedPromocodesCount, request, role);
        await employeeRepository.Update(updateEmployee, ct);
        return Ok(Mapper.ToEmployeeResponse(updateEmployee));
    }

    /// <summary>
    /// Удалить сотрудника
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        try
        {
            await employeeRepository.Delete(id, ct);
        }
        catch (EntityNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Сотрудник не найден",
                Status = 404,
                Detail = $"Сотрудник с идентификатором \"{id}\" не найден",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        return NoContent();
    }
}
