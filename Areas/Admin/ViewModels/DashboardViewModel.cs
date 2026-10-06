namespace E_Commerce.Areas.Admin.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TotalIncome { get; set; }
        public decimal IncomeThisMonth { get; set; }
        public decimal IncomeLastMonth { get; set; }
        public double IncomeChangePct { get; set; }
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalBooks { get; set; }
        public int LowStockBooks { get; set; }

        public List<MonthlyIncomeItem> MonthlyIncome { get; set; } = new();
        public List<StatusCountItem> OrdersByStatus { get; set; } = new();
        public List<TopBookItem> TopBooks { get; set; } = new();
        public List<RecentOrderItem> RecentOrders { get; set; } = new();
        public List<LowStockBookItem> LowStock { get; set; } = new();
    }

    public class MonthlyIncomeItem
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class StatusCountItem
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class TopBookItem
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Cover { get; set; }
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }

    public class RecentOrderItem
    {
        public int OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string Customer { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class LowStockBookItem
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Cover { get; set; }
        public int Stock { get; set; }
    }
}
