using Microi.net.Business;

namespace Microi.Plugin.Demo.SalesOrder
{
    public class SalesOrderParam : BusinessParam
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal? TotalAmount { get; set; }
        public string BillNo { get; set; }
        public int? Status { get; set; }
        public string OrderDate { get; set; }
    }
}
