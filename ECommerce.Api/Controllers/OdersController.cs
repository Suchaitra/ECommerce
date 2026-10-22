using ECommerceRbac.Attributes;
using ECommerceRbac.Data;
using ECommerceRbac.DTOs;
using ECommerceRbac.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ECommerceRbac.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [Authorize]
    [RbacEndpoint("Login")] // Any authenticated customer can place an order
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized();

        var order = new Order
        {
            UserId = userId,
            TotalAmount = 0
        };

        foreach (var itemDto in dto.Items)
        {
            var product = await _db.Products.FindAsync(itemDto.ProductId);
            if (product == null || product.Stock < itemDto.Quantity)
                return BadRequest(new { Message = $"Product {itemDto.ProductId} is unavailable or out of stock." });

            product.Stock -= itemDto.Quantity;

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                Quantity = itemDto.Quantity,
                UnitPrice = product.Price
            };

            order.TotalAmount += (orderItem.Quantity * orderItem.UnitPrice);
            order.Items.Add(orderItem);
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return Ok(order);
    }

    [HttpGet]
    [Authorize]
    [RbacEndpoint("Permission")] // Only staff with mapped permissions can view all orders
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .ToListAsync();

        return Ok(orders);
    }
}