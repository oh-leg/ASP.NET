using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Controllers;
using PromoCodeFactory.WebHost.Models.PromoCodes;
using Soenneker.Utils.AutoBogus;
using System.Linq.Expressions;

namespace PromoCodeFactory.UnitTests.WebHost.Controllers.PromoCodes;

public class CreateTests
{
    private Mock<IRepository<PromoCode>> _promoCodesRepositoryMock;
    private Mock<IRepository<Customer>> _customersRepositoryMock;
    private Mock<IRepository<CustomerPromoCode>> _customerPromoCodesRepositoryMock;
    private Mock<IRepository<Partner>> _partnersRepositoryMock;
    private Mock<IRepository<Preference>> _preferencesRepositoryMock;
    private PromoCodesController _promoCodesController;

    public CreateTests()
    {
        _promoCodesRepositoryMock = new Mock<IRepository<PromoCode>>();
        _customersRepositoryMock = new Mock<IRepository<Customer>>();
        _customerPromoCodesRepositoryMock = new Mock<IRepository<CustomerPromoCode>>();
        _partnersRepositoryMock = new Mock<IRepository<Partner>>();
        _preferencesRepositoryMock = new Mock<IRepository<Preference>>();
        _promoCodesController = new PromoCodesController(
                _promoCodesRepositoryMock.Object,
                _customersRepositoryMock.Object,
                _customerPromoCodesRepositoryMock.Object,
                _partnersRepositoryMock.Object,
                _preferencesRepositoryMock.Object
            );
    }

