namespace E_Commerce.ViewModels
{
    public class CartViewModel
    {
        public List<CartLineViewModel> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public int ItemCount { get; set; }
        public bool HasStockIssue { get; set; }
        public decimal FreeShippingThreshold { get; set; } = 30m;
    }

    public class CartLineViewModel
    {
        public int CartItemId { get; set; }
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal? OldPrice { get; set; }
        public int Quantity { get; set; }
        public int MaxStock { get; set; }
        public decimal Subtotal { get; set; }
    }
}
