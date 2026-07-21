using System.ComponentModel.DataAnnotations;

namespace PromoCodeFactory.WebHost.Models.PromoCodes;

public record PromoCodeCreateRequest(
    [Required]
    [StringLength(100, MinimumLength = 3)]
    string Code,

    [Required]
    [StringLength(256)]
    string ServiceInfo,

    [Required]
    [StringLength(256)]
    string PartnerName,

    [Required]
    [CustomValidation(typeof(PromoCodeCreateRequest), nameof(PromoCodeCreateRequest.ValidateDates))]
    DateTimeOffset BeginDate,

    [Required]
    [CustomValidation(typeof(PromoCodeCreateRequest), nameof(PromoCodeCreateRequest.ValidateDates))]
    DateTimeOffset EndDate,

    [Required]
    Guid PartnerManagerId,

    [Required]
    Guid PreferenceId)
{
    public static ValidationResult? ValidateDates(DateTimeOffset date, ValidationContext context)
    {
        var instance = (PromoCodeCreateRequest)context.ObjectInstance;

        if (context.MemberName == nameof(EndDate) && instance.EndDate <= instance.BeginDate)
        {
            return new ValidationResult("Укажите корректный период: начало не позже окончания");
        }

        if (context.MemberName == nameof(BeginDate) && instance.BeginDate < DateTimeOffset.UtcNow.Date)
        {
            return new ValidationResult("Дата начала не должна быть в прошлом");
        }

        return ValidationResult.Success;
    }
}
