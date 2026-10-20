namespace Ecom.Core.Entities.Order
{
    public class TblOrders : BaseEntity<int>
    {
        public TblOrders()
        {
        }

        public TblOrders(string buyerEmail, decimal subTotal, TblShippingAddress shippingAddress, TblDeliveryMethod deliveryMethod, IReadOnlyList<TblOrderItem> orderItems)
        {
            BuyerEmail = buyerEmail;
            SubTotal = subTotal;
            this.shippingAddress = shippingAddress;
            this.deliveryMethod = deliveryMethod;
            this.orderItems = orderItems;
        }

        public string BuyerEmail { get; set; }
        public decimal SubTotal { get; set; }

        //public DateTime OrderDate { get; set; } // CreatedAt!

        public TblShippingAddress shippingAddress { get; set; }

        public TblDeliveryMethod deliveryMethod { get; set; }
        public IReadOnlyList<TblOrderItem> orderItems { get; set; }

        public Status status { get; set; } = Status.Pending;
        public decimal GetTotal()
        {
            return SubTotal + deliveryMethod.Price;
        }

    }
}
