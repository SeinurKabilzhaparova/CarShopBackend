using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using CarShopFinal.Application.Features.Seller.CreateSeller;
using CarShopFinal.Application.Features.Seller.GetSellerByEmail;
using CarShopFinal.Application.Features.Seller.GetSellerById;
using CarShopFinal.Application.Features.Seller.GetSellerByUserId;
using CarShopFinal.Application.Features.Seller.UpdateSeller;
using CarShopFinal.Application.Features.Seller.UpdateSellerCity;
using Microsoft.AspNetCore.Mvc;

namespace CarShopFinal.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class SellerController:ControllerBase
{
    private readonly CreateSellerHandler _create;
    private readonly GetSellerByUserIDHandler _getSellerByUserID;
    private readonly GetSellerByEmailHandler _getSellerByEmail;
    private readonly GetSellerByIdHandler _getSellerById;
    private readonly UpdateSellerCityHandler  _updateSellerCity;
    private readonly UpdateSellerContactInfoHandler  _updateSellerContactInfo;
    
    public SellerController(
        CreateSellerHandler create, 
        GetSellerByUserIDHandler getSellerByUserID,
        GetSellerByEmailHandler getSellerByEmail,
        GetSellerByIdHandler getSellerById,
        UpdateSellerCityHandler updateSellerCity,
        UpdateSellerContactInfoHandler updateSellerContactInfo)
    {
        _create = create;
        _getSellerByUserID = getSellerByUserID;
        _getSellerByEmail = getSellerByEmail;
        _getSellerById = getSellerById;
        _updateSellerCity = updateSellerCity;
        _updateSellerContactInfo = updateSellerContactInfo;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateSeller([FromBody] CreateSellerCommand command)
    {
        return await CreateForCurrentUser(command);
    }

    [Authorize]
    [HttpPost("private")]
    public async Task<IActionResult> CreatePrivateSeller([FromBody] CreatePrivateSellerRequest request)
    {
        return await CreateForCurrentUser(new CreateSellerCommand(
            request.Name, Guid.Empty, request.City, string.Empty, request.Phone, request.Email));
    }

    private async Task<IActionResult> CreateForCurrentUser(CreateSellerCommand command)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var existing = await _getSellerByUserID.Handle(new GetSellerByUserIDQuery(userId));
        if (existing is not null) return Conflict(new { sellerId = existing.Id });

        var sellerId = await _create.Handle(command with { UserID = userId });
        return Ok(sellerId);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSellerById([FromRoute] GetSellerByIdQuery query)
    {
        var seller = await _getSellerById.Handle(new GetSellerByIdQuery(query.id));
        if (seller is null) return NotFound();
        return Ok(seller);
    }

    [HttpGet("email")]
    public async Task<IActionResult> GetSellerByEmail([FromQuery] string email)
    {
        var seller = await _getSellerByEmail.Handle(new GetSellerByEmailQuery(email));
        if (seller is null) return NotFound();
        return Ok(seller);
    }

    [HttpGet("UserId")]
    public async Task<IActionResult> GetSellerByUserId([FromQuery] Guid userId)
    {
        var seller = await _getSellerByUserID.Handle(new GetSellerByUserIDQuery(userId));
        if (seller is null) return NotFound();
        return Ok(seller);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMySeller()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var seller = await _getSellerByUserID.Handle(new GetSellerByUserIDQuery(userId));
        return seller is null ? NotFound() : Ok(seller);
    }

    private async Task<bool> OwnsSeller(Guid sellerId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return false;

        var seller = await _getSellerByUserID.Handle(new GetSellerByUserIDQuery(userId));
        return seller?.Id == sellerId;
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSellerContactInfo([FromRoute] Guid id, [FromBody] string phone, string email)
    {
        if (!await OwnsSeller(id)) return NotFound();
        await _updateSellerContactInfo.Handle(new UpdateSellerContactInfoCommand(id, phone, email));
        return NoContent();
    }

    [Authorize]
    [HttpPut("{id:guid}/city")]
    public async Task<IActionResult> UpdateSellerCity([FromRoute] Guid id, [FromBody] string city, string address)
    {
        if (!await OwnsSeller(id)) return NotFound();
        await _updateSellerCity.Handle(new UpdateSellerCityCommand(id,city,address));
        return NoContent();
    }
}

public record CreatePrivateSellerRequest(string Name, string City, string Phone, string Email);
