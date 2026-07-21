using Microsoft.AspNetCore.Mvc;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Mapping;
using PromoCodeFactory.WebHost.Models.PromoCodes;
using System.Diagnostics;

namespace PromoCodeFactory.WebHost.Controllers;

/// <summary>
/// Промокоды
/// </summary>
public class PromoCodesController(
        IRepository<PromoCode> promoCodeRepository,
        IRepository<Employee> employeeRepository,
        IRepository<Preference> preferenceRepository,
        IRepository<CustomerPromoCode> customerPromoCodeRepository,
        IRepository<Customer> customerRepository
    ) : BaseController
{
    /// <summary>
    /// Получить все промокоды
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PromoCodeShortResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PromoCodeShortResponse>>> Get(CancellationToken ct)
    {
        var promoCodes = await promoCodeRepository.GetAll(true, ct);
        return Ok(promoCodes.Select(PromoCodesMapper.ToPromoCodeShortResponse));
    }

    /// <summary>
    /// Получить промокод по id
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PromoCodeShortResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromoCodeShortResponse>> GetById(Guid id, CancellationToken ct)
    {
        var promoCode = await promoCodeRepository.GetById(id, true, ct);

        if (promoCode == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Промокод не найден",
                Status = 404,
                Detail = $"Не найден промокод по идентификатору: {id}",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        return Ok(PromoCodesMapper.ToPromoCodeShortResponse(promoCode));
    }

    /// <summary>
    /// Создать промокод и выдать его клиентам с указанным предпочтением
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PromoCodeShortResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromoCodeShortResponse>> Create(PromoCodeCreateRequest request, CancellationToken ct)
    {
        var partnerManager = await employeeRepository.GetById(request.PartnerManagerId, true, ct);

        if (partnerManager == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Ошибка в указании менеджера партнёра",
                Status = 404,
                Detail = $"Не найден сотрудник по идентификатору {request.PartnerManagerId}",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        const string rolePartnerManagerName = "PartnerManager";

        if (partnerManager.Role.Name != rolePartnerManagerName)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Ошибка в указании менеджера партнёра",
                Status = 400,
                Detail = $"Не найден сотрудник с идентификатором {request.PartnerManagerId} не соответствует роли \"{rolePartnerManagerName}\"",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var preference = await preferenceRepository.GetById(request.PreferenceId, true, ct);

        if (preference == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Ошибка в указании предпочтения",
                Status = 404,
                Detail = $"Не найдено предпочтение по идентификатору {request.PreferenceId}",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var customerWithPreference = await customerRepository.GetWhere(c => c.Preferences.Contains(preference), true, ct);

        var customerPromoCodes = customerWithPreference.Select(c => new CustomerPromoCode
        {
            Id = Guid.NewGuid(),
            CustomerId = c.Id,
            CreatedAt = DateTime.UtcNow
        });

        var promoCode = PromoCodesMapper.ToPromoCode(request, partnerManager, preference, customerPromoCodes);

        await promoCodeRepository.Add(promoCode, ct);

        return CreatedAtAction(
                nameof(GetById),
                new { id = promoCode.Id },
                PromoCodesMapper.ToPromoCodeShortResponse(promoCode)
            );
    }

    /// <summary>
    /// Применить промокод (отметить, что клиент использовал промокод)
    /// </summary>
    [HttpPost("{id:guid}/apply")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Apply(
        [FromRoute] Guid id,
        [FromBody] PromoCodeApplyRequest request,
        CancellationToken ct)
    {
        var promoCode = await promoCodeRepository.GetById(id, false, ct);

        if (promoCode == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Промокод не найден",
                Status = 404,
                Detail = $"Не найден промокод по идентификатору {id}",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var utcNow = DateTime.UtcNow;

        if (promoCode.BeginDate > utcNow || promoCode.EndDate < utcNow)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Промокод недействителен",
                Status = 400,
                Detail = $"Срок действия промокода истёк",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var customerPromoCodes = await customerPromoCodeRepository
            .GetWhere(
                x => x.CustomerId == request.CustomerId
                    && x.PromoCodeId == id
                    && x.AppliedAt == null,
                false,
                ct
            );

        if (!customerPromoCodes.Any())
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Промокод недействителен",
                Status = 400,
                Detail = $"У клиента отсутсвуют промокоды для активации",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                }
            });
        }

        var customerPromoCode = customerPromoCodes.First();
        customerPromoCode.AppliedAt = DateTime.UtcNow;

        await customerPromoCodeRepository.Update(customerPromoCode, ct);

        return NoContent();
    }
}
