using AwesomeAssertions;
using Bogus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.Core.Exceptions;
using PromoCodeFactory.WebHost.Controllers;
using PromoCodeFactory.WebHost.Models.Partners;
using Soenneker.Utils.AutoBogus;

namespace PromoCodeFactory.UnitTests.WebHost.Controllers.Partners;

public class SetLimitTests
{
    private Mock<IRepository<Partner>> _partnersRepositoryMock;
    private Mock<IRepository<PartnerPromoCodeLimit>> _partnerPromoCodeLimitRepositoryMock;
    private PartnersController _partnersController;

    public SetLimitTests()
    {
        _partnersRepositoryMock = new Mock<IRepository<Partner>>();
        _partnerPromoCodeLimitRepositoryMock = new Mock<IRepository<PartnerPromoCodeLimit>>();
        _partnersController = new PartnersController(
            _partnersRepositoryMock.Object, _partnerPromoCodeLimitRepositoryMock.Object);
    }

    [Fact]
    public async Task CreateLimit_WhenPartnerNotFound_ReturnsNotFound()
    {
        // Arrange

        var partnerId = Guid.NewGuid();
        var request = CreateRequest();

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Partner?)null);

        // Act

        var result = await _partnersController.CreateLimit(partnerId, request, It.IsAny<CancellationToken>());

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
    public async Task CreateLimit_WhenPartnerBlocked_ReturnsUnprocessableEntity()
    {
        // Arrange

        var request = CreateRequest();
        var partner = CreatePartner(false);

        _partnersRepositoryMock
            .Setup(r => r.GetById(partner.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        // Act

        var result = await _partnersController.CreateLimit(partner.Id, request, It.IsAny<CancellationToken>());

        // Assert

        var objectResult = result.Result
            .Should()
            .BeAssignableTo<ObjectResult>()
            .Which;

        objectResult.StatusCode
            .Should()
            .Be(StatusCodes.Status422UnprocessableEntity);

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
    public async Task CreateLimit_WhenValidRequest_ReturnsCreatedAndAddsLimit()
    {
        // Arrange

        var request = CreateRequest();
        var partner = CreatePartner();
        Guid? limitId = null; 

        _partnersRepositoryMock
            .Setup(r => r.GetById(partner.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _partnersRepositoryMock
            .Setup(r => r.Update(partner, It.IsAny<CancellationToken>()));

        _partnerPromoCodeLimitRepositoryMock
            .Setup(r => r.Add(It.IsAny<PartnerPromoCodeLimit>(), It.IsAny<CancellationToken>()))
            .Callback<PartnerPromoCodeLimit, CancellationToken>((ppcl, _) => limitId = ppcl.Id);

        // Act

        var result = await _partnersController.CreateLimit(partner.Id, request, It.IsAny<CancellationToken>());

        // Assert

        _partnersRepositoryMock
            .Verify(r => r.Update(partner, It.IsAny<CancellationToken>()), Times.Once);

        _partnerPromoCodeLimitRepositoryMock
            .Verify(r => r.Add(It.IsAny<PartnerPromoCodeLimit>(), It.IsAny<CancellationToken>()), Times.Once);

        limitId.Should().NotBeNull();

        var actionResult = result.Result
            .Should()
            .BeOfType<CreatedAtActionResult>()
            .Which;

        actionResult.ActionName
            .Should().Be(nameof(_partnersController.GetLimit));

        actionResult.RouteValues.Should().NotBeNull();

        actionResult.RouteValues.Should().ContainKey("partnerId");
        actionResult.RouteValues["partnerId"].Should().Be(partner.Id);

        actionResult.RouteValues.Should().ContainKey("limitId");
        actionResult.RouteValues["limitId"].Should().Be(limitId!.Value);

        actionResult.Value.Should()
            .BeOfType<PartnerPromoCodeLimitResponse>()
            .Which.Limit.Should().Be(request.Limit);
    }

    [Fact]
    public async Task CreateLimit_WhenValidRequestWithActiveLimits_CancelsOldLimitsAndAddsNew()
    {
        // Arrange

        var request = CreateRequest();
        var partner = CreatePartner();
        PartnerPromoCodeLimit? partnerPromoCodeLimit = null;

        _partnersRepositoryMock
            .Setup(r => r.GetById(partner.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _partnersRepositoryMock
            .Setup(r => r.Update(partner, It.IsAny<CancellationToken>()));

        _partnerPromoCodeLimitRepositoryMock
            .Setup(r => r.Add(It.IsAny<PartnerPromoCodeLimit>(), It.IsAny<CancellationToken>()))
            .Callback<PartnerPromoCodeLimit, CancellationToken>((ppcl, _) => partnerPromoCodeLimit = ppcl);

        // Act

        var result = await _partnersController.CreateLimit(partner.Id, request, It.IsAny<CancellationToken>());

        // Assert

        _partnersRepositoryMock
            .Verify(r => r.Update(partner, It.IsAny<CancellationToken>()), Times.Once);

        _partnerPromoCodeLimitRepositoryMock
            .Verify(r => r.Add(It.IsAny<PartnerPromoCodeLimit>(), It.IsAny<CancellationToken>()), Times.Once);

        partner.PartnerLimits.First().CanceledAt
            .Should()
            .NotBeNull();

        partnerPromoCodeLimit
            .Should()
            .NotBeNull();

        partnerPromoCodeLimit.CanceledAt
            .Should()
            .BeNull();

        partnerPromoCodeLimit.Partner
            .Should()
            .Be(partner);
    }

    [Fact]
    public async Task CreateLimit_WhenUpdateThrowsEntityNotFoundException_ReturnsNotFound()
    {
        // Arrange

        var request = CreateRequest();
        var partner = CreatePartner();

        _partnersRepositoryMock
            .Setup(r => r.GetById(partner.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        _partnersRepositoryMock
            .Setup(r => r.Update(partner, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException<Partner>(partner.Id));

        // Act

        var result = await _partnersController.CreateLimit(partner.Id, request, It.IsAny<CancellationToken>());

        // Assert

        _partnersRepositoryMock
            .Verify(r => r.Update(partner, It.IsAny<CancellationToken>()), Times.Once);

        var statusCodeResult = result.Result
            .Should()
            .BeAssignableTo<StatusCodeResult>()
            .Which;

        statusCodeResult.StatusCode
            .Should()
            .Be(StatusCodes.Status404NotFound);
    }

    private PartnerPromoCodeLimitCreateRequest CreateRequest()
    {
        var faker = new Faker<PartnerPromoCodeLimitCreateRequest>()
            .CustomInstantiator(f => new PartnerPromoCodeLimitCreateRequest(
                EndAt: f.Date.Future(1, DateTimeOffset.UtcNow.AddDays(1).DateTime),
                Limit: f.Random.Int(1, 1000)
            ));

        return faker.Generate();
    }

    private Partner CreatePartner(bool isActive = true)
    {
        var partnerPromoCodeLimitAutoFaker = new AutoFaker<PartnerPromoCodeLimit>()
                                                .RuleFor(ppcl => ppcl.CanceledAt, _ => null);

        var partnerAutoFaker = new AutoFaker<Partner>()
            .RuleFor(p => p.IsActive, _ => isActive)
            .RuleFor(p => p.PartnerLimits, _ => partnerPromoCodeLimitAutoFaker.Generate(1));

        return partnerAutoFaker.Generate();
    }
}
