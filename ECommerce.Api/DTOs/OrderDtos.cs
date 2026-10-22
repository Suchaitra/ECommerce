namespace ECommerceRbac.DTOs;

public class CreateOrderDto
{
    public required List<OrderItemDto> Items { get; set; }
}

public class OrderItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}