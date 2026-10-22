using ECommerceRbac.Attributes;
using ECommerceRbac.Data;
using ECommerceRbac.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceRbac.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RbacEndpoint("Public")]
    public async Task<IActionResult> GetProducts()
    {
        return Ok(await _db.Products.ToListAsync());
    }

    [HttpPost]
    [Authorize]
    [RbacEndpoint("Permission")]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return Ok(product);
    }
}