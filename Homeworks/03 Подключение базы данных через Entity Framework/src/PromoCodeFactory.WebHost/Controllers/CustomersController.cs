using Microsoft.AspNetCore.Mvc;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Mapping;
using PromoCodeFactory.WebHost.Models.Customers;
using System.Diagnostics;

namespace PromoCodeFactory.WebHost.Controllers;

/// <summary>
/// Клиенты
/// </summary>
public class CustomersController(
        IRepository<Customer> customerRepository,
        IRepository<PromoCode> promoCodeRepository,
        IRepository<Preference> preferenceRepository
    ) : BaseController
{
    /// <summary>
    /// Получить данные всех клиентов
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CustomerShortResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CustomerShortResponse>>> Get(CancellationToken ct)
    {
        var customers = await customerRepository.GetAll(true, ct);
        return Ok(customers.Select(CustomersMapper.ToCustomerShortResponse));
    }

    /// <summary>
    /// Получить данные клиента по Id
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken ct)
    {
        var customer = await customerRepository.GetById(id, true, ct);

        if (customer == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Клиент не найден",
                Status = 404,
                Detail = $"Клиент по идентификатору \"{id}\" не найден",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var customerPromoCodes = customer.CustomerPromoCodes.Select(x => x.PromoCodeId);
        var promoCodes = await promoCodeRepository.GetByRangeId(customerPromoCodes, true, ct);

        return Ok(CustomersMapper.ToCustomerResponse(customer, promoCodes));
    }

    /// <summary>
    /// Создать клиента
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerShortResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerShortResponse>> Create([FromBody] CustomerCreateRequest request, CancellationToken ct)
    {
        var preferencesIds = request.PreferenceIds.Distinct();

        var preferences = await preferenceRepository.GetByRangeId(preferencesIds, false, ct);

        var invalidPreferencesIds = preferencesIds
            .Except(preferences.Select(x => x.Id))
            .ToList();

        if (invalidPreferencesIds.Any())
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Неверные идентификаторы предпочтений",
                Status = 400,
                Detail = $"Не найдены предпочтения по идентификаторам: {string.Join(",", invalidPreferencesIds)}",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var customer = CustomersMapper.ToCustomer(request, preferences);
        await customerRepository.Add(customer, ct);

        return CreatedAtAction(
                nameof(GetById),
                new { id = customer.Id },
                CustomersMapper.ToCustomerShortResponse(customer)
            );
    }

    /// <summary>
    /// Обновить клиента
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerShortResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerShortResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] CustomerUpdateRequest request,
        CancellationToken ct)
    {
        var customer = await customerRepository.GetById(id, true, ct);

        if (customer == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Клиент не найден",
                Status = 404,
                Detail = $"Не найден клиент по идентификатору {id}",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var preferencesIds = request.PreferenceIds.Distinct();

        var preferences = await preferenceRepository.GetByRangeId(preferencesIds, false, ct);

        var invalidPreferencesIds = preferencesIds
            .Except(preferences.Select(x => x.Id))
            .ToList();

        if (invalidPreferencesIds.Any())
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Неверные идентификаторы предпочтений",
                Status = 400,
                Detail = $"Не найдены предпочтения по идентификаторам: {string.Join(",", invalidPreferencesIds)}",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        customer.FirstName = request.FirstName;
        customer.LastName = request.LastName;
        customer.Email = request.Email;
        customer.Preferences = preferences.ToList();

        await customerRepository.Update(customer, ct);

        return Ok(CustomersMapper.ToCustomerShortResponse(customer));
    }

    /// <summary>
    /// Удалить клиента
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await customerRepository.Delete(id, ct);
        }
        catch (EntityNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Клиент не найден",
                Status = 404,
                Detail = $"Клиент с идентификатором \"{id}\" не найден",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        return NoContent();
    }
}