    [Fact]
    public async Task Create_WhenPartnerNotFound_ReturnsNotFound()
    {
        // Arrange

        var request = CreateRequest();

        _partnersRepositoryMock
            .Setup(r => r.GetById(
                request.PartnerId,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Partner?)null);

        // Act

        var result = await _promoCodesController.Create(request, It.IsAny<CancellationToken>());

        // Assert

        var objectResult = result.Result
            .Should()
            .BeAssignableTo<ObjectResult>()
            .Which;

        objectResult.StatusCode
            .Should()
            .Be(StatusCodes.Status404NotFound);

        var problemDetails = objectResult.Value
            .Should()
            .BeOfType<ProblemDetails>()
            .Which;

        problemDetails.Title
            .Should()
            .NotBeNullOrWhiteSpace();

        problemDetails.Detail
            .Should()
            .NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_WhenPreferenceNotFound_ReturnsNotFound()
    {
        // Arrange

        var request = CreateRequest();
        var partner = CreatePartner(request.PartnerId);

        _partnersRepositoryMock
            .Setup(r => r.GetById(
                request.PartnerId,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _preferencesRepositoryMock
            .Setup(r => r.GetById(
                request.PreferenceId,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Preference?)null);

        // Act

        var result = await _promoCodesController.Create(request, It.IsAny<CancellationToken>());

        // Assert

        _preferencesRepositoryMock
            .Verify(r => r.GetById(request.PreferenceId, false, It.IsAny<CancellationToken>()), Times.Once);

        var objectResult = result.Result
            .Should()
            .BeAssignableTo<ObjectResult>()
            .Which;

        objectResult.StatusCode
            .Should()
            .Be(StatusCodes.Status404NotFound);

        var problemDetails = objectResult.Value.Should()
            .BeOfType<ProblemDetails>()
            .Which;

        problemDetails.Title
            .Should()
            .NotBeNullOrWhiteSpace();

        problemDetails.Detail
            .Should()
            .NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_WhenNoActiveLimit_ReturnsUnprocessableEntity()
    {
        // Arrange

        var request = CreateRequest();
        var partner = CreatePartner(request.PartnerId, "noActiveLimit");
        var preference = CreatePreference(request.PreferenceId);

        _partnersRepositoryMock
            .Setup(r => r.GetById(
                request.PartnerId,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _preferencesRepositoryMock
            .Setup(r => r.GetById(
                request.PreferenceId,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);

        // Act

        var result = await _promoCodesController.Create(request, It.IsAny<CancellationToken>());

        // Assert

        var objectResult = result.Result
            .Should()
            .BeOfType<ObjectResult>()
            .Which;

        objectResult.StatusCode
            .Should()
            .Be(StatusCodes.Status422UnprocessableEntity);

        var problemDetails = objectResult.Value.Should()
            .BeOfType<ProblemDetails>()
            .Which;

        problemDetails.Title
            .Should()
            .NotBeNullOrWhiteSpace();

        problemDetails.Detail
            .Should()
            .NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_WhenLimitExceeded_ReturnsUnprocessableEntity()
    {
        // Arrange

        var request = CreateRequest();
        var partner = CreatePartner(request.PartnerId, "limitExceeded");
        var preference = CreatePreference(request.PreferenceId);

        _partnersRepositoryMock
            .Setup(r => r.GetById(
                request.PartnerId,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _preferencesRepositoryMock
            .Setup(r => r.GetById(
                request.PreferenceId,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);

        // Act

        var result = await _promoCodesController.Create(request, It.IsAny<CancellationToken>());

        // Assert

        var objectResult = result.Result
            .Should()
            .BeOfType<ObjectResult>()
            .Which;

        objectResult.StatusCode
            .Should()
            .Be(StatusCodes.Status422UnprocessableEntity);

        var problemDetails = objectResult.Value.Should()
            .BeOfType<ProblemDetails>()
            .Which;

        problemDetails.Title
            .Should()
            .NotBeNullOrWhiteSpace();

        problemDetails.Detail
            .Should()
            .NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_WhenValidRequest_ReturnsCreatedAndIncrementsIssuedCount()
    {
        // Arrange

        var request = CreateRequest();
        var partner = CreatePartner(request.PartnerId);
        var preference = CreatePreference(request.PreferenceId);
        var customers = CreateCustomers(preference);

        var oldIssuedCount = partner.PartnerLimits.First().IssuedCount;
        PromoCode? promoCode = null;

        _partnersRepositoryMock
            .Setup(r => r.GetById(
                request.PartnerId,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _preferencesRepositoryMock
            .Setup(r => r.GetById(
                request.PreferenceId,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);

        _customersRepositoryMock
            .Setup(r => r.GetWhere(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(customers);

        _promoCodesRepositoryMock
            .Setup(r => r.Add(
                It.IsAny<PromoCode>(),
                It.IsAny<CancellationToken>()))
            .Callback<PromoCode, CancellationToken>((pc, ct) => promoCode = pc);
            

        // Act

        var result = await _promoCodesController.Create(request, It.IsAny<CancellationToken>());

        // Assert

        _promoCodesRepositoryMock.Verify(r => r.Add(
                It.IsAny<PromoCode>(),
                It.IsAny<CancellationToken>()), Times.Once);

        promoCode.Should().NotBeNull();

        var actionResult = result.Result
            .Should()
            .BeOfType<CreatedAtActionResult>()
            .Which;

        actionResult.ActionName
            .Should()
            .Be(nameof(_promoCodesController.GetById));

        actionResult.RouteValues
            .Should()
            .NotBeNull();

        actionResult.RouteValues.Should().ContainKey("id");
        actionResult.RouteValues["id"].Should().Be(promoCode!.Id);

        partner.PartnerLimits.First().IssuedCount
            .Should()
            .BeGreaterThan(oldIssuedCount);
    }

    private PromoCodeCreateRequest CreateRequest()
    {
        return new AutoFaker<PromoCodeCreateRequest>()
            .Generate();
    }

    private Partner CreatePartner(Guid partnerId, string activeLimitType = "hasActiveLimit")
    {
        var partnerPromoCodeLimitAutoFaker = new AutoFaker<PartnerPromoCodeLimit>();

        switch (activeLimitType)
        {
            case "noActiveLimit":
                partnerPromoCodeLimitAutoFaker
                    .RuleFor(ppcl => ppcl.CanceledAt, f => f.Date.Past());
                break;
            case "limitExceeded":
                partnerPromoCodeLimitAutoFaker
                    .RuleFor(ppcl => ppcl.Limit, f => f.Random.Int(0, 5))
                    .RuleFor(ppcl => ppcl.IssuedCount, (f, ppcl) => ppcl.Limit);
                break;
            default:
                partnerPromoCodeLimitAutoFaker
                    .RuleFor(ppcl => ppcl.CanceledAt, _ => null)
                    .RuleFor(ppcl => ppcl.EndAt, f => f.Date.FutureOffset())
                    .RuleFor(ppcl => ppcl.Limit, f => f.Random.Int(2, 5))
                    .RuleFor(ppcl => ppcl.IssuedCount, (f, ppcl) => 0);
                break;
        }

        var partnerAutoFaker = new AutoFaker<Partner>()
            .RuleFor(p => p.Id, _ => partnerId)
            .RuleFor(p => p.IsActive, _ => true)
            .RuleFor(p => p.PartnerLimits, _ => partnerPromoCodeLimitAutoFaker.Generate(1));

        return partnerAutoFaker.Generate();
    }

    private Preference CreatePreference(Guid preferenceId)
    {
        return new AutoFaker<Preference>()
            .Generate();
    }

    private IReadOnlyCollection<Customer> CreateCustomers(Preference preference)
    {
        return new AutoFaker<Customer>()
            .RuleFor(c => c.Preferences, _ => [preference])
            .Generate(3);
    }
}
