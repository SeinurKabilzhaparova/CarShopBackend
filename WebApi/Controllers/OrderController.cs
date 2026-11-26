using System.Security.Claims;
using CarShopFinal.Application.Features.Order.CancelOrder;
using CarShopFinal.Application.Features.Order.CreateOrder;
using CarShopFinal.Application.Features.Order.GetOrderById;
using CarShopFinal.Domain.Interfaces;
using CarShopFinal.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarShopFinal.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly CreateOrderHandle _createOrderHandle;
    private readonly GetOrderByIdHandler _getOrderByIdHandler;
    private readonly CancelOrderHandler _cancelOrderHandler;

    private readonly ICustomerRepository _customerRepository;

    public OrderController(
        CreateOrderHandle createOrderHandle,
        GetOrderByIdHandler getOrderByIdHandler,
        CancelOrderHandler cancelOrderHandler,
        ICustomerRepository customerRepository)
    {
        _createOrderHandle = createOrderHandle;
        _getOrderByIdHandler = getOrderByIdHandler;
        _cancelOrderHandler = cancelOrderHandler;
        _customerRepository = customerRepository;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder()
    {
        var customer = await GetCurrentCustomer();
        if (customer is null) return Forbid();

        var orderId = await _createOrderHandle.CreateOrderAsync(new CreateOrderCommand(customer.Id));
        return Ok(orderId);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        var customer = await GetCurrentCustomer();
        if (customer is null) return Forbid();

        var order = await _getOrderByIdHandler.Handle(new GetOrderByIdQuery(id, customer.Id));
        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        var customer = await GetCurrentCustomer();
        if (customer is null) return Forbid();

        await _cancelOrderHandler.Handle(new CancelOrderCommand(id, customer.Id));
        return NoContent();
    }

    private async Task<Customer?> GetCurrentCustomer()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return null;

        return await _customerRepository.GetByUserId(userId);
    }
}
