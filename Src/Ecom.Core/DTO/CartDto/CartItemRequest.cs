using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Core.DTO.CartDto
{
    public class CartItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
