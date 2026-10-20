using Ecom.Core.Entities.Order;

namespace Ecom.Core.DTO.CartDto.Order
{
    public class OrderDto
    {
        public int deliveryMethodId { get; set; }
        //public string basketId { get; set; }//or user id!
        public shipAddressDto shipAddress { get; set; }
    }
    public class shipAddressDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }
        public string Street { get; set; }
        public string State { get; set; }
    }
    /// <summary>
    /// Read model returned by POST Order/CreateOrder. Mirrors the frontend IOrder
    /// interface (camelCased on the wire), so the order entity is never serialized.
    /// </summary>
    public class CreateOrderResponse
    {
        public int Id { get; set; }
        public string BuyerEmail { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Total { get; set; }
        public DateTime OrderDate { get; set; }
        public shipAddressDto ShippingAddress { get; set; }
        public List<OrderItemResponse> OrderItems { get; set; }
        public string DeliveryMethod { get; set; }
        public string Status { get; set; }
    }

    public class OrderItemResponse
    {
        public int ProductItemId { get; set; }
        public string MainImage { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Quntity { get; set; }
    }

    public class OrderResponse()
    {


        public int OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int deliveryMethodId { get; set; }
        public shipAddressDto shipAddress { get; set; }
        public List<TblOrderItem> items { get; set; }
        public decimal SubTotal { get; set; }
        // Migration + identity (order id and delivery method seeding data ) [deliveryAddress]
        public decimal Total { get; set; }
        public string Status { get; set; }


    }
    public class DeliveryMethodResponse()
    {
        public int id { get; set; }
        public string name { get; set; }
        public decimal price { get; set; }
    }

}
